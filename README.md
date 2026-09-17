# AvevaPi / Tag Intelligence Assistant

.NET **10** Clean Architecture backend for AVEVA PI Web API + Tag Intelligence (deterministic API first, AI chat second).

## Architecture

```text
Api (Presentation) → Application → Domain
                         ▲
                  Infrastructure
```

See [docs/CLEAN-ARCHITECTURE.md](docs/CLEAN-ARCHITECTURE.md) and `.cursor/rules/clean-architecture-pi.mdc`.

## Projects

| Project | Layer |
|---|---|
| `PiAiAssistant.Domain` | Entities + PI ports |
| `PiAiAssistant.Application` | Tag intelligence use cases |
| `PiAiAssistant.Infrastructure` | PI Web API / Demo / SQLite / LLM |
| `PiAiAssistant.Api` | Thin HTTP host |
| `AvevaPi.ConsoleApp` | Smoke console against Domain ports |
| `PiAiAssistant.Tests` | Application tests with Demo + in-memory SQLite |

## Quick start

```powershell
dotnet run --project src/PiAiAssistant.Api
```

- Scalar UI: `http://localhost:5041/scalar/v1`
- Milestone: `GET /api/tags/Plant1.Boiler03.SteamPressure`

## Docs

- [Clean Architecture](docs/CLEAN-ARCHITECTURE.md)
- [Tag Intelligence Assistant](docs/TAG-INTELLIGENCE-ASSISTANT.md)
- [PI System from scratch](docs/PI-SYSTEM-FROM-SCRATCH.md)
