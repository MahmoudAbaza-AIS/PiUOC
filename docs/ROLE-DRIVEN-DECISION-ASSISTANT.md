# PI Vision AI — Role-Driven Decision Assistant

Guideline source: [`reqs/PI Vision AI — Role-Driven Decision Assistant Guideline (1).pdf`](../reqs/PI%20Vision%20AI%20—%20Role-Driven%20Decision%20Assistant%20Guideline%20(1).pdf)  
Screen references: Kingdom / Sector / Plant / Unit PNGs under [`reqs/`](../reqs/).

## Principle

```text
Need-first, not tag-first
Persona → decision → business-language question → semantic layer (Plant/Block/Unit/KPI) → narrative + action + citation
```

If a user must know a PI tag name to get an answer, the feature has failed this guideline.

Tag-level tools remain available at `POST /api/chat/tags` for reliability deep-dives — they are **not** the primary path.

## What changed vs Tag Intelligence MVP

| Before | Now |
|---|---|
| One generic tag chatbot | Five personas with distinct lenses |
| Tag catalog as primary identity | AFAG semantic objects (Kingdom → Sector → Plant → Block → Unit) |
| Pull-only chat | Proactive briefing APIs per persona |
| English-only UX copy | English + Arabic narratives / briefs |
| No SOP citations | In-memory SOP corpus with citations |
| Tag charts only | Fleet / plant KPI screens + decision chat |

## Personas → screen levels

| Screen | Persona | Primary API |
|---|---|---|
| Kingdom (KSA) | Executive | `GET /api/fleet/kingdom`, morning brief |
| Sector (COA…) | Sector Ops | `GET /api/fleet/sectors/{code}` |
| Plant (PP09) | Plant Manager | `GET /api/fleet/plants/{code}` |
| Unit (GT01) | Shift Operator | `GET /api/fleet/plants/.../units/{code}` |
| Cross-level | Reliability | anomaly digest + plant evidence |

## Endpoints

### Semantic (deterministic)

| Endpoint | Purpose |
|---|---|
| `GET /api/fleet/kingdom` | Fleet narrative + sector rollup + flagged sector |
| `GET /api/fleet/sectors/{sectorCode}` | Sector KPIs + plants below target |
| `GET /api/fleet/sectors/{sectorCode}/plants/below-target` | Ranked deltas |
| `GET /api/fleet/plants/{plantCode}` | Root-cause narrative + blocks + trends |
| `GET /api/fleet/plants/{plant}/blocks/{block}/units/{unit}` | Live unit status + SOP action |

### Proactive pushes

| Endpoint | Persona |
|---|---|
| `GET /api/briefings/executive/morning?lang=en\|ar` | VP |
| `GET /api/briefings/sector/COA` | Sector Ops |
| `GET /api/briefings/plants/PP09` | Plant Manager |
| `GET /api/briefings/alarms/PP09/A1/GT01` | Shift Operator |
| `GET /api/briefings/demo` | All five pushes |

### Chat

```http
POST /api/chat
{
  "message": "Why is PP09 heat rate worse than last week?",
  "persona": "PlantManager",
  "language": "en",
  "screenLevel": "plant"
}
```

Response includes `answer`, `recommendedAction`, `visionJumpPath`, `kpis`, `sources`, and optional `visualization`.  
If Ollama is offline, the assistant returns a **deterministic semantic narrative** (demo-safe).

`POST /api/chat/tags` — legacy read-only tag tools (catalog + PI streams).

## Demo storyline (four scenes)

1. **VP** — morning brief: fleet ~40.4 GW net, 75% loading, COA dragging via PP09 heat rate.  
2. **Sector Ops** — Arabic/English: plants below COA loading target, PP09 ranked.  
3. **Plant Manager** — PP09 heat-rate root cause → Block A1 fuel-flow + SOP citation.  
4. **Operator** — GT01 flame-intensity BAD → SOP-GT-04 §3.2, escalate after 5 min.

## Architecture (Clean Architecture)

```text
Api  →  Application (AfagSemanticService, BriefingService, DecisionAssistant port)
     →  Domain (Sector/Plant/Block/Unit/KPI/Sop + IAfagHierarchyReader)
Infra → AfagDemoHierarchyReader, InMemorySopStore, DecisionAssistant (LLM adapter)
```

Demo AFAG data lives in Infrastructure only. Application never branches on demo vs live for semantic objects.

## UI

Open `http://localhost:5041/app/index.html`:

- Level tabs: KSA → COA → PP09 → GT01  
- Persona + language selectors  
- Proactive brief card  
- Decision chat with guideline prompt chips  

## Still out of scope for this iteration

Full production channels from the guideline (PI Vision embed, Teams/Outlook cards, email dispatcher, voice kiosk, live alarm bus, GADS certification, SQL Server semantic warehouse). Those plug into the same Application ports (`IBriefingService`, `IAfagHierarchyReader`, `ISopKnowledgeStore`).
