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

public class TagIntelligenceExtendedTests
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
        await scope.ServiceProvider.GetRequiredService<ITagCatalogSyncService>().SyncPiIdentitiesAsync();

        return (sp, connection);
    }

    [Fact]
    public async Task GetChartSeries_Returns_points_with_unit()
    {
        var (sp, connection) = await BuildAsync();
        await using var _ = connection;
        await using var provider = sp;
        await using var scope = sp.CreateAsyncScope();

        var svc = scope.ServiceProvider.GetRequiredService<ITagIntelligenceService>();
        var chart = await svc.GetChartSeriesAsync(
            "Houston.B-210.Temperature",
            startUtc: DateTimeOffset.UtcNow.AddHours(-6),
            maxCount: 50);


        Assert.Equal(TagResolutionStatus.Found, chart.Status);
        Assert.False(string.IsNullOrWhiteSpace(chart.CanonicalName));
        Assert.NotEmpty(chart.Points);
        Assert.All(chart.Points, p => Assert.True(p.Value is null or >= -1000));
    }

    [Fact]
    public async Task GetSummary_Returns_min_max_avg()
    {
        var (sp, connection) = await BuildAsync();
        await using var _ = connection;
        await using var provider = sp;
        await using var scope = sp.CreateAsyncScope();

        var svc = scope.ServiceProvider.GetRequiredService<ITagIntelligenceService>();
        var summary = await svc.GetSummaryAsync(
            "Houston.B-210.Temperature",
            startUtc: DateTimeOffset.UtcNow.AddHours(-6));


        Assert.Equal(TagResolutionStatus.Found, summary.Status);
        Assert.NotNull(summary.Stats);
        Assert.True(summary.Stats!.Count > 0);
        Assert.True(summary.Stats.GoodCount > 0);
        Assert.True(summary.Stats.Max >= summary.Stats.Min);
    }

    [Fact]
    public async Task CompareTags_Returns_multiple_series()
    {
        var (sp, connection) = await BuildAsync();
        await using var _ = connection;
        await using var provider = sp;
        await using var scope = sp.CreateAsyncScope();

        var svc = scope.ServiceProvider.GetRequiredService<ITagIntelligenceService>();
        var compare = await svc.CompareTagsAsync(
            ["Houston.B-210.Temperature", "Houston.B-210.Pressure"],
            startUtc: DateTimeOffset.UtcNow.AddHours(-1),
            maxCount: 30);

        Assert.Equal(TagResolutionStatus.Found, compare.Status);
        Assert.True(compare.Series.Count >= 2);
        Assert.All(compare.Series, s => Assert.NotEmpty(s.Points));
    }

    [Fact]
    public async Task CompareTags_Unknown_returns_not_found_or_partial()
    {
        var (sp, connection) = await BuildAsync();
        await using var _ = connection;
        await using var provider = sp;
        await using var scope = sp.CreateAsyncScope();

        var svc = scope.ServiceProvider.GetRequiredService<ITagIntelligenceService>();
        var compare = await svc.CompareTagsAsync(["DOES_NOT_EXIST_AAA", "DOES_NOT_EXIST_BBB"]);

        Assert.NotEqual(TagResolutionStatus.Found, compare.Status);
    }

    [Fact]
    public async Task GetRelatedTags_Returns_neighbors()
    {
        var (sp, connection) = await BuildAsync();
        await using var _ = connection;
        await using var provider = sp;
        await using var scope = sp.CreateAsyncScope();

        var svc = scope.ServiceProvider.GetRequiredService<ITagIntelligenceService>();
        var details = await svc.GetTagDetailsAsync("Houston.B-210.Temperature");

        Assert.Equal(TagResolutionStatus.Found, details.Status);
        Assert.NotEmpty(details.Details!.RelatedTags);
    }

    [Fact]
    public async Task History_unknown_tag_is_not_found()
    {
        var (sp, connection) = await BuildAsync();
        await using var _ = connection;
        await using var provider = sp;
        await using var scope = sp.CreateAsyncScope();

        var svc = scope.ServiceProvider.GetRequiredService<ITagIntelligenceService>();
        var history = await svc.GetTagHistoryAsync("NO_SUCH_TAG_ZZZ");

        Assert.Equal(TagResolutionStatus.NotFound, history.Status);
        Assert.Empty(history.Values);
    }

    [Fact]
    public async Task Chart_unknown_tag_is_not_found()
    {
        var (sp, connection) = await BuildAsync();
        await using var _ = connection;
        await using var provider = sp;
        await using var scope = sp.CreateAsyncScope();

        var svc = scope.ServiceProvider.GetRequiredService<ITagIntelligenceService>();
        var chart = await svc.GetChartSeriesAsync("NO_SUCH_TAG_ZZZ");

        Assert.Equal(TagResolutionStatus.NotFound, chart.Status);
    }

    [Fact]
    public async Task Search_empty_or_whitespace_returns_catalog_or_empty_without_throwing()
    {
        var (sp, connection) = await BuildAsync();
        await using var _ = connection;
        await using var provider = sp;
        await using var scope = sp.CreateAsyncScope();

        var svc = scope.ServiceProvider.GetRequiredService<ITagIntelligenceService>();
        var hits = await svc.SearchTagsAsync("   ", 5);

        Assert.NotNull(hits);
        Assert.True(hits.Count <= 5);
    }

    [Fact]
    public async Task Resolve_exact_canonical_is_found()
    {
        var (sp, connection) = await BuildAsync();
        await using var _ = connection;
        await using var provider = sp;
        await using var scope = sp.CreateAsyncScope();

        var resolver = scope.ServiceProvider.GetRequiredService<ITagResolver>();
        var result = await resolver.ResolveAsync("Houston.B-210.Pressure");

        Assert.Equal(TagResolutionStatus.Found, result.Status);
        Assert.Equal("Houston.B-210.Pressure", result.Tag!.CanonicalName);
    }

    [Fact]
    public async Task Demo_connectivity_reports_connected()
    {
        var (sp, connection) = await BuildAsync();
        await using var _ = connection;
        await using var provider = sp;
        await using var scope = sp.CreateAsyncScope();

        var pi = scope.ServiceProvider.GetRequiredService<IPiConnectivity>();
        Assert.True(await pi.TryConnectAsync());
        Assert.False(string.IsNullOrWhiteSpace(await pi.GetSystemStatusAsync()));
    }

    [Fact]
    public async Task Details_include_business_metadata_keys()
    {
        var (sp, connection) = await BuildAsync();
        await using var _ = connection;
        await using var provider = sp;
        await using var scope = sp.CreateAsyncScope();

        var svc = scope.ServiceProvider.GetRequiredService<ITagIntelligenceService>();
        var result = await svc.GetTagDetailsAsync("Houston.B-210.Temperature");

        Assert.Contains(result.Details!.BusinessMetadata, b => b.Key is "OwnerTeam" or "Criticality" or "ExpectedMin");
    }

    [Fact]
    public async Task Summary_unknown_is_not_found()
    {
        var (sp, connection) = await BuildAsync();
        await using var _ = connection;
        await using var provider = sp;
        await using var scope = sp.CreateAsyncScope();

        var svc = scope.ServiceProvider.GetRequiredService<ITagIntelligenceService>();
        var summary = await svc.GetSummaryAsync("MISSING_TAG_QQQ");

        Assert.Equal(TagResolutionStatus.NotFound, summary.Status);
    }
}
