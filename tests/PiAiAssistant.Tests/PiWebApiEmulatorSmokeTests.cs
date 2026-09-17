using Microsoft.Extensions.Options;
using PiAiAssistant.Infrastructure.Options;
using PiAiAssistant.Infrastructure.Pi;

namespace PiAiAssistant.Tests;

/// <summary>
/// Optional live smoke against Shirajum Munir's PI WebAPI Emulator on localhost:5000.
/// Skips automatically when the emulator is not running.
/// </summary>
public sealed class PiWebApiEmulatorSmokeTests
{
    private const string EmulatorBase = "http://localhost:5000/piwebapi";

    [Fact]
    public async Task Emulator_FindAndReadHoustonTemperature_WhenRunning()
    {
        if (!await IsEmulatorUpAsync())
        {
            return; // skip silently when emulator is offline
        }

        var options = Options.Create(new PiConnectionOptions
        {
            UseDemoMode = false,
            BaseUrl = EmulatorBase,
            DataArchiveName = "PIServer1",
            DefaultAfServer = "AFServer1",
            DefaultAfDatabase = "NuGreen",
            AuthMode = "Anonymous",
            TimeoutSeconds = 10
        });

        using var http = new HttpClient { BaseAddress = new Uri(EmulatorBase.TrimEnd('/') + "/") };
        var sut = new PiWebApiDataSource(http, options);

        Assert.True(await sut.TryConnectAsync());

        var point = await sut.FindByNameAsync("Houston.B-210.Temperature");
        Assert.NotNull(point);
        Assert.Equal("pt_Houston.B-210.Temperature", point!.WebId);

        var sample = await sut.GetCurrentByWebIdAsync(point.WebId!);
        Assert.NotNull(sample);
        Assert.True(sample!.IsGood);
        Assert.NotNull(sample.Value);

        var attr = await sut.FindAttributeByPathAsync(
            @"\\AFServer1\NuGreen\Houston\Cracking Process\Equipment\B-210|Temperature");
        Assert.NotNull(attr);
        Assert.Equal("Temperature", attr!.Name);
        Assert.Equal("Double", attr.TypeName);

        var hits = await sut.SearchByNameAsync("Houston.B-210", 10);
        Assert.True(hits.Count >= 2);
    }

    private static async Task<bool> IsEmulatorUpAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var response = await http.GetAsync(EmulatorBase);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
