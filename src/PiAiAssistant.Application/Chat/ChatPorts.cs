using PiAiAssistant.Domain.Entities;
using PiAiAssistant.Domain.Enums;

namespace PiAiAssistant.Application.Chat;

public sealed record ChatAskRequest(
    string Message,
    string? ConversationId = null,
    string? Model = null,
    AssistantPersona? Persona = null,
    string? Language = null,
    string? ScreenLevel = null);

public sealed record ChatAskResponse(
    string ConversationId,
    string Answer,
    IReadOnlyList<ToolTraceItem> ToolTrace,
    IReadOnlyList<ChatSource> Sources,
    VisualizationPayload? Visualization = null,
    string? Model = null,
    AssistantPersona? Persona = null,
    string? RecommendedAction = null,
    string? VisionJumpPath = null,
    IReadOnlyList<KpiSnapshot>? Kpis = null,
    bool UsedDeterministicFallback = false);

public sealed record ToolTraceItem(string Tool, string Status, long DurationMs);

public sealed record ChatSource(string Type, string? CanonicalTagName, string? WebId, string? BusinessObject = null);

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

/// <summary>Legacy tag-first assistant (kept for deep tag drills).</summary>
public interface ITagAssistant
{
    Task<ChatAskResponse> AskAsync(ChatAskRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Role-driven decision assistant: persona → tools over business objects (need-first).</summary>
public interface IDecisionAssistant
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
