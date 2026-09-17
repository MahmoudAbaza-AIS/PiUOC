using Microsoft.Extensions.DependencyInjection;
using PiAiAssistant.Application.Tags;

namespace PiAiAssistant.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ITagResolver, TagResolver>();
        services.AddScoped<ITagIntelligenceService, TagIntelligenceService>();
        services.AddScoped<ITagCatalogSyncService, TagCatalogSyncService>();
        return services;
    }
}
