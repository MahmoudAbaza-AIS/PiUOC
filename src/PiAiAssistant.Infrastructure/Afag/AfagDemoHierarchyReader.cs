using PiAiAssistant.Domain.Entities;
using PiAiAssistant.Domain.Enums;
using PiAiAssistant.Domain.Interfaces;

namespace PiAiAssistant.Infrastructure.Afag;

/// <summary>
/// Demo semantic layer for Saudi AFAG generation displays (Kingdom → Sector → PP09 → GT01).
/// Values aligned with guideline mockups / demo storyline.
/// </summary>
public sealed class AfagDemoHierarchyReader : IAfagHierarchyReader
{
    private readonly Sector _kingdom = new()
    {
        Code = "KSA",
        Name = "Kingdom of Saudi Arabia",
        GrossMw = 41687,
        NetMw = 40359,
        CapacityMw = 55547,
        LoadingFactorPercent = 75,
        InServiceUnits = 321,
        TotalUnits = 484,
        OutOfServiceUnits = 163,
        OutageCapacityMw = 1858,
        DailyPeakMw = 46621,
        DailyPeakAtLocal = "6:30 PM",
        AnnualPeakMw = 49579,
        AnnualPeakDate = "10/09",
        GenMwhYesterday = 890000,
        Co2TonPerDay = 482000
    };

    private readonly List<Sector> _sectors =
    [
        new()
        {
            Code = "WOA", Name = "Western Operating Area", GrossMw = 13739, NetMw = 13300, CapacityMw = 18000,
            LoadingFactorPercent = 76, InServiceUnits = 58, TotalUnits = 132, OutOfServiceUnits = 3, OutageCapacityMw = 188,
            DailyPeakMw = 15000, GenMwhYesterday = 280000, Co2TonPerDay = 120000
        },
        new()
        {
            Code = "EOA", Name = "Eastern Operating Area", GrossMw = 10652, NetMw = 10300, CapacityMw = 15000,
            LoadingFactorPercent = 71, InServiceUnits = 52, TotalUnits = 71, OutOfServiceUnits = 2, OutageCapacityMw = 1104,
            DailyPeakMw = 12000, GenMwhYesterday = 220000, Co2TonPerDay = 110000
        },
        new()
        {
            Code = "SOA", Name = "Southern Operating Area", GrossMw = 4490, NetMw = 4350, CapacityMw = 6500,
            LoadingFactorPercent = 69, InServiceUnits = 32, TotalUnits = 49, OutOfServiceUnits = 4, OutageCapacityMw = 86,
            DailyPeakMw = 5000, GenMwhYesterday = 90000, Co2TonPerDay = 52000
        },
        new()
        {
            Code = "COA", Name = "Central Operating Area", GrossMw = 12806, NetMw = 12805, CapacityMw = 18116,
            LoadingFactorPercent = 71, InServiceUnits = 179, TotalUnits = 232, OutOfServiceUnits = 5, OutageCapacityMw = 480,
            DailyPeakMw = 14553, DailyPeakAtLocal = "6:05 PM", AnnualPeakMw = 16208, AnnualPeakDate = "June 22",
            GenMwhYesterday = 222651, Co2TonPerDay = 200000
        }
    ];

    private readonly List<Plant> _plants =
    [
        Plant("HAIL", "Hail", "COA", 465, 800, 58, 70, GenerationTechnology.Gas, PlantHealthState.DataUnhealthy, -0.5),
        Plant("PP13", "PP13", "COA", 1511, 2100, 72, 75, GenerationTechnology.Combined, PlantHealthState.Healthy, -0.2),
        Plant("PP12", "PP12", "COA", 1560, 2100, 74, 75, GenerationTechnology.Combined, PlantHealthState.Healthy, 0.1),
        Plant("PP08", "PP08", "COA", 1175, 1800, 65, 72, GenerationTechnology.Gas, PlantHealthState.DataUnhealthy, -1.1),
        Plant("PP10", "PP10", "COA", 2781, 3600, 77, 78, GenerationTechnology.Combined, PlantHealthState.Healthy, 0.0),
        Plant("PP14", "PP14", "COA", 1523, 2100, 72, 75, GenerationTechnology.Combined, PlantHealthState.Healthy, -0.3),
        Plant("QCPP", "Qassim CPP", "COA", 240, 400, 60, 70, GenerationTechnology.Combined, PlantHealthState.Healthy, -0.4),
        Plant("LAYLA", "Layla", "COA", 82, 150, 55, 65, GenerationTechnology.Steam, PlantHealthState.Healthy, 0.2),
        Plant("JUBA", "Juba", "COA", 266, 400, 66, 70, GenerationTechnology.Steam, PlantHealthState.Healthy, -0.1),
        Plant("PP07", "PP07", "COA", 1008, 1600, 63, 72, GenerationTechnology.Gas, PlantHealthState.DataUnhealthy, -1.5),
        Plant("PP09", "PP09", "COA", 2195.8, 4097, 60, 72, GenerationTechnology.Combined, PlantHealthState.Healthy, -3.2,
            naturalGas: 5_903_270, liquid: 5_508_950.5, co2: 27_674, ambient: 53, inService: 54, total: 60)
    ];

    private readonly List<PlantBlock> _pp09Blocks =
    [
        new() { PlantCode = "PP09", Code = "A1", Name = "Block A1", GrossMw = 202.15, FuelKgPerDay = 980_000 },
        new() { PlantCode = "PP09", Code = "A2", Name = "Block A2", GrossMw = 200.44, FuelKgPerDay = 970_000 },
        new() { PlantCode = "PP09", Code = "B1", Name = "Block B1", GrossMw = 195.83, FuelKgPerDay = 960_000 },
        new() { PlantCode = "PP09", Code = "B2", Name = "Block B2", GrossMw = 204.2, FuelKgPerDay = 990_000 },
        new() { PlantCode = "PP09", Code = "C", Name = "Block C", GrossMw = 427.28, FuelKgPerDay = 0 },
        new() { PlantCode = "PP09", Code = "D", Name = "Block D", GrossMw = 351.12, FuelKgPerDay = 0 },
        new() { PlantCode = "PP09", Code = "E", Name = "Block E", GrossMw = 229.8, FuelKgPerDay = 3_980_800 },
        new() { PlantCode = "PP09", Code = "F", Name = "Block F", GrossMw = 385, FuelKgPerDay = null }
    ];

    private readonly GenerationUnit _gt01 = new()
    {
        PlantCode = "PP09",
        BlockCode = "A1",
        Code = "GT01",
        Name = "Block A1 GT01",
        ActivePowerMw = 44.73,
        ReactivePowerMvar = 7.89,
        FrequencyHz = 59.96,
        LoadingPercent = 73.46,
        TurbineSpeedRpm = 3603.75,
        FuelFlowKgPerSec = 3.87,
        FuelConsGasKg = 335_710,
        GenMwhDay = 1074,
        InletAirTempC = 45.11,
        InletAirPressureInH2o = 2.93,
        CompAirTempC = 344.44,
        CompAirPressureBar = 7.31,
        ExhaustTempC = 565.25,
        ExhaustPressureInH2o = 9.52,
        FlameIntensityStatus = ["A:Bad", "B:Bad", "C:Bad", "D:Bad"],
        VisionJumpPath = "PI Vision://COA/PP09/Block A1/GT01"
    };

    private static Plant Plant(
        string code,
        string name,
        string sector,
        double gross,
        double capacity,
        double loading,
        double target,
        GenerationTechnology tech,
        PlantHealthState health,
        double heatDelta,
        double naturalGas = 0,
        double liquid = 0,
        double co2 = 0,
        double? ambient = null,
        int inService = 10,
        int total = 12) => new()
    {
        Code = code,
        Name = name,
        SectorCode = sector,
        GrossMw = gross,
        NetMw = gross,
        CapacityMw = capacity,
        LoadingFactorPercent = loading,
        TargetLoadingPercent = target,
        HeatRateKjPerKwh = 9_800 + Math.Abs(heatDelta) * 100,
        HeatRateDeltaWeekPercent = heatDelta,
        InServiceUnits = inService,
        TotalUnits = total,
        Technology = tech,
        Health = health,
        NaturalGasKgPerDay = naturalGas,
        LiquidFuelKgPerDay = liquid,
        Co2TonPerDay = co2,
        AmbientTempC = ambient,
        VisionJumpPath = $"PI Vision://{sector}/{code}"
    };

    public Task<Sector> GetKingdomAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_kingdom);

    public Task<IReadOnlyList<Sector>> GetSectorsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Sector>>(_sectors);

    public Task<Sector?> GetSectorAsync(string sectorCode, CancellationToken cancellationToken = default)
        => Task.FromResult(_sectors.FirstOrDefault(s =>
            s.Code.Equals(sectorCode, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<Plant>> GetPlantsAsync(string? sectorCode = null, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Plant> list = string.IsNullOrWhiteSpace(sectorCode)
            ? _plants
            : _plants.Where(p => p.SectorCode.Equals(sectorCode, StringComparison.OrdinalIgnoreCase)).ToList();
        return Task.FromResult(list);
    }

    public Task<Plant?> GetPlantAsync(string plantCode, CancellationToken cancellationToken = default)
        => Task.FromResult(_plants.FirstOrDefault(p => p.Code.Equals(plantCode, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<PlantBlock>> GetBlocksAsync(string plantCode, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PlantBlock> blocks = plantCode.Equals("PP09", StringComparison.OrdinalIgnoreCase)
            ? _pp09Blocks
            : [];
        return Task.FromResult(blocks);
    }

    public Task<GenerationUnit?> GetUnitAsync(
        string plantCode,
        string blockCode,
        string unitCode,
        CancellationToken cancellationToken = default)
    {
        if (plantCode.Equals("PP09", StringComparison.OrdinalIgnoreCase)
            && blockCode.Equals("A1", StringComparison.OrdinalIgnoreCase)
            && unitCode.Equals("GT01", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<GenerationUnit?>(_gt01);
        }

        return Task.FromResult<GenerationUnit?>(null);
    }

    public Task<IReadOnlyList<GenerationUnit>> GetUnitsAsync(
        string plantCode,
        string? blockCode = null,
        CancellationToken cancellationToken = default)
    {
        if (!plantCode.Equals("PP09", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<IReadOnlyList<GenerationUnit>>([]);
        }

        if (blockCode is null || blockCode.Equals("A1", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<IReadOnlyList<GenerationUnit>>([_gt01]);
        }

        return Task.FromResult<IReadOnlyList<GenerationUnit>>([]);
    }

    public async Task<IReadOnlyList<KpiSnapshot>> GetKpisAsync(
        string scope,
        string? code = null,
        CancellationToken cancellationToken = default)
    {
        if (scope.Equals("Kingdom", StringComparison.OrdinalIgnoreCase) || scope.Equals("KSA", StringComparison.OrdinalIgnoreCase))
        {
            var k = await GetKingdomAsync(cancellationToken);
            return
            [
                new KpiSnapshot { Key = "gross_mw", DisplayName = "Gross Power", Value = k.GrossMw, Unit = "MW", Scope = "Kingdom" },
                new KpiSnapshot { Key = "net_mw", DisplayName = "Net Power", Value = k.NetMw, Unit = "MW", Scope = "Kingdom" },
                new KpiSnapshot { Key = "loading_factor", DisplayName = "Loading Factor", Value = k.LoadingFactorPercent, Unit = "%", Scope = "Kingdom" },
                new KpiSnapshot { Key = "capacity_mw", DisplayName = "Total Capacity", Value = k.CapacityMw, Unit = "MW", Scope = "Kingdom" },
                new KpiSnapshot { Key = "co2", DisplayName = "CO₂", Value = k.Co2TonPerDay, Unit = "t/day", Scope = "Kingdom" }
            ];
        }

        if (scope.Equals("Sector", StringComparison.OrdinalIgnoreCase) && code is not null)
        {
            var s = await GetSectorAsync(code, cancellationToken);
            if (s is null) return [];
            return
            [
                new KpiSnapshot { Key = "gross_mw", DisplayName = "Gross Power", Value = s.GrossMw, Unit = "MW", Scope = s.Code },
                new KpiSnapshot { Key = "loading_factor", DisplayName = "Loading Factor", Value = s.LoadingFactorPercent, Unit = "%", Scope = s.Code },
                new KpiSnapshot { Key = "in_service", DisplayName = "In-Service", Value = $"{s.InServiceUnits}/{s.TotalUnits}", Scope = s.Code }
            ];
        }

        if (scope.Equals("Plant", StringComparison.OrdinalIgnoreCase) && code is not null)
        {
            var p = await GetPlantAsync(code, cancellationToken);
            if (p is null) return [];
            return
            [
                new KpiSnapshot { Key = "gross_mw", DisplayName = "Gross Power", Value = p.GrossMw, Unit = "MW", Scope = p.Code },
                new KpiSnapshot { Key = "loading_factor", DisplayName = "Loading Factor", Value = p.LoadingFactorPercent, Unit = "%", Target = p.TargetLoadingPercent, Scope = p.Code },
                new KpiSnapshot { Key = "heat_rate_delta", DisplayName = "Heat rate Δ week", Value = p.HeatRateDeltaWeekPercent ?? 0, Unit = "%", Scope = p.Code }
            ];
        }

        return [];
    }

    public Task<IReadOnlyList<(DateTimeOffset TimestampUtc, double GenMwh, double NaturalGasKg, double LiquidFuelKg)>> GetPlantFuelTrendAsync(
        string plantCode,
        int days = 7,
        CancellationToken cancellationToken = default)
    {
        if (!plantCode.Equals("PP09", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<IReadOnlyList<(DateTimeOffset, double, double, double)>>([]);
        }

        var end = DateTimeOffset.UtcNow.Date;
        var list = new List<(DateTimeOffset, double, double, double)>();
        for (var i = days - 1; i >= 0; i--)
        {
            var day = end.AddDays(-i);
            var wobble = 1 + Math.Sin(i) * 0.03;
            list.Add((day, 49_844.56 * wobble, 5_903_270 * wobble, 5_508_950.5 * wobble));
        }

        return Task.FromResult<IReadOnlyList<(DateTimeOffset, double, double, double)>>(list);
    }
}
