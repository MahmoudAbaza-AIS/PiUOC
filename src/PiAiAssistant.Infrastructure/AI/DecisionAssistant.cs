using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PiAiAssistant.Application.Chat;
using PiAiAssistant.Application.Fleet;
using PiAiAssistant.Domain.Entities;
using PiAiAssistant.Domain.Enums;
using PiAiAssistant.Domain.Interfaces;
using PiAiAssistant.Infrastructure.Options;

namespace PiAiAssistant.Infrastructure.AI;

/// <summary>
/// Need-first decision assistant: persona router → semantic/SOP tools → narrative + action.
/// Never requires the user to know a PI tag name.
/// </summary>
public sealed class DecisionAssistant(
    IChatClient chatClient,
    IAfagSemanticService semantic,
    IBriefingService briefings,
    ISopKnowledgeStore sops,
    IChatAuditStore auditStore,
    IOptions<OllamaOptions> ollamaOptions,
    ILogger<DecisionAssistant> logger) : IDecisionAssistant
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly Regex ThinkBlockRegex = new(
        @"<think\b[^>]*>[\s\S]*?</think>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ThinkOpenRegex = new(
        @"<think\b[^>]*>[\s\S]*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<ChatAskResponse> AskAsync(ChatAskRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("Message is required.", nameof(request));
        }

        var persona = request.Persona ?? InferPersona(request.Message, request.ScreenLevel);
        var language = string.IsNullOrWhiteSpace(request.Language)
            ? InferLanguage(request.Message)
            : request.Language!;
        var conversationId = string.IsNullOrWhiteSpace(request.ConversationId)
            ? Guid.NewGuid().ToString("N")
            : request.ConversationId!;
        var correlationId = Guid.NewGuid().ToString("N");
        var modelId = ollamaOptions.Value.ResolveModelId(request.Model);
        var toolTrace = new List<ToolTraceItem>();
        var sources = new List<ChatSource>();
        var sw = Stopwatch.StartNew();

        // Deterministic path first — guarantees demo works without LLM and never invents tags.
        var deterministic = await BuildDeterministicAnswerAsync(persona, request.Message, language, cancellationToken);
        if (!ollamaOptions.Value.Enabled)
        {
            await auditStore.AppendAsync(
                conversationId, correlationId, request.Message, deterministic.Answer,
                persona.ToString(), "[]", (int)sw.ElapsedMilliseconds, cancellationToken);
            return deterministic with { ConversationId = conversationId, Persona = persona, UsedDeterministicFallback = true };
        }

        try
        {
            var toolChat = chatClient.AsBuilder().UseFunctionInvocation().Build();
            AIFunction[] tools =
            [
                AIFunctionFactory.Create(GetKingdomOverviewTool),
                AIFunctionFactory.Create(GetSectorOverviewTool),
                AIFunctionFactory.Create(RankPlantsBelowTargetTool),
                AIFunctionFactory.Create(GetPlantOverviewTool),
                AIFunctionFactory.Create(GetUnitOverviewTool),
                AIFunctionFactory.Create(SearchSopTool),
                AIFunctionFactory.Create(GetProactiveBriefTool)
            ];

            var response = await toolChat.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, BuildInstructions(persona, language)),
                    new ChatMessage(ChatRole.User, request.Message)
                ],
                new ChatOptions
                {
                    ModelId = modelId,
                    Tools = tools,
                    Temperature = 0.1f
                },
                cancellationToken);

            // qwen3 / deepseek-r1 often return <think>…</think> with an empty visible answer.
            var answer = VisibleAssistantText(response);
            if (string.IsNullOrWhiteSpace(answer))
            {
                logger.LogInformation("LLM returned no visible answer; using deterministic semantic narrative.");
                answer = deterministic.Answer;
            }

            var result = new ChatAskResponse(
                conversationId,
                answer,
                toolTrace,
                sources.Count > 0 ? sources : deterministic.Sources,
                deterministic.Visualization,
                modelId,
                persona,
                deterministic.RecommendedAction,
                deterministic.VisionJumpPath,
                deterministic.Kpis);

            await auditStore.AppendAsync(
                conversationId, correlationId, request.Message, result.Answer,
                persona.ToString(), JsonSerializer.Serialize(toolTrace), (int)sw.ElapsedMilliseconds, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DecisionAssistant LLM path failed; using deterministic semantic answer.");
            await auditStore.AppendAsync(
                conversationId, correlationId, request.Message, deterministic.Answer,
                persona.ToString(), "[]", (int)sw.ElapsedMilliseconds, cancellationToken);
            return deterministic with
            {
                ConversationId = conversationId,
                Persona = persona,
                UsedDeterministicFallback = true,
                Model = modelId
            };
        }

        async Task<string> GetKingdomOverviewTool()
        {
            var swt = Stopwatch.StartNew();
            var data = await semantic.GetKingdomOverviewAsync(cancellationToken);
            toolTrace.Add(new ToolTraceItem("get_kingdom_overview", "success", swt.ElapsedMilliseconds));
            sources.Add(new ChatSource("AFAG", null, null, "Kingdom/KSA"));
            return JsonSerializer.Serialize(data, JsonOptions);
        }

        async Task<string> GetSectorOverviewTool([Description("Sector code e.g. COA, EOA, SOA, WOA")] string sectorCode)
        {
            var swt = Stopwatch.StartNew();
            var data = await semantic.GetSectorOverviewAsync(sectorCode, cancellationToken);
            toolTrace.Add(new ToolTraceItem("get_sector_overview", data is null ? "not_found" : "success", swt.ElapsedMilliseconds));
            if (data is not null) sources.Add(new ChatSource("AFAG", null, null, $"Sector/{sectorCode}"));
            return JsonSerializer.Serialize(data, JsonOptions);
        }

        async Task<string> RankPlantsBelowTargetTool([Description("Sector code")] string sectorCode)
        {
            var swt = Stopwatch.StartNew();
            var data = await semantic.RankPlantsBelowTargetAsync(sectorCode, cancellationToken);
            toolTrace.Add(new ToolTraceItem("rank_plants_below_target", "success", swt.ElapsedMilliseconds));
            sources.Add(new ChatSource("AFAG", null, null, $"Sector/{sectorCode}/plants"));
            return JsonSerializer.Serialize(data, JsonOptions);
        }

        async Task<string> GetPlantOverviewTool([Description("Plant code e.g. PP09")] string plantCode)
        {
            var swt = Stopwatch.StartNew();
            var data = await semantic.GetPlantOverviewAsync(plantCode, cancellationToken);
            toolTrace.Add(new ToolTraceItem("get_plant_overview", data is null ? "not_found" : "success", swt.ElapsedMilliseconds));
            if (data is not null) sources.Add(new ChatSource("AFAG", null, null, $"Plant/{plantCode}"));
            return JsonSerializer.Serialize(data, JsonOptions);
        }

        async Task<string> GetUnitOverviewTool(
            [Description("Plant code")] string plantCode,
            [Description("Block code e.g. A1")] string blockCode,
            [Description("Unit code e.g. GT01")] string unitCode)
        {
            var swt = Stopwatch.StartNew();
            var data = await semantic.GetUnitOverviewAsync(plantCode, blockCode, unitCode, cancellationToken);
            toolTrace.Add(new ToolTraceItem("get_unit_overview", data is null ? "not_found" : "success", swt.ElapsedMilliseconds));
            if (data is not null) sources.Add(new ChatSource("AFAG", null, null, $"Unit/{plantCode}/{blockCode}/{unitCode}"));
            return JsonSerializer.Serialize(data, JsonOptions);
        }

        async Task<string> SearchSopTool([Description("Natural language query for SOP / spec")] string query)
        {
            var swt = Stopwatch.StartNew();
            var data = await sops.SearchAsync(query, 3, cancellationToken);
            toolTrace.Add(new ToolTraceItem("search_sop", "success", swt.ElapsedMilliseconds));
            foreach (var sop in data)
            {
                sources.Add(new ChatSource("SOP", null, null, $"{sop.Id} §{sop.Section}"));
            }

            return JsonSerializer.Serialize(data, JsonOptions);
        }

        async Task<string> GetProactiveBriefTool(
            [Description("executive|sector|plant|operator|reliability")] string kind)
        {
            var swt = Stopwatch.StartNew();
            ProactiveBrief brief = kind.ToLowerInvariant() switch
            {
                "sector" => await briefings.GetSectorDeviationAlertAsync("COA", language, cancellationToken),
                "plant" => await briefings.GetPlantDigestAsync("PP09", language, cancellationToken),
                "operator" => await briefings.GetOperatorAlarmCardAsync("PP09", "A1", "GT01", language, cancellationToken),
                "reliability" => await briefings.GetReliabilityAnomalyDigestAsync(language, cancellationToken),
                _ => await briefings.GetMorningExecutiveBriefAsync(language, cancellationToken)
            };
            toolTrace.Add(new ToolTraceItem("get_proactive_brief", "success", swt.ElapsedMilliseconds));
            sources.Add(new ChatSource("Briefing", null, null, brief.Title));
            return JsonSerializer.Serialize(brief, JsonOptions);
        }
    }

    private async Task<ChatAskResponse> BuildDeterministicAnswerAsync(
        AssistantPersona persona,
        string message,
        string language,
        CancellationToken cancellationToken)
    {
        var ar = language.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        VisualizationPayload? viz = null;
        string answer;
        string? action;
        string? jump;
        IReadOnlyList<KpiSnapshot>? kpis = null;
        var sources = new List<ChatSource>();

        switch (persona)
        {
            case AssistantPersona.SectorOps:
            {
                var sector = await semantic.GetSectorOverviewAsync("COA", cancellationToken);
                answer = ar ? sector!.NarrativeAr : sector!.NarrativeEn;
                action = sector.RecommendedAction;
                jump = "PI Vision://Sector/COA";
                kpis = await semantic.GetScopeKpisAsync("Sector", "COA", cancellationToken);
                sources.Add(new ChatSource("AFAG", null, null, "Sector/COA"));
                break;
            }
            case AssistantPersona.PlantManager:
            {
                var plant = await semantic.GetPlantOverviewAsync("PP09", cancellationToken);
                answer = ar ? plant!.RootCauseNarrativeAr : plant!.RootCauseNarrativeEn;
                if (!string.IsNullOrWhiteSpace(plant.SopCitation))
                {
                    answer += ar ? $" المرجع: {plant.SopCitation}." : $" Citation: {plant.SopCitation}.";
                }

                action = plant.RecommendedAction;
                jump = plant.Plant.VisionJumpPath;
                kpis = await semantic.GetScopeKpisAsync("Plant", "PP09", cancellationToken);
                sources.Add(new ChatSource("AFAG", null, null, "Plant/PP09"));
                if (plant.GenTrend.Count > 0)
                {
                    viz = new VisualizationPayload(
                        "line",
                        "PP09 Gen MWh vs Fuel",
                        [
                            new ChartSeriesPayload("Gen MWh", "MWh",
                                plant.GenTrend.Select(p => new ChartPointPayload(p.TimestampUtc, p.Value)).ToList()),
                            new ChartSeriesPayload("Fuel", "kg",
                                plant.FuelTrend.Select(p => new ChartPointPayload(p.TimestampUtc, p.Value)).ToList())
                        ]);
                }

                break;
            }
            case AssistantPersona.ShiftOperator:
            {
                var unit = await semantic.GetUnitOverviewAsync("PP09", "A1", "GT01", cancellationToken);
                answer = ar ? unit!.NarrativeAr : unit!.NarrativeEn;
                if (!string.IsNullOrWhiteSpace(unit.SopCitation))
                {
                    answer += ar ? $" SOP: {unit.SopCitation}." : $" SOP: {unit.SopCitation}.";
                }

                action = unit.RecommendedAction;
                jump = unit.Unit.VisionJumpPath;
                sources.Add(new ChatSource("AFAG", null, null, "Unit/PP09/A1/GT01"));
                if (unit.SopCitation is not null)
                {
                    sources.Add(new ChatSource("SOP", null, null, unit.SopCitation));
                }

                break;
            }
            case AssistantPersona.ReliabilityEngineer:
            {
                var plant = await semantic.GetPlantOverviewAsync("PP09", cancellationToken);
                answer = ar
                    ? $"وحدات خارج خط الأساس: PP09 (Δ معدل الحرارة {Math.Abs(plant!.Plant.HeatRateDeltaWeekPercent ?? 0):0.#}%). {plant.RootCauseNarrativeAr}"
                    : $"Units trending outside baseline: PP09 (heat-rate Δ {Math.Abs(plant!.Plant.HeatRateDeltaWeekPercent ?? 0):0.#}%). {plant.RootCauseNarrativeEn}";
                action = plant.RecommendedAction;
                jump = "PI Vision://Reliability/Fleet";
                sources.Add(new ChatSource("AFAG", null, null, "Plant/PP09"));
                break;
            }
            default:
            {
                var fleet = await semantic.GetKingdomOverviewAsync(cancellationToken);
                answer = ar ? fleet.NarrativeAr : fleet.NarrativeEn;
                action = fleet.RecommendedAction;
                jump = fleet.VisionJumpPath;
                kpis =
                [
                    new KpiSnapshot { Key = "gross_mw", DisplayName = "Gross MW", Value = fleet.Kingdom.GrossMw, Unit = "MW", Scope = "Kingdom" },
                    new KpiSnapshot { Key = "loading_factor", DisplayName = "Loading Factor", Value = fleet.Kingdom.LoadingFactorPercent, Unit = "%", Scope = "Kingdom" },
                    new KpiSnapshot { Key = "outages", DisplayName = "Flagged sector", Value = fleet.FlaggedSectorCode, Scope = "Kingdom" },
                    new KpiSnapshot { Key = "co2", DisplayName = "CO₂", Value = fleet.Kingdom.Co2TonPerDay, Unit = "t/day", Scope = "Kingdom" }
                ];
                sources.Add(new ChatSource("AFAG", null, null, "Kingdom/KSA"));
                break;
            }
        }

        // Light keyword overrides for explicit asks
        var lower = message.ToLowerInvariant();
        if (lower.Contains("pp09") && lower.Contains("heat"))
        {
            var plant = await semantic.GetPlantOverviewAsync("PP09", cancellationToken);
            if (plant is not null)
            {
                answer = ar ? plant.RootCauseNarrativeAr : plant.RootCauseNarrativeEn;
                action = plant.RecommendedAction;
                jump = plant.Plant.VisionJumpPath;
            }
        }

        if (lower.Contains("flame") || message.Contains("لهب"))
        {
            var unit = await semantic.GetUnitOverviewAsync("PP09", "A1", "GT01", cancellationToken);
            if (unit is not null)
            {
                answer = ar ? unit.NarrativeAr : unit.NarrativeEn;
                action = unit.RecommendedAction;
                jump = unit.Unit.VisionJumpPath;
            }
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            answer += ar ? $"\n\nالخطوة التالية: {action}" : $"\n\nNext step: {action}";
        }

        return new ChatAskResponse(
            Guid.NewGuid().ToString("N"),
            answer,
            [new ToolTraceItem("semantic_layer", "success", 0)],
            sources,
            viz,
            null,
            persona,
            action,
            jump,
            kpis,
            true);
    }

    private static string BuildInstructions(AssistantPersona persona, string language)
    {
        var langRule = language.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
            ? "Respond in Arabic."
            : "Respond in English unless the user wrote in Arabic.";

        var lens = persona switch
        {
            AssistantPersona.Executive =>
                "Persona: VP/Executive. Answer with a short narrative + KPI meaning (Gross MW, Loading Factor, Outages, CO₂). Flag the worst sector. Never mention PI tag names.",
            AssistantPersona.SectorOps =>
                "Persona: Sector Operations. Rank plants vs loading targets with deltas and Vision jump-links. Never mention PI tag names.",
            AssistantPersona.PlantManager =>
                "Persona: Plant Manager. Explain heat-rate / MW shortfall via blocks, fuel mix, weather. Cite SOP when available. Never mention PI tag names.",
            AssistantPersona.ShiftOperator =>
                "Persona: Shift Operator. Give live unit status, likely cause, exact SOP step, escalation. Never mention PI tag names.",
            _ =>
                "Persona: Reliability Engineer. Rank anomalies vs baseline, cite evidence, propose investigation. Never mention PI tag names."
        };

        return
            $"""
            You are the AFAG PI Vision AI role-driven decision assistant.
            {lens}
            {langRule}

            Rules:
            - Need-first: users ask in business language (Plant, Block, Unit, KPI). Resolve via tools.
            - NEVER invent values. Call tools for every factual claim.
            - NEVER ask the user for a PI tag name.
            - Every answer must include a recommended next action and cite a source (AFAG KPI, SOP, or Vision jump-link) when available.
            - Do not write to PI, acknowledge alarms, or perform control actions.
            - Informational only — does not replace operating procedures.
            """;
    }

    private static AssistantPersona InferPersona(string message, string? screenLevel)
    {
        if (!string.IsNullOrWhiteSpace(screenLevel))
        {
            return screenLevel.ToLowerInvariant() switch
            {
                "kingdom" or "ksa" => AssistantPersona.Executive,
                "sector" or "coa" or "eoa" or "soa" or "woa" => AssistantPersona.SectorOps,
                "plant" => AssistantPersona.PlantManager,
                "unit" => AssistantPersona.ShiftOperator,
                _ => AssistantPersona.Executive
            };
        }

        var m = message.ToLowerInvariant();
        if (m.Contains("flame") || m.Contains("alarm") || m.Contains("gt01") || m.Contains("لهب") || m.Contains("إنذار"))
            return AssistantPersona.ShiftOperator;
        if (m.Contains("heat rate") || m.Contains("pp09") || m.Contains("block") || m.Contains("معدل الحرارة"))
            return AssistantPersona.PlantManager;
        if (m.Contains("coa") || m.Contains("sector") || m.Contains("plants below") || m.Contains("محطات") || m.Contains("قطاع"))
            return AssistantPersona.SectorOps;
        if (m.Contains("baseline") || m.Contains("anomaly") || m.Contains("eaf") || m.Contains("eford"))
            return AssistantPersona.ReliabilityEngineer;
        return AssistantPersona.Executive;
    }

    private static string? VisibleAssistantText(ChatResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.Text))
        {
            var fromText = StripThinkBlocks(response.Text);
            if (!string.IsNullOrWhiteSpace(fromText))
            {
                return fromText;
            }
        }

        var builder = new StringBuilder();
        foreach (var message in response.Messages)
        {
            foreach (var content in message.Contents)
            {
                if (content is TextContent text && !string.IsNullOrWhiteSpace(text.Text))
                {
                    builder.AppendLine(text.Text);
                }
            }
        }

        return StripThinkBlocks(builder.ToString());
    }

    private static string? StripThinkBlocks(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var stripped = ThinkBlockRegex.Replace(text, string.Empty);
        stripped = ThinkOpenRegex.Replace(stripped, string.Empty).Trim();
        return string.IsNullOrWhiteSpace(stripped) ? null : stripped;
    }

    private static string InferLanguage(string message)
    {
        foreach (var ch in message)
        {
            if (ch is >= '\u0600' and <= '\u06FF')
            {
                return "ar";
            }
        }

        return "en";
    }
}
