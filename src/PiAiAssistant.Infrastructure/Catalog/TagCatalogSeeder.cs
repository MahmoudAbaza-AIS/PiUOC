using Microsoft.EntityFrameworkCore;

namespace PiAiAssistant.Infrastructure.Catalog;

/// <summary>
/// Seeds the SQLite tag catalog with NuGreen demo tags matching Shirajum Munir's PI WebAPI Emulator.
/// </summary>
public static class TagCatalogSeeder
{
    public const string NuGreenAnchorCanonical = "Houston.B-210.Temperature";

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Tags.AnyAsync(cancellationToken))
        {
            return;
        }

        await InsertNuGreenAsync(db, cancellationToken);
    }

    /// <summary>
    /// Replaces legacy Plant1/SINUSOID demo catalog with NuGreen when switching to the emulator.
    /// </summary>
    public static async Task EnsureNuGreenCatalogAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var hasNuGreen = await db.Tags.AnyAsync(t => t.CanonicalName == NuGreenAnchorCanonical, cancellationToken);
        if (hasNuGreen)
        {
            return;
        }

        db.Aliases.RemoveRange(db.Aliases);
        db.Relationships.RemoveRange(db.Relationships);
        db.Documentation.RemoveRange(db.Documentation);
        db.Tags.RemoveRange(db.Tags);
        await db.SaveChangesAsync(cancellationToken);
        await InsertNuGreenAsync(db, cancellationToken);
    }

    private static async Task InsertNuGreenAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        TagCatalogEntity Tag(
            string id,
            string canonical,
            string display,
            string point,
            string afPath,
            string equipment,
            string unit,
            string description,
            params string[] aliases)
        {
            return new TagCatalogEntity
            {
                Id = Guid.Parse(id),
                CanonicalName = canonical,
                DisplayName = display,
                PiPointName = point,
                PiWebId = $"pt_{point}",
                AfAttributePath = afPath,
                DescriptionOverride = description,
                EquipmentId = equipment,
                OwnerTeam = "NuGreen Operations",
                Criticality = "Medium",
                Unit = unit,
                CreatedUtc = now,
                UpdatedUtc = now,
                Aliases = aliases
                    .Select(a => new TagAliasEntityRow { Id = Guid.NewGuid(), Alias = a })
                    .ToList()
            };
        }

        var houstonTemp = Tag(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1",
            "Houston.B-210.Temperature",
            "Houston B-210 Temperature",
            "Houston.B-210.Temperature",
            @"\\AFServer1\NuGreen\Houston\Cracking Process\Equipment\B-210|Temperature",
            "B-210",
            "°C",
            "Boiler B-210 temperature at Houston NuGreen site.",
            "B-210 Temperature", "Houston temperature");

        var houstonPressure = Tag(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2",
            "Houston.B-210.Pressure",
            "Houston B-210 Pressure",
            "Houston.B-210.Pressure",
            @"\\AFServer1\NuGreen\Houston\Cracking Process\Equipment\B-210|Pressure",
            "B-210",
            "psi",
            "Boiler B-210 pressure at Houston NuGreen site.",
            "B-210 Pressure");

        var houstonSteam = Tag(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3",
            "Houston.B-210.SteamFlow",
            "Houston B-210 Steam Flow",
            "Houston.B-210.SteamFlow",
            @"\\AFServer1\NuGreen\Houston\Cracking Process\Equipment\B-210|Steam Flow",
            "B-210",
            "lb/hr",
            "Boiler B-210 steam flow at Houston NuGreen site.");

        var houstonRpm = Tag(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4",
            "Houston.C-110.RPM",
            "Houston C-110 RPM",
            "Houston.C-110.RPM",
            @"\\AFServer1\NuGreen\Houston\Cracking Process\Equipment\C-110|RPM",
            "C-110",
            "rpm",
            "Compressor C-110 RPM at Houston.");

        var houstonVibration = Tag(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5",
            "Houston.C-110.Vibration",
            "Houston C-110 Vibration",
            "Houston.C-110.Vibration",
            @"\\AFServer1\NuGreen\Houston\Cracking Process\Equipment\C-110|Vibration",
            "C-110",
            "mil",
            "Compressor C-110 vibration at Houston.");

        var oaklandTemp = Tag(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa6",
            "Oakland.B-220.Temperature",
            "Oakland B-220 Temperature",
            "Oakland.B-220.Temperature",
            @"\\AFServer1\NuGreen\Oakland\Cracking Process\Equipment\B-220|Temperature",
            "B-220",
            "°C",
            "Boiler B-220 temperature at Oakland NuGreen site.",
            "Oakland temperature");

        var oaklandPressure = Tag(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa7",
            "Oakland.B-220.Pressure",
            "Oakland B-220 Pressure",
            "Oakland.B-220.Pressure",
            @"\\AFServer1\NuGreen\Oakland\Cracking Process\Equipment\B-220|Pressure",
            "B-220",
            "psi",
            "Boiler B-220 pressure at Oakland.");

        db.Tags.AddRange(
            houstonTemp, houstonPressure, houstonSteam, houstonRpm, houstonVibration,
            oaklandTemp, oaklandPressure);

        db.Relationships.AddRange(
            new TagRelationshipEntity
            {
                Id = Guid.NewGuid(),
                FromCanonicalName = houstonTemp.CanonicalName,
                ToCanonicalName = houstonPressure.CanonicalName,
                Relationship = "Associated process variable",
                Description = "Pressure companion for B-210 temperature"
            },
            new TagRelationshipEntity
            {
                Id = Guid.NewGuid(),
                FromCanonicalName = houstonTemp.CanonicalName,
                ToCanonicalName = houstonSteam.CanonicalName,
                Relationship = "Associated process variable",
                Description = "Steam flow companion for B-210"
            },
            new TagRelationshipEntity
            {
                Id = Guid.NewGuid(),
                FromCanonicalName = houstonRpm.CanonicalName,
                ToCanonicalName = houstonVibration.CanonicalName,
                Relationship = "Associated process variable",
                Description = "Vibration companion for C-110 RPM"
            });

        db.Documentation.Add(new TagDocumentationEntity
        {
            Id = Guid.NewGuid(),
            CanonicalName = houstonTemp.CanonicalName,
            Title = "Emulator note",
            Body = "NuGreen sample tag from the AVEVA PI WebAPI Emulator (AFServer1 / PIServer1).",
            Source = "TagDocumentation"
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
