using Microsoft.EntityFrameworkCore;
using PiAiAssistant.Application.Tags;

namespace PiAiAssistant.Infrastructure.Catalog;

public sealed class TagCatalogRepository(AppDbContext db) : ITagCatalogRepository
{
    public async Task<TagCatalogItem?> FindByAliasAsync(string alias, CancellationToken cancellationToken = default)
    {
        var normalized = alias.Trim().ToLowerInvariant();
        var tagId = await db.Aliases
            .AsNoTracking()
            .Where(a => a.Alias.ToLower() == normalized)
            .Select(a => (Guid?)a.TagCatalogId)
            .FirstOrDefaultAsync(cancellationToken);

        if (tagId is null)
        {
            return null;
        }

        var row = await db.Tags
            .AsNoTracking()
            .Include(t => t.Aliases)
            .FirstOrDefaultAsync(t => t.Id == tagId && t.IsEnabled, cancellationToken);

        return row is null ? null : Map(row);
    }

    public async Task<TagCatalogItem?> FindByCanonicalNameAsync(string canonicalName, CancellationToken cancellationToken = default)
    {
        var normalized = canonicalName.Trim().ToLowerInvariant();
        var row = await db.Tags
            .AsNoTracking()
            .Include(t => t.Aliases)
            .FirstOrDefaultAsync(
                t => t.CanonicalName.ToLower() == normalized && t.IsEnabled,
                cancellationToken);

        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<TagCatalogItem>> SearchAsync(string query, int maxResults, CancellationToken cancellationToken = default)
    {
        var q = query.Trim();
        var rows = await db.Tags
            .AsNoTracking()
            .Include(t => t.Aliases)
            .Where(t => t.IsEnabled && t.IsSearchable &&
                        (EF.Functions.Like(t.CanonicalName, $"%{q}%")
                         || (t.DisplayName != null && EF.Functions.Like(t.DisplayName, $"%{q}%"))
                         || (t.PiPointName != null && EF.Functions.Like(t.PiPointName, $"%{q}%"))
                         || (t.DescriptionOverride != null && EF.Functions.Like(t.DescriptionOverride, $"%{q}%"))
                         || t.Aliases.Any(a => EF.Functions.Like(a.Alias, $"%{q}%"))))
            .OrderBy(t => t.CanonicalName)
            .Take(Math.Clamp(maxResults, 1, 50))
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<TagCatalogItem>> ListEnabledAsync(
        int maxResults = 200,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.Tags
            .AsNoTracking()
            .Include(t => t.Aliases)
            .Where(t => t.IsEnabled)
            .OrderBy(t => t.CanonicalName)
            .Take(Math.Clamp(maxResults, 1, 500))
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task UpdatePiIdentityAsync(
        string canonicalName,
        string? piPointName,
        string? piWebId,
        CancellationToken cancellationToken = default)
    {
        var normalized = canonicalName.Trim().ToLowerInvariant();
        var row = await db.Tags.FirstOrDefaultAsync(
            t => t.CanonicalName.ToLower() == normalized,
            cancellationToken);

        if (row is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(piPointName))
        {
            row.PiPointName = piPointName;
        }

        if (!string.IsNullOrWhiteSpace(piWebId))
        {
            row.PiWebId = piWebId;
        }

        row.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TagRelationshipItem>> GetRelationshipsAsync(
        string canonicalName,
        CancellationToken cancellationToken = default)
    {
        return await db.Relationships
            .AsNoTracking()
            .Where(r => r.FromCanonicalName == canonicalName)
            .Select(r => new TagRelationshipItem
            {
                FromCanonicalName = r.FromCanonicalName,
                ToCanonicalName = r.ToCanonicalName,
                Relationship = r.Relationship,
                Description = r.Description
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TagDocumentationItem>> GetDocumentationAsync(
        string canonicalName,
        CancellationToken cancellationToken = default)
    {
        return await db.Documentation
            .AsNoTracking()
            .Where(d => d.CanonicalName == canonicalName)
            .Select(d => new TagDocumentationItem
            {
                CanonicalName = d.CanonicalName,
                Title = d.Title,
                Body = d.Body,
                Source = d.Source
            })
            .ToListAsync(cancellationToken);
    }

    private static TagCatalogItem Map(TagCatalogEntity e) => new()
    {
        Id = e.Id,
        PiWebId = e.PiWebId,
        PiPointName = e.PiPointName,
        AfAttributePath = e.AfAttributePath,
        CanonicalName = e.CanonicalName,
        DisplayName = e.DisplayName,
        DescriptionOverride = e.DescriptionOverride,
        EquipmentId = e.EquipmentId,
        OwnerTeam = e.OwnerTeam,
        Criticality = e.Criticality,
        ExpectedMin = e.ExpectedMin,
        ExpectedMax = e.ExpectedMax,
        Unit = e.Unit,
        IsSearchable = e.IsSearchable,
        IsEnabled = e.IsEnabled,
        CreatedUtc = e.CreatedUtc,
        UpdatedUtc = e.UpdatedUtc,
        Aliases = e.Aliases.Select(a => new TagAliasDto
        {
            Id = a.Id,
            TagCatalogId = a.TagCatalogId,
            Alias = a.Alias
        }).ToList()
    };
}
