## Start

```powershell
dotnet run --project src/PiAiAssistant.Api
```

Open **http://localhost:5041/app/index.html**

**Status pills (top right):**
- **PI: simulator** — connected to the local PI WebAPI emulator (`localhost:5000`)
- **PI: demo (sim down)** — emulator unreachable; in-process demo data
- **PI: demo** — explicit demo mode
- **Ollama: …** — if offline, chat still works (deterministic answers)

---

## What each part does

| UI piece | Purpose |
|---|---|
| **KSA / COA / PP09 / GT01** | Four AFAG levels. Loads KPIs + narrative and sets the matching persona. |
| **Left panel** | “Screen” view: KPI tiles, story text, optional chart. |
| **Refresh** | Reloads the current level. |
| **Proactive push** | AI speaks first — morning brief / alert / alarm card for the current persona. |
| **Language EN / العربية** | Briefs and chat language. |
| **Persona** | VP, Sector Ops, Plant Manager, Operator, Reliability — changes answer style. |
| **Prompt chips** | Click to fill a good question for that role. |
| **Decision assistant** | Ask in business language (plant, block, loading, flame) — **not tag names**. |
| **Model** | qwen / deepseek via Ollama (ignored when Ollama is down). |

---

## Demo walk (best storyline)

### 1) VP — click **KSA**
1. **Load persona brief** → morning fleet briefing.  
2. Ask (or use chips):
   - *How are we running today vs. yesterday?*
   - *Which sector has the largest generation decline?*  

**Expect:** ~40.4 GW net, ~75% loading, COA dragging, PP09 heat-rate note, a next action.

### 2) Sector Ops — click **COA**
1. Optional: language → **العربية**, load brief.  
2. Ask:
   - *Which plants in COA are below target loading?*
   - *أي محطات في COA أقل من هدف التحميل؟*  

**Expect:** ranked plants vs target; PP09 near the top; no tags.

### 3) Plant Manager — click **PP09**
1. Load brief, then ask:
   - *Why is PP09 heat rate worse than last week?*
   - *Which PP09 blocks account for today’s MW shortfall?*  

**Expect:** Block A1 fuel-flow story, SOP citation, trend chart.

### 4) Operator — click **GT01**
1. Load brief (alarm card).  
2. Ask:
   - *Flame intensity says bad on GT01 — what do I do?*
   - *What changed on GT01 since the start of my shift?*  

**Expect:** MW / loading / fuel, BAD flame sensors, **SOP-GT-04 §3.2**, escalate after 5 min.

### 5) Reliability (optional)
Set persona to **Reliability**, ask:
- *Show me units trending outside their baseline over 30 days*
- *Do comparable units show the same heat-rate trend?*

---

## What to ask the AI

**Good:**
- How is the fleet / COA / PP09 / GT01 doing?
- Which plants are below loading target?
- Why is heat rate worse?
- What do I do about this flame alarm?

**Avoid in this UI:**
- *What is the value of tag XYZ…?*  
  That’s the old tag path → `POST /api/chat/tags` (API only), not the main chat.

---

## Mental model

```text
Pick role + screen → see KPIs
        ↓
Load brief (push)
        ↓
Ask in plant/sector language
        ↓
Answer = narrative + next action + SOP/KPI citation
```

Full copy also lives in `docs/WALKTHROUGH.md`. Scalar APIs: http://localhost:5041/scalar/v1