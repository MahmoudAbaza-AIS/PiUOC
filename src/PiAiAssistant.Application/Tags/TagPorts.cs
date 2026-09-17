namespace PiAiAssistant.Application.Tags;

public interface ITagResolver
{
    Task<TagResolutionResult> ResolveAsync(string query, CancellationToken cancellationToken = default);
}

public interface ITagIntelligenceService
{
    Task<TagDetailsResult> GetTagDetailsAsync(
        string tagReference,
        TagDetailsQueryOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TagCandidate>> SearchTagsAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default);

    Task<TagHistoryResult> GetTagHistoryAsync(
        string tagReference,
        DateTimeOffset? startUtc = null,
        DateTimeOffset? endUtc = null,
        int maxCount = 100,
        CancellationToken cancellationToken = default);

    Task<TagChartResult> GetChartSeriesAsync(
        string tagReference,
        DateTimeOffset? startUtc = null,
        DateTimeOffset? endUtc = null,
        int maxCount = 200,
        CancellationToken cancellationToken = default);

    Task<TagSummaryResult> GetSummaryAsync(
        string tagReference,
        DateTimeOffset? startUtc = null,
        DateTimeOffset? endUtc = null,
        int maxCount = 500,
        CancellationToken cancellationToken = default);

    Task<MultiTagChartResult> CompareTagsAsync(
        IReadOnlyList<string> tagReferences,
        DateTimeOffset? startUtc = null,
        DateTimeOffset? endUtc = null,
        int maxCount = 150,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RelatedTagDto>> GetRelatedTagsAsync(
        string tagReference,
        CancellationToken cancellationToken = default);
}

/// <summary>Application port for curated business metadata (implemented in Infrastructure).</summary>
public interface ITagCatalogRepository
{
    Task<TagCatalogItem?> FindByAliasAsync(string alias, CancellationToken cancellationToken = default);
    Task<TagCatalogItem?> FindByCanonicalNameAsync(string canonicalName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TagCatalogItem>> SearchAsync(string query, int maxResults, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TagRelationshipItem>> GetRelationshipsAsync(string canonicalName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TagDocumentationItem>> GetDocumentationAsync(string canonicalName, CancellationToken cancellationToken = default);
}
