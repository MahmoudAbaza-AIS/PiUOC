using PiAiAssistant.Domain.Enums;

namespace PiAiAssistant.Application.Tags;

public sealed record TagDetails(
    string RequestedName,
    string CanonicalName,
    string? WebId,
    string? PiPointName,
    string? Description,
    string? EngineeringUnits,
    string? DataType,
    string? PointSource,
    string? InstrumentTag,
    string? Location1,
    string? Location2,
    string? Location3,
    string? Location4,
    string? Location5,
    string? ScanClass,
    string? DigitalSet,
    string? AssetPath,
    string? ElementName,
    string? AttributeName,
    string? TemplateName,
    string? Category,
    TagCurrentValueDto? CurrentValue,
    IReadOnlyList<TagValueDto> RecentValues,
    IReadOnlyList<RelatedTagDto> RelatedTags,
    IReadOnlyList<BusinessMetadataDto> BusinessMetadata,
    IReadOnlyList<string> Warnings);

public sealed record TagCurrentValueDto(
    object? Value,
    DateTimeOffset TimestampUtc,
    bool IsGood,
    string? Status,
    bool IsQuestionable,
    bool IsSubstituted);

public sealed record TagValueDto(
    DateTimeOffset TimestampUtc,
    object? Value,
    string? Status);

public sealed record RelatedTagDto(
    string Name,
    string Relationship,
    string? Description,
    string? WebId);

public sealed record BusinessMetadataDto(
    string Key,
    string Value,
    string Source);

public sealed record TagCandidate(
    string CanonicalName,
    string? Description,
    string? AssetPath,
    string? PiPointName,
    string? WebId,
    string MatchReason);

public sealed record ResolvedTag(
    string CanonicalName,
    string? WebId,
    string? PiPointName,
    string? AfAttributePath,
    string? Description,
    string ResolutionSource);

public sealed record TagResolutionResult(
    TagResolutionStatus Status,
    ResolvedTag? Tag,
    IReadOnlyList<TagCandidate> Candidates,
    string? Message = null);

public sealed record TagDetailsResult(
    TagResolutionStatus Status,
    TagDetails? Details,
    IReadOnlyList<TagCandidate> Candidates,
    string? Message = null);

public sealed record TagDetailsQueryOptions(
    bool IncludeCurrentValue = true,
    bool IncludeRecentValues = true,
    bool IncludeRelatedTags = true,
    bool IncludeBusinessMetadata = true,
    int RecentValueLimit = 10,
    string RecentStartTime = "*-1h");

public sealed record TagHistoryResult(
    TagResolutionStatus Status,
    string? CanonicalName,
    IReadOnlyList<TagValueDto> Values,
    IReadOnlyList<TagCandidate> Candidates,
    string? Message = null,
    IReadOnlyList<string>? Warnings = null);

/// <summary>Chart-ready time series for UI visualization.</summary>
public sealed record ChartPointDto(DateTimeOffset TimestampUtc, double? Value);

public sealed record ChartSeriesDto(
    string Name,
    string? Unit,
    IReadOnlyList<ChartPointDto> Points);

public sealed record TagChartResult(
    TagResolutionStatus Status,
    string? CanonicalName,
    string? Unit,
    IReadOnlyList<ChartPointDto> Points,
    IReadOnlyList<TagCandidate> Candidates,
    string? Message = null,
    IReadOnlyList<string>? Warnings = null);

public sealed record TagSummaryStats(
    int Count,
    int GoodCount,
    double? Min,
    double? Max,
    double? Average,
    double? First,
    double? Last,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    string? Unit);

public sealed record TagSummaryResult(
    TagResolutionStatus Status,
    string? CanonicalName,
    TagSummaryStats? Stats,
    IReadOnlyList<TagCandidate> Candidates,
    string? Message = null,
    IReadOnlyList<string>? Warnings = null);

public sealed record MultiTagChartResult(
    TagResolutionStatus Status,
    IReadOnlyList<ChartSeriesDto> Series,
    IReadOnlyList<TagCandidate> AmbiguousCandidates,
    string? Message = null,
    IReadOnlyList<string>? Warnings = null);

public sealed class TagCatalogItem
{
    public Guid Id { get; set; }
    public string? PiWebId { get; set; }
    public string? PiPointName { get; set; }
    public string? AfAttributePath { get; set; }
    public required string CanonicalName { get; set; }
    public string? DisplayName { get; set; }
    public string? DescriptionOverride { get; set; }
    public string? EquipmentId { get; set; }
    public string? OwnerTeam { get; set; }
    public string? Criticality { get; set; }
    public decimal? ExpectedMin { get; set; }
    public decimal? ExpectedMax { get; set; }
    public string? Unit { get; set; }
    public bool IsSearchable { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public List<TagAliasDto> Aliases { get; set; } = [];
}

public sealed class TagAliasDto
{
    public Guid Id { get; set; }
    public Guid TagCatalogId { get; set; }
    public required string Alias { get; set; }
}

public sealed class TagRelationshipItem
{
    public required string FromCanonicalName { get; set; }
    public required string ToCanonicalName { get; set; }
    public required string Relationship { get; set; }
    public string? Description { get; set; }
}

public sealed class TagDocumentationItem
{
    public required string CanonicalName { get; set; }
    public required string Title { get; set; }
    public required string Body { get; set; }
    public string Source { get; set; } = "TagDocumentation";
}
