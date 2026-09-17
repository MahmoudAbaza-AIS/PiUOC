using PiAiAssistant.Application.Tags;
using PiAiAssistant.Domain.Enums;
using PiAiAssistant.Domain.Interfaces;

namespace PiAiAssistant.Application.Tags;

/// <summary>Canonical tag intelligence use case — fully testable via mocked ports.</summary>
public sealed class TagIntelligenceService(
    ITagResolver resolver,
    IPiPointReader points,
    IAfAttributeReader attributes,
    ITagValueReader values,
    ITagCatalogRepository catalog) : ITagIntelligenceService
{
    private const int MaxRawPoints = 1000;
    private static readonly TimeSpan MaxHistoryWithoutAggregation = TimeSpan.FromHours(24);

    public async Task<TagDetailsResult> GetTagDetailsAsync(
        string tagReference,
        TagDetailsQueryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new TagDetailsQueryOptions();
        var resolution = await resolver.ResolveAsync(tagReference, cancellationToken);

        if (resolution.Status == TagResolutionStatus.Ambiguous)
        {
            return new TagDetailsResult(TagResolutionStatus.Ambiguous, null, resolution.Candidates, resolution.Message);
        }

        if (resolution.Status != TagResolutionStatus.Found || resolution.Tag is null)
        {
            return new TagDetailsResult(TagResolutionStatus.NotFound, null, resolution.Candidates, resolution.Message);
        }

        var warnings = new List<string>();
        var resolved = resolution.Tag;
        var catalogItem = await catalog.FindByCanonicalNameAsync(resolved.CanonicalName, cancellationToken);

        Domain.Entities.PiPoint? point = null;
        Domain.Entities.AfAttribute? attribute = null;

        if (!string.IsNullOrWhiteSpace(resolved.AfAttributePath))
        {
            attribute = await attributes.FindAttributeByPathAsync(resolved.AfAttributePath, cancellationToken);
        }

        var pointName = resolved.PiPointName ?? catalogItem?.PiPointName;
        if (!string.IsNullOrWhiteSpace(pointName))
        {
            point = await points.FindByNameAsync(pointName, cancellationToken);
        }

        if (point is null && attribute is null)
        {
            point = await points.FindByNameAsync(resolved.CanonicalName, cancellationToken);
        }

        var webId = point?.WebId ?? attribute?.WebId ?? resolved.WebId ?? catalogItem?.PiWebId;
        TagCurrentValueDto? current = null;
        IReadOnlyList<TagValueDto> recent = [];

        if (options.IncludeCurrentValue && !string.IsNullOrWhiteSpace(webId))
        {
            var snap = await values.GetCurrentByWebIdAsync(webId, cancellationToken);
            if (snap is not null)
            {
                current = new TagCurrentValueDto(
                    snap.Value,
                    snap.TimestampUtc,
                    snap.IsGood,
                    snap.Status,
                    snap.IsQuestionable,
                    snap.IsSubstituted);

                if (!snap.IsGood)
                {
                    warnings.Add("Latest PI value is not Good quality.");
                }

                if (snap.IsQuestionable)
                {
                    warnings.Add("Latest PI value is marked Questionable.");
                }
            }
            else
            {
                warnings.Add("Current value was not available from PI.");
            }
        }

        if (options.IncludeRecentValues && !string.IsNullOrWhiteSpace(webId))
        {
            var limit = Math.Clamp(options.RecentValueLimit, 1, 100);
            var history = await values.GetRecordedByWebIdAsync(
                webId,
                options.RecentStartTime,
                "*",
                limit,
                cancellationToken);

            recent = history
                .Select(v => new TagValueDto(v.TimestampUtc, v.Value, v.Status))
                .ToList();
        }

        IReadOnlyList<RelatedTagDto> related = [];
        if (options.IncludeRelatedTags)
        {
            var rels = await catalog.GetRelationshipsAsync(resolved.CanonicalName, cancellationToken);
            related = rels
                .Select(r => new RelatedTagDto(r.ToCanonicalName, r.Relationship, r.Description, null))
                .ToList();
        }

        var business = new List<BusinessMetadataDto>();
        if (options.IncludeBusinessMetadata && catalogItem is not null)
        {
            void Add(string key, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    business.Add(new BusinessMetadataDto(key, value, "TagCatalog"));
                }
            }

            Add("OwnerTeam", catalogItem.OwnerTeam);
            Add("Criticality", catalogItem.Criticality);
            Add("EquipmentId", catalogItem.EquipmentId);
            Add("ExpectedMin", catalogItem.ExpectedMin?.ToString());
            Add("ExpectedMax", catalogItem.ExpectedMax?.ToString());
            Add("CatalogUnit", catalogItem.Unit);

            var docs = await catalog.GetDocumentationAsync(resolved.CanonicalName, cancellationToken);
            foreach (var doc in docs)
            {
                business.Add(new BusinessMetadataDto(doc.Title, doc.Body, doc.Source));
            }
        }

        var details = new TagDetails(
            RequestedName: tagReference,
            CanonicalName: resolved.CanonicalName,
            WebId: webId,
            PiPointName: point?.Name ?? pointName,
            Description: catalogItem?.DescriptionOverride ?? attribute?.Description ?? point?.Descriptor ?? resolved.Description,
            EngineeringUnits: point?.EngineeringUnits ?? attribute?.DefaultUnitsName ?? catalogItem?.Unit,
            DataType: point?.PointType ?? attribute?.TypeName,
            PointSource: point?.PointSource,
            InstrumentTag: point?.InstrumentTag,
            Location1: point?.Location1,
            Location2: point?.Location2,
            Location3: point?.Location3,
            Location4: point?.Location4,
            Location5: point?.Location5,
            ScanClass: point?.ScanClass,
            DigitalSet: point?.DigitalSetName,
            AssetPath: attribute?.ElementPath ?? catalogItem?.AfAttributePath,
            ElementName: attribute?.ElementName,
            AttributeName: attribute?.Name,
            TemplateName: attribute?.TemplateName,
            Category: attribute?.CategoryNames,
            CurrentValue: current,
            RecentValues: recent,
            RelatedTags: related,
            BusinessMetadata: business,
            Warnings: warnings);

        return new TagDetailsResult(TagResolutionStatus.Found, details, [], null);
    }

    public async Task<IReadOnlyList<TagCandidate>> SearchTagsAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        maxResults = Math.Clamp(maxResults, 1, 50);
        var catalogHits = await catalog.SearchAsync(query, maxResults, cancellationToken);
        var candidates = catalogHits
            .Select(h => new TagCandidate(
                h.CanonicalName,
                h.DescriptionOverride ?? h.DisplayName,
                h.AfAttributePath,
                h.PiPointName,
                h.PiWebId,
                "CatalogSearch"))
            .ToList();

        if (candidates.Count < maxResults)
        {
            var piHits = await points.SearchByNameAsync(query, maxResults - candidates.Count, cancellationToken);
            foreach (var hit in piHits)
            {
                if (candidates.Any(c => string.Equals(c.PiPointName, hit.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                candidates.Add(new TagCandidate(hit.Name, hit.Description, hit.Path, hit.Name, hit.WebId, "PiSearch"));
            }
        }

        return candidates;
    }

    public async Task<TagHistoryResult> GetTagHistoryAsync(
        string tagReference,
        DateTimeOffset? startUtc = null,
        DateTimeOffset? endUtc = null,
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var end = endUtc ?? DateTimeOffset.UtcNow;
        var start = startUtc ?? end.AddHours(-1);

        if (end < start)
        {
            return new TagHistoryResult(TagResolutionStatus.Error, null, [], [], "endUtc must be >= startUtc.", warnings);
        }

        if (end - start > MaxHistoryWithoutAggregation)
        {
            warnings.Add($"Requested range exceeds {MaxHistoryWithoutAggregation.TotalHours}h without aggregation; truncated to last 24 hours.");
            start = end - MaxHistoryWithoutAggregation;
        }

        maxCount = Math.Clamp(maxCount, 1, MaxRawPoints);

        var resolution = await resolver.ResolveAsync(tagReference, cancellationToken);
        if (resolution.Status == TagResolutionStatus.Ambiguous)
        {
            return new TagHistoryResult(TagResolutionStatus.Ambiguous, null, [], resolution.Candidates, resolution.Message, warnings);
        }

        if (resolution.Status != TagResolutionStatus.Found || resolution.Tag is null)
        {
            return new TagHistoryResult(TagResolutionStatus.NotFound, null, [], resolution.Candidates, resolution.Message, warnings);
        }

        var details = await GetTagDetailsAsync(
            resolution.Tag.CanonicalName,
            new TagDetailsQueryOptions(
                IncludeCurrentValue: false,
                IncludeRecentValues: false,
                IncludeRelatedTags: false,
                IncludeBusinessMetadata: false),
            cancellationToken);

        var webId = details.Details?.WebId;
        if (string.IsNullOrWhiteSpace(webId))
        {
            warnings.Add("Resolved tag has no WebId for stream history.");
            return new TagHistoryResult(TagResolutionStatus.Error, resolution.Tag.CanonicalName, [], [], "No WebId.", warnings);
        }

        var samples = await values.GetRecordedByWebIdAsync(
            webId,
            start.ToString("O"),
            end.ToString("O"),
            maxCount,
            cancellationToken);

        return new TagHistoryResult(
            TagResolutionStatus.Found,
            resolution.Tag.CanonicalName,
            samples.Select(v => new TagValueDto(v.TimestampUtc, v.Value, v.Status)).ToList(),
            [],
            null,
            warnings);
    }

    public async Task<TagChartResult> GetChartSeriesAsync(
        string tagReference,
        DateTimeOffset? startUtc = null,
        DateTimeOffset? endUtc = null,
        int maxCount = 200,
        CancellationToken cancellationToken = default)
    {
        var history = await GetTagHistoryAsync(tagReference, startUtc, endUtc, maxCount, cancellationToken);
        if (history.Status != TagResolutionStatus.Found)
        {
            return new TagChartResult(
                history.Status,
                history.CanonicalName,
                null,
                [],
                history.Candidates,
                history.Message,
                history.Warnings);
        }

        var details = await GetTagDetailsAsync(
            history.CanonicalName!,
            new TagDetailsQueryOptions(
                IncludeCurrentValue: false,
                IncludeRecentValues: false,
                IncludeRelatedTags: false,
                IncludeBusinessMetadata: false),
            cancellationToken);

        var points = history.Values
            .Select(v => new ChartPointDto(v.TimestampUtc, ToNullableDouble(v.Value)))
            .Where(p => p.Value.HasValue)
            .ToList();

        return new TagChartResult(
            TagResolutionStatus.Found,
            history.CanonicalName,
            details.Details?.EngineeringUnits,
            points,
            [],
            null,
            history.Warnings);
    }

    public async Task<TagSummaryResult> GetSummaryAsync(
        string tagReference,
        DateTimeOffset? startUtc = null,
        DateTimeOffset? endUtc = null,
        int maxCount = 500,
        CancellationToken cancellationToken = default)
    {
        var chart = await GetChartSeriesAsync(tagReference, startUtc, endUtc, maxCount, cancellationToken);
        if (chart.Status != TagResolutionStatus.Found)
        {
            return new TagSummaryResult(chart.Status, chart.CanonicalName, null, chart.Candidates, chart.Message, chart.Warnings);
        }

        var numeric = chart.Points.Where(p => p.Value.HasValue).Select(p => p.Value!.Value).ToList();
        if (numeric.Count == 0)
        {
            return new TagSummaryResult(
                TagResolutionStatus.Found,
                chart.CanonicalName,
                new TagSummaryStats(0, 0, null, null, null, null, null, startUtc, endUtc, chart.Unit),
                [],
                "No numeric samples in range.",
                chart.Warnings);
        }

        var history = await GetTagHistoryAsync(tagReference, startUtc, endUtc, maxCount, cancellationToken);
        var goodCount = history.Values.Count(v => string.Equals(v.Status, "Good", StringComparison.OrdinalIgnoreCase));

        var stats = new TagSummaryStats(
            Count: numeric.Count,
            GoodCount: goodCount,
            Min: numeric.Min(),
            Max: numeric.Max(),
            Average: numeric.Average(),
            First: numeric.First(),
            Last: numeric.Last(),
            StartUtc: chart.Points.FirstOrDefault()?.TimestampUtc,
            EndUtc: chart.Points.LastOrDefault()?.TimestampUtc,
            Unit: chart.Unit);

        return new TagSummaryResult(TagResolutionStatus.Found, chart.CanonicalName, stats, [], null, chart.Warnings);
    }

    public async Task<MultiTagChartResult> CompareTagsAsync(
        IReadOnlyList<string> tagReferences,
        DateTimeOffset? startUtc = null,
        DateTimeOffset? endUtc = null,
        int maxCount = 150,
        CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var series = new List<ChartSeriesDto>();
        var ambiguous = new List<TagCandidate>();

        foreach (var reference in tagReferences.Take(5))
        {
            var chart = await GetChartSeriesAsync(reference, startUtc, endUtc, maxCount, cancellationToken);
            if (chart.Status == TagResolutionStatus.Ambiguous)
            {
                ambiguous.AddRange(chart.Candidates);
                continue;
            }

            if (chart.Status != TagResolutionStatus.Found || string.IsNullOrWhiteSpace(chart.CanonicalName))
            {
                warnings.Add($"Could not resolve '{reference}': {chart.Message}");
                continue;
            }

            series.Add(new ChartSeriesDto(chart.CanonicalName, chart.Unit, chart.Points));
        }

        if (ambiguous.Count > 0 && series.Count == 0)
        {
            return new MultiTagChartResult(TagResolutionStatus.Ambiguous, [], ambiguous, "One or more tags are ambiguous.", warnings);
        }

        if (series.Count == 0)
        {
            return new MultiTagChartResult(TagResolutionStatus.NotFound, [], [], "No series could be loaded.", warnings);
        }

        return new MultiTagChartResult(TagResolutionStatus.Found, series, [], null, warnings);
    }

    public async Task<IReadOnlyList<RelatedTagDto>> GetRelatedTagsAsync(
        string tagReference,
        CancellationToken cancellationToken = default)
    {
        var resolution = await resolver.ResolveAsync(tagReference, cancellationToken);
        if (resolution.Status != TagResolutionStatus.Found || resolution.Tag is null)
        {
            return [];
        }

        var rels = await catalog.GetRelationshipsAsync(resolution.Tag.CanonicalName, cancellationToken);
        return rels
            .Select(r => new RelatedTagDto(r.ToCanonicalName, r.Relationship, r.Description, null))
            .ToList();
    }

    private static double? ToNullableDouble(object? value) => value switch
    {
        null => null,
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        decimal m => (double)m,
        string s when double.TryParse(s, out var parsed) => parsed,
        _ => null
    };
}
