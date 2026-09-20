using PiAiAssistant.Domain.Enums;
using PiAiAssistant.Domain.Interfaces;
using PiAiAssistant.Infrastructure.Afag;

namespace PiAiAssistant.Tests;

public class AfagHierarchyReaderTests
{
    private readonly IAfagHierarchyReader _reader = new AfagDemoHierarchyReader();

    [Fact]
    public async Task Kingdom_matches_afag_mockup_kpis()
    {
        var k = await _reader.GetKingdomAsync();

        Assert.Equal("KSA", k.Code);
        Assert.Equal(41687, k.GrossMw);
        Assert.Equal(40359, k.NetMw);
        Assert.Equal(55547, k.CapacityMw);
        Assert.Equal(75, k.LoadingFactorPercent);
        Assert.Equal(321, k.InServiceUnits);
        Assert.Equal(484, k.TotalUnits);
    }

    [Fact]
    public async Task Sectors_include_four_operating_areas()
    {
        var sectors = await _reader.GetSectorsAsync();

        Assert.Equal(4, sectors.Count);
        Assert.Contains(sectors, s => s.Code == "COA");
        Assert.Contains(sectors, s => s.Code == "EOA");
        Assert.Contains(sectors, s => s.Code == "SOA");
        Assert.Contains(sectors, s => s.Code == "WOA");
    }

    [Fact]
    public async Task Coa_sector_matches_mockup()
    {
        var coa = await _reader.GetSectorAsync("COA");

        Assert.NotNull(coa);
        Assert.Equal(12806, coa!.GrossMw);
        Assert.Equal(18116, coa.CapacityMw);
        Assert.Equal(71, coa.LoadingFactorPercent);
        Assert.Equal(179, coa.InServiceUnits);
        Assert.Equal(232, coa.TotalUnits);
    }

    [Theory]
    [InlineData("coa")]
    [InlineData("CoA")]
    [InlineData("COA")]
    public async Task GetSector_is_case_insensitive(string code)
    {
        var sector = await _reader.GetSectorAsync(code);
        Assert.NotNull(sector);
        Assert.Equal("COA", sector!.Code);
    }

    [Fact]
    public async Task Unknown_sector_returns_null()
    {
        Assert.Null(await _reader.GetSectorAsync("XYZ"));
    }

    [Fact]
    public async Task Coa_plants_include_pp09_with_heat_rate_drift()
    {
        var plants = await _reader.GetPlantsAsync("COA");

        Assert.True(plants.Count >= 10);
        var pp09 = plants.Single(p => p.Code == "PP09");
        Assert.Equal(2195.8, pp09.GrossMw);
        Assert.Equal(4097, pp09.CapacityMw);
        Assert.Equal(60, pp09.LoadingFactorPercent);
        Assert.Equal(72, pp09.TargetLoadingPercent);
        Assert.Equal(-3.2, pp09.HeatRateDeltaWeekPercent);
        Assert.Equal(GenerationTechnology.Combined, pp09.Technology);
    }

    [Fact]
    public async Task Pp09_blocks_match_generation_totals()
    {
        var blocks = await _reader.GetBlocksAsync("PP09");

        Assert.Equal(8, blocks.Count);
        Assert.Contains(blocks, b => b.Code == "A1" && b.GrossMw == 202.15);
        Assert.Contains(blocks, b => b.Code == "F" && b.FuelKgPerDay is null);
        Assert.True(blocks.Sum(b => b.GrossMw) > 2000);
    }

    [Fact]
    public async Task Other_plant_has_no_blocks_in_demo()
    {
        var blocks = await _reader.GetBlocksAsync("PP10");
        Assert.Empty(blocks);
    }

    [Fact]
    public async Task Gt01_unit_has_bad_flame_and_process_values()
    {
        var unit = await _reader.GetUnitAsync("PP09", "A1", "GT01");

        Assert.NotNull(unit);
        Assert.Equal(44.73, unit!.ActivePowerMw);
        Assert.Equal(73.46, unit.LoadingPercent);
        Assert.Equal(3.87, unit.FuelFlowKgPerSec);
        Assert.Equal(4, unit.FlameIntensityStatus.Count);
        Assert.All(unit.FlameIntensityStatus, s => Assert.Contains("Bad", s, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("PP09", "A1", "GT02")]
    [InlineData("PP09", "B1", "GT01")]
    [InlineData("PP10", "A1", "GT01")]
    public async Task Unknown_unit_returns_null(string plant, string block, string unit)
    {
        Assert.Null(await _reader.GetUnitAsync(plant, block, unit));
    }

    [Fact]
    public async Task GetUnits_filters_by_block()
    {
        var a1 = await _reader.GetUnitsAsync("PP09", "A1");
        var b1 = await _reader.GetUnitsAsync("PP09", "B1");

        Assert.Single(a1);
        Assert.Empty(b1);
    }

    [Fact]
    public async Task Kingdom_kpis_include_loading_and_co2()
    {
        var kpis = await _reader.GetKpisAsync("Kingdom");

        Assert.Contains(kpis, k => k.Key == "loading_factor" && Equals(k.Value, 75d));
        Assert.Contains(kpis, k => k.Key == "co2");
    }

    [Fact]
    public async Task Plant_kpis_include_heat_rate_delta()
    {
        var kpis = await _reader.GetKpisAsync("Plant", "PP09");

        Assert.Contains(kpis, k => k.Key == "heat_rate_delta");
        Assert.Contains(kpis, k => k.Key == "loading_factor" && k.Target is not null);
    }

    [Fact]
    public async Task Sector_kpis_unknown_code_empty()
    {
        var kpis = await _reader.GetKpisAsync("Sector", "NOPE");
        Assert.Empty(kpis);
    }

    [Fact]
    public async Task Pp09_fuel_trend_has_seven_days()
    {
        var trend = await _reader.GetPlantFuelTrendAsync("PP09", 7);

        Assert.Equal(7, trend.Count);
        Assert.All(trend, t =>
        {
            Assert.True(t.GenMwh > 0);
            Assert.True(t.NaturalGasKg > 0);
            Assert.True(t.LiquidFuelKg > 0);
        });
    }

    [Fact]
    public async Task Other_plant_fuel_trend_empty()
    {
        var trend = await _reader.GetPlantFuelTrendAsync("PP07");
        Assert.Empty(trend);
    }
}
