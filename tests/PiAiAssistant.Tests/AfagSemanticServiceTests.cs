using Microsoft.Extensions.DependencyInjection;
using PiAiAssistant.Application.Fleet;
using PiAiAssistant.Domain.Enums;
using PiAiAssistant.Domain.Interfaces;
using PiAiAssistant.Infrastructure.Afag;
using PiAiAssistant.Infrastructure.Knowledge;

namespace PiAiAssistant.Tests;

public class AfagSemanticServiceTests
{
    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAfagHierarchyReader, AfagDemoHierarchyReader>();
        services.AddSingleton<ISopKnowledgeStore, InMemorySopStore>();
        services.AddScoped<IAfagSemanticService, AfagSemanticService>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Kingdom_narrative_uses_gw_and_flags_coa_via_pp09()
    {
        await using var sp = Build();
        var fleet = await sp.GetRequiredService<IAfagSemanticService>().GetKingdomOverviewAsync();

        Assert.Equal("COA", fleet.FlaggedSectorCode);
        Assert.Contains("40.4", fleet.NarrativeEn); // 40359 MW → GW
        Assert.Contains("PP09", fleet.NarrativeEn);
        Assert.Contains("3.2", fleet.NarrativeEn);
        Assert.DoesNotContain("Zuluf", fleet.NarrativeEn);
        Assert.False(string.IsNullOrWhiteSpace(fleet.NarrativeAr));
        Assert.Contains("Kingdom", fleet.VisionJumpPath);
    }

    [Fact]
    public async Task Sector_overview_lists_below_target_sorted_by_delta()
    {
        await using var sp = Build();
        var sector = await sp.GetRequiredService<IAfagSemanticService>().GetSectorOverviewAsync("COA");

        Assert.NotNull(sector);
        Assert.NotEmpty(sector!.PlantsBelowTarget);
        Assert.True(sector.PlantsBelowTarget.Zip(sector.PlantsBelowTarget.Skip(1))
            .All(pair => pair.First.DeltaVsTarget <= pair.Second.DeltaVsTarget));
        Assert.Contains(sector.PlantsBelowTarget, p => p.PlantCode == "PP09" && p.DeltaVsTarget == -12);
        Assert.Contains("71", sector.NarrativeEn);
    }

    [Fact]
    public async Task Unknown_sector_returns_null()
    {
        await using var sp = Build();
        Assert.Null(await sp.GetRequiredService<IAfagSemanticService>().GetSectorOverviewAsync("NOPE"));
    }

    [Fact]
    public async Task Plant_overview_attributes_block_a1_and_cites_sop()
    {
        await using var sp = Build();
        var plant = await sp.GetRequiredService<IAfagSemanticService>().GetPlantOverviewAsync("PP09");

        Assert.NotNull(plant);
        Assert.Contains("A1", plant!.RootCauseNarrativeEn);
        Assert.Contains("3.2", plant.RootCauseNarrativeEn);
        Assert.NotNull(plant.SopCitation);
        Assert.Equal(8, plant.Blocks.Count);
        Assert.Equal(7, plant.GenTrend.Count);
        Assert.Equal(7, plant.FuelTrend.Count);
        Assert.Contains("fuel", plant.RecommendedAction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_plant_returns_null()
    {
        await using var sp = Build();
        Assert.Null(await sp.GetRequiredService<IAfagSemanticService>().GetPlantOverviewAsync("PP99"));
    }

    [Fact]
    public async Task Unit_overview_marks_instrumentation_not_combustion()
    {
        await using var sp = Build();
        var unit = await sp.GetRequiredService<IAfagSemanticService>().GetUnitOverviewAsync("PP09", "A1", "GT01");

        Assert.NotNull(unit);
        Assert.Contains("instrumentation", unit!.LikelyCause, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not verified combustion", unit.LikelyCause, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Shift Lead / Control Room", unit.EscalationContact);
        Assert.Contains("GT-04", unit.SopCitation ?? "");
    }

    [Fact]
    public async Task Unknown_unit_returns_null()
    {
        await using var sp = Build();
        Assert.Null(await sp.GetRequiredService<IAfagSemanticService>()
            .GetUnitOverviewAsync("PP09", "A1", "GT99"));
    }

    [Fact]
    public async Task Rank_plants_excludes_those_at_or_above_target()
    {
        await using var sp = Build();
        var ranked = await sp.GetRequiredService<IAfagSemanticService>().RankPlantsBelowTargetAsync("COA");

        Assert.All(ranked, p =>
        {
            Assert.NotNull(p.TargetLoadingPercent);
            Assert.True(p.LoadingPercent < p.TargetLoadingPercent);
            Assert.True(p.DeltaVsTarget < 0);
            Assert.StartsWith("PI Vision://", p.VisionJumpPath);
        });
    }

    [Fact]
    public async Task Scope_kpis_kingdom_and_plant()
    {
        await using var sp = Build();
        var svc = sp.GetRequiredService<IAfagSemanticService>();

        var kingdom = await svc.GetScopeKpisAsync("Kingdom");
        var plant = await svc.GetScopeKpisAsync("Plant", "PP09");

        Assert.NotEmpty(kingdom);
        Assert.NotEmpty(plant);
        Assert.All(kingdom, k => Assert.Equal("Kingdom", k.Scope));
    }

    [Fact]
    public async Task Arabic_plant_narrative_is_populated()
    {
        await using var sp = Build();
        var plant = await sp.GetRequiredService<IAfagSemanticService>().GetPlantOverviewAsync("PP09");

        Assert.NotNull(plant);
        Assert.Contains("PP09", plant!.RootCauseNarrativeAr);
        Assert.Contains(plant!.RootCauseNarrativeAr, c => c is >= '\u0600' and <= '\u06FF');
    }
}
