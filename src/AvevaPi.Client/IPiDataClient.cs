using AvevaPi.Client.Models;

namespace AvevaPi.Client;

/// <summary>
/// Read-focused PI Web API operations used by the Tag Intelligence API.
/// Write is intentionally omitted from the assistant path.
/// </summary>
public interface IPiDataClient
{
    Task<string> GetSystemStatusAsync(CancellationToken cancellationToken = default);

    Task<bool> TryConnectAsync(CancellationToken cancellationToken = default);

    Task<PiPointInfo?> FindPointAsync(string tagName, CancellationToken cancellationToken = default);

    Task<PiPointInfo?> FindPointByPathAsync(string path, CancellationToken cancellationToken = default);

    Task<PiAttributeInfo?> FindAttributeByPathAsync(string path, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PiSearchHit>> SearchPointsAsync(
        string nameFilter,
        int maxCount = 10,
        CancellationToken cancellationToken = default);

    Task<PiTimedValue?> GetCurrentValueByWebIdAsync(string webId, CancellationToken cancellationToken = default);

    Task<PiTimedValue?> GetCurrentValueAsync(string tagName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PiTimedValue>> GetRecordedValuesByWebIdAsync(
        string webId,
        string startTime = "*-1h",
        string endTime = "*",
        int maxCount = 100,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PiTimedValue>> GetRecordedValuesAsync(
        string tagName,
        string startTime = "*-1h",
        string endTime = "*",
        int maxCount = 100,
        CancellationToken cancellationToken = default);
}
