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
            UpdatedUtc = now
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
            UpdatedUtc = now
        };

        var burner = new TagCatalogEntity
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            CanonicalName = "Plant1.Boiler03.BurnerLoad",
            DisplayName = "Boiler 03 Burner Load",
            PiPointName = "B03_BURNER_LOAD",
            DescriptionOverride = "Boiler 03 burner load.",
            EquipmentId = "BOILER-03",
            OwnerTeam = "Boiler Operations",
            Criticality = "Medium",
            Unit = "%",
            CreatedUtc = now,
            UpdatedUtc = now
        };

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

        var header = new TagCatalogEntity
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            CanonicalName = "Plant1.Header.MainPressure",
            DisplayName = "Main Steam Header Pressure",
            PiPointName = "HEADER_STEAM_PRESSURE",
            DescriptionOverride = "Main steam header pressure.",
            EquipmentId = "HDR-MAIN",
            OwnerTeam = "Steam Systems",
            Criticality = "High",
            Unit = "bar(g)",
            CreatedUtc = now,
            UpdatedUtc = now,
            Aliases =
            [
                new TagAliasEntityRow { Id = Guid.NewGuid(), Alias = "Header.SteamPressure" }
            ]
        };

        var sinusoid = new TagCatalogEntity
        {
            Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            CanonicalName = "SINUSOID",
            DisplayName = "SINUSOID",
            PiPointName = "SINUSOID",
            DescriptionOverride = "Classic PI demo sine-wave tag.",
            OwnerTeam = "PI Admins",
            Criticality = "Low",
            Unit = "deg",
            CreatedUtc = now,
            UpdatedUtc = now
        };

        db.Tags.AddRange(boiler03, steamTemp, feedwater, burner, boiler04, header, sinusoid);

        db.Relationships.AddRange(
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
            });

        db.Documentation.Add(new TagDocumentationEntity
        {
            Id = Guid.NewGuid(),
            CanonicalName = boiler03.CanonicalName,
            Title = "Operating note",
            Body = "Expected operating range from the application metadata database: 38–46 bar(g).",
            Source = "TagDocumentation"
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
