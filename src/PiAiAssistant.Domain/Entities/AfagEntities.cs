using PiAiAssistant.Domain.Enums;

namespace PiAiAssistant.Domain.Entities;

/// <summary>Business object: operating sector (COA/EOA/SOA/WOA) or kingdom aggregate.</summary>
public sealed class Sector
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public double GrossMw { get; init; }
    public double NetMw { get; init; }
    public double CapacityMw { get; init; }
    public double LoadingFactorPercent { get; init; }
    public int InServiceUnits { get; init; }
    public int TotalUnits { get; init; }
    public int OutOfServiceUnits { get; init; }
    public double OutageCapacityMw { get; init; }
    public double DailyPeakMw { get; init; }
    public string? DailyPeakAtLocal { get; init; }
    public double AnnualPeakMw { get; init; }
    public string? AnnualPeakDate { get; init; }
    public double GenMwhYesterday { get; init; }
    public double Co2TonPerDay { get; init; }
}

/// <summary>Business object: power plant.</summary>
public sealed class Plant
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string SectorCode { get; init; }
    public double GrossMw { get; init; }
    public double NetMw { get; init; }
    public double CapacityMw { get; init; }
    public double LoadingFactorPercent { get; init; }
    public double? TargetLoadingPercent { get; init; }
    public double? HeatRateKjPerKwh { get; init; }
    public double? HeatRateDeltaWeekPercent { get; init; }
    public int InServiceUnits { get; init; }
    public int TotalUnits { get; init; }
    public GenerationTechnology Technology { get; init; }
    public PlantHealthState Health { get; init; }
    public double NaturalGasKgPerDay { get; init; }
    public double LiquidFuelKgPerDay { get; init; }
    public double Co2TonPerDay { get; init; }
    public double? AmbientTempC { get; init; }
    public string? VisionJumpPath { get; init; }
}

/// <summary>Business object: plant block.</summary>
public sealed class PlantBlock
{
    public required string PlantCode { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public double GrossMw { get; init; }
    public double? FuelKgPerDay { get; init; }
}

/// <summary>Business object: generation unit (e.g. GT01).</summary>
public sealed class GenerationUnit
{
    public required string PlantCode { get; init; }
    public required string BlockCode { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public double ActivePowerMw { get; init; }
    public double ReactivePowerMvar { get; init; }
    public double FrequencyHz { get; init; }
    public double LoadingPercent { get; init; }
    public double TurbineSpeedRpm { get; init; }
    public double FuelFlowKgPerSec { get; init; }
    public double FuelConsGasKg { get; init; }
    public double GenMwhDay { get; init; }
    public double? InletAirTempC { get; init; }
    public double? InletAirPressureInH2o { get; init; }
    public double? CompAirTempC { get; init; }
    public double? CompAirPressureBar { get; init; }
    public double? ExhaustTempC { get; init; }
    public double? ExhaustPressureInH2o { get; init; }
    public IReadOnlyList<string> FlameIntensityStatus { get; init; } = [];
    public string? VisionJumpPath { get; init; }
}

/// <summary>Named KPI snapshot with unit and optional target.</summary>
public sealed class KpiSnapshot
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required object Value { get; init; }
    public string? Unit { get; init; }
    public object? Target { get; init; }
    public string? TrendNote { get; init; }
    public string Scope { get; init; } = "Fleet";
}

/// <summary>Curated SOP excerpt for operator/advisor answers.</summary>
public sealed class SopExcerpt
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Section { get; init; }
    public required string Body { get; init; }
    public string? AppliesTo { get; init; }
}

/// <summary>Proactive notification / briefing item.</summary>
public sealed class ProactiveBrief
{
    public required AssistantPersona Persona { get; init; }
    public required string Title { get; init; }
    public required string Narrative { get; init; }
    public required string Trigger { get; init; }
    public string? RecommendedAction { get; init; }
    public string? VisionJumpPath { get; init; }
    public string Language { get; init; } = "en";
    public DateTimeOffset GeneratedUtc { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<KpiSnapshot> Kpis { get; init; } = [];
}
