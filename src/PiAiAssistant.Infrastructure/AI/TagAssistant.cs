using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PiAiAssistant.Application.Chat;
using PiAiAssistant.Application.Tags;
using PiAiAssistant.Domain.Enums;
using PiAiAssistant.Infrastructure.Options;

namespace PiAiAssistant.Infrastructure.AI;

/// <summary>
/// Broad PI Q&amp;A assistant: natural language → read-only tools → Application services.
/// Never invents PI facts; optional chart payload for the UI.
/// </summary>
public sealed class TagAssistant(
    IChatClient chatClient,
    ITagIntelligenceService tags,
    IChatAuditStore auditStore,
    IOptions<OllamaOptions> ollamaOptions,
    ILogger<TagAssistant> logger) : ITagAssistant
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string Instructions =
        """
        You are an industrial AVEVA PI System assistant for operations and engineering users.

        Scope — answer ANY natural-language question about PI tags / AF attributes that can be satisfied with the available tools:
        - find / search tags and aliases
        - tag specifications and metadata
        - current values and quality
        - history / trends over a time window
        - summaries (min, max, average)
        - related tags
        - comparing multiple tags (for charts)

        Rules:
        - ALWAYS call tools for PI facts. Never invent tags, values, units, timestamps, ranges, or ownership.
        - If a tool returns ambiguous candidates, ask the user to pick one — do not guess.
        - If data is missing or quality is bad, say so clearly.
        - Time defaults: when the user does not specify a window, use the last 1 hour.
        - Relative phrases: "last hour", "last 6 hours", "today" → convert to UTC start/end when calling tools.
        - For trend / chart / plot / visualize requests, call get_chart_series or compare_tags.
        - For "how is X doing" / "stats" / "min max avg", call get_tag_summary.
        - Do not write to PI, acknowledge alarms, or run unrestricted queries.
        - Keep answers concise and operational. Mention units and timestamps (UTC).
        """;

    public async Task<ChatAskResponse> AskAsync(ChatAskRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("Message is required.", nameof(request));
        }

        if (!ollamaOptions.Value.Enabled)
        {
            return new ChatAskResponse(
                request.ConversationId ?? Guid.NewGuid().ToString("N"),
                "Chat assistant is disabled. Use the Tag APIs or open /app for visualization without AI.",
                [],
                []);
        }

        var conversationId = string.IsNullOrWhiteSpace(request.ConversationId)
            ? Guid.NewGuid().ToString("N")
            : request.ConversationId!;
        var correlationId = Guid.NewGuid().ToString("N");
        var toolTrace = new List<ToolTraceItem>();
        var sources = new List<ChatSource>();
        VisualizationPayload? visualization = null;
        var sw = Stopwatch.StartNew();

        var toolChat = chatClient.AsBuilder().UseFunctionInvocation().Build();
        AIFunction[] tools =
        [
            AIFunctionFactory.Create(SearchTagsTool),
            AIFunctionFactory.Create(GetTagDetailsTool),
            AIFunctionFactory.Create(GetTagHistoryTool),
            AIFunctionFactory.Create(GetTagSummaryTool),
            AIFunctionFactory.Create(GetRelatedTagsTool),
            AIFunctionFactory.Create(GetChartSeriesTool),
            AIFunctionFactory.Create(CompareTagsTool)
        ];

        string answer;
        try
        {
            var response = await toolChat.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, Instructions),
                    new ChatMessage(ChatRole.User, request.Message)
                ],
                new ChatOptions
                {
                    ModelId = ollamaOptions.Value.Model,
                    Tools = tools,
                    Temperature = 0.1f
                },
                cancellationToken);

            answer = response.Text?.Trim() ?? "(No response text returned by the model.)";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tag assistant failed. CorrelationId={CorrelationId}", correlationId);
            answer =
                "The assistant could not complete this request. Verify Ollama is running and tag APIs work. " +
                $"Details: {ex.Message}";
        }

        sw.Stop();
        await auditStore.AppendAsync(
            conversationId,
            correlationId,
            request.Message,
            answer,
            sources.FirstOrDefault()?.CanonicalTagName,
            JsonSerializer.Serialize(toolTrace, JsonOptions),
            (int)sw.ElapsedMilliseconds,
            cancellationToken);

        return new ChatAskResponse(conversationId, answer, toolTrace, sources, visualization);

        [Description("Search PI/catalog tags by free text when the tag name is unclear.")]
        async Task<string> SearchTagsTool(string query, int maxResults = 10)
        {
            return await RunToolAsync("search_tags", async () =>
            {
                var results = await tags.SearchTagsAsync(query, Math.Clamp(maxResults, 1, 20), cancellationToken);
                return new
                {
                    status = results.Count == 0 ? "not_found" : results.Count == 1 ? "found" : "ambiguous",
                    candidates = results
                };
            });
        }

        [Description("Get authoritative tag metadata, current value, units, AF context, related tags, and business notes.")]
        async Task<string> GetTagDetailsTool(string tagReference, bool includeCurrentValue = true, int recentValueLimit = 10)
        {
            return await RunToolAsync("get_tag_details", async () =>
            {
                var result = await tags.GetTagDetailsAsync(
                    tagReference,
                    new TagDetailsQueryOptions(
                        IncludeCurrentValue: includeCurrentValue,
                        IncludeRecentValues: true,
                        IncludeRelatedTags: true,
                        IncludeBusinessMetadata: true,
                        RecentValueLimit: Math.Clamp(recentValueLimit, 1, 50)),
                    cancellationToken);

                TrackSource(result.Status, result.Details?.CanonicalName, result.Details?.WebId);
                return new
                {
                    status = Status(result.Status),
                    message = result.Message,
                    candidates = result.Candidates,
                    tag = result.Details
                };
            });
        }

        [Description("Get recorded history for a tag. Use ISO-8601 UTC for startUtc/endUtc when possible.")]
        async Task<string> GetTagHistoryTool(string tagReference, string? startUtc = null, string? endUtc = null, int maxCount = 100)
        {
            return await RunToolAsync("get_tag_history", async () =>
            {
                var (start, end) = ParseRange(startUtc, endUtc);
                var result = await tags.GetTagHistoryAsync(tagReference, start, end, Math.Clamp(maxCount, 1, 500), cancellationToken);
                TrackSource(result.Status, result.CanonicalName, null);
                if (result.Status == TagResolutionStatus.Found)
                {
                    visualization = ToVisualization(result.CanonicalName, null, result.Values);
                }

                return new
                {
                    status = Status(result.Status),
                    message = result.Message,
                    canonicalName = result.CanonicalName,
                    candidates = result.Candidates,
                    warnings = result.Warnings,
                    values = result.Values.Take(50)
                };
            });
        }

        [Description("Get summary statistics (min/max/average/count) for a tag over a time window.")]
        async Task<string> GetTagSummaryTool(string tagReference, string? startUtc = null, string? endUtc = null)
        {
            return await RunToolAsync("get_tag_summary", async () =>
            {
                var (start, end) = ParseRange(startUtc, endUtc);
                var result = await tags.GetSummaryAsync(tagReference, start, end, 500, cancellationToken);
                TrackSource(result.Status, result.CanonicalName, null);
                return new
                {
                    status = Status(result.Status),
                    message = result.Message,
                    canonicalName = result.CanonicalName,
                    candidates = result.Candidates,
                    stats = result.Stats,
                    warnings = result.Warnings
                };
            });
        }

        [Description("List related tags for a resolved tag (upstream/downstream/associated signals).")]
        async Task<string> GetRelatedTagsTool(string tagReference)
        {
            return await RunToolAsync("get_related_tags", async () =>
            {
                var related = await tags.GetRelatedTagsAsync(tagReference, cancellationToken);
                return new { status = related.Count == 0 ? "not_found" : "found", related };
            });
        }

        [Description("Get chart-ready time series for one tag. Prefer this for trend/plot/visualize requests.")]
        async Task<string> GetChartSeriesTool(string tagReference, string? startUtc = null, string? endUtc = null, int maxCount = 200)
        {
            return await RunToolAsync("get_chart_series", async () =>
            {
                var (start, end) = ParseRange(startUtc, endUtc);
                var result = await tags.GetChartSeriesAsync(tagReference, start, end, Math.Clamp(maxCount, 10, 500), cancellationToken);
                TrackSource(result.Status, result.CanonicalName, null);
                if (result.Status == TagResolutionStatus.Found && result.CanonicalName is not null)
                {
                    visualization = new VisualizationPayload(
                        "line",
                        result.CanonicalName,
                        [new ChartSeriesPayload(result.CanonicalName, result.Unit,
                            result.Points.Select(p => new ChartPointPayload(p.TimestampUtc, p.Value)).ToList())]);
                }

                return new
                {
                    status = Status(result.Status),
                    message = result.Message,
                    canonicalName = result.CanonicalName,
                    unit = result.Unit,
                    pointCount = result.Points.Count,
                    candidates = result.Candidates,
                    warnings = result.Warnings,
                    samplePoints = result.Points.Take(20)
                };
            });
        }

        [Description("Compare up to 5 tags on one chart. Pass tagReferences as a comma-separated list.")]
        async Task<string> CompareTagsTool(string tagReferences, string? startUtc = null, string? endUtc = null)
        {
            return await RunToolAsync("compare_tags", async () =>
            {
                var refs = tagReferences.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var (start, end) = ParseRange(startUtc, endUtc);
                var result = await tags.CompareTagsAsync(refs, start, end, 150, cancellationToken);
                if (result.Status == TagResolutionStatus.Found && result.Series.Count > 0)
                {
                    visualization = new VisualizationPayload(
                        "line",
                        "Tag comparison",
                        result.Series.Select(s => new ChartSeriesPayload(
                            s.Name,
                            s.Unit,
                            s.Points.Select(p => new ChartPointPayload(p.TimestampUtc, p.Value)).ToList())).ToList());
                    foreach (var s in result.Series)
                    {
                        sources.Add(new ChatSource("PI", s.Name, null));
                    }
                }

                return new
                {
                    status = Status(result.Status),
                    message = result.Message,
                    series = result.Series.Select(s => new { s.Name, s.Unit, pointCount = s.Points.Count }),
                    ambiguousCandidates = result.AmbiguousCandidates,
                    warnings = result.Warnings
                };
            });
        }

        async Task<string> RunToolAsync(string name, Func<Task<object>> action)
        {
            var localSw = Stopwatch.StartNew();
            try
            {
                var payload = await action();
                localSw.Stop();
                toolTrace.Add(new ToolTraceItem(name, "success", localSw.ElapsedMilliseconds));
                return JsonSerializer.Serialize(payload, JsonOptions);
            }
            catch (Exception ex)
            {
                localSw.Stop();
                toolTrace.Add(new ToolTraceItem(name, "error", localSw.ElapsedMilliseconds));
                return JsonSerializer.Serialize(new { status = "error", message = ex.Message }, JsonOptions);
            }
        }

        void TrackSource(TagResolutionStatus status, string? name, string? webId)
        {
            if (status == TagResolutionStatus.Found && !string.IsNullOrWhiteSpace(name))
            {
                sources.Add(new ChatSource("PI", name, webId));
            }
        }
    }

    private static string Status(TagResolutionStatus status) => status.ToString().ToLowerInvariant();

    private static (DateTimeOffset? Start, DateTimeOffset? End) ParseRange(string? startUtc, string? endUtc)
    {
        DateTimeOffset? start = null;
        DateTimeOffset? end = null;
        if (!string.IsNullOrWhiteSpace(startUtc) &&
            DateTimeOffset.TryParse(startUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var s))
        {
            start = s;
        }

        if (!string.IsNullOrWhiteSpace(endUtc) &&
            DateTimeOffset.TryParse(endUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var e))
        {
            end = e;
        }

        return (start, end);
    }

    private static VisualizationPayload ToVisualization(string? name, string? unit, IReadOnlyList<TagValueDto> values)
    {
        var points = values
            .Select(v => new ChartPointPayload(v.TimestampUtc, v.Value switch
            {
                double d => d,
                float f => f,
                int i => i,
                long l => l,
                _ => null
            }))
            .Where(p => p.Value.HasValue)
            .ToList();

        return new VisualizationPayload(
            "line",
            name,
            [new ChartSeriesPayload(name ?? "series", unit, points)]);
    }
}

public sealed class RawChatService(
    string providerName,
    bool enabled,
    string modelId,
    IChatClient chatClient,
    ILogger logger) : IRawChatService
{
    public string ProviderName { get; } = providerName;
    public bool IsEnabled { get; } = enabled;

    public async Task<ChatAskResponse> AskAsync(ChatAskRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException($"{ProviderName} chat is disabled.");
        }

        _ = logger;
        var response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, request.Message)],
            new ChatOptions { ModelId = modelId },
            cancellationToken);

        return new ChatAskResponse(
            request.ConversationId ?? Guid.NewGuid().ToString("N"),
            response.Text?.Trim() ?? "No response generated",
            [],
            []);
    }
}
