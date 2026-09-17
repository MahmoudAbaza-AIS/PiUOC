# AVEVA PI System — From Scratch Guide (for absolute beginners)

This document explains **what the AVEVA PI System is**, **how data moves through it**, and **how this .NET sample talks to it**.  
Read it top-to-bottom once. Then run the app in demo mode. Then connect to a real PI Web API when your team gives you credentials.

---

## 1. What is AVEVA PI System in plain English?

Imagine a factory (or power plant, water plant, building, etc.) full of sensors:

- temperatures
- pressures
- levels
- valve positions
- motor speeds
- energy meters

Those sensors produce numbers **continuously**, often every few seconds (or faster).

**AVEVA PI System** (historically called **OSIsoft PI**) is industrial software that:

1. Collects those values from PLCs, DCS, OPC servers, IoT gateways, files, databases, etc.
2. Stores them efficiently as **time-series history** (value + timestamp + quality).
3. Makes them available to engineers, dashboards, analytics, and custom apps (like this .NET app).

Think of it as:

> **a specialized historian / time-series database for industrial operations**, plus tools around it.

It is **not** a replacement for SQL Server for business tables.  
It is optimized for: “what was Pump 12’s discharge pressure at 14:03:17 yesterday?”

---

## 2. The big pieces (vocabulary you will hear every day)

### 2.1 PI Data Archive (often just “PI Server”)

The core historian. It stores **PI Points** (tags) and their values over time.

- Snapshot (current) value
- Recorded historical values
- Compression / exception settings (to avoid storing useless noise)

### 2.2 PI Point / Tag

One named signal.

Examples:

- `SINUSOID` (classic demo sine wave tag on many PI servers)
- `CDT158` (another common demo tag)
- `UNIT1.REACTOR.TEMP`
- `BA:LEVEL.1`

Each point has metadata: data type, engineering units, description, compression rules, security.

### 2.3 PI Interface / Connector / Adapter

Software that **reads from a source system** and **writes into PI**.

Examples:

- OPC DA / UA interface
- Modbus
- RDBMS (SQL)
- MQTT / IoT
- File interface

Your .NET app can also write values (if allowed), but in production most writes come from interfaces, not from random apps.

### 2.4 Asset Framework (AF)

A **tree of assets** (Plant → Area → Unit → Equipment) with attributes that point to tags.

Example:

```text
Green Power Company
  └── Wind Farm A
        └── Turbine 03
              ├── PowerOutput   --> PI tag \\PISERVER\WF_A.T03.PWR
              ├── WindSpeed     --> PI tag \\PISERVER\WF_A.T03.WS
              └── Status        --> PI tag \\PISERVER\WF_A.T03.STS
```

AF answers: “show me Turbine 03”, not only “show me tag XYZ123”.

### 2.5 PI Vision / ProcessBook / clients

Visualization tools for trends, dashboards, process graphics.

### 2.6 PI Web API

A **REST/HTTPS API** that exposes PI Data Archive + AF over the web.

This sample uses PI Web API because:

- it works with modern .NET
- it is easier to learn than native AF SDK first
- it can run without installing heavy Windows client SDKs on every machine

### 2.7 AF SDK

The official **.NET library** (`OSIsoft.AFSDK` / newer `Aveva.AFSDK`) for deep Windows/.NET integration with AF and Data Archive.

Use AF SDK when you need rich AF object models, event frames, bulk native performance, Windows services tightly coupled to PI.

---

## 3. How data flows (the mental model)

```text
Sensors / PLC / DCS
        |
        v
 PI Interface / Connector
        |
        v
  PI Data Archive  <---- stores time-series tags
        |
        +-----> AF attributes map to tags
        |
        +-----> PI Vision / Excel / custom apps
        |
        +-----> PI Web API  <---- THIS .NET APP
```

When your app asks for “current value of SINUSOID”:

1. App calls PI Web API: `GET /streams/{webId}/value`
2. Web API asks Data Archive for the snapshot
3. JSON comes back with timestamp + value + quality
4. Your C# code prints / stores / processes it

---

## 4. Important PI concepts beginners usually miss

### 4.1 Time is special in PI

PI accepts friendly relative times:

| Expression | Meaning |
|---|---|
| `*` | now |
| `*-1h` | one hour ago |
| `*-1d` | one day ago |
| `y` | yesterday |
| `t` | today |

Absolute ISO timestamps also work.

### 4.2 Not every sample is stored

PI uses **exception** and **compression** to skip values that barely change.  
So “recorded values” are not always one-per-second even if the source updates that fast.

### 4.3 Quality / “Good” flag

A value can exist but be bad/questionable (sensor fault, communication loss). Always check quality in real apps.

### 4.4 Security is strict

Having network access is not enough. You need:

- permission to read the point
- permission to write the point (often denied by default)
- correct authentication (Windows/Kerberos, Basic, etc. depending on Web API setup)

### 4.5 WebId

PI Web API identifies objects with a **WebId** string.  
Typical flow:

1. Find point by path: `\\PISERVER\SINUSOID`
2. Receive WebId
3. Use WebId for value reads/writes

This sample hides that detail inside `PiWebApiClient`.

---

## 5. Two common ways to integrate from .NET

| Approach | What it is | Pros | Cons | Best for |
|---|---|---|---|---|
| **PI Web API** (this repo) | HTTPS REST | Modern .NET friendly, language-agnostic, easier firewall path | Slightly more HTTP overhead | Web apps, services, cross-platform, learning |
| **AF SDK** | Native .NET library | Rich AF model, strong for thick clients/services on Windows | Historically Windows /.NET Framework oriented; client install/licensing considerations | Desktop tools, Windows services, deep AF work |

You can start with Web API (this project), then move hotspots to AF SDK later if needed.

---

## 6. What this repository contains

```text
UOC-V2/
├── AvevaPi.slnx
├── README.md
├── docs/
│   └── PI-SYSTEM-FROM-SCRATCH.md   ← you are here
└── src/
    ├── AvevaPi.Client/             ← reusable library
    │   ├── IPiDataClient.cs
    │   ├── DemoPiDataClient.cs     ← offline fake PI
    │   ├── PiWebApiClient.cs       ← real PI Web API
    │   ├── Models/
    │   └── Options/
    └── AvevaPi.ConsoleApp/         ← runnable demo
        ├── Program.cs
        └── appsettings.json
```

### What the console app does

1. Reads config from `appsettings.json`
2. Chooses **Demo mode** or **Live PI Web API**
3. For each configured tag:
   - finds the point
   - reads snapshot
   - reads recent history
4. Writes one sample value (demo-safe; on live PI needs write rights)

---

## 7. Run it offline first (recommended)

You do **not** need a PI server to learn the flow.

```powershell
cd C:\Users\SESA652755\Documents\Projects\AIS\UOC-V2
dotnet run --project src/AvevaPi.ConsoleApp
```

With default settings (`UseDemoMode: true`) you should see:

- a DEMO MODE status line
- fake metadata for `SINUSOID`, `CDT158`, `BA:LEVEL.1`
- snapshot + history samples
- a successful demo write

That proves your .NET plumbing works before fighting network/auth/certificates.

---

## 8. Connect to a real PI Web API

Ask your PI admin for:

1. PI Web API base URL  
   Example: `https://piwebapi.mycompany.com/piwebapi`
2. Data Archive name  
   Example: `PISRV01`
3. Auth method (Windows integrated or Basic username/password)
4. A few tag names you are allowed to read
5. Whether write is allowed (often no)

Then edit `src/AvevaPi.ConsoleApp/appsettings.json`:

```json
{
  "PiConnection": {
    "UseDemoMode": false,
    "BaseUrl": "https://piwebapi.mycompany.com/piwebapi",
    "DataArchiveName": "PISRV01",
    "AuthMode": "Windows",
    "Username": "",
    "Password": "",
    "AcceptInvalidCertificates": false,
    "SampleTagNames": [ "SINUSOID", "CDT158" ]
  }
}
```

### AuthMode values

- `Demo` — offline fake data
- `Windows` — use current Windows credentials (common on corporate domain)
- `Basic` — username/password (set Username/Password)
- `Anonymous` — rare; only if Web API allows it

### Lab certificates

If your lab uses a self-signed HTTPS cert, set:

```json
"AcceptInvalidCertificates": true
```

**Never do this in production.**

Then run again:

```powershell
dotnet run --project src/AvevaPi.ConsoleApp
```

---

## 9. What happens under the hood (live mode)

For tag `SINUSOID` on archive `PISRV01`:

1. **Find point**
   - `GET /piwebapi/points?path=\\PISRV01\SINUSOID`
2. **Read snapshot**
   - `GET /piwebapi/streams/{webId}/value`
3. **Read history**
   - `GET /piwebapi/streams/{webId}/recorded?startTime=*-1h&endTime=*&maxCount=100`
4. **Write value**
   - `POST /piwebapi/streams/{webId}/value`
   - JSON body roughly: `{ "Timestamp": "...", "Value": 12.3 }`

All of that is wrapped by `PiWebApiClient`.

---

## 10. How to explain this project to your team / manager

You can say:

> We built a .NET sample that connects to AVEVA PI through PI Web API.  
> It can run offline in demo mode, then switch to a real PI server with config only.  
> The app resolves tags, reads current and historical values, and can write a value when permitted.  
> This is the foundation for UOC/AIS integrations that need plant historian data in .NET services.

If they ask “why not AF SDK first?”:

> Web API is faster to onboard for a newbie, works cleanly with modern .NET hosting, and matches how many enterprise integrations are done across firewalls. AF SDK remains available later for deeper AF/Windows scenarios.

---

## 11. Typical next steps after this sample

1. Replace demo tags with your real plant tags.
2. Add structured logging + retries/timeouts.
3. Map AF attribute paths (not only raw tags) if your site is AF-centric.
4. Bulk-read with StreamSets endpoints for many tags.
5. Persist selected values into your own SQL/API for UOC-V2 business logic.
6. Add authentication secrets via User Secrets / Azure Key Vault (do not commit passwords).
7. If you need thick AF features, evaluate AF SDK on the Windows hosts that will run it.

---

## 12. Troubleshooting cheat sheet

| Symptom | Likely cause | What to try |
|---|---|---|
| Demo works, live fails with SSL error | Bad/missing certificate trust | Install corp CA, or temporarily `AcceptInvalidCertificates` in lab |
| 401 / 403 | Auth or point security | Confirm AuthMode, account, PI point permissions |
| 404 on point | Wrong archive name or tag name | Verify path in PI System Explorer / PI SMT |
| Empty history | Tag exists but no recorded values in range | Widen `*-1d`, check if interface is sending data |
| Write fails | No write privilege / point is read-only / wrong annotation settings | Ask PI admin; many tags are intentionally not writable from apps |
| Timeout | Network / firewall / wrong BaseUrl | Open browser to `https://.../piwebapi` and confirm the landing page |

Useful manual check: open the PI Web API home page in a browser while logged into the same machine/account.

---

## 13. AF SDK mini-overview (so you recognize it later)

When someone shows AF SDK code, it often looks like:

```csharp
// Conceptual AF SDK example (not used by this sample)
var servers = new OSIsoft.AF.PI.PIServers();
var server = servers.DefaultPIServer;
server.Connect();

var point = OSIsoft.AF.PI.PIPoint.FindPIPoint(server, "SINUSOID");
var value = point.CurrentValue;
Console.WriteLine($"{value.Timestamp}: {value.Value}");
```

Same idea as this sample (connect → find tag → read value), but through a native library instead of HTTP.

Official learning path:

- [AF SDK Getting Started (AVEVA docs)](https://docs.aveva.com/bundle/af-sdk-getting-started/page/1011016.html)
- [AVEVA AF SDK Getting Started Guide on GitHub](https://github.com/AVEVA/AF-SDK-Getting-Started-Guide)

---

## 14. Glossary (keep this nearby)

- **PI / PI System** — AVEVA’s industrial historian platform
- **Data Archive** — the tag historian server
- **PI Point / Tag** — one time-series signal
- **Snapshot** — latest value
- **Recorded values** — archived history
- **AF** — Asset Framework (asset hierarchy + attributes)
- **Attribute** — AF property that often references a PI Point
- **Event Frame** — time-bounded event (batch, downtime, excursion)
- **PI Web API** — REST interface to PI/AF
- **AF SDK** — .NET SDK for PI/AF
- **WebId** — PI Web API object identifier
- **Interface** — collector that feeds PI from OT systems
- **UOM** — unit of measure

---

## 15. What “happened” in this sample (story you can retell)

1. We treated PI as a **time-series plant database**, not a normal SQL app DB.
2. We chose **PI Web API** as the first integration path for a newbie-friendly .NET app.
3. We built a small clean architecture:
   - `IPiDataClient` = what the app needs
   - `DemoPiDataClient` = learn offline
   - `PiWebApiClient` = talk to real PI
4. The console app demonstrates the four core operations every PI integration needs:
   - connect/status
   - find tag
   - read now + history
   - write (when allowed)
5. Configuration is externalized so switching from demo → live does not require rewriting code.

That is the whole journey from zero PI knowledge to a working .NET integration skeleton.

---

## Next: Tag Intelligence Assistant + local/cloud AI

This repo also includes a **Tag Intelligence API** that follows the safe pattern:

> LLM chooses tools → .NET services query PI/catalog → model only explains returned facts.

Current stack highlights:

- Target framework: **.NET 10**
- API explorer: **Scalar** (`/scalar/v1`)
- Chat: Tag Assistant tools via **Ollama**, plus optional raw **Qwen Online** (DashScope) smoke-test endpoint

See [TAG-INTELLIGENCE-ASSISTANT.md](TAG-INTELLIGENCE-ASSISTANT.md).
