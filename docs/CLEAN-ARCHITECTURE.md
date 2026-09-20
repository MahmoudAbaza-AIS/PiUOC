# Clean Architecture

This solution follows Uncle Bob’s Dependency Rule for the PI Web API Tag Intelligence backend.

## Layers

```text
PiAiAssistant.Api              Presentation (thin endpoints + composition root)
        │
        ▼
PiAiAssistant.Application      Use cases / DTOs / ports (no HttpClient, EF, ASP.NET)
        │
        ▼
PiAiAssistant.Domain           Entities + narrow interfaces (zero NuGet deps)
        ▲
        │
PiAiAssistant.Infrastructure   PI Web API, Demo strategy, EF/SQLite, LLM adapters
```

Dependencies point **inward only**. Infrastructure and Presentation reference Application/Domain; Domain never references outward.

## Key types

| Layer | Examples |
|---|---|
| Domain | `PiPoint`, `TagSample`, `Sector`, `Plant`, `GenerationUnit`, `IPiPointReader`, `ITagValueReader`, `IAfagHierarchyReader`, `ISopKnowledgeStore` |
| Application | `ITagIntelligenceService`, `IAfagSemanticService`, `IBriefingService`, `IDecisionAssistant`, `ITagAssistant` |
| Infrastructure | `PiWebApiDataSource`, `DemoPiDataSource`, `AfagDemoHierarchyReader`, `InMemorySopStore`, `DecisionAssistant`, `TagAssistant`, `AddInfrastructure` |
| Presentation | `Program.cs` minimal APIs only |

## Role-driven path (primary)

Persona router + semantic layer (Plant/Block/Unit/KPI) — see [ROLE-DRIVEN-DECISION-ASSISTANT.md](ROLE-DRIVEN-DECISION-ASSISTANT.md). Tag intelligence remains a supporting slice for specialist drills.

## Demo vs live PI (Strategy + DI)

Selected **once** in `Infrastructure/DependencyInjection.cs` from `PiConnectionOptions.UseDemoMode` / `AuthMode`. Application use cases never branch on demo vs live; they only call Domain ports.

In **Development**, when `UseDemoMode` is false, the composition root probes the configured BaseUrl (PI WebAPI emulator on `localhost:5000` by default). If reachable → `PiWebApiDataSource` (`source: simulator`). If not → Demo fallback (`source: demo-fallback`). Non-Development hosts never auto-fallback.

## Auth modes (Infrastructure)

Configured via `PiConnection:AuthMode`: `Demo`, `Anonymous`, `Basic`, `Windows` / `DefaultCredentials` / `Kerberos`, `NetworkCredential`, `Bearer`.

## Cursor rule

Persistent agent guidance: `.cursor/rules/clean-architecture-pi.mdc` (`alwaysApply: true`).

## Legacy

`src/AvevaPi.Client` is retired (logic moved into Infrastructure). Prefer Domain ports + Infrastructure implementations.
