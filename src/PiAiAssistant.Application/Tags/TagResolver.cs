using PiAiAssistant.Application.Abstractions;
using PiAiAssistant.Application.Tags;
using PiAiAssistant.Domain.Enums;
using PiAiAssistant.Domain.Interfaces;

namespace PiAiAssistant.Application.Tags;

/// <summary>
/// Resolves user input: alias → AF path → PI point → constrained search.
/// Depends only on Domain/Application ports (no HttpClient / EF).
/// </summary>
public sealed class TagResolver(
    ITagCatalogRepository catalog,
    IPiPointReader points,
    IAfAttributeReader attributes,
    IMetadataCache cache) : ITagResolver
{
    private static readonly TimeSpan MetadataCacheTtl = TimeSpan.FromMinutes(15);

    public async Task<TagResolutionResult> ResolveAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var raw = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new TagResolutionResult(TagResolutionStatus.NotFound, null, [], "Empty tag reference.");
        }

        var cacheKey = $"resolve:{raw.ToLowerInvariant()}";
        if (cache.TryGet(cacheKey, out TagResolutionResult? cached) && cached is not null
            && cached.Status is TagResolutionStatus.Found or TagResolutionStatus.Ambiguous)
        {
            return cached;
        }

        var byAlias = await catalog.FindByAliasAsync(raw, cancellationToken);
        if (byAlias is not null)
        {
            return Cache(cacheKey, FoundFromCatalog(byAlias, "Alias"));
        }

        var byCanonical = await catalog.FindByCanonicalNameAsync(raw, cancellationToken);
        if (byCanonical is not null)
        {
            return Cache(cacheKey, FoundFromCatalog(byCanonical, "CatalogCanonical"));
        }

        var catalogHits = await catalog.SearchAsync(raw, 10, cancellationToken);
        var exactCatalog = catalogHits.FirstOrDefault(h =>
            string.Equals(h.CanonicalName, raw, StringComparison.OrdinalIgnoreCase)
            || string.Equals(h.PiPointName, raw, StringComparison.OrdinalIgnoreCase)
            || string.Equals(h.DisplayName, raw, StringComparison.OrdinalIgnoreCase)
            || h.Aliases.Any(a => string.Equals(a.Alias, raw, StringComparison.OrdinalIgnoreCase)));

        if (exactCatalog is not null)
        {
            return Cache(cacheKey, FoundFromCatalog(exactCatalog, "CatalogExact"));
        }

        if (LooksLikeAfPath(raw))
        {
            var attr = await attributes.FindAttributeByPathAsync(raw, cancellationToken);
            if (attr is not null)
            {
                var resolved = new ResolvedTag(
                    CanonicalName: attr.Path ?? attr.Name,
                    WebId: attr.WebId,
                    PiPointName: ExtractPiPointFromConfig(attr.ConfigString),
                    AfAttributePath: attr.Path,
                    Description: attr.Description,
                    ResolutionSource: "AfAttributePath");

                return Cache(cacheKey, new TagResolutionResult(TagResolutionStatus.Found, resolved, []));
            }
        }

        var point = await points.FindByNameAsync(raw, cancellationToken);
        if (point is not null)
        {
            var catalogForPoint = catalogHits.FirstOrDefault(h =>
                string.Equals(h.PiPointName, point.Name, StringComparison.OrdinalIgnoreCase));

            var resolved = new ResolvedTag(
                CanonicalName: catalogForPoint?.CanonicalName ?? point.Name,
                WebId: point.WebId,
                PiPointName: point.Name,
                AfAttributePath: catalogForPoint?.AfAttributePath,
                Description: catalogForPoint?.DescriptionOverride ?? point.Descriptor,
                ResolutionSource: "PiPoint");

            return Cache(cacheKey, new TagResolutionResult(TagResolutionStatus.Found, resolved, []));
        }

        var candidates = new List<TagCandidate>();
        foreach (var hit in catalogHits)
        {
            candidates.Add(new TagCandidate(
                hit.CanonicalName,
                hit.DescriptionOverride ?? hit.DisplayName,
                hit.AfAttributePath,
                hit.PiPointName,
                hit.PiWebId,
                "CatalogSearch"));
        }

        var piHits = await points.SearchByNameAsync(raw, 10, cancellationToken);
        foreach (var hit in piHits)
        {
            if (candidates.Any(c => string.Equals(c.PiPointName, hit.Name, StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(c.CanonicalName, hit.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            candidates.Add(new TagCandidate(
                hit.Name,
                hit.Description,
                hit.Path,
                hit.Name,
                hit.WebId,
                "PiSearch"));
        }

        if (candidates.Count == 1)
        {
            var only = candidates[0];
            var resolved = new ResolvedTag(
                only.CanonicalName,
                only.WebId,
                only.PiPointName,
                only.AssetPath,
                only.Description,
                only.MatchReason);

            return Cache(cacheKey, new TagResolutionResult(TagResolutionStatus.Found, resolved, []));
        }

        if (candidates.Count > 1)
        {
            return Cache(cacheKey, new TagResolutionResult(
                TagResolutionStatus.Ambiguous,
                null,
                candidates.Take(10).ToList(),
                $"Found {candidates.Count} matching tags. Please choose one."));
        }

        return new TagResolutionResult(
            TagResolutionStatus.NotFound,
            null,
            [],
            $"No tag found for '{raw}'.");
    }

    private TagResolutionResult Cache(string key, TagResolutionResult result)
    {
        if (result.Status is TagResolutionStatus.Found or TagResolutionStatus.Ambiguous)
        {
            cache.Set(key, result, MetadataCacheTtl);
        }

        return result;
    }

    private static TagResolutionResult FoundFromCatalog(TagCatalogItem item, string source)
        => new(
            TagResolutionStatus.Found,
            new ResolvedTag(
                item.CanonicalName,
                item.PiWebId,
                item.PiPointName,
                item.AfAttributePath,
                item.DescriptionOverride ?? item.DisplayName,
                source),
            []);

    private static bool LooksLikeAfPath(string value)
        => value.Contains('|') || value.Contains(@"\\") || value.StartsWith(@"\\", StringComparison.Ordinal);

    private static string? ExtractPiPointFromConfig(string? configString)
    {
        if (string.IsNullOrWhiteSpace(configString))
        {
            return null;
        }

        var trimmed = configString.Trim().TrimStart('\\');
        return trimmed.Split('\\', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
    }
}
