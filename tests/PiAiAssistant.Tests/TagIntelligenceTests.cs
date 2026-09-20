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
    private static async Task<ServiceProvider> BuildAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddSingleton<IMetadataCache, MemoryMetadataCache>();
        services.Configure<PiConnectionOptions>(o =>
        {
            o.UseDemoMode = true;
            o.AuthMode = "Demo";
            o.SampleTagNames = ["SINUSOID", "B03_STEAM_PRESSURE"];
        });
        services.AddSingleton<DemoPiDataSource>();
        services.AddSingleton<IPiConnectivity>(sp => sp.GetRequiredService<DemoPiDataSource>());
        services.AddSingleton<IPiPointReader>(sp => sp.GetRequiredService<DemoPiDataSource>());
        services.AddSingleton<ITagValueReader>(sp => sp.GetRequiredService<DemoPiDataSource>());
        services.AddSingleton<IAfAttributeReader>(sp => sp.GetRequiredService<DemoPiDataSource>());
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("TagCatalogTest_" + Guid.NewGuid()));
        services.AddScoped<ITagCatalogRepository, TagCatalogRepository>();
        services.AddApplication();

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await TagCatalogSeeder.SeedAsync(db);

        return sp;
    }

    [Fact]
    public async Task GetTagDetails_ExactKnownTag_ReturnsFoundWithUnitsAndCurrentValue()
    {
        await using var provider = await BuildAsync();
        await using var scope = provider.CreateAsyncScope();

        var svc = scope.ServiceProvider.GetRequiredService<ITagIntelligenceService>();
        var result = await svc.GetTagDetailsAsync("Plant1.Boiler03.SteamPressure");

        Assert.Equal(TagResolutionStatus.Found, result.Status);
        Assert.NotNull(result.Details);
        Assert.Equal("B03_STEAM_PRESSURE", result.Details!.PiPointName);
        Assert.Equal("bar(g)", result.Details.EngineeringUnits);
        Assert.NotNull(result.Details.CurrentValue);
        Assert.Contains(result.Details.RelatedTags, r => r.Name.Contains("SteamTemperature"));
        Assert.Contains(result.Details.BusinessMetadata, b => b.Key == "ExpectedMin");
    }

    [Fact]
    public async Task Resolve_Alias_MapsToCanonical()
    {
        await using var provider = await BuildAsync();
        await using var scope = provider.CreateAsyncScope();

        var resolver = scope.ServiceProvider.GetRequiredService<ITagResolver>();
        var result = await resolver.ResolveAsync("Boiler03.SteamPressure");

        Assert.Equal(TagResolutionStatus.Found, result.Status);
        Assert.Equal("Plant1.Boiler03.SteamPressure", result.Tag!.CanonicalName);
    }

    [Fact]
    public async Task Search_Pressure_ReturnsMultipleCandidates()
    {
        await using var provider = await BuildAsync();
        await using var scope = provider.CreateAsyncScope();

        var svc = scope.ServiceProvider.GetRequiredService<ITagIntelligenceService>();
        var hits = await svc.SearchTagsAsync("SteamPressure", 10);

        Assert.True(hits.Count >= 2);
    }

    [Fact]
    public async Task UnknownTag_ReturnsNotFound()
    {
        await using var provider = await BuildAsync();
        await using var scope = provider.CreateAsyncScope();

        var svc = scope.ServiceProvider.GetRequiredService<ITagIntelligenceService>();
        var result = await svc.GetTagDetailsAsync("DOES_NOT_EXIST_TAG_XYZ");

        Assert.Equal(TagResolutionStatus.NotFound, result.Status);
    }
}
