using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PiAiAssistant.Application;
using PiAiAssistant.Application.Abstractions;
using PiAiAssistant.Application.Tags;
using PiAiAssistant.Domain.Enums;
using PiAiAssistant.Domain.Interfaces;
using PiAiAssistant.Infrastructure.Caching;
using PiAiAssistant.Infrastructure.Catalog;
using PiAiAssistant.Infrastructure.Options;
using PiAiAssistant.Infrastructure.Pi;

namespace PiAiAssistant.Tests;

public class TagIntelligenceTests
{
    private static async Task<(ServiceProvider Sp, SqliteConnection Connection)> BuildAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddSingleton<IMetadataCache, MemoryMetadataCache>();
        services.Configure<PiConnectionOptions>(o =>
        {
            o.UseDemoMode = true;
            o.AuthMode = "Demo";
            o.DataArchiveName = "PIServer1";
            o.SampleTagNames = ["Houston.B-210.Temperature", "Houston.B-210.Pressure"];
        });
        services.AddSingleton<DemoPiDataSource>();
        services.AddSingleton<IPiConnectivity>(sp => sp.GetRequiredService<DemoPiDataSource>());
        services.AddSingleton<IPiPointReader>(sp => sp.GetRequiredService<DemoPiDataSource>());
        services.AddSingleton<ITagValueReader>(sp => sp.GetRequiredService<DemoPiDataSource>());
        services.AddSingleton<IAfAttributeReader>(sp => sp.GetRequiredService<DemoPiDataSource>());
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
        services.AddScoped<ITagCatalogRepository, TagCatalogRepository>();
        services.AddApplication();

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await TagCatalogSeeder.SeedAsync(db);

        return (sp, connection);
    }

    [Fact]
    public async Task GetTagDetails_ExactKnownTag_ReturnsFoundWithUnitsAndCurrentValue()
    {
        var (sp, connection) = await BuildAsync();
        await using var _ = connection;
        await using var provider = sp;
        await using var scope = sp.CreateAsyncScope();

        var svc = scope.ServiceProvider.GetRequiredService<ITagIntelligenceService>();
        var result = await svc.GetTagDetailsAsync("Houston.B-210.Temperature");

        Assert.Equal(TagResolutionStatus.Found, result.Status);
        Assert.NotNull(result.Details);
        Assert.Equal("Houston.B-210.Temperature", result.Details!.PiPointName);
        Assert.Equal("°C", result.Details.EngineeringUnits);
        Assert.NotNull(result.Details.CurrentValue);
        Assert.Contains(result.Details.RelatedTags, r => r.Name.Contains("Pressure", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Details.BusinessMetadata, b => b.Key == "OwnerTeam");
    }

    [Fact]
    public async Task Resolve_Alias_MapsToCanonical()
    {
        var (sp, connection) = await BuildAsync();
        await using var _ = connection;
        await using var provider = sp;
        await using var scope = sp.CreateAsyncScope();

        var resolver = scope.ServiceProvider.GetRequiredService<ITagResolver>();
        var result = await resolver.ResolveAsync("B-210 Temperature");

        Assert.Equal(TagResolutionStatus.Found, result.Status);
        Assert.Equal("Houston.B-210.Temperature", result.Tag!.CanonicalName);
    }

    [Fact]
    public async Task Search_Pressure_ReturnsMultipleCandidates()
    {
        var (sp, connection) = await BuildAsync();
        await using var _ = connection;
        await using var provider = sp;
        await using var scope = sp.CreateAsyncScope();

        var svc = scope.ServiceProvider.GetRequiredService<ITagIntelligenceService>();
        var hits = await svc.SearchTagsAsync("Pressure", 10);

        Assert.True(hits.Count >= 2);
    }

    [Fact]
    public async Task UnknownTag_ReturnsNotFound()
    {
        var (sp, connection) = await BuildAsync();
        await using var _ = connection;
        await using var provider = sp;
        await using var scope = sp.CreateAsyncScope();

        var svc = scope.ServiceProvider.GetRequiredService<ITagIntelligenceService>();
        var result = await svc.GetTagDetailsAsync("DOES_NOT_EXIST_TAG_XYZ");

        Assert.Equal(TagResolutionStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ListCatalogTags_ReturnsSeededCanonicalNames()
    {
        var (sp, connection) = await BuildAsync();
        await using var _ = connection;
        await using var provider = sp;
        await using var scope = sp.CreateAsyncScope();

        var svc = scope.ServiceProvider.GetRequiredService<ITagIntelligenceService>();
        var tags = await svc.ListCatalogTagsAsync();

        Assert.Contains(tags, t => t.CanonicalName == "Houston.B-210.Temperature");
        Assert.Contains(tags, t => t.CanonicalName == "Oakland.B-220.Temperature");
    }

    [Fact]
    public async Task SyncCatalog_BackfillsMissingPiWebIds()
    {
        var (sp, connection) = await BuildAsync();
        await using var _ = connection;
        await using var provider = sp;
        await using var scope = sp.CreateAsyncScope();

        var catalog = scope.ServiceProvider.GetRequiredService<ITagCatalogRepository>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Tags.FirstAsync(t => t.CanonicalName == "Houston.B-210.Temperature");
        row.PiWebId = "DEMO_STALE";
        await db.SaveChangesAsync();

        var sync = scope.ServiceProvider.GetRequiredService<ITagCatalogSyncService>();
        var result = await sync.SyncPiIdentitiesAsync();

        Assert.True(result.Updated >= 1);
        var refreshed = await catalog.FindByCanonicalNameAsync("Houston.B-210.Temperature");
        Assert.False(string.IsNullOrWhiteSpace(refreshed?.PiWebId));
        Assert.NotEqual("DEMO_STALE", refreshed!.PiWebId);
    }

    [Fact]
    public async Task GetTagHistory_UsesCatalogResolvedPoint()
    {
        var (sp, connection) = await BuildAsync();
        await using var _ = connection;
        await using var provider = sp;
        await using var scope = sp.CreateAsyncScope();

        var sync = scope.ServiceProvider.GetRequiredService<ITagCatalogSyncService>();
        await sync.SyncPiIdentitiesAsync();

        var svc = scope.ServiceProvider.GetRequiredService<ITagIntelligenceService>();
        var history = await svc.GetTagHistoryAsync("B-210 Temperature");

        Assert.Equal(TagResolutionStatus.Found, history.Status);
        Assert.NotEmpty(history.Values);
    }
}
