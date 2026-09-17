using PiAiAssistant.Domain.Interfaces;

namespace PiAiAssistant.Application.Tags;

/// <summary>
/// Resolves PI point identity for catalog rows and writes WebIds back to SQLite.
/// Application use case — depends only on ports (no EF / HttpClient).
/// </summary>
public sealed class TagCatalogSyncService(
    ITagCatalogRepository catalog,
    IPiPointReader points) : ITagCatalogSyncService
{
    public async Task<TagCatalogSyncResult> SyncPiIdentitiesAsync(CancellationToken cancellationToken = default)
    {
        var rows = await catalog.ListEnabledAsync(500, cancellationToken);
        var updated = 0;
        var unresolved = 0;
        var warnings = new List<string>();

        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(row.PiWebId)
                && !string.IsNullOrWhiteSpace(row.PiPointName)
                && !row.PiWebId.StartsWith("DEMO_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lookupName = row.PiPointName ?? row.CanonicalName;
            var point = await points.FindByNameAsync(lookupName, cancellationToken);
            if (point is null && !string.Equals(lookupName, row.CanonicalName, StringComparison.OrdinalIgnoreCase))
            {
                point = await points.FindByNameAsync(row.CanonicalName, cancellationToken);
            }

            if (point is null)
            {
                unresolved++;
                warnings.Add($"No PI point for catalog tag '{row.CanonicalName}' (tried '{lookupName}').");
                continue;
            }

            await catalog.UpdatePiIdentityAsync(
                row.CanonicalName,
                point.Name,
                point.WebId,
                cancellationToken);
            updated++;
        }

        return new TagCatalogSyncResult(rows.Count, updated, unresolved, warnings);
    }
}
