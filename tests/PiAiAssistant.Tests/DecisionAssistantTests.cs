using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PiAiAssistant.Application.Chat;
using PiAiAssistant.Application.Fleet;
using PiAiAssistant.Domain.Enums;
using PiAiAssistant.Domain.Interfaces;
using PiAiAssistant.Infrastructure.Afag;
using PiAiAssistant.Infrastructure.AI;
using PiAiAssistant.Infrastructure.Knowledge;
using PiAiAssistant.Infrastructure.Options;

namespace PiAiAssistant.Tests;

public class DecisionAssistantTests
{
    private sealed class ThrowingChatClient : IChatClient
    {
        public void Dispose() { }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("LLM should not be called when Ollama is disabled.");

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("LLM should not be called when Ollama is disabled.");

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
    }

    private sealed class MemoryAuditStore : IChatAuditStore
    {
        public List<(string ConversationId, string UserMessage, string? Answer, string? Resolved)> Rows { get; } = [];

        public Task AppendAsync(
            string conversationId,
            string correlationId,
            string userMessage,
            string? assistantAnswer,
            string? resolvedTag,
            string toolsUsedJson,
            int durationMs,
            CancellationToken cancellationToken = default)
        {
            Rows.Add((conversationId, userMessage, assistantAnswer, resolvedTag));
            return Task.CompletedTask;
        }
    }

    private static DecisionAssistant Create(out MemoryAuditStore audit, bool ollamaEnabled = false)
    {
        var hierarchy = new AfagDemoHierarchyReader();
        var sops = new InMemorySopStore();
        var semantic = new AfagSemanticService(hierarchy, sops);
        var briefings = new BriefingService(semantic);
        audit = new MemoryAuditStore();
        var ollama = Options.Create(new OllamaOptions { Enabled = ollamaEnabled, DefaultModel = "qwen3:8b" });

        return new DecisionAssistant(
            new ThrowingChatClient(),
            semantic,
            briefings,
            sops,
            audit,
            ollama,
            NullLogger<DecisionAssistant>.Instance);
    }

    [Fact]
    public async Task Empty_message_throws()
    {
        var assistant = Create(out _);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            assistant.AskAsync(new ChatAskRequest("  ")));
    }

    [Fact]
    public async Task Executive_deterministic_answer_has_action_and_kpis()
    {
        var assistant = Create(out var audit);
        var response = await assistant.AskAsync(new ChatAskRequest(
            "How are we running today vs. yesterday?",
            Persona: AssistantPersona.Executive,
            Language: "en"));

        Assert.True(response.UsedDeterministicFallback);
        Assert.Equal(AssistantPersona.Executive, response.Persona);
        Assert.Contains("loading", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(response.RecommendedAction));
        Assert.NotNull(response.Kpis);
        Assert.True(response.Kpis!.Count >= 3);
        Assert.Contains(response.Sources, s => s.Type == "AFAG");
        Assert.DoesNotContain(".MEAS", response.Answer);
        Assert.Single(audit.Rows);
    }

    [Fact]
    public async Task Sector_ops_ranks_without_tag_names()
    {
        var assistant = Create(out _);
        var response = await assistant.AskAsync(new ChatAskRequest(
            "Which plants in COA are below target loading?",
            Persona: AssistantPersona.SectorOps));

        Assert.Equal(AssistantPersona.SectorOps, response.Persona);
        Assert.Contains("PP09", response.Answer);
        Assert.DoesNotContain("tag", response.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Plant_manager_includes_visualization_and_sop()
    {
        var assistant = Create(out _);
        var response = await assistant.AskAsync(new ChatAskRequest(
            "Why is PP09 heat rate worse than last week?",
            Persona: AssistantPersona.PlantManager));

        Assert.Equal(AssistantPersona.PlantManager, response.Persona);
        Assert.NotNull(response.Visualization);
        Assert.True(response.Visualization!.Series.Count >= 1);
        Assert.Contains("Next step:", response.Answer);
        Assert.Contains("PI Vision://", response.VisionJumpPath);
    }

    [Fact]
    public async Task Operator_flame_question_cites_sop()
    {
        var assistant = Create(out _);
        var response = await assistant.AskAsync(new ChatAskRequest(
            "Flame intensity says bad on GT01 — what do I do?",
            Persona: AssistantPersona.ShiftOperator));

        Assert.Equal(AssistantPersona.ShiftOperator, response.Persona);
        Assert.Contains("SOP", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(response.Sources, s => s.Type is "SOP" or "AFAG");
    }

    [Fact]
    public async Task Reliability_persona_mentions_baseline()
    {
        var assistant = Create(out _);
        var response = await assistant.AskAsync(new ChatAskRequest(
            "Show me units trending outside their baseline",
            Persona: AssistantPersona.ReliabilityEngineer));

        Assert.Equal(AssistantPersona.ReliabilityEngineer, response.Persona);
        Assert.Contains("PP09", response.Answer);
    }

    [Fact]
    public async Task Infers_operator_persona_from_flame_keyword()
    {
        var assistant = Create(out _);
        var response = await assistant.AskAsync(new ChatAskRequest(
            "flame intensity bad — help"));

        Assert.Equal(AssistantPersona.ShiftOperator, response.Persona);
    }

    [Fact]
    public async Task Infers_sector_persona_from_screen_level()
    {
        var assistant = Create(out _);
        var response = await assistant.AskAsync(new ChatAskRequest(
            "How are plants doing?",
            ScreenLevel: "sector"));

        Assert.Equal(AssistantPersona.SectorOps, response.Persona);
    }

    [Fact]
    public async Task Infers_arabic_language_from_message_script()
    {
        var assistant = Create(out _);
        var response = await assistant.AskAsync(new ChatAskRequest(
            "أي محطات في COA أقل من هدف التحميل؟",
            Persona: AssistantPersona.SectorOps));

        Assert.Contains(response.Answer, c => c is >= '\u0600' and <= '\u06FF');
    }

    [Fact]
    public async Task Arabic_language_flag_returns_arabic_narrative()
    {
        var assistant = Create(out _);
        var response = await assistant.AskAsync(new ChatAskRequest(
            "How is the fleet?",
            Persona: AssistantPersona.Executive,
            Language: "ar"));

        Assert.Contains(response.Answer, c => c is >= '\u0600' and <= '\u06FF');
    }

    [Fact]
    public async Task Reuses_conversation_id_when_provided()
    {
        var assistant = Create(out var audit);
        var response = await assistant.AskAsync(new ChatAskRequest(
            "How are we today?",
            ConversationId: "fixed-conversation-id",
            Persona: AssistantPersona.Executive));

        Assert.Equal("fixed-conversation-id", response.ConversationId);
        Assert.Equal("fixed-conversation-id", audit.Rows[0].ConversationId);
    }

    [Fact]
    public async Task Heat_rate_keyword_overrides_to_plant_narrative()
    {
        var assistant = Create(out _);
        var response = await assistant.AskAsync(new ChatAskRequest(
            "Tell me about pp09 heat rate issues",
            Persona: AssistantPersona.Executive));

        Assert.Contains("heat rate", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("A1", response.Answer);
    }
}
