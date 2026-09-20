# AvevaPi / AFAG PI Vision AI

.NET **10** Clean Architecture backend: **role-driven decision assistant** for AFAG generation displays (need-first), plus retained tag-intelligence APIs for deep drills.

## Architecture

```text
Api (Presentation) → Application → Domain
                         ▲
                  Infrastructure
```

See [docs/CLEAN-ARCHITECTURE.md](docs/CLEAN-ARCHITECTURE.md) and [docs/ROLE-DRIVEN-DECISION-ASSISTANT.md](docs/ROLE-DRIVEN-DECISION-ASSISTANT.md).

## Quick start

```powershell
dotnet run --project src/PiAiAssistant.Api
```

- **App UI (4 levels + personas + briefings):** http://localhost:5041/app/index.html  
- Scalar: http://localhost:5041/scalar/v1  

## What the assistant does

Answers **business-language** questions for VP, Sector Ops, Plant Manager, Shift Operator, and Reliability — through Plant/Block/Unit/KPI tools and SOP citations. Proactive briefs are available per persona (EN/AR). Users should not need PI tag names.

Tag tools remain at `POST /api/chat/tags` for specialist drills.

## Requirements

Product guideline and AFAG screen mockups: [`reqs/`](reqs/).

## Docs

- [Role-Driven Decision Assistant](docs/ROLE-DRIVEN-DECISION-ASSISTANT.md)
- [Tag Intelligence Assistant](docs/TAG-INTELLIGENCE-ASSISTANT.md) (underlying tag APIs)
- [Clean Architecture](docs/CLEAN-ARCHITECTURE.md)
- [PI System from scratch](docs/PI-SYSTEM-FROM-SCRATCH.md)
