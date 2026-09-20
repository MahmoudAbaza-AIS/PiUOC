using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PiAiAssistant.Domain.Interfaces;
using PiAiAssistant.Infrastructure.Options;

namespace PiAiAssistant.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    private readonly IPiConnectivity _pi;
    private readonly IOptions<PiConnectionOptions> _options;
    private readonly PiDataSourceRuntimeInfo _runtime;

    public HealthController(IPiConnectivity pi, IOptions<PiConnectionOptions> options, PiDataSourceRuntimeInfo runtime)
    {
        _pi = pi;
        _options = options;
        _runtime = runtime;
    }

    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        service = "PiAiAssistant.Api",
        product = "AFAG PI Vision AI — Role-Driven Decision Assistant",
        architecture = "Clean Architecture",
        utc = DateTimeOffset.UtcNow
    });

    [HttpGet("pi")]
    public async Task<IActionResult> GetPi(CancellationToken ct)
    {
        var connected = await _pi.TryConnectAsync(ct);
        var status = connected ? await _pi.GetSystemStatusAsync(ct) : "Unable to connect.";
        return Ok(new
        {
            connected,
            demoMode = _runtime.UseDemo,
            source = _runtime.ActiveSource,
            developmentPreferSimulator = _runtime.DevelopmentPreferSimulator,
            fellBackFromSimulator = _runtime.FellBackFromSimulator,
            baseUrl = _options.Value.BaseUrl,
            configuredBaseUrl = _runtime.ConfiguredBaseUrl,
            dataArchive = _options.Value.DataArchiveName,
            status
        });
    }

    [HttpGet("ollama")]
    public async Task<IActionResult> GetOllama([FromServices] IOptions<PiAiAssistant.Infrastructure.Options.OllamaOptions> options, CancellationToken ct)
    {
        var ollama = options.Value;
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(ollama.BaseUrl), Timeout = TimeSpan.FromSeconds(5) };
            using var response = await http.GetAsync("api/tags", ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return Ok(new
            {
                reachable = response.IsSuccessStatusCode,
                model = ollama.DefaultModel,
                models = ollama.ListModels(),
                enabled = ollama.Enabled,
                statusCode = (int)response.StatusCode,
                preview = body.Length > 300 ? body[..300] + "..." : body
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                reachable = false,
                model = ollama.DefaultModel,
                models = ollama.ListModels(),
                enabled = ollama.Enabled,
                error = ex.Message
            });
        }
    }
}
