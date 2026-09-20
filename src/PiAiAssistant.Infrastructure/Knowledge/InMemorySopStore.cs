using PiAiAssistant.Domain.Entities;
using PiAiAssistant.Domain.Interfaces;

namespace PiAiAssistant.Infrastructure.Knowledge;

/// <summary>Demo SOP corpus (3 SOPs + 1 spec) for citation in operator/plant answers.</summary>
public sealed class InMemorySopStore : ISopKnowledgeStore
{
    private readonly IReadOnlyList<SopExcerpt> _items =
    [
        new()
        {
            Id = "SOP-GT-04",
            Title = "Gas turbine flame intensity troubleshooting",
            Section = "3.2",
            AppliesTo = "PP09 Block A1 GT01",
            Body =
                "If flame intensity channels A–D indicate BAD while MW, exhaust temperature, and fuel flow remain stable, " +
                "treat as instrumentation first. Verify local panel readings. Do not de-rate solely on BAD digital quality. " +
                "Escalate to shift lead if two of four sensors remain BAD after 5 minutes."
        },
        new()
        {
            Id = "SOP-HR-12",
            Title = "Heat rate deviation investigation",
            Section = "2.1",
            AppliesTo = "PP09",
            Body =
                "When week-on-week heat rate worsens >2% at comparable load, compare block fuel meters, ambient correction, " +
                "and derates. Prioritize blocks with fuel-flow imbalance before declaring thermal efficiency loss."
        },
        new()
        {
            Id = "SOP-SEC-01",
            Title = "Sector loading shortfall response",
            Section = "1.4",
            AppliesTo = "COA",
            Body =
                "Rank plants below approved loading target. Open each plant Vision screen. Confirm outages and derates " +
                "before requesting additional units from the next sector."
        },
        new()
        {
            Id = "SPEC-GT01-FUEL",
            Title = "GT01 fuel system data sheet",
            Section = "4",
            AppliesTo = "PP09 A1 GT01",
            Body =
                "Nominal fuel flow at 70–75% load ≈ 3.7–4.0 kg/s natural gas. Sustained deviation >8% requires metering check."
        }
    ];

    public Task<IReadOnlyList<SopExcerpt>> SearchAsync(
        string query,
        int maxResults = 3,
        CancellationToken cancellationToken = default)
    {
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hits = _items
            .Select(item => new
            {
                Item = item,
                Score = tokens.Count(t =>
                    item.Title.Contains(t, StringComparison.OrdinalIgnoreCase)
                    || item.Body.Contains(t, StringComparison.OrdinalIgnoreCase)
                    || (item.AppliesTo?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false)
                    || item.Id.Contains(t, StringComparison.OrdinalIgnoreCase))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => x.Item)
            .ToList();

        if (hits.Count == 0)
        {
            hits = _items.Take(maxResults).ToList();
        }

        return Task.FromResult<IReadOnlyList<SopExcerpt>>(hits);
    }

    public Task<SopExcerpt?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(_items.FirstOrDefault(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase)));
}
