using Microsoft.EntityFrameworkCore;

namespace PiAiAssistant.Infrastructure.Catalog;

public static class TagCatalogSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Tags.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;

        // ===== BOILER 03 TAGS =====
        var boiler03 = new TagCatalogEntity
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CanonicalName = "Plant1.Boiler03.SteamPressure",
            DisplayName = "Boiler 03 Steam Pressure",
            PiPointName = "B03_STEAM_PRESSURE",
            AfAttributePath = @"\\AFSERVER\Production\Plant1\Boiler03|Steam Pressure",
            DescriptionOverride = "Main steam pressure downstream of Boiler 03.",
            EquipmentId = "BOILER-03",
            OwnerTeam = "Boiler Operations",
            Criticality = "High",
            ExpectedMin = 38m,
            ExpectedMax = 46m,
            Unit = "bar(g)",
            CreatedUtc = now,
            UpdatedUtc = now,
            Aliases =
            [
                new TagAliasEntityRow { Id = Guid.NewGuid(), Alias = "Boiler03.SteamPressure" },
                new TagAliasEntityRow { Id = Guid.NewGuid(), Alias = "boiler 03 steam pressure" },
                new TagAliasEntityRow { Id = Guid.NewGuid(), Alias = "B03 steam pressure" }
            ]
        };

        var steamTemp = new TagCatalogEntity
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            CanonicalName = "Plant1.Boiler03.SteamTemperature",
            DisplayName = "Boiler 03 Steam Temperature",
            PiPointName = "B03_STEAM_TEMP",
            AfAttributePath = @"\\AFSERVER\Production\Plant1\Boiler03|Steam Temperature",
            DescriptionOverride = "Main steam temperature at Boiler 03.",
            EquipmentId = "BOILER-03",
            OwnerTeam = "Boiler Operations",
            Criticality = "High",
            ExpectedMin = 480m,
            ExpectedMax = 540m,
            Unit = "degC",
            CreatedUtc = now,
            UpdatedUtc = now,
            Aliases =
            [
                new TagAliasEntityRow { Id = Guid.NewGuid(), Alias = "B03 steam temperature" },
                new TagAliasEntityRow { Id = Guid.NewGuid(), Alias = "Boiler03.SteamTemp" }
            ]
        };

        var feedwater = new TagCatalogEntity
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            CanonicalName = "Plant1.Boiler03.FeedWaterFlow",
            DisplayName = "Boiler 03 Feedwater Flow",
            PiPointName = "B03_FEEDWATER_FLOW",
            DescriptionOverride = "Boiler 03 feedwater flow.",
            EquipmentId = "BOILER-03",
            OwnerTeam = "Boiler Operations",
            Criticality = "Medium",
            Unit = "t/h",
            CreatedUtc = now,
            UpdatedUtc = now,
            Aliases =
            [
                new TagAliasEntityRow { Id = Guid.NewGuid(), Alias = "B03 feedwater flow" }
            ]
        };

        var burner = new TagCatalogEntity
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            CanonicalName = "Plant1.Boiler03.BurnerLoad",
            DisplayName = "Boiler 03 Burner Load",
            PiPointName = "B03_BURNER_LOAD",
            DescriptionOverride = "Boiler 03 burner load percentage.",
            EquipmentId = "BOILER-03",
            OwnerTeam = "Boiler Operations",
            Criticality = "Medium",
            ExpectedMin = 0m,
            ExpectedMax = 100m,
            Unit = "%",
            CreatedUtc = now,
            UpdatedUtc = now,
            Aliases =
            [
                new TagAliasEntityRow { Id = Guid.NewGuid(), Alias = "B03 burner load" }
            ]
        };

        var boilerFume = new TagCatalogEntity
        {
            Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
            CanonicalName = "Plant1.Boiler03.FumeGasTemperature",
            DisplayName = "Boiler 03 Fume Gas Temperature",
            PiPointName = "B03_FUME_GAS_TEMP",
            DescriptionOverride = "Fume gas temperature at outlet.",
            EquipmentId = "BOILER-03",
            OwnerTeam = "Boiler Operations",
            Criticality = "High",
            ExpectedMin = 150m,
            ExpectedMax = 250m,
            Unit = "degC",
            CreatedUtc = now,
            UpdatedUtc = now
        };

        // ===== BOILER 04 TAGS =====
        var boiler04 = new TagCatalogEntity
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            CanonicalName = "Plant1.Boiler04.SteamPressure",
            DisplayName = "Boiler 04 Steam Pressure",
            PiPointName = "B04_STEAM_PRESSURE",
            DescriptionOverride = "Main steam pressure downstream of Boiler 04.",
            EquipmentId = "BOILER-04",
            OwnerTeam = "Boiler Operations",
            Criticality = "High",
            ExpectedMin = 38m,
            ExpectedMax = 46m,
            Unit = "bar(g)",
            CreatedUtc = now,
            UpdatedUtc = now,
            Aliases =
            [
                new TagAliasEntityRow { Id = Guid.NewGuid(), Alias = "Boiler04.SteamPressure" }
            ]
        };

        var boiler04Temp = new TagCatalogEntity
        {
            Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),
            CanonicalName = "Plant1.Boiler04.SteamTemperature",
            DisplayName = "Boiler 04 Steam Temperature",
            PiPointName = "B04_STEAM_TEMP",
            DescriptionOverride = "Main steam temperature at Boiler 04.",
            EquipmentId = "BOILER-04",
            OwnerTeam = "Boiler Operations",
            Criticality = "High",
            ExpectedMin = 480m,
            ExpectedMax = 540m,
            Unit = "degC",
            CreatedUtc = now,
            UpdatedUtc = now
        };

        var boiler04Feed = new TagCatalogEntity
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            CanonicalName = "Plant1.Boiler04.FeedWaterFlow",
            DisplayName = "Boiler 04 Feedwater Flow",
            PiPointName = "B04_FEEDWATER_FLOW",
            DescriptionOverride = "Boiler 04 feedwater flow.",
            EquipmentId = "BOILER-04",
            OwnerTeam = "Boiler Operations",
            Criticality = "Medium",
            Unit = "t/h",
            CreatedUtc = now,
            UpdatedUtc = now
        };

        // ===== STEAM HEADER TAGS =====
        var header = new TagCatalogEntity
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            CanonicalName = "Plant1.Header.MainPressure",
            DisplayName = "Main Steam Header Pressure",
            PiPointName = "HEADER_STEAM_PRESSURE",
            DescriptionOverride = "Main steam header pressure combining all boiler outputs.",
            EquipmentId = "HDR-MAIN",
            OwnerTeam = "Steam Systems",
            Criticality = "High",
            ExpectedMin = 35m,
            ExpectedMax = 50m,
            Unit = "bar(g)",
            CreatedUtc = now,
            UpdatedUtc = now,
            Aliases =
            [
                new TagAliasEntityRow { Id = Guid.NewGuid(), Alias = "Header.SteamPressure" },
                new TagAliasEntityRow { Id = Guid.NewGuid(), Alias = "Main header pressure" }
            ]
        };

        var headerTemp = new TagCatalogEntity
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            CanonicalName = "Plant1.Header.MainTemperature",
            DisplayName = "Main Steam Header Temperature",
            PiPointName = "HEADER_STEAM_TEMP",
            DescriptionOverride = "Main steam header temperature.",
            EquipmentId = "HDR-MAIN",
            OwnerTeam = "Steam Systems",
            Criticality = "High",
            ExpectedMin = 450m,
            ExpectedMax = 550m,
            Unit = "degC",
            CreatedUtc = now,
            UpdatedUtc = now
        };

        var headerFlow = new TagCatalogEntity
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            CanonicalName = "Plant1.Header.MainFlow",
            DisplayName = "Main Steam Header Flow",
            PiPointName = "HEADER_STEAM_FLOW",
            DescriptionOverride = "Total steam flow through main header.",
            EquipmentId = "HDR-MAIN",
            OwnerTeam = "Steam Systems",
            Criticality = "High",
            Unit = "t/h",
            CreatedUtc = now,
            UpdatedUtc = now
        };

        // ===== DEMO TAG =====
        var sinusoid = new TagCatalogEntity
        {
            Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            CanonicalName = "SINUSOID",
            DisplayName = "SINUSOID",
            PiPointName = "SINUSOID",
            DescriptionOverride = "Classic PI demo sine-wave tag for testing.",
            OwnerTeam = "PI Admins",
            Criticality = "Low",
            Unit = "deg",
            CreatedUtc = now,
            UpdatedUtc = now
        };

        // ===== CONDENSER TAGS =====
        var condenser = new TagCatalogEntity
        {
            Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            CanonicalName = "Plant1.Condenser.CoolingWaterInlet",
            DisplayName = "Condenser Cooling Water Inlet Temperature",
            PiPointName = "COND_COOL_WATER_IN_TEMP",
            DescriptionOverride = "Cooling water inlet temperature to condenser.",
            EquipmentId = "COND-001",
            OwnerTeam = "Cooling Systems",
            Criticality = "High",
            ExpectedMin = 20m,
            ExpectedMax = 35m,
            Unit = "degC",
            CreatedUtc = now,
            UpdatedUtc = now
        };

        var condenserVacuum = new TagCatalogEntity
        {
            Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            CanonicalName = "Plant1.Condenser.Vacuum",
            DisplayName = "Condenser Vacuum Pressure",
            PiPointName = "COND_VACUUM",
            DescriptionOverride = "Condenser vacuum pressure (negative absolute).",
            EquipmentId = "COND-001",
            OwnerTeam = "Cooling Systems",
            Criticality = "High",
            ExpectedMin = -0.1m,
            ExpectedMax = -0.08m,
            Unit = "bar(a)",
            CreatedUtc = now,
            UpdatedUtc = now
        };

        // ===== DEAERATOR TAGS =====
        var deaerator = new TagCatalogEntity
        {
            Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            CanonicalName = "Plant1.Deaerator.Level",
            DisplayName = "Deaerator Water Level",
            PiPointName = "DEAE_LEVEL",
            DescriptionOverride = "Deaerator tank water level.",
            EquipmentId = "DEAE-001",
            OwnerTeam = "Water Treatment",
            Criticality = "Medium",
            ExpectedMin = 40m,
            ExpectedMax = 60m,
            Unit = "%",
            CreatedUtc = now,
            UpdatedUtc = now
        };

        var deaeratorTemp = new TagCatalogEntity
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111112"),
            CanonicalName = "Plant1.Deaerator.Temperature",
            DisplayName = "Deaerator Temperature",
            PiPointName = "DEAE_TEMP",
            DescriptionOverride = "Deaerator outlet water temperature.",
            EquipmentId = "DEAE-001",
            OwnerTeam = "Water Treatment",
            Criticality = "Medium",
            ExpectedMin = 100m,
            ExpectedMax = 110m,
            Unit = "degC",
            CreatedUtc = now,
            UpdatedUtc = now
        };

        // Add all tags
        db.Tags.AddRange(
            boiler03, steamTemp, feedwater, burner, boilerFume,
            boiler04, boiler04Temp, boiler04Feed,
            header, headerTemp, headerFlow,
            sinusoid,
            condenser, condenserVacuum,
            deaerator, deaeratorTemp
        );

        // ===== RELATIONSHIPS =====
        db.Relationships.AddRange(
            // Boiler 03 relationships
            new TagRelationshipEntity
            {
                Id = Guid.NewGuid(),
                FromCanonicalName = boiler03.CanonicalName,
                ToCanonicalName = steamTemp.CanonicalName,
                Relationship = "Associated process variable",
                Description = "Temperature companion for steam pressure"
            },
            new TagRelationshipEntity
            {
                Id = Guid.NewGuid(),
                FromCanonicalName = boiler03.CanonicalName,
                ToCanonicalName = feedwater.CanonicalName,
                Relationship = "Upstream input",
                Description = "Feedwater flow affecting steam generation"
            },
            new TagRelationshipEntity
            {
                Id = Guid.NewGuid(),
                FromCanonicalName = boiler03.CanonicalName,
                ToCanonicalName = burner.CanonicalName,
                Relationship = "Operating-load indicator",
                Description = "Burner load context for pressure"
            },
            new TagRelationshipEntity
            {
                Id = Guid.NewGuid(),
                FromCanonicalName = boiler03.CanonicalName,
                ToCanonicalName = boilerFume.CanonicalName,
                Relationship = "Outlet condition",
                Description = "Fume gas temperature indicates combustion efficiency"
            },
            // Boiler 03 to Header
            new TagRelationshipEntity
            {
                Id = Guid.NewGuid(),
                FromCanonicalName = boiler03.CanonicalName,
                ToCanonicalName = header.CanonicalName,
                Relationship = "Upstream source",
                Description = "Boiler 03 contributes to main steam header"
            },
            // Boiler 04 relationships
            new TagRelationshipEntity
            {
                Id = Guid.NewGuid(),
                FromCanonicalName = boiler04.CanonicalName,
                ToCanonicalName = boiler04Temp.CanonicalName,
                Relationship = "Associated process variable",
                Description = "Temperature companion for steam pressure"
            },
            new TagRelationshipEntity
            {
                Id = Guid.NewGuid(),
                FromCanonicalName = boiler04.CanonicalName,
                ToCanonicalName = boiler04Feed.CanonicalName,
                Relationship = "Upstream input",
                Description = "Feedwater flow affecting steam generation"
            },
            new TagRelationshipEntity
            {
                Id = Guid.NewGuid(),
                FromCanonicalName = boiler04.CanonicalName,
                ToCanonicalName = header.CanonicalName,
                Relationship = "Upstream source",
                Description = "Boiler 04 contributes to main steam header"
            },
            // Header relationships
            new TagRelationshipEntity
            {
                Id = Guid.NewGuid(),
                FromCanonicalName = header.CanonicalName,
                ToCanonicalName = headerTemp.CanonicalName,
                Relationship = "Associated process variable",
                Description = "Temperature companion for pressure"
            },
            new TagRelationshipEntity
            {
                Id = Guid.NewGuid(),
                FromCanonicalName = header.CanonicalName,
                ToCanonicalName = headerFlow.CanonicalName,
                Relationship = "Associated process variable",
                Description = "Flow rate indicator"
            },
            // Header to Condenser
            new TagRelationshipEntity
            {
                Id = Guid.NewGuid(),
                FromCanonicalName = header.CanonicalName,
                ToCanonicalName = condenser.CanonicalName,
                Relationship = "Downstream process",
                Description = "Steam flow goes to condenser"
            },
            // Condenser relationships
            new TagRelationshipEntity
            {
                Id = Guid.NewGuid(),
                FromCanonicalName = condenser.CanonicalName,
                ToCanonicalName = condenserVacuum.CanonicalName,
                Relationship = "Associated condition",
                Description = "Vacuum pressure indicates condenser efficiency"
            },
            // Condenser to Deaerator
            new TagRelationshipEntity
            {
                Id = Guid.NewGuid(),
                FromCanonicalName = condenser.CanonicalName,
                ToCanonicalName = deaerator.CanonicalName,
                Relationship = "Downstream process",
                Description = "Condensed water flows to deaerator"
            },
            // Deaerator relationships
            new TagRelationshipEntity
            {
                Id = Guid.NewGuid(),
                FromCanonicalName = deaerator.CanonicalName,
                ToCanonicalName = deaeratorTemp.CanonicalName,
                Relationship = "Associated condition",
                Description = "Temperature indicates deaeration performance"
            },
            // Deaerator back to feedwater
            new TagRelationshipEntity
            {
                Id = Guid.NewGuid(),
                FromCanonicalName = deaerator.CanonicalName,
                ToCanonicalName = feedwater.CanonicalName,
                Relationship = "Upstream source",
                Description = "Deaerated water supplied as feedwater"
            }
        );

        // ===== DOCUMENTATION =====
        db.Documentation.AddRange(
            new TagDocumentationEntity
            {
                Id = Guid.NewGuid(),
                CanonicalName = boiler03.CanonicalName,
                Title = "Boiler 03 Operating Pressures",
                Body = "Expected operating range from the application metadata database: 38–46 bar(g). Critical safety limit: maximum 50 bar(g).",
                Source = "TagDocumentation"
            },
            new TagDocumentationEntity
            {
                Id = Guid.NewGuid(),
                CanonicalName = steamTemp.CanonicalName,
                Title = "Steam Temperature Safety Notes",
                Body = "Superheated steam temperature must remain between 480–540 °C to prevent equipment damage. Monitor continuously during operation.",
                Source = "TagDocumentation"
            },
            new TagDocumentationEntity
            {
                Id = Guid.NewGuid(),
                CanonicalName = header.CanonicalName,
                Title = "Main Steam Header Design & Operation",
                Body = "Main steam header combines output from both Boiler 03 and Boiler 04. Pressure should be maintained between 35–50 bar(g). The header supplies steam to all downstream consumers including condensers and industrial processes.",
                Source = "TagDocumentation"
            },
            new TagDocumentationEntity
            {
                Id = Guid.NewGuid(),
                CanonicalName = feedwater.CanonicalName,
                Title = "Feedwater Flow Control",
                Body = "Feedwater flow must be carefully balanced with steam generation rate. Excessive flow increases fuel consumption; insufficient flow risks boiler tube damage.",
                Source = "TagDocumentation"
            },
            new TagDocumentationEntity
            {
                Id = Guid.NewGuid(),
                CanonicalName = deaerator.CanonicalName,
                Title = "Deaerator Operation Manual",
                Body = "The deaerator removes dissolved oxygen and other gases from feedwater before it enters the boiler. Maintain water level between 40–60% and temperature above 100 °C for optimal performance.",
                Source = "TagDocumentation"
            },
            new TagDocumentationEntity
            {
                Id = Guid.NewGuid(),
                CanonicalName = condenser.CanonicalName,
                Title = "Condenser Cooling Water Management",
                Body = "Cooling water inlet temperature should be maintained between 20–35 °C for efficient condensation. Higher inlet temperatures reduce condenser efficiency and increase steam chest temperature.",
                Source = "TagDocumentation"
            }
        );

        await db.SaveChangesAsync(cancellationToken);
    }
}
