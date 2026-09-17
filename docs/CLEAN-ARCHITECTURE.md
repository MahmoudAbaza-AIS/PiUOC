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
| Domain | `PiPoint`, `TagSample`, `IPiPointReader`, `ITagValueReader`, `IAfAttributeReader`, `IPiConnectivity` |
| Application | `ITagIntelligenceService`, `TagResolver`, `TagIntelligenceService`, `ITagCatalogRepository`, `ITagAssistant` |
| Infrastructure | `PiWebApiDataSource`, `DemoPiDataSource`, `TagCatalogRepository`, `TagAssistant` (LLM adapter), `AddInfrastructure` |
| Presentation | `Program.cs` minimal APIs only |

## Demo vs live PI (Strategy + DI)

Selected **once** in `Infrastructure/DependencyInjection.cs` from `PiConnectionOptions.UseDemoMode` / `AuthMode`. Application use cases never branch on demo vs live; they only call Domain ports.

## Auth modes (Infrastructure)

Configured via `PiConnection:AuthMode`: `Demo`, `Anonymous`, `Basic`, `Windows` / `DefaultCredentials` / `Kerberos`, `NetworkCredential`, `Bearer`.

## Cursor rule

Persistent agent guidance: `.cursor/rules/clean-architecture-pi.mdc` (`alwaysApply: true`).

## Legacy

`src/AvevaPi.Client` is retired (logic moved into Infrastructure). Prefer Domain ports + Infrastructure implementations.
