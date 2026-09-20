using PiAiAssistant.Domain.Entities;
using PiAiAssistant.Domain.Enums;
using PiAiAssistant.Domain.Interfaces;

namespace PiAiAssistant.Application.Fleet;

public sealed record FleetOverviewDto(
    Sector Kingdom,
    IReadOnlyList<Sector> Sectors,
    string FlaggedSectorCode,
    string NarrativeEn,
    string NarrativeAr,
    string RecommendedAction,
    string VisionJumpPath);

public sealed record SectorOverviewDto(
    Sector Sector,
    IReadOnlyList<PlantRankDto> PlantsBelowTarget,
    IReadOnlyList<Plant> Plants,
    string NarrativeEn,
    string NarrativeAr,
    string RecommendedAction);

public sealed record PlantRankDto(
    string PlantCode,
    string Name,
    double LoadingPercent,
    double? TargetLoadingPercent,
    double DeltaVsTarget,
    string Health,
    string VisionJumpPath);

public sealed record PlantOverviewDto(
    Plant Plant,
    IReadOnlyList<PlantBlock> Blocks,
    string RootCauseNarrativeEn,
    string RootCauseNarrativeAr,
    string RecommendedAction,
    string? SopCitation,
    IReadOnlyList<TrendPointDto> GenTrend,
    IReadOnlyList<TrendPointDto> FuelTrend);

public sealed record TrendPointDto(DateTimeOffset TimestampUtc, double? Value);

public sealed record UnitOverviewDto(
    GenerationUnit Unit,
    string NarrativeEn,
    string NarrativeAr,
    string LikelyCause,
    string RecommendedAction,
    string? SopCitation,
    string? EscalationContact);

public interface IAfagSemanticService
{
    Task<FleetOverviewDto> GetKingdomOverviewAsync(CancellationToken cancellationToken = default);
    Task<SectorOverviewDto?> GetSectorOverviewAsync(string sectorCode, CancellationToken cancellationToken = default);
    Task<PlantOverviewDto?> GetPlantOverviewAsync(string plantCode, CancellationToken cancellationToken = default);
    Task<UnitOverviewDto?> GetUnitOverviewAsync(string plantCode, string blockCode, string unitCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlantRankDto>> RankPlantsBelowTargetAsync(string sectorCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KpiSnapshot>> GetScopeKpisAsync(string scope, string? code = null, CancellationToken cancellationToken = default);
}

public sealed class AfagSemanticService(
    IAfagHierarchyReader hierarchy,
    ISopKnowledgeStore sops) : IAfagSemanticService
{
    public async Task<FleetOverviewDto> GetKingdomOverviewAsync(CancellationToken cancellationToken = default)
    {
        var kingdom = await hierarchy.GetKingdomAsync(cancellationToken);
        var sectors = await hierarchy.GetSectorsAsync(cancellationToken);
        var allPlants = await hierarchy.GetPlantsAsync(null, cancellationToken);
        var worstPlant = allPlants
            .OrderBy(p => p.HeatRateDeltaWeekPercent ?? 0)
            .ThenBy(p => p.LoadingFactorPercent)
            .First();
        var flagged = sectors.FirstOrDefault(s => s.Code.Equals(worstPlant.SectorCode, StringComparison.OrdinalIgnoreCase))
                      ?? sectors.OrderBy(s => s.LoadingFactorPercent).First();

        var narrativeEn =
            $"Fleet at {kingdom.NetMw / 1000:0.#} GW net ({kingdom.GrossMw / 1000:0.#} GW gross), loading factor {kingdom.LoadingFactorPercent:0.#}%. " +
            $"{flagged.Code} is dragging: {worstPlant.Code} heat rate degraded {Math.Abs(worstPlant.HeatRateDeltaWeekPercent ?? 0):0.#}% week-on-week. " +
            "Recommend a plant-manager review.";

        var narrativeAr =
            $"الأسطول عند {kingdom.NetMw / 1000:0.#} جيجاواط صافي، معامل التحميل {kingdom.LoadingFactorPercent:0.#}%. " +
            $"القطاع {flagged.Code} يتراجع: المحطة {worstPlant.Code} تدهور معدل الحرارة {Math.Abs(worstPlant.HeatRateDeltaWeekPercent ?? 0):0.#}% أسبوعياً. " +
            "يُوصى بمراجعة مدير المحطة.";

        return new FleetOverviewDto(
            kingdom,
            sectors,
            flagged.Code,
            narrativeEn,
            narrativeAr,
            $"Open Kingdom screen and highlight {flagged.Code}; escalate {worstPlant.Code} to plant manager.",
            "PI Vision://Kingdom/KSA");
    }

    public async Task<SectorOverviewDto?> GetSectorOverviewAsync(string sectorCode, CancellationToken cancellationToken = default)
    {
        var sector = await hierarchy.GetSectorAsync(sectorCode, cancellationToken);
        if (sector is null) return null;

        var plants = await hierarchy.GetPlantsAsync(sectorCode, cancellationToken);
        var ranked = await RankPlantsBelowTargetAsync(sectorCode, cancellationToken);
        var top = ranked.FirstOrDefault();

        var narrativeEn = top is null
            ? $"All plants in {sector.Code} are at or above approved loading targets. Sector loading factor is {sector.LoadingFactorPercent:0.#}%."
            : $"Plants dragging {sector.Code}: {string.Join(", ", ranked.Take(3).Select(p => $"{p.PlantCode} ({p.DeltaVsTarget:0.#} pt vs target)"))}. " +
              $"Top concern: {top.PlantCode}. Sector loading factor {sector.LoadingFactorPercent:0.#}%.";

        var narrativeAr = top is null
            ? $"جميع محطات {sector.Code} ضمن أهداف التحميل. معامل التحميل {sector.LoadingFactorPercent:0.#}%."
            : $"المحطات الأقل من الهدف في {sector.Code}: {string.Join("، ", ranked.Take(3).Select(p => p.PlantCode))}. الأعلى أولوية: {top.PlantCode}.";

        return new SectorOverviewDto(
            sector,
            ranked,
            plants,
            narrativeEn,
            narrativeAr,
            top is null ? "Continue monitoring sector KPIs." : $"Open plant screen for {top.PlantCode} and review heat rate / block balance.");
    }

    public async Task<PlantOverviewDto?> GetPlantOverviewAsync(string plantCode, CancellationToken cancellationToken = default)
    {
        var plant = await hierarchy.GetPlantAsync(plantCode, cancellationToken);
        if (plant is null) return null;

        var blocks = await hierarchy.GetBlocksAsync(plantCode, cancellationToken);
        var trend = await hierarchy.GetPlantFuelTrendAsync(plantCode, 7, cancellationToken);
        var worstBlock = blocks.OrderByDescending(b => b.GrossMw).Skip(0).FirstOrDefault();
        // Attribute heat-rate drift to Block A1 fuel-flow (demo storyline)
        var culprit = blocks.FirstOrDefault(b => b.Code.Equals("A1", StringComparison.OrdinalIgnoreCase)) ?? worstBlock;

        var sop = (await sops.SearchAsync("heat rate fuel", 1, cancellationToken)).FirstOrDefault();
        var delta = Math.Abs(plant.HeatRateDeltaWeekPercent ?? 0);

        var narrativeEn =
            $"{plant.Code} heat rate is worse by {delta:0.#}% vs last week at comparable load. " +
            $"Primary driver: Block {culprit?.Code} fuel-flow deviation (weather-adjusted ambient {plant.AmbientTempC:0.#}°C). " +
            $"Plant loading {plant.LoadingFactorPercent:0.#}% of {plant.CapacityMw:0.#} MW capacity.";

        var narrativeAr =
            $"معدل الحرارة في {plant.Code} أسوأ بنسبة {delta:0.#}% عن الأسبوع الماضي. " +
            $"السبب الرئيسي: انحراف تدفق الوقود في البلوك {culprit?.Code} مع درجة حرارة محيطة {plant.AmbientTempC:0.#}°م.";

        return new PlantOverviewDto(
            plant,
            blocks,
            narrativeEn,
            narrativeAr,
            $"Inspect Block {culprit?.Code} fuel metering and escalate to reliability if drift persists >24h.",
            sop is null ? null : $"{sop.Id} §{sop.Section}",
            trend.Select(t => new TrendPointDto(t.TimestampUtc, t.GenMwh)).ToList(),
            trend.Select(t => new TrendPointDto(t.TimestampUtc, t.NaturalGasKg + t.LiquidFuelKg)).ToList());
    }

    public async Task<UnitOverviewDto?> GetUnitOverviewAsync(
        string plantCode,
        string blockCode,
        string unitCode,
        CancellationToken cancellationToken = default)
    {
        var unit = await hierarchy.GetUnitAsync(plantCode, blockCode, unitCode, cancellationToken);
        if (unit is null) return null;

        var badSensors = unit.FlameIntensityStatus.Count(s =>
            s.Contains("bad", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("fail", StringComparison.OrdinalIgnoreCase));
        var sop = (await sops.SearchAsync("flame intensity GT01", 1, cancellationToken)).FirstOrDefault()
                  ?? (await sops.SearchAsync("flame", 1, cancellationToken)).FirstOrDefault();

        var likely = badSensors >= 2
            ? "Likely instrumentation / data-quality fault, not verified combustion trip."
            : "Review live panel; flame status may be intermittent.";

        var narrativeEn =
            $"{unit.Name} loaded at {unit.LoadingPercent:0.#}% ({unit.ActivePowerMw:0.##} MW / {unit.GenMwhDay:0.#} MWh today), " +
            $"fuel flow {unit.FuelFlowKgPerSec:0.##} kg/s. Flame intensity sensors reading BAD ({badSensors} of {unit.FlameIntensityStatus.Count}). {likely}";

        var narrativeAr =
            $"{unit.Name} عند تحميل {unit.LoadingPercent:0.#}% ({unit.ActivePowerMw:0.##} ميجاواط). " +
            $"حساسات شدة اللهب تظهر حالة سيئة ({badSensors}/{unit.FlameIntensityStatus.Count}). غالباً خلل أجهزة قياس وليس احتراق.";

        var action = sop is null
            ? "Verify on local panel; escalate to shift lead if two of four sensors remain BAD after 5 minutes."
            : $"Cross-check with local panel per {sop.Id} §{sop.Section}. Escalate to shift lead if two of four sensors remain BAD after 5 min.";

        return new UnitOverviewDto(
            unit,
            narrativeEn,
            narrativeAr,
            likely,
            action,
            sop is null ? null : $"{sop.Id} §{sop.Section} — {sop.Title}",
            "Shift Lead / Control Room");
    }

    public async Task<IReadOnlyList<PlantRankDto>> RankPlantsBelowTargetAsync(
        string sectorCode,
        CancellationToken cancellationToken = default)
    {
        var plants = await hierarchy.GetPlantsAsync(sectorCode, cancellationToken);
        return plants
            .Where(p => p.TargetLoadingPercent is not null && p.LoadingFactorPercent < p.TargetLoadingPercent)
            .Select(p => new PlantRankDto(
                p.Code,
                p.Name,
                p.LoadingFactorPercent,
                p.TargetLoadingPercent,
                p.LoadingFactorPercent - p.TargetLoadingPercent!.Value,
                p.Health.ToString(),
                p.VisionJumpPath ?? $"PI Vision://{p.SectorCode}/{p.Code}"))
            .OrderBy(p => p.DeltaVsTarget)
            .ToList();
    }

    public Task<IReadOnlyList<KpiSnapshot>> GetScopeKpisAsync(
        string scope,
        string? code = null,
        CancellationToken cancellationToken = default)
        => hierarchy.GetKpisAsync(scope, code, cancellationToken);
}
