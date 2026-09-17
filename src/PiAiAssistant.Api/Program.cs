using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PiAiAssistant.Application.Chat;
using PiAiAssistant.Application.Tags;
using PiAiAssistant.Domain.Enums;
using PiAiAssistant.Domain.Interfaces;
using PiAiAssistant.Infrastructure;
using PiAiAssistant.Infrastructure.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();
await app.Services.InitializeInfrastructureAsync();

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/", () => Results.Redirect("/app/index.html"));

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "PiAiAssistant.Api",
    architecture = "Clean Architecture",
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/health/pi", async (IPiConnectivity pi, IOptions<PiConnectionOptions> options, CancellationToken ct) =>
{
    var connected = await pi.TryConnectAsync(ct);
    var status = connected ? await pi.GetSystemStatusAsync(ct) : "Unable to connect.";
    return Results.Ok(new
    {
        connected,
        demoMode = options.Value.UseDemoMode,
        baseUrl = options.Value.BaseUrl,
        dataArchive = options.Value.DataArchiveName,
        status
    });
});

app.MapGet("/health/ollama", async (IOptions<OllamaOptions> options, CancellationToken ct) =>
{
    var ollama = options.Value;
    try
    {
        using var http = new HttpClient { BaseAddress = new Uri(ollama.BaseUrl), Timeout = TimeSpan.FromSeconds(5) };
        using var response = await http.GetAsync("api/tags", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return Results.Ok(new
        {
            reachable = response.IsSuccessStatusCode,
            model = ollama.DefaultModel,
            models = ollama.ListModels(),
            enabled = ollama.Enabled,
            statusCode = (int)response.StatusCode,
            preview = body.Length > 300 ? body[..300] + "..." : body
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            reachable = false,
            model = ollama.DefaultModel,
            models = ollama.ListModels(),
            enabled = ollama.Enabled,
            error = ex.Message
        });
    }
});

var tags = app.MapGroup("/api/tags").WithTags("Tags");

tags.MapGet("/search", async (string q, int? maxResults, ITagIntelligenceService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(q))
    {
        return Results.BadRequest(new { message = "Query parameter 'q' is required." });
    }

    return Results.Ok(await svc.SearchTagsAsync(q, maxResults ?? 10, ct));
});

tags.MapGet("/history", async (
    string reference,
    DateTimeOffset? startUtc,
    DateTimeOffset? endUtc,
    int? maxCount,
    ITagIntelligenceService svc,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(reference))
    {
        return Results.BadRequest(new { message = "Query parameter 'reference' is required." });
    }

    var result = await svc.GetTagHistoryAsync(reference, startUtc, endUtc, maxCount ?? 100, ct);
    return MapTagResult(result.Status, result);
});

tags.MapGet("/chart", async (
    string reference,
    DateTimeOffset? startUtc,
    DateTimeOffset? endUtc,
    int? maxCount,
    ITagIntelligenceService svc,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(reference))
    {
        return Results.BadRequest(new { message = "Query parameter 'reference' is required." });
    }

    var result = await svc.GetChartSeriesAsync(reference, startUtc, endUtc, maxCount ?? 200, ct);
    return MapTagResult(result.Status, result);
});

tags.MapGet("/summary", async (
    string reference,
    DateTimeOffset? startUtc,
    DateTimeOffset? endUtc,
    ITagIntelligenceService svc,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(reference))
    {
        return Results.BadRequest(new { message = "Query parameter 'reference' is required." });
    }

    var result = await svc.GetSummaryAsync(reference, startUtc, endUtc, 500, ct);
    return MapTagResult(result.Status, result);
});

tags.MapGet("/compare", async (
    string references,
    DateTimeOffset? startUtc,
    DateTimeOffset? endUtc,
    ITagIntelligenceService svc,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(references))
    {
        return Results.BadRequest(new { message = "Query parameter 'references' is required (comma-separated)." });
    }

    var list = references.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var result = await svc.CompareTagsAsync(list, startUtc, endUtc, 150, ct);
    return MapTagResult(result.Status, result);
});

tags.MapGet("/details", async (
    string reference,
    bool? includeCurrentValue,
    bool? includeRecentValues,
    bool? includeRelatedTags,
    bool? includeBusinessMetadata,
    int? recentValueLimit,
    ITagIntelligenceService svc,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(reference))
    {
        return Results.BadRequest(new { message = "Query parameter 'reference' is required." });
    }

    var result = await svc.GetTagDetailsAsync(
        reference,
        new TagDetailsQueryOptions(
            IncludeCurrentValue: includeCurrentValue ?? true,
            IncludeRecentValues: includeRecentValues ?? true,
            IncludeRelatedTags: includeRelatedTags ?? true,
            IncludeBusinessMetadata: includeBusinessMetadata ?? true,
            RecentValueLimit: recentValueLimit ?? 10),
        ct);

    return MapTagResult(result.Status, result);
});

tags.MapGet("/{tagReference}", async (
    string tagReference,
    bool? includeCurrentValue,
    bool? includeRecentValues,
    bool? includeRelatedTags,
    bool? includeBusinessMetadata,
    int? recentValueLimit,
    ITagIntelligenceService svc,
    CancellationToken ct) =>
{
    var result = await svc.GetTagDetailsAsync(
        Uri.UnescapeDataString(tagReference),
        new TagDetailsQueryOptions(
            IncludeCurrentValue: includeCurrentValue ?? true,
            IncludeRecentValues: includeRecentValues ?? true,
            IncludeRelatedTags: includeRelatedTags ?? true,
            IncludeBusinessMetadata: includeBusinessMetadata ?? true,
            RecentValueLimit: recentValueLimit ?? 10),
        ct);

    return MapTagResult(result.Status, result);
});

var chat = app.MapGroup("/api/chat").WithTags("Chat");

chat.MapGet("/models", (IOptions<OllamaOptions> options) =>
{
    var ollama = options.Value;
    return Results.Ok(new
    {
        enabled = ollama.Enabled,
        defaultModel = ollama.DefaultModel,
        models = ollama.ListModels()
    });
}).WithName("ListLocalChatModels");

chat.MapPost("/", async (ChatAskRequest request, ITagAssistant assistant, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { message = "message is required" });
    }

    return Results.Ok(await assistant.AskAsync(request, ct));
}).WithName("AskTagAssistant");

chat.MapPost("/ollama", async (
    ChatAskRequest request,
    [FromKeyedServices("ollama")] IRawChatService chatService,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { message = "message is required" });
    }

    if (!chatService.IsEnabled)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        return Results.Ok(await chatService.AskAsync(request, ct));
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}).WithName("ChatOllama");

chat.MapPost("/deepseek", async (
    ChatAskRequest request,
    [FromKeyedServices("deepseek")] IRawChatService chatService,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { message = "message is required" });
    }

    if (!chatService.IsEnabled)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        return Results.Ok(await chatService.AskAsync(request, ct));
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}).WithName("ChatDeepSeek");

chat.MapPost("/qwen-online", async (
    ChatAskRequest request,
    [FromKeyedServices("qwen-online")] IRawChatService chatService,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { message = "message is required" });
    }

    if (!chatService.IsEnabled)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        return Results.Ok(await chatService.AskAsync(request, ct));
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}).WithName("ChatQwenOnline");

app.Run();

static IResult MapTagResult(TagResolutionStatus status, object payload) => status switch
{
    TagResolutionStatus.Found => Results.Ok(payload),
    TagResolutionStatus.Ambiguous => Results.Json(payload, statusCode: StatusCodes.Status409Conflict),
    TagResolutionStatus.NotFound => Results.NotFound(payload),
    TagResolutionStatus.Forbidden => Results.Json(payload, statusCode: StatusCodes.Status403Forbidden),
    _ => Results.BadRequest(payload)
};

public partial class Program;
