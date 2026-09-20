using Microsoft.AspNetCore.Mvc;
using PiAiAssistant.Application.Fleet;

namespace PiAiAssistant.Api.Controllers;

[ApiController]
[Route("api/briefings")]
public sealed class BriefingsController : ControllerBase
{
    private readonly IBriefingService _svc;

    public BriefingsController(IBriefingService svc)
    {
        _svc = svc;
    }

    [HttpGet("demo")]
    public async Task<IActionResult> GetDemo([FromQuery] string? lang, CancellationToken ct) => Ok(await _svc.GetDemoPushesAsync(lang ?? "en", ct));

    [HttpGet("executive/morning")]
    public async Task<IActionResult> GetMorningExecutive([FromQuery] string? lang, CancellationToken ct) => Ok(await _svc.GetMorningExecutiveBriefAsync(lang ?? "en", ct));

    [HttpGet("sector/{sectorCode}")]
    public async Task<IActionResult> GetSectorDeviationAlert(string sectorCode, [FromQuery] string? lang, CancellationToken ct) => Ok(await _svc.GetSectorDeviationAlertAsync(sectorCode, lang ?? "en", ct));

    [HttpGet("plants/{plantCode}")]
    public async Task<IActionResult> GetPlantDigest(string plantCode, [FromQuery] string? lang, CancellationToken ct) => Ok(await _svc.GetPlantDigestAsync(plantCode, lang ?? "en", ct));

    [HttpGet("alarms/{plantCode}/{blockCode}/{unitCode}")]
    public async Task<IActionResult> GetOperatorAlarmCard(string plantCode, string blockCode, string unitCode, [FromQuery] string? lang, CancellationToken ct) => Ok(await _svc.GetOperatorAlarmCardAsync(plantCode, blockCode, unitCode, lang ?? "en", ct));
}
