using PiAiAssistant.Domain.Entities;
using PiAiAssistant.Domain.Enums;

namespace PiAiAssistant.Application.Fleet;

public interface IBriefingService
{
    Task<ProactiveBrief> GetMorningExecutiveBriefAsync(string language = "en", CancellationToken cancellationToken = default);
    Task<ProactiveBrief> GetSectorDeviationAlertAsync(string sectorCode, string language = "en", CancellationToken cancellationToken = default);
    Task<ProactiveBrief> GetPlantDigestAsync(string plantCode, string language = "en", CancellationToken cancellationToken = default);
    Task<ProactiveBrief> GetOperatorAlarmCardAsync(string plantCode, string blockCode, string unitCode, string language = "en", CancellationToken cancellationToken = default);
    Task<ProactiveBrief> GetReliabilityAnomalyDigestAsync(string language = "en", CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProactiveBrief>> GetDemoPushesAsync(string language = "en", CancellationToken cancellationToken = default);
}

public sealed class BriefingService(IAfagSemanticService semantic) : IBriefingService
{
    public async Task<ProactiveBrief> GetMorningExecutiveBriefAsync(string language = "en", CancellationToken cancellationToken = default)
    {
        var fleet = await semantic.GetKingdomOverviewAsync(cancellationToken);
        var ar = language.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        return new ProactiveBrief
        {
            Persona = AssistantPersona.Executive,
            Title = ar ? "إحاطة الأسطول الصباحية" : "06:30 Fleet briefing",
            Narrative = ar ? fleet.NarrativeAr : fleet.NarrativeEn,
            Trigger = "Daily schedule",
            RecommendedAction = fleet.RecommendedAction,
            VisionJumpPath = fleet.VisionJumpPath,
            Language = ar ? "ar" : "en",
            Kpis =
            [
                new KpiSnapshot { Key = "gross_mw", DisplayName = "Gross MW", Value = fleet.Kingdom.GrossMw, Unit = "MW", Scope = "Kingdom" },
                new KpiSnapshot { Key = "loading_factor", DisplayName = "Loading Factor", Value = fleet.Kingdom.LoadingFactorPercent, Unit = "%", Scope = "Kingdom" },
                new KpiSnapshot { Key = "in_service", DisplayName = "In-Service Units", Value = $"{fleet.Kingdom.InServiceUnits}/{fleet.Kingdom.TotalUnits}", Scope = "Kingdom" },
                new KpiSnapshot { Key = "co2", DisplayName = "CO₂", Value = fleet.Kingdom.Co2TonPerDay, Unit = "t/day", Scope = "Kingdom" }
            ]
        };
    }

    public async Task<ProactiveBrief> GetSectorDeviationAlertAsync(
        string sectorCode,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        var sector = await semantic.GetSectorOverviewAsync(sectorCode, cancellationToken)
                     ?? throw new InvalidOperationException($"Unknown sector '{sectorCode}'.");
        var ar = language.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        return new ProactiveBrief
        {
            Persona = AssistantPersona.SectorOps,
            Title = ar ? $"تنبيه معامل تحميل {sector.Sector.Code}" : $"{sector.Sector.Code} loading-factor deviation",
            Narrative = ar ? sector.NarrativeAr : sector.NarrativeEn,
            Trigger = "KPI breach vs. target",
            RecommendedAction = sector.RecommendedAction,
            VisionJumpPath = $"PI Vision://Sector/{sector.Sector.Code}",
            Language = ar ? "ar" : "en",
            Kpis =
            [
                new KpiSnapshot { Key = "loading_factor", DisplayName = "Loading Factor", Value = sector.Sector.LoadingFactorPercent, Unit = "%", Scope = sector.Sector.Code }
            ]
        };
    }

    public async Task<ProactiveBrief> GetPlantDigestAsync(
        string plantCode,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        var plant = await semantic.GetPlantOverviewAsync(plantCode, cancellationToken)
                    ?? throw new InvalidOperationException($"Unknown plant '{plantCode}'.");
        var ar = language.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        return new ProactiveBrief
        {
            Persona = AssistantPersona.PlantManager,
            Title = ar ? $"ملخص أداء {plant.Plant.Code}" : $"{plant.Plant.Code} heat-rate / availability digest",
            Narrative = ar ? plant.RootCauseNarrativeAr : plant.RootCauseNarrativeEn,
            Trigger = "Weekly + threshold",
            RecommendedAction = plant.RecommendedAction,
            VisionJumpPath = plant.Plant.VisionJumpPath,
            Language = ar ? "ar" : "en",
            Kpis =
            [
                new KpiSnapshot
                {
                    Key = "heat_rate_delta",
                    DisplayName = "Heat rate Δ week",
                    Value = plant.Plant.HeatRateDeltaWeekPercent ?? 0,
                    Unit = "%",
                    Scope = plant.Plant.Code
                }
            ]
        };
    }

    public async Task<ProactiveBrief> GetOperatorAlarmCardAsync(
        string plantCode,
        string blockCode,
        string unitCode,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        var unit = await semantic.GetUnitOverviewAsync(plantCode, blockCode, unitCode, cancellationToken)
                   ?? throw new InvalidOperationException("Unknown unit.");
        var ar = language.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        return new ProactiveBrief
        {
            Persona = AssistantPersona.ShiftOperator,
            Title = ar ? $"بطاقة إنذار {unit.Unit.Name}" : $"{unit.Unit.Name} flame-intensity alarm card",
            Narrative = ar ? unit.NarrativeAr : unit.NarrativeEn,
            Trigger = "Alarm event",
            RecommendedAction = unit.RecommendedAction,
            VisionJumpPath = unit.Unit.VisionJumpPath,
            Language = ar ? "ar" : "en",
            Kpis =
            [
                new KpiSnapshot { Key = "active_mw", DisplayName = "Active Power", Value = unit.Unit.ActivePowerMw, Unit = "MW", Scope = unit.Unit.Code },
                new KpiSnapshot { Key = "loading", DisplayName = "Loading", Value = unit.Unit.LoadingPercent, Unit = "%", Scope = unit.Unit.Code }
            ]
        };
    }

    public async Task<ProactiveBrief> GetReliabilityAnomalyDigestAsync(
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        var plant = await semantic.GetPlantOverviewAsync("PP09", cancellationToken);
        var ar = language.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        var narrative = plant is null
            ? "No fleet anomalies above baseline threshold."
            : (ar ? plant.RootCauseNarrativeAr : plant.RootCauseNarrativeEn);

        return new ProactiveBrief
        {
            Persona = AssistantPersona.ReliabilityEngineer,
            Title = ar ? "ملخص الشذوذ عبر الأسطول" : "Emerging-anomaly digest",
            Narrative = narrative,
            Trigger = "Baseline deviation",
            RecommendedAction = plant?.RecommendedAction ?? "Review validated 30-day baselines.",
            VisionJumpPath = "PI Vision://Reliability/Fleet",
            Language = ar ? "ar" : "en"
        };
    }

    public async Task<IReadOnlyList<ProactiveBrief>> GetDemoPushesAsync(
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        return
        [
            await GetMorningExecutiveBriefAsync(language, cancellationToken),
            await GetSectorDeviationAlertAsync("COA", language, cancellationToken),
            await GetPlantDigestAsync("PP09", language, cancellationToken),
            await GetOperatorAlarmCardAsync("PP09", "A1", "GT01", language, cancellationToken),
            await GetReliabilityAnomalyDigestAsync(language, cancellationToken)
        ];
    }
}
