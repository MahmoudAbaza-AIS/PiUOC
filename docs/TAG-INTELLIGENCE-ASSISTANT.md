# Tag Intelligence Assistant

Deterministic PI/AF tag intelligence API first, then AI chat layers that can explain results. The **Tag Assistant** path can **only** call read-only .NET tools; raw provider endpoints are for smoke-testing models without tools.

## Principle

```text
User language → agent chooses tool → typed .NET service → PI Web API + SQLite catalog → structured JSON → model explains
```

The LLM never queries PI or SQL directly on the Tag Assistant path (`POST /api/chat`).

## Stack notes (current)

| Item | Value |
|---|---|
| Target framework | **.NET 10** (`net10.0`) |
| API docs UI | **Scalar** at `/scalar/v1` (OpenAPI at `/openapi/v1.json`) |
| Local model | Ollama OpenAI-compatible API |
| Cloud model (optional) | Qwen Online via DashScope compatible mode |
| Catalog DB | SQLite (`tag-catalog.db`) |

## Run

```powershell
cd C:\Users\SESA652755\Documents\Projects\AIS\UOC-V2
dotnet run --project src/PiAiAssistant.Api
```

- App UI (chat + charts): `http://localhost:5041/app/index.html`
- Scalar: `http://localhost:5041/scalar/v1`
- In Development, `appsettings.Development.json` forces **demo PI** (`UseDemoMode: true`).

### Milestone endpoints

| Endpoint | Purpose |
|---|---|
| `GET /health` | Process health |
| `GET /health/pi` | PI connectivity (demo or live) |
| `GET /health/ollama` | Local Ollama reachability |
| `GET /api/tags/{TagName}` | Canonical tag details JSON |
| `GET /api/tags/search?q=...` | Constrained search |
| `GET /api/tags/history?reference=...` | Recent history with server limits |
| `GET /api/tags/chart?reference=...` | Chart-ready time series |
| `GET /api/tags/summary?reference=...` | Min/max/avg summary |
| `GET /api/tags/compare?references=A,B` | Multi-tag chart series |
| `GET /api/tags/details?reference=...` | Details via query string |
| `POST /api/chat` | Broad PI assistant (tools + optional chart payload) |
| `POST /api/chat/ollama` | Raw local Qwen (**no tools**) |
| `POST /api/chat/qwen-online` | Raw cloud Qwen (**no tools**) |

Example:

```http
GET /api/tags/chart?reference=SINUSOID
POST /api/chat
{ "message": "Show the trend for SINUSOID over the last hour" }
```

The assistant handles natural-language PI questions (search, specs, current value, history, stats, related tags, compare) through **read-only tools only** — not unrestricted historian access.

## Chat endpoints (what changed)

Three chat surfaces now exist. Use the right one for the job.

### 1) `POST /api/chat` — Tag Intelligence Assistant (recommended)

- Uses `ITagAssistant` / `TagAssistant`
- Registers tools: `list_catalog_tags`, `search_tags`, `get_tag_details`, `get_tag_history`, `get_tag_summary`, `get_related_tags`, `get_chart_series`, `compare_tags`
- Resolves tag identity from **SQLite tag catalog** first; stream values come from Demo/live PI using catalog `PiWebId` / `PiPointName`
- On startup, `ITagCatalogSyncService` backfills missing `PiWebId` values into the catalog DB
- Model is local Ollama (`Ollama:Model`, default `qwen3:8b`)
- Writes a `ChatAudit` row
- Returns `toolTrace` + `sources` when tools ran

```http
POST /api/chat
Content-Type: application/json

{
  "message": "Tell me everything about Plant1.Boiler03.SteamPressure",
  "conversationId": null
}
```

### 2) `POST /api/chat/ollama` — local model smoke test

- Keyed client `"ollama"`
- **No PI tools** — plain chat completion only
- Useful to verify Ollama/Qwen without tag logic
- Returns HTTP 503 if `Ollama:Enabled` is false or the call fails

### 3) `POST /api/chat/qwen-online` — cloud Qwen smoke test

- Keyed client `"qwen-online"`
- OpenAI-compatible DashScope endpoint: `https://dashscope.aliyuncs.com/compatible-mode/v1/`
- **No PI tools** — plain chat completion only
- Requires `QwenOnline:ApiKey`
- Disabled by default (`QwenOnline:Enabled: false`)
- Returns HTTP 503 if disabled or the call fails

```http
POST /api/chat/qwen-online
Content-Type: application/json

{
  "message": "Say hello in one sentence."
}
```

## Configuration

### `Ollama` (local)

```json
"Ollama": {
  "Enabled": true,
  "BaseUrl": "http://localhost:11434/",
  "Model": "qwen3:8b"
}
```

```bash
ollama pull qwen3:8b
ollama serve
```

### `QwenOnline` (cloud)

```json
"QwenOnline": {
  "Enabled": false,
  "ApiKey": "your-qwen-api-key-here",
  "Model": "qwen-max"
}
```

Prefer user secrets / environment variables for the API key:

```text
QwenOnline__Enabled=true
QwenOnline__ApiKey=...
QwenOnline__Model=qwen-max
```

### `PiConnection`

Base `appsettings.json` may contain a **live PI template** (`UseDemoMode: false`, Basic auth placeholders).  
Development overrides keep local work on demo PI:

```json
"PiConnection": {
  "UseDemoMode": true,
  "AuthMode": "Demo"
}
```

For a real server (non-Development or explicit config):

```text
PiConnection__UseDemoMode=false
PiConnection__BaseUrl=https://pi-web-api-host/piwebapi
PiConnection__DataArchiveName=PISRV01
PiConnection__AuthMode=Windows
```

Do **not** commit real PI passwords or Qwen API keys. Keep secrets in user secrets, env vars, or a secret store.

## DI / wiring (Program.cs)

- Default `IChatClient` → Ollama (used by Tag Assistant)
- Keyed `IChatClient` `"ollama"` → same local endpoint (raw `/api/chat/ollama`)
- Keyed `IChatClient` `"qwen-online"` → DashScope compatible API (raw `/api/chat/qwen-online`)
- Options bound: `PiConnection`, `Ollama`, `QwenOnline`
- API explorer: `AddOpenApi()` + `MapScalarApiReference()` (Swagger UI package removed)

## Solution layout (Clean Architecture)

```text
src/
  PiAiAssistant.Domain/           entities + narrow PI ports
  PiAiAssistant.Application/      tag intelligence use cases / DTOs
  PiAiAssistant.Infrastructure/   PI Web API, Demo strategy, SQLite, LLM adapters
  PiAiAssistant.Api/              thin Presentation host (Scalar + endpoints)
  AvevaPi.ConsoleApp/             smoke console against Domain ports
tests/
  PiAiAssistant.Tests/
```

See [CLEAN-ARCHITECTURE.md](CLEAN-ARCHITECTURE.md). Demo vs live PI is selected only in `Infrastructure/DependencyInjection.cs`.

## Seeded NuGreen catalog tags

Aligned with the PI WebAPI Emulator:

- `Houston.B-210.Temperature` / `Pressure` / `SteamFlow`
- `Houston.C-110.RPM` / `Vibration`
- `Oakland.B-220.Temperature` / `Pressure`
- aliases such as `B-210 Temperature`

## Safety already baked in

- Tag Assistant tools are read-only (`list_catalog_tags`, `search_tags`, `get_tag_details`, history/summary/chart/compare/related)
- Catalog DB (`tag-catalog.db`) is the curated identity source; AI never runs raw SQL
- Ambiguous matches return HTTP 409 + candidates (no silent pick)
- History capped (max 1000 points, max 24h raw window)
- Chat audit rows in `ChatAudit` for Tag Assistant requests
- Metadata WebIds cached briefly; live values are not cached long-term
- Raw `/ollama` and `/qwen-online` endpoints intentionally have **no** PI tool access

## PI WebAPI Emulator (local live PI)

Point the app at [Shirajum Munir's AVEVA PI WebAPI Emulator](https://github.com/mdshirajum/--Aveva_PI_WebAPI_Emulator) (NuGreen hierarchy on `localhost:5000`):

```json
"PiConnection": {
  "UseDemoMode": false,
  "BaseUrl": "http://localhost:5000/piwebapi",
  "DataArchiveName": "PIServer1",
  "DefaultAfServer": "AFServer1",
  "DefaultAfDatabase": "NuGreen",
  "AuthMode": "Anonymous"
}
```

`PiWebApiDataSource` falls back when GetByPath / points/search are missing (as on this emulator): `points/pt_{name}`, `dataservers/{id}/points`, and AF attribute WebId / tree walk.

Sample tags: `Houston.B-210.Temperature`, `Houston.B-210.Pressure`, `Oakland.B-220.Temperature`.

## Tests

```powershell
dotnet test tests/PiAiAssistant.Tests
```

Includes:

- Application/catalog unit tests (Demo PI + SQLite)
- **WireMock.NET** HTTP contract tests for `PiWebApiDataSource` (fake PI Web API: 200/401/404/500, query params, exception mapping, emulator-style fallbacks)
- Optional smoke test against `http://localhost:5000/piwebapi` (no-ops if emulator is down)

## Sample HTTP file

See `src/PiAiAssistant.Api/PiAiAssistant.Api.http` for ready-to-run requests (including the new chat routes).
