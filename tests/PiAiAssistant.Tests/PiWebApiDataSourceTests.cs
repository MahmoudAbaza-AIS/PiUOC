using Microsoft.Extensions.Options;
using PiAiAssistant.Application.Exceptions;
using PiAiAssistant.Domain.Exceptions;
using PiAiAssistant.Infrastructure.Options;
using PiAiAssistant.Infrastructure.Pi;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace PiAiAssistant.Tests;

/// <summary>
/// Infrastructure HTTP contract tests: real <see cref="PiWebApiDataSource"/> against a fake PI Web API (WireMock).
/// Complements DemoPiDataSource / Application unit tests by validating request shapes and HTTP→exception mapping.
/// </summary>
public sealed class PiWebApiDataSourceTests : IDisposable
{
    private const string Archive = "PISRV01";
    private const string WebId = "F1DPabc123";
    private const string PointPath = @"\\PISRV01\SINUSOID";

    private readonly WireMockServer _server;
    private readonly HttpClient _http;
    private readonly PiWebApiDataSource _sut;

    public PiWebApiDataSourceTests()
    {
        _server = WireMockServer.Start();

        var options = Options.Create(new PiConnectionOptions
        {
            UseDemoMode = false,
            BaseUrl = $"{_server.Urls[0].TrimEnd('/')}/piwebapi",
            DataArchiveName = Archive,
            AuthMode = "Anonymous",
            TimeoutSeconds = 10
        });

        _http = new HttpClient
        {
            BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/")
        };

        _sut = new PiWebApiDataSource(_http, options);
    }

    public void Dispose()
    {
        _http.Dispose();
        _server.Stop();
        _server.Dispose();
    }

    [Fact]
    public async Task GetCurrentByName_ReturnsValue_WhenPointExists()
    {
        StubPointLookup(PointPath, WebId, "SINUSOID");
        StubCurrentValue(WebId, 42.3, good: true);

        var result = await _sut.GetCurrentByNameAsync("SINUSOID");

        Assert.NotNull(result);
        Assert.Equal(42.3, Assert.IsType<double>(result!.Value));
        Assert.True(result.IsGood);
        Assert.Equal("Good", result.Status);

        Assert.Contains(_server.LogEntries, l =>
            l.RequestMessage?.Path?.Contains("/piwebapi/points", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(_server.LogEntries, l =>
            l.RequestMessage?.Path?.Contains($"/piwebapi/streams/{WebId}/value", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task FindByName_ReturnsNull_WhenTagDoesNotExist()
    {
        _server
            .Given(Request.Create()
                .WithPath("/piwebapi/points")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        var point = await _sut.FindByNameAsync("NOT_A_REAL_TAG");

        Assert.Null(point);
    }

    [Fact]
    public async Task GetCurrentByName_ThrowsTagNotFound_WhenPointMissing()
    {
        _server
            .Given(Request.Create()
                .WithPath("/piwebapi/points")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        await Assert.ThrowsAsync<TagNotFoundException>(() => _sut.GetCurrentByNameAsync("NOT_A_REAL_TAG"));
    }

    [Fact]
    public async Task FindByName_ThrowsPiAuthenticationException_When401Returned()
    {
        _server
            .Given(Request.Create()
                .WithPath("/piwebapi/points")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(401));

        await Assert.ThrowsAsync<PiAuthenticationException>(() => _sut.FindByNameAsync("SINUSOID"));
    }

    [Fact]
    public async Task GetCurrentByWebId_ThrowsPiConnectionException_When500Returned()
    {
        _server
            .Given(Request.Create()
                .WithPath($"/piwebapi/streams/{WebId}/value")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

        await Assert.ThrowsAsync<PiConnectionException>(() => _sut.GetCurrentByWebIdAsync(WebId));
    }

    [Fact]
    public async Task GetRecordedByWebId_SendsTimeRangeQueryParams()
    {
        _server
            .Given(Request.Create()
                .WithPath($"/piwebapi/streams/{WebId}/recorded")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                    {
                      "Items": [
                        { "Timestamp": "2026-09-17T13:00:00Z", "Value": 10.0, "Good": true },
                        { "Timestamp": "2026-09-17T14:00:00Z", "Value": 11.5, "Good": true }
                      ]
                    }
                    """));

        var samples = await _sut.GetRecordedByWebIdAsync(WebId, "*-1h", "*", 50);

        Assert.Equal(2, samples.Count);
        Assert.Equal(10.0, Assert.IsType<double>(samples[0].Value));
        Assert.Equal(11.5, Assert.IsType<double>(samples[1].Value));

        var recorded = Assert.Single(_server.LogEntries, l =>
            l.RequestMessage?.Path?.Contains("/recorded", StringComparison.OrdinalIgnoreCase) == true);
        var query = recorded.RequestMessage?.RawQuery ?? string.Empty;
        Assert.Contains("startTime", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("endTime", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("maxCount=50", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FindByName_RequestsArchiveQualifiedPath()
    {
        StubPointLookup(PointPath, WebId, "SINUSOID");

        var point = await _sut.FindByNameAsync("SINUSOID");

        Assert.NotNull(point);
        Assert.Equal("SINUSOID", point!.Name);
        Assert.Equal(WebId, point.WebId);

        var entry = Assert.Single(_server.LogEntries);
        Assert.Contains("/piwebapi/points", entry.RequestMessage?.Path ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        var pathParam = entry.RequestMessage?.GetParameter("path")?.FirstOrDefault();
        Assert.Equal(PointPath, pathParam);
    }

    [Fact]
    public async Task TryConnect_ReturnsTrue_WhenHomeSucceeds()
    {
        // Absolute home without trailing slash (emulator-compatible).
        _server
            .Given(Request.Create().WithPath("/piwebapi").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("""{"Links":{}}"""));

        Assert.True(await _sut.TryConnectAsync());
    }

    [Fact]
    public async Task TryConnect_FallsBackToDataServers_WhenTrailingSlashHome404()
    {
        _server
            .Given(Request.Create().WithPath("/piwebapi").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        _server
            .Given(Request.Create().WithPath("/piwebapi/").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        _server
            .Given(Request.Create().WithPath("/piwebapi/dataservers").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{ "Items": [ { "Name": "PIServer1", "WebId": "dataserver_id" } ] }"""));

        Assert.True(await _sut.TryConnectAsync());
    }

    private void StubPointLookup(string path, string webId, string name)
    {
        _server
            .Given(Request.Create()
                .WithPath("/piwebapi/points")
                .WithParam("path", path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(
                    $$"""
                    {
                      "WebId": "{{webId}}",
                      "Name": "{{name}}",
                      "Descriptor": "WireMock test point",
                      "EngineeringUnits": "deg",
                      "PointType": "Float32"
                    }
                    """));
    }

    private void StubCurrentValue(string webId, double value, bool good)
    {
        _server
            .Given(Request.Create()
                .WithPath($"/piwebapi/streams/{webId}/value")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody($$"""
                    {
                      "Timestamp": "2026-09-17T14:00:00Z",
                      "Value": {{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
                      "Good": {{(good ? "true" : "false")}},
                      "UnitsAbbreviation": "deg"
                    }
                    """));
    }

    [Fact]
    public async Task FindByName_FallsBackToPointWebId_WhenPathLookupMissing()
    {
        _server
            .Given(Request.Create()
                .WithPath("/piwebapi/points")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        _server
            .Given(Request.Create()
                .WithPath("/piwebapi/points/pt_Houston.B-210.Temperature")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                    {
                      "WebId": "pt_Houston.B-210.Temperature",
                      "Name": "Houston.B-210.Temperature",
                      "Path": "\\\\PIServer1\\Houston.B-210.Temperature",
                      "EngineeringUnits": "C",
                      "PointType": "Float32"
                    }
                    """));

        var options = Options.Create(new PiConnectionOptions
        {
            UseDemoMode = false,
            BaseUrl = $"{_server.Urls[0].TrimEnd('/')}/piwebapi",
            DataArchiveName = "PIServer1",
            AuthMode = "Anonymous"
        });
        using var http = new HttpClient { BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/") };
        var sut = new PiWebApiDataSource(http, options);

        var point = await sut.FindByNameAsync("Houston.B-210.Temperature");

        Assert.NotNull(point);
        Assert.Equal("Houston.B-210.Temperature", point!.Name);
        Assert.Equal("pt_Houston.B-210.Temperature", point.WebId);
    }

    [Fact]
    public async Task SearchByName_FallsBackToDataServerPoints_WhenSearchEndpointMissing()
    {
        _server
            .Given(Request.Create()
                .WithPath("/piwebapi/points/search")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        _server
            .Given(Request.Create()
                .WithPath("/piwebapi/dataservers")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                    { "Items": [ { "Name": "PIServer1", "WebId": "dataserver_id" } ] }
                    """));

        _server
            .Given(Request.Create()
                .WithPath("/piwebapi/dataservers/dataserver_id/points")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                    {
                      "Items": [
                        { "Name": "Houston.B-210.Temperature", "WebId": "pt_Houston.B-210.Temperature", "Descriptor": "temp" },
                        { "Name": "Houston.B-210.Pressure", "WebId": "pt_Houston.B-210.Pressure", "Descriptor": "press" }
                      ]
                    }
                    """));

        var hits = await _sut.SearchByNameAsync("Pressure", 10);

        Assert.Contains(hits, h => h.Name == "Houston.B-210.Pressure");
    }
}
