using Microsoft.AspNetCore.Mvc;
using PiAiAssistant.Application.Tags;
using PiAiAssistant.Domain.Enums;

namespace PiAiAssistant.Api.Controllers;

[ApiController]
[Route("api/tags")]
public sealed class TagsController : ControllerBase
{
    private readonly ITagIntelligenceService _svc;

    public TagsController(ITagIntelligenceService svc)
    {
        _svc = svc;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int? maxResults, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { message = "Query parameter 'q' is required." });
        }

        return Ok(await _svc.SearchTagsAsync(q, maxResults ?? 10, ct));
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] string reference, [FromQuery] DateTimeOffset? startUtc, [FromQuery] DateTimeOffset? endUtc, [FromQuery] int? maxCount, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return BadRequest(new { message = "Query parameter 'reference' is required." });
        }

        var result = await _svc.GetTagHistoryAsync(reference, startUtc, endUtc, maxCount ?? 100, ct);
        return MapTagResult(result.Status, result);
    }

    [HttpGet("chart")]
    public async Task<IActionResult> Chart([FromQuery] string reference, [FromQuery] DateTimeOffset? startUtc, [FromQuery] DateTimeOffset? endUtc, [FromQuery] int? maxCount, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return BadRequest(new { message = "Query parameter 'reference' is required." });
        }

        var result = await _svc.GetChartSeriesAsync(reference, startUtc, endUtc, maxCount ?? 200, ct);
        return MapTagResult(result.Status, result);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] string reference, [FromQuery] DateTimeOffset? startUtc, [FromQuery] DateTimeOffset? endUtc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return BadRequest(new { message = "Query parameter 'reference' is required." });
        }

        var result = await _svc.GetSummaryAsync(reference, startUtc, endUtc, 500, ct);
        return MapTagResult(result.Status, result);
    }

    [HttpGet("compare")]
    public async Task<IActionResult> Compare([FromQuery] string references, [FromQuery] DateTimeOffset? startUtc, [FromQuery] DateTimeOffset? endUtc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(references))
        {
            return BadRequest(new { message = "Query parameter 'references' is required (comma-separated)." });
        }

        var list = references.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = await _svc.CompareTagsAsync(list, startUtc, endUtc, 150, ct);
        return MapTagResult(result.Status, result);
    }

    [HttpGet("details")]
    public async Task<IActionResult> Details(
        [FromQuery] string reference,
        [FromQuery] bool? includeCurrentValue,
        [FromQuery] bool? includeRecentValues,
        [FromQuery] bool? includeRelatedTags,
        [FromQuery] bool? includeBusinessMetadata,
        [FromQuery] int? recentValueLimit,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return BadRequest(new { message = "Query parameter 'reference' is required." });
        }

        var result = await _svc.GetTagDetailsAsync(
            reference,
            new TagDetailsQueryOptions(
                IncludeCurrentValue: includeCurrentValue ?? true,
                IncludeRecentValues: includeRecentValues ?? true,
                IncludeRelatedTags: includeRelatedTags ?? true,
                IncludeBusinessMetadata: includeBusinessMetadata ?? true,
                RecentValueLimit: recentValueLimit ?? 10),
            ct);

        return MapTagResult(result.Status, result);
    }

    [HttpGet("{tagReference}")]
    public async Task<IActionResult> GetTag(
        string tagReference,
        [FromQuery] bool? includeCurrentValue,
        [FromQuery] bool? includeRecentValues,
        [FromQuery] bool? includeRelatedTags,
        [FromQuery] bool? includeBusinessMetadata,
        [FromQuery] int? recentValueLimit,
        CancellationToken ct)
    {
        var result = await _svc.GetTagDetailsAsync(
            Uri.UnescapeDataString(tagReference),
            new TagDetailsQueryOptions(
                IncludeCurrentValue: includeCurrentValue ?? true,
                IncludeRecentValues: includeRecentValues ?? true,
                IncludeRelatedTags: includeRelatedTags ?? true,
                IncludeBusinessMetadata: includeBusinessMetadata ?? true,
                RecentValueLimit: recentValueLimit ?? 10),
            ct);

        return MapTagResult(result.Status, result);
    }

    private IActionResult MapTagResult(TagResolutionStatus status, object payload) => status switch
    {
        TagResolutionStatus.Found => Ok(payload),
        TagResolutionStatus.Ambiguous => StatusCode(StatusCodes.Status409Conflict, payload),
        TagResolutionStatus.NotFound => NotFound(payload),
        TagResolutionStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, payload),
        _ => BadRequest(payload)
    };
}
