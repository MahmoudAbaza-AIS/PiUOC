using System.ClientModel;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using PiAiAssistant.Application;
using PiAiAssistant.Application.Abstractions;
using PiAiAssistant.Application.Chat;
using PiAiAssistant.Application.Tags;
using PiAiAssistant.Domain.Interfaces;
using PiAiAssistant.Infrastructure.AI;
using PiAiAssistant.Infrastructure.Caching;
using PiAiAssistant.Infrastructure.Catalog;
using PiAiAssistant.Infrastructure.Options;
using PiAiAssistant.Infrastructure.Pi;

namespace PiAiAssistant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddSingleton<IMetadataCache, MemoryMetadataCache>();

        services.AddOptions<PiConnectionOptions>()
            .Bind(configuration.GetSection(PiConnectionOptions.SectionName));
        services.AddOptions<OllamaOptions>()
            .Bind(configuration.GetSection(OllamaOptions.SectionName));
        services.AddOptions<QwenOnlineOptions>()
            .Bind(configuration.GetSection(QwenOnlineOptions.SectionName));

        var pi = configuration.GetSection(PiConnectionOptions.SectionName).Get<PiConnectionOptions>()
                 ?? new PiConnectionOptions();

        // Strategy: Demo vs live PI — selected once at composition root, not in use cases.
        if (pi.UseDemoMode ||
            string.Equals(pi.AuthMode, "Demo", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<DemoPiDataSource>();
            services.AddSingleton<IPiConnectivity>(sp => sp.GetRequiredService<DemoPiDataSource>());
            services.AddSingleton<IPiPointReader>(sp => sp.GetRequiredService<DemoPiDataSource>());
            services.AddSingleton<ITagValueReader>(sp => sp.GetRequiredService<DemoPiDataSource>());
            services.AddSingleton<IAfAttributeReader>(sp => sp.GetRequiredService<DemoPiDataSource>());
        }
        else
        {
            services.AddHttpClient<PiWebApiDataSource>((sp, client) =>
                {
                    var options = sp.GetRequiredService<IOptions<PiConnectionOptions>>().Value;
                    ConfigureHttpClient(client, options);
                })
                .ConfigurePrimaryHttpMessageHandler(sp =>
                {
                    var options = sp.GetRequiredService<IOptions<PiConnectionOptions>>().Value;
                    return CreateHandler(options);
                });

            services.AddScoped<IPiConnectivity>(sp => sp.GetRequiredService<PiWebApiDataSource>());
            services.AddScoped<IPiPointReader>(sp => sp.GetRequiredService<PiWebApiDataSource>());
            services.AddScoped<ITagValueReader>(sp => sp.GetRequiredService<PiWebApiDataSource>());
            services.AddScoped<IAfAttributeReader>(sp => sp.GetRequiredService<PiWebApiDataSource>());
        }

        var cs = configuration.GetConnectionString("TagCatalog");
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(cs));
        services.AddScoped<ITagCatalogRepository, TagCatalogRepository>();
        services.AddScoped<IChatAuditStore, EfChatAuditStore>();

        // Default chat client for Tag Assistant tools = Ollama
        services.AddSingleton<IChatClient>(sp => CreateOllamaChatClient(sp.GetRequiredService<IOptions<OllamaOptions>>().Value));
        services.AddScoped<ITagAssistant, TagAssistant>();

        services.AddKeyedSingleton<IRawChatService>("ollama", (sp, _) =>
        {
            var opt = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
            return new RawChatService(
                "ollama",
                opt.Enabled,
                opt.Model,
                CreateOllamaChatClient(opt),
                sp.GetRequiredService<ILoggerFactory>().CreateLogger("OllamaRawChat"));
        });

        services.AddKeyedSingleton<IRawChatService>("qwen-online", (sp, _) =>
        {
            var opt = sp.GetRequiredService<IOptions<QwenOnlineOptions>>().Value;
            var client = new OpenAIClient(
                new ApiKeyCredential(string.IsNullOrWhiteSpace(opt.ApiKey) ? "missing" : opt.ApiKey),
                new OpenAIClientOptions
                {
                    Endpoint = new Uri("https://dashscope.aliyuncs.com/compatible-mode/v1/")
                });
            return new RawChatService(
                "qwen-online",
                opt.Enabled,
                opt.Model,
                client.GetChatClient(opt.Model).AsIChatClient(),
                sp.GetRequiredService<ILoggerFactory>().CreateLogger("QwenOnlineRawChat"));
        });

        services.AddApplication();
        return services;
    }

    public static async Task InitializeInfrastructureAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        await TagCatalogSeeder.SeedAsync(db, cancellationToken);
    }

    private static IChatClient CreateOllamaChatClient(OllamaOptions ollama)
    {
        var endpoint = ollama.BaseUrl.TrimEnd('/');
        if (!endpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            endpoint += "/v1";
        }

        var client = new OpenAIClient(
            new ApiKeyCredential("ollama"),
            new OpenAIClientOptions { Endpoint = new Uri(endpoint + "/") });

        return client.GetChatClient(ollama.Model).AsIChatClient();
    }

    internal static void ConfigureHttpClient(HttpClient client, PiConnectionOptions options)
    {
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));

        if (string.Equals(options.AuthMode, "Basic", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(options.Username))
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Username}:{options.Password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        }

        if (string.Equals(options.AuthMode, "Bearer", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(options.BearerToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.BearerToken);
        }
    }

    internal static HttpMessageHandler CreateHandler(PiConnectionOptions options)
    {
        var handler = new HttpClientHandler();

        if (string.Equals(options.AuthMode, "Windows", StringComparison.OrdinalIgnoreCase)
            || string.Equals(options.AuthMode, "DefaultCredentials", StringComparison.OrdinalIgnoreCase)
            || string.Equals(options.AuthMode, "Kerberos", StringComparison.OrdinalIgnoreCase))
        {
            handler.UseDefaultCredentials = true;
        }

        if (string.Equals(options.AuthMode, "NetworkCredential", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(options.Username))
        {
            handler.Credentials = new NetworkCredential(options.Username, options.Password, options.Domain);
        }

        if (options.AcceptInvalidCertificates)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        return handler;
    }
}

public sealed class EfChatAuditStore(AppDbContext db) : IChatAuditStore
{
    public async Task AppendAsync(
        string conversationId,
        string correlationId,
        string userMessage,
        string? assistantAnswer,
        string? resolvedTag,
        string toolsUsedJson,
        int durationMs,
        CancellationToken cancellationToken = default)
    {
        db.ChatAudits.Add(new ChatAuditEntity
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            CorrelationId = correlationId,
            UserMessage = userMessage,
            AssistantAnswer = assistantAnswer,
            ResolvedTag = resolvedTag,
            ToolsUsedJson = toolsUsedJson,
            DurationMs = durationMs,
            CreatedUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
