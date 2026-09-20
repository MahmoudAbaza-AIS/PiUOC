using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PiAiAssistant.Domain.Entities;
using PiAiAssistant.Domain.Interfaces;
using PiAiAssistant.Infrastructure.Options;

namespace PiAiAssistant.Infrastructure.Pi;

/// <summary>
/// Development-only router: prefer the PI WebAPI emulator when reachable, otherwise Demo PI.
/// Re-probes periodically so starting the emulator after the API does not require a restart.
/// </summary>
public sealed class DevPreferSimulatorDataSource(
    DemoPiDataSource demo,
    IServiceScopeFactory scopeFactory,
    PiDataSourceRuntimeInfo runtime,
    ILogger<DevPreferSimulatorDataSource> logger) :
    IPiConnectivity, IPiPointReader, ITagValueReader, IAfAttributeReader
{
    private static readonly TimeSpan ProbeTtl = TimeSpan.FromSeconds(10);
    private readonly SemaphoreSlim _probeLock = new(1, 1);
    private DateTimeOffset _nextProbeUtc = DateTimeOffset.MinValue;
    private bool _useDemo = true;

    public async Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
    {
        await EnsureRouteAsync(cancellationToken);
        if (_useDemo)
        {
            return await demo.TryConnectAsync(cancellationToken);
        }

        return await WithLiveAsync((live, ct) => live.TryConnectAsync(ct), cancellationToken);
    }

    public async Task<string> GetSystemStatusAsync(CancellationToken cancellationToken = default)
    {
        await EnsureRouteAsync(cancellationToken);
        if (_useDemo)
        {
            return await demo.GetSystemStatusAsync(cancellationToken)
                   + " (emulator unreachable — demo fallback; will retry automatically)";
        }

        return await WithLiveAsync((live, ct) => live.GetSystemStatusAsync(ct), cancellationToken);
    }

    public async Task<PiPoint?> FindByNameAsync(string tagName, CancellationToken cancellationToken = default)
    {
        await EnsureRouteAsync(cancellationToken);
        return _useDemo
            ? await demo.FindByNameAsync(tagName, cancellationToken)
            : await WithLiveAsync((live, ct) => live.FindByNameAsync(tagName, ct), cancellationToken);
    }

    public async Task<PiPoint?> FindPointByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        await EnsureRouteAsync(cancellationToken);
        return _useDemo
            ? await demo.FindPointByPathAsync(path, cancellationToken)
            : await WithLiveAsync((live, ct) => live.FindPointByPathAsync(path, ct), cancellationToken);
    }

    public async Task<IReadOnlyList<PiSearchHit>> SearchByNameAsync(
        string nameFilter,
        int maxCount = 10,
        CancellationToken cancellationToken = default)
    {
        await EnsureRouteAsync(cancellationToken);
        return _useDemo
            ? await demo.SearchByNameAsync(nameFilter, maxCount, cancellationToken)
            : await WithLiveAsync((live, ct) => live.SearchByNameAsync(nameFilter, maxCount, ct), cancellationToken);
    }

    public async Task<TagSample?> GetCurrentByWebIdAsync(string webId, CancellationToken cancellationToken = default)
    {
        await EnsureRouteAsync(cancellationToken);
        return _useDemo
            ? await demo.GetCurrentByWebIdAsync(webId, cancellationToken)
            : await WithLiveAsync((live, ct) => live.GetCurrentByWebIdAsync(webId, ct), cancellationToken);
    }

    public async Task<TagSample?> GetCurrentByNameAsync(string tagName, CancellationToken cancellationToken = default)
    {
        await EnsureRouteAsync(cancellationToken);
        return _useDemo
            ? await demo.GetCurrentByNameAsync(tagName, cancellationToken)
            : await WithLiveAsync((live, ct) => live.GetCurrentByNameAsync(tagName, ct), cancellationToken);
    }

    public async Task<IReadOnlyList<TagSample>> GetRecordedByWebIdAsync(
        string webId,
        string startTime = "*-1h",
        string endTime = "*",
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        await EnsureRouteAsync(cancellationToken);
        return _useDemo
            ? await demo.GetRecordedByWebIdAsync(webId, startTime, endTime, maxCount, cancellationToken)
            : await WithLiveAsync(
                (live, ct) => live.GetRecordedByWebIdAsync(webId, startTime, endTime, maxCount, ct),
                cancellationToken);
    }

    public async Task<IReadOnlyList<TagSample>> GetRecordedByNameAsync(
        string tagName,
        string startTime = "*-1h",
        string endTime = "*",
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        await EnsureRouteAsync(cancellationToken);
        return _useDemo
            ? await demo.GetRecordedByNameAsync(tagName, startTime, endTime, maxCount, cancellationToken)
            : await WithLiveAsync(
                (live, ct) => live.GetRecordedByNameAsync(tagName, startTime, endTime, maxCount, ct),
                cancellationToken);
    }

    public async Task<AfAttribute?> FindAttributeByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        await EnsureRouteAsync(cancellationToken);
        return _useDemo
            ? await demo.FindAttributeByPathAsync(path, cancellationToken)
            : await WithLiveAsync((live, ct) => live.FindAttributeByPathAsync(path, ct), cancellationToken);
    }

    private async Task EnsureRouteAsync(CancellationToken cancellationToken)
    {
        if (DateTimeOffset.UtcNow < _nextProbeUtc)
        {
            return;
        }

        await _probeLock.WaitAsync(cancellationToken);
        try
        {
            if (DateTimeOffset.UtcNow < _nextProbeUtc)
            {
                return;
            }

            var reachable = false;
            try
            {
                reachable = await WithLiveAsync((live, ct) => live.TryConnectAsync(ct), cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Simulator probe failed for {BaseUrl}", runtime.ConfiguredBaseUrl);
            }

            var wasDemo = _useDemo;
            _useDemo = !reachable;
            _nextProbeUtc = DateTimeOffset.UtcNow.Add(ProbeTtl);

            if (reachable)
            {
                runtime.SetSimulator();
                if (wasDemo)
                {
                    logger.LogInformation(
                        "PI emulator reachable at {BaseUrl} — switching from demo fallback to simulator.",
                        runtime.ConfiguredBaseUrl);
                }
            }
            else
            {
                runtime.SetDemoFallback();
                if (!wasDemo)
                {
                    logger.LogWarning(
                        "PI emulator at {BaseUrl} unreachable — using demo fallback (re-probe every {Seconds}s).",
                        runtime.ConfiguredBaseUrl,
                        ProbeTtl.TotalSeconds);
                }
            }
        }
        finally
        {
            _probeLock.Release();
        }
    }

    private async Task<T> WithLiveAsync<T>(Func<PiWebApiDataSource, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var live = scope.ServiceProvider.GetRequiredService<PiWebApiDataSource>();
        return await action(live, ct);
    }
}
