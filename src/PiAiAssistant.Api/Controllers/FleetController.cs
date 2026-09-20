using Microsoft.AspNetCore.Mvc;
using PiAiAssistant.Application.Fleet;

namespace PiAiAssistant.Api.Controllers;

[ApiController]
[Route("api/fleet")]
public sealed class FleetController : ControllerBase
{
    private readonly IAfagSemanticService _svc;

    public FleetController(IAfagSemanticService svc)
    {
        _svc = svc;
    }

    [HttpGet("kingdom")]
    public async Task<IActionResult> GetKingdomOverview(CancellationToken ct) => Ok(await _svc.GetKingdomOverviewAsync(ct));

    [HttpGet("sectors/{sectorCode}")]
    public async Task<IActionResult> GetSectorOverview(string sectorCode, CancellationToken ct)
    {
        var result = await _svc.GetSectorOverviewAsync(sectorCode, ct);
        return result is null ? NotFound(new { message = $"Sector '{sectorCode}' not found." }) : Ok(result);
    }

    [HttpGet("sectors/{sectorCode}/plants/below-target")]
    public async Task<IActionResult> RankPlantsBelowTarget(string sectorCode, CancellationToken ct) => Ok(await _svc.RankPlantsBelowTargetAsync(sectorCode, ct));

    [HttpGet("plants/{plantCode}")]
    public async Task<IActionResult> GetPlantOverview(string plantCode, CancellationToken ct)
    {
        var result = await _svc.GetPlantOverviewAsync(plantCode, ct);
        return result is null ? NotFound(new { message = $"Plant '{plantCode}' not found." }) : Ok(result);
    }

    [HttpGet("plants/{plantCode}/blocks/{blockCode}/units/{unitCode}")]
    public async Task<IActionResult> GetUnitOverview(string plantCode, string blockCode, string unitCode, CancellationToken ct)
    {
        var result = await _svc.GetUnitOverviewAsync(plantCode, blockCode, unitCode, ct);
        return result is null ? NotFound(new { message = "Unit not found." }) : Ok(result);
    }
}
