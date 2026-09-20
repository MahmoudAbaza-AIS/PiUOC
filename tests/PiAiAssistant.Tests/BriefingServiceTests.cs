using Microsoft.Extensions.DependencyInjection;
using PiAiAssistant.Application.Fleet;
using PiAiAssistant.Domain.Enums;
using PiAiAssistant.Domain.Interfaces;
using PiAiAssistant.Infrastructure.Afag;
using PiAiAssistant.Infrastructure.Knowledge;

namespace PiAiAssistant.Tests;

public class BriefingServiceTests
{
    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAfagHierarchyReader, AfagDemoHierarchyReader>();
        services.AddSingleton<ISopKnowledgeStore, InMemorySopStore>();
        services.AddScoped<IAfagSemanticService, AfagSemanticService>();
        services.AddScoped<IBriefingService, BriefingService>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Morning_executive_brief_has_four_kpi_tiles()
    {
        await using var sp = Build();
        var brief = await sp.GetRequiredService<IBriefingService>().GetMorningExecutiveBriefAsync();

        Assert.Equal(AssistantPersona.Executive, brief.Persona);
        Assert.Equal("Daily schedule", brief.Trigger);
        Assert.Equal(4, brief.Kpis.Count);
        Assert.Contains(brief.Kpis, k => k.Key == "loading_factor");
        Assert.Contains(brief.Kpis, k => k.Key == "co2");
        Assert.Contains("PI Vision://Kingdom", brief.VisionJumpPath);
    }

    [Fact]
    public async Task Sector_alert_trigger_is_kpi_breach()
    {
        await using var sp = Build();
        var brief = await sp.GetRequiredService<IBriefingService>().GetSectorDeviationAlertAsync("COA");

        Assert.Equal(AssistantPersona.SectorOps, brief.Persona);
        Assert.Equal("KPI breach vs. target", brief.Trigger);
        Assert.Contains("COA", brief.Title);
    }

    [Fact]
    public async Task Unknown_sector_brief_throws()
    {
        await using var sp = Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sp.GetRequiredService<IBriefingService>().GetSectorDeviationAlertAsync("ZZZ"));
    }

    [Fact]
    public async Task Plant_digest_mentions_heat_rate()
    {
        await using var sp = Build();
        var brief = await sp.GetRequiredService<IBriefingService>().GetPlantDigestAsync("PP09");

        Assert.Equal(AssistantPersona.PlantManager, brief.Persona);
        Assert.Contains("heat", brief.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(brief.Kpis, k => k.Key == "heat_rate_delta");
        Assert.Equal("Weekly + threshold", brief.Trigger);
    }

    [Fact]
    public async Task Unknown_plant_digest_throws()
    {
        await using var sp = Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sp.GetRequiredService<IBriefingService>().GetPlantDigestAsync("NOPE"));
    }

    [Fact]
    public async Task Operator_alarm_card_is_event_triggered()
    {
        await using var sp = Build();
        var brief = await sp.GetRequiredService<IBriefingService>()
            .GetOperatorAlarmCardAsync("PP09", "A1", "GT01");

        Assert.Equal(AssistantPersona.ShiftOperator, brief.Persona);
        Assert.Equal("Alarm event", brief.Trigger);
        Assert.Contains("flame", brief.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(brief.Kpis, k => k.Key == "active_mw");
    }

    [Fact]
    public async Task Reliability_digest_uses_baseline_trigger()
    {
        await using var sp = Build();
        var brief = await sp.GetRequiredService<IBriefingService>().GetReliabilityAnomalyDigestAsync();

        Assert.Equal(AssistantPersona.ReliabilityEngineer, brief.Persona);
        Assert.Equal("Baseline deviation", brief.Trigger);
        Assert.False(string.IsNullOrWhiteSpace(brief.RecommendedAction));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ar")]
    public async Task Demo_pushes_cover_five_personas_in_requested_language(string lang)
    {
        await using var sp = Build();
        var briefs = await sp.GetRequiredService<IBriefingService>().GetDemoPushesAsync(lang);

        Assert.Equal(5, briefs.Count);
        Assert.All(briefs, b => Assert.Equal(lang == "ar" ? "ar" : "en", b.Language));
        Assert.Contains(briefs, b => b.Persona == AssistantPersona.Executive);
        Assert.Contains(briefs, b => b.Persona == AssistantPersona.SectorOps);
        Assert.Contains(briefs, b => b.Persona == AssistantPersona.PlantManager);
        Assert.Contains(briefs, b => b.Persona == AssistantPersona.ShiftOperator);
        Assert.Contains(briefs, b => b.Persona == AssistantPersona.ReliabilityEngineer);
    }

    [Fact]
    public async Task Arabic_executive_brief_contains_arabic_script()
    {
        await using var sp = Build();
        var brief = await sp.GetRequiredService<IBriefingService>().GetMorningExecutiveBriefAsync("ar");

        Assert.Equal("ar", brief.Language);
        Assert.Contains(brief.Narrative, c => c is >= '\u0600' and <= '\u06FF');
        Assert.Contains(brief.Title, c => c is >= '\u0600' and <= '\u06FF');
    }
}
