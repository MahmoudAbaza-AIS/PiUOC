# AvevaPi / Tag Intelligence Assistant

.NET **10** Clean Architecture backend for AVEVA PI Web API + Tag Intelligence (deterministic API first, AI chat + visualization second).

## Architecture

```text
Api (Presentation) → Application → Domain
                         ▲
                  Infrastructure
```

See [docs/CLEAN-ARCHITECTURE.md](docs/CLEAN-ARCHITECTURE.md).

## Quick start

```powershell
dotnet run --project src/PiAiAssistant.Api
```

- **App UI (chat + charts):** http://localhost:5041/app/index.html  
- Scalar: http://localhost:5041/scalar/v1  

## What the assistant can do

Natural-language PI questions via **read-only tools** (search, specs, current value, history, summary stats, related tags, multi-tag compare). Charts render in the UI when trend tools run. Not unrestricted PI/SQL access — that is intentional for safety.

## Chart APIs

- `GET /api/tags/chart?reference={TagName}`
- `GET /api/tags/summary?reference={TagName}`
- `GET /api/tags/compare?references=TagA,TagB`

## Docs

- [Clean Architecture](docs/CLEAN-ARCHITECTURE.md)
- [Tag Intelligence Assistant](docs/TAG-INTELLIGENCE-ASSISTANT.md)
- [PI System from scratch](docs/PI-SYSTEM-FROM-SCRATCH.md)
