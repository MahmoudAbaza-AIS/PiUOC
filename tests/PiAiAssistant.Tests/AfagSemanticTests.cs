using Microsoft.Extensions.DependencyInjection;
using PiAiAssistant.Application.Chat;
using PiAiAssistant.Application.Fleet;
using PiAiAssistant.Domain.Enums;
using PiAiAssistant.Domain.Interfaces;
using PiAiAssistant.Infrastructure.Afag;
using PiAiAssistant.Infrastructure.Knowledge;

namespace PiAiAssistant.Tests;

public class AfagSemanticTests
{
    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAfagHierarchyReader, AfagDemoHierarchyReader>();
        services.AddSingleton<ISopKnowledgeStore, InMemorySopStore>();
        services.AddScoped<IAfagSemanticService, AfagSemanticService>();
        services.AddScoped<IBriefingService, BriefingService>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Kingdom_overview_flags_a_sector_without_tag_names()
    {
        await using var sp = BuildServices();
        var svc = sp.GetRequiredService<IAfagSemanticService>();
        var fleet = await svc.GetKingdomOverviewAsync();

        Assert.Equal("KSA", fleet.Kingdom.Code);
        Assert.False(string.IsNullOrWhiteSpace(fleet.FlaggedSectorCode));
        Assert.Contains("loading factor", fleet.NarrativeEn, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".MEAS", fleet.NarrativeEn);
        Assert.False(string.IsNullOrWhiteSpace(fleet.RecommendedAction));
    }

    [Fact]
    public async Task Sector_ranks_plants_below_target()
    {
        await using var sp = BuildServices();
        var svc = sp.GetRequiredService<IAfagSemanticService>();
        var ranked = await svc.RankPlantsBelowTargetAsync("COA");

        Assert.Contains(ranked, p => p.PlantCode == "PP09");
        Assert.True(ranked[0].DeltaVsTarget < 0);
    }

    [Fact]
    public async Task Unit_alarm_cites_sop_and_action()
    {
        await using var sp = BuildServices();
        var svc = sp.GetRequiredService<IAfagSemanticService>();
        var unit = await svc.GetUnitOverviewAsync("PP09", "A1", "GT01");

        Assert.NotNull(unit);
        Assert.Contains("SOP", unit!.SopCitation ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Escalate", unit.RecommendedAction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Bad", string.Join(",", unit.Unit.FlameIntensityStatus), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Demo_pushes_cover_all_personas()
    {
        await using var sp = BuildServices();
        var briefs = await sp.GetRequiredService<IBriefingService>().GetDemoPushesAsync("en");

        Assert.Equal(5, briefs.Count);
        Assert.Contains(briefs, b => b.Persona == AssistantPersona.Executive);
        Assert.Contains(briefs, b => b.Persona == AssistantPersona.ShiftOperator);
        Assert.All(briefs, b => Assert.False(string.IsNullOrWhiteSpace(b.Narrative)));
    }

    [Fact]
    public async Task Arabic_brief_is_non_empty()
    {
        await using var sp = BuildServices();
        var brief = await sp.GetRequiredService<IBriefingService>()
            .GetSectorDeviationAlertAsync("COA", "ar");

        Assert.Equal("ar", brief.Language);
        Assert.False(string.IsNullOrWhiteSpace(brief.Narrative));
    }
}
