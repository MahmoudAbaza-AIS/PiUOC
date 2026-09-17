namespace PiAiAssistant.Application.Chat;

public sealed record ChatAskRequest(string Message, string? ConversationId = null);

public sealed record ChatAskResponse(
    string ConversationId,
    string Answer,
    IReadOnlyList<ToolTraceItem> ToolTrace,
    IReadOnlyList<ChatSource> Sources,
    VisualizationPayload? Visualization = null);

public sealed record ToolTraceItem(string Tool, string Status, long DurationMs);

public sealed record ChatSource(string Type, string? CanonicalTagName, string? WebId);

/// <summary>Optional chart payload returned with chat so the UI can render trends.</summary>
public sealed record VisualizationPayload(
    string ChartType,
    string? Title,
    IReadOnlyList<ChartSeriesPayload> Series);

public sealed record ChartSeriesPayload(
    string Name,
    string? Unit,
    IReadOnlyList<ChartPointPayload> Points);

public sealed record ChartPointPayload(DateTimeOffset TimestampUtc, double? Value);

/// <summary>PI assistant (tools → Application services → structured answer + optional chart).</summary>
public interface ITagAssistant
{
    Task<ChatAskResponse> AskAsync(ChatAskRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Raw LLM smoke-test port (no PI tools). Implemented per provider in Infrastructure.</summary>
public interface IRawChatService
{
    string ProviderName { get; }
    bool IsEnabled { get; }
    Task<ChatAskResponse> AskAsync(ChatAskRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Optional audit sink for assistant conversations.</summary>
public interface IChatAuditStore
{
    Task AppendAsync(
        string conversationId,
        string correlationId,
        string userMessage,
        string? assistantAnswer,
        string? resolvedTag,
        string toolsUsedJson,
        int durationMs,
        CancellationToken cancellationToken = default);
}
