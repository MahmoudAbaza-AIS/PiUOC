# PiAiAssistant: Complete Project Walkthrough

## Overview

**PiAiAssistant** is a role-driven decision assistant powered by AI that integrates with **AVEVA PI System** for industrial data intelligence. The app uses **clean architecture** with clear separation of concerns: Domain, Application, Infrastructure, and API layers.

**Technology Stack:**
- **.NET 10** (latest framework)
- **ASP.NET Core** for the Web API
- **Entity Framework Core** with SQLite for metadata caching
- **OpenAI + Ollama** for LLM capabilities
- **Scalar/OpenAPI** for interactive API documentation

---

## Project Structure

### High-Level Architecture

```
┌────────────────────────────────────────────────────────────────┐
│                        API Layer                                │
│  (Controllers, HTTP routing, static files, authentication)      │
│  PiAiAssistant.Api                                              │
└────────────────────────────────────────────────────────────────┘
						   │
						   ▼
┌────────────────────────────────────────────────────────────────┐
│                    Application Layer                            │
│  (Use cases, business logic, ports/interfaces)                 │
│  PiAiAssistant.Application                                      │
│  - Tag Intelligence (search, resolve, get history/charts)      │
│  - Chat Assistants (OpenAI, Ollama, DeepSeek)                  │
│  - Briefings (role-driven business context)                    │
└────────────────────────────────────────────────────────────────┘
						   │
						   ▼
┌────────────────────────────────────────────────────────────────┐
│                   Domain Layer (Entities & Ports)              │
│  (Pure business rules, no dependencies on frameworks)          │
│  PiAiAssistant.Domain                                           │
│  - Entities: PiPoint, AfAttribute, TagSample                   │
│  - Ports: IPiConnectivity, IPiPointReader, ITagValueReader    │
│  - Enums: TagResolutionStatus, PiAuthenticationMode            │
└────────────────────────────────────────────────────────────────┘
						   │
						   ▼
┌────────────────────────────────────────────────────────────────┐
│                  Infrastructure Layer                           │
│  (Implements Domain ports, external services)                  │
│  PiAiAssistant.Infrastructure                                   │
│  - PiWebApiDataSource (real PI Web API client)                 │
│  - DemoPiDataSource (offline mock for dev/demo)                │
│  - TagCatalogRepository (SQLite EF Core)                       │
│  - Chat Assistants (TagAssistant, RawChatService)              │
└────────────────────────────────────────────────────────────────┘
```

---

## Layer 1: Domain Layer (`src/PiAiAssistant.Domain`)

The domain layer contains **core business entities and port definitions**. It has **no external dependencies** (no NuGet packages, no HttpClient, no databases).

### Files:

#### 📄 `Entities/PiEntities.cs`
**Purpose:** Defines data transfer objects (DTOs) for PI System metadata and values.

**Key Classes:**
- **`PiPoint`**: Represents a PI Point (historian tag) with metadata.
  - Properties: Name, WebId, Path, PointType, EngineeringUnits, etc.
  - Immutable (init-only properties) for thread-safety.

- **`AfAttribute`**: Represents an AVEVA AF (Asset Framework) attribute.
  - Maps to PI Points via ConfigString or ElementPath.
  - Properties: Name, WebId, Path, TypeName, DefaultUnitsName, etc.

- **`TagSample`**: One timestamped value from a tag.
  - Properties: TimestampUtc, Value, IsGood, IsQuestionable, Status.
  - Used for current values and historical series.

- **`PiSearchHit`**: Lightweight search result from PI or catalog.
  - Properties: Name, Path, WebId, Description, Kind.

---

#### 📄 `Enums/PiEnums.cs`
**Purpose:** Defines enumerations for tag resolution and authentication.

**Key Enums:**
- **`TagResolutionStatus`**:
  - `Found`: Tag successfully resolved.
  - `NotFound`: No matching tag.
  - `Ambiguous`: Multiple matches (user must choose).
  - `Forbidden`: Access denied.
  - `Error`: Unexpected failure.

- **`PiAuthenticationMode`**:
  - `Demo`, `Anonymous`, `Basic`, `Windows`, `DefaultCredentials`, `NetworkCredential`, `Bearer`.
  - Selects how the app connects to PI Web API.

---

#### 📄 `Interfaces/PiPorts.cs`
**Purpose:** Defines abstract interfaces (ports) that the Infrastructure layer must implement.

**Key Interfaces:**

- **`IPiConnectivity`**: Probe connectivity to PI Web API.
  ```csharp
  Task<bool> TryConnectAsync(CancellationToken ct);
  Task<string> GetSystemStatusAsync(CancellationToken ct);
  ```
  - Implemented by: `PiWebApiDataSource`, `DemoPiDataSource`.

- **`IPiPointReader`**: Search and resolve PI Points.
  ```csharp
  Task<PiPoint?> FindByNameAsync(string tagName, CancellationToken ct);
  Task<PiPoint?> FindPointByPathAsync(string path, CancellationToken ct);
  Task<IReadOnlyList<PiSearchHit>> SearchByNameAsync(string nameFilter, int maxCount = 10, CancellationToken ct);
  ```
  - Implemented by: `PiWebApiDataSource`, `DemoPiDataSource`.

- **`ITagValueReader`**: Get current and historical values.
  ```csharp
  Task<TagSample?> GetCurrentByWebIdAsync(string webId, CancellationToken ct);
  Task<TagSample?> GetCurrentByNameAsync(string tagName, CancellationToken ct);
  Task<IReadOnlyList<TagSample>> GetRecordedByWebIdAsync(string webId, string startTime = "*-1h", string endTime = "*", int maxCount = 100, CancellationToken ct);
  Task<IReadOnlyList<TagSample>> GetRecordedByNameAsync(string tagName, string startTime = "*-1h", string endTime = "*", int maxCount = 100, CancellationToken ct);
  ```
  - Implemented by: `PiWebApiDataSource`, `DemoPiDataSource`.

- **`IAfAttributeReader`**: Resolve AF attributes by path.
  ```csharp
  Task<AfAttribute?> FindAttributeByPathAsync(string path, CancellationToken ct);
  ```
  - Implemented by: `PiWebApiDataSource`, `DemoPiDataSource`.

---

#### 📄 `Exceptions/DomainExceptions.cs`
**Purpose:** Custom exceptions for domain-level errors.

Common exceptions: `TagNotResolvedException`, `PiConnectionException`, `InvalidTagReferenceException`.

---

## Layer 2: Application Layer (`src/PiAiAssistant.Application`)

The application layer contains **use cases, business logic, and application-level ports**. It depends on the Domain layer but not on the Infrastructure layer.

### Files:

#### 📄 `Abstractions/IMetadataCache.cs`
**Purpose:** Application-level cache abstraction.

```csharp
public interface IMetadataCache
{
	bool TryGet<T>(string key, out T? value);
	void Set<T>(string key, T value, TimeSpan ttl);
}
```
- **Implemented by:** `MemoryMetadataCache` in Infrastructure.
- **Used for:** Caching tag metadata and resolution results to reduce redundant PI queries.

---

#### 📄 `Chat/ChatPorts.cs`
**Purpose:** Defines chat-related request/response types and interfaces.

**Key Records & Interfaces:**

- **`ChatAskRequest`**: User input to the assistant.
  ```csharp
  record ChatAskRequest(string Message, string? ConversationId = null);
  ```

- **`ChatAskResponse`**: AI-generated response with metadata.
  ```csharp
  record ChatAskResponse(
	  string ConversationId,
	  string Answer,
	  IReadOnlyList<ToolTraceItem> ToolTrace,
	  IReadOnlyList<ChatSource> Sources,
	  VisualizationPayload? Visualization = null);
  ```

- **`ITagAssistant`**: Role-driven decision assistant with PI tools.
  ```csharp
  Task<ChatAskResponse> AskAsync(ChatAskRequest request, CancellationToken ct);
  ```
  - Implemented by: `TagAssistant` in Infrastructure.

- **`IRawChatService`**: LLM smoke-test (no PI tools).
  ```csharp
  Task<ChatAskResponse> AskAsync(ChatAskRequest request, CancellationToken ct);
  ```
  - Implemented by: `RawChatService` (Ollama, DeepSeek, QwenOnline).

- **`IChatAuditStore`**: Optional conversation logging.
  ```csharp
  Task AppendAsync(string conversationId, string userMessage, string? assistantAnswer, string toolsUsedJson, int durationMs, CancellationToken ct);
  ```

---

#### 📄 `Tags/TagPorts.cs`
**Purpose:** Interfaces and DTOs for tag intelligence (search, resolve, fetch history).

**Key Interfaces:**

- **`ITagResolver`**: Resolve ambiguous user input to a specific tag.
  ```csharp
  Task<TagResolutionResult> ResolveAsync(string query, CancellationToken ct);
  ```
  - Implemented by: `TagResolver`.

- **`ITagIntelligenceService`**: Comprehensive tag data queries.
  ```csharp
  Task<TagDetailsResult> GetTagDetailsAsync(string tagReference, TagDetailsQueryOptions? options = null, CancellationToken ct);
  Task<IReadOnlyList<TagCandidate>> SearchTagsAsync(string query, int maxResults = 10, CancellationToken ct);
  Task<TagHistoryResult> GetTagHistoryAsync(string tagReference, DateTimeOffset? startUtc = null, DateTimeOffset? endUtc = null, int maxCount = 100, CancellationToken ct);
  Task<TagChartResult> GetChartSeriesAsync(string tagReference, DateTimeOffset? startUtc = null, DateTimeOffset? endUtc = null, int maxCount = 200, CancellationToken ct);
  Task<TagSummaryResult> GetSummaryAsync(string tagReference, DateTimeOffset? startUtc = null, DateTimeOffset? endUtc = null, int maxCount = 500, CancellationToken ct);
  Task<MultiTagChartResult> CompareTagsAsync(IReadOnlyList<string> tagReferences, DateTimeOffset? startUtc = null, DateTimeOffset? endUtc = null, int maxCount = 150, CancellationToken ct);
  Task<IReadOnlyList<RelatedTagDto>> GetRelatedTagsAsync(string tagReference, CancellationToken ct);
  Task<IReadOnlyList<TagCandidate>> ListCatalogTagsAsync(int maxResults = 50, CancellationToken ct);
  ```

- **`ITagCatalogRepository`**: Access curated business metadata in SQLite.
  ```csharp
  Task<TagCatalogItem?> FindByAliasAsync(string alias, CancellationToken ct);
  Task<TagCatalogItem?> FindByCanonicalNameAsync(string canonicalName, CancellationToken ct);
  Task<IReadOnlyList<TagCatalogItem>> SearchAsync(string query, int maxResults, CancellationToken ct);
  Task<IReadOnlyList<TagCatalogItem>> ListEnabledAsync(int maxResults = 200, CancellationToken ct);
  Task<IReadOnlyList<TagRelationshipItem>> GetRelationshipsAsync(string canonicalName, CancellationToken ct);
  Task<IReadOnlyList<TagDocumentationItem>> GetDocumentationAsync(string canonicalName, CancellationToken ct);
  ```

---

#### 📄 `Tags/TagResolver.cs`
**Purpose:** Core business logic for resolving ambiguous user input to a specific tag.

**Flow:**
1. Check cache for previous resolution.
2. Try exact alias match in catalog.
3. Try exact canonical name match in catalog.
4. Try fuzzy search in catalog.
5. If looks like AF path, query AF reader.
6. Try PI Point by name.
7. Return ambiguous list if multiple candidates.
8. Cache successful resolution.

**Key Method:**
```csharp
Task<TagResolutionResult> ResolveAsync(string query, CancellationToken ct);
```

---

#### 📄 `Tags/TagIntelligenceService.cs`
**Purpose:** Comprehensive tag data queries (history, charts, summaries, comparisons).

**Key Methods:**
- `GetTagDetailsAsync()`: Fetch full metadata, current value, recent history, related tags.
- `SearchTagsAsync()`: Find tags by keyword.
- `GetTagHistoryAsync()`: Fetch recorded values over a time range.
- `GetChartSeriesAsync()`: Format history for charting.
- `CompareTagsAsync()`: Multi-tag comparison.
- `GetSummaryAsync()`: Compute min/max/avg over a time range.

---

#### 📄 `Tags/TagDtos.cs`
**Purpose:** Data transfer objects for tag query results.

**Key DTOs:**
- `TagCandidate`: Search result candidate.
- `TagCatalogItem`: Curated metadata from SQLite.
- `TagDetailsResult`: Response from GetTagDetailsAsync.
- `TagHistoryResult`: Response from GetTagHistoryAsync.
- `TagChartResult`: Formatted for charting.
- etc.

---

#### 📄 `DependencyInjection.cs`
**Purpose:** Registers Application layer services.

```csharp
services.AddApplication();
```
Registers:
- `TagResolver`
- `TagIntelligenceService`
- `TagAssistant`
- etc.

---

## Layer 3: Infrastructure Layer (`src/PiAiAssistant.Infrastructure`)

The infrastructure layer implements the domain/application ports and integrates with external systems.

### Files:

#### 📄 `Options/InfrastructureOptions.cs`
**Purpose:** Configuration classes for PI, Ollama, and QwenOnline.

**Key Classes:**

- **`PiConnectionOptions`**:
  ```csharp
  public bool UseDemoMode { get; set; } = true;
  public string BaseUrl { get; set; } = "https://localhost/piwebapi";
  public string AuthMode { get; set; } = "Demo";
  public string? Username { get; set; }
  public string? Password { get; set; }
  public int TimeoutSeconds { get; set; } = 30;
  public string[] SampleTagNames { get; set; } = [];
  ```
  - Source: `appsettings.json` → `PiConnection` section.

- **`OllamaOptions`**:
  ```csharp
  public string BaseUrl { get; set; } = "http://localhost:11434/";
  public string DefaultModel { get; set; } = "qwen3:8b";
  public bool Enabled { get; set; } = true;
  public Dictionary<string, string> Models { get; set; } // e.g., "qwen" → "qwen3:8b"
  ```
  - Source: `appsettings.json` → `Ollama` section.

- **`QwenOnlineOptions`**:
  ```csharp
  public string ApiKey { get; set; } = "";
  public string Model { get; set; } = "qwen-max";
  public bool Enabled { get; set; } = false;
  ```

---

#### 📄 `Pi/DemoPiDataSource.cs`
**Purpose:** Offline mock implementation of PI ports for development and demos.

**Key Features:**
- Implements: `IPiConnectivity`, `IPiPointReader`, `ITagValueReader`, `IAfAttributeReader`.
- Preloaded demo data: SINUSOID, CDT158, BA:LEVEL.1, Boiler steam/temperature tags, etc.
- Generates synthetic time series (sine waves).
- No network calls; always available.

**Key Methods:**
```csharp
Task<bool> TryConnectAsync(CancellationToken ct); // Always returns true
Task<PiPoint?> FindByNameAsync(string tagName, CancellationToken ct);
Task<IReadOnlyList<TagSample>> GetRecordedByNameAsync(string tagName, string startTime = "*-1h", string endTime = "*", int maxCount = 100, CancellationToken ct);
```

---

#### 📄 `Pi/PiWebApiDataSource.cs`
**Purpose:** Real PI Web API client (HTTP calls to AVEVA PI Web API).

**Key Features:**
- Implements: `IPiConnectivity`, `IPiPointReader`, `ITagValueReader`, `IAfAttributeReader`.
- Uses `HttpClient` to query PI Web API endpoints.
- Handles authentication (Basic, Windows, Bearer).
- Parses JSON responses into Domain entities.
- Caches results to reduce redundant calls.

**Key Methods:**
```csharp
Task<bool> TryConnectAsync(CancellationToken ct); // Checks PI home endpoint
Task<PiPoint?> FindByNameAsync(string tagName, CancellationToken ct);
Task<IReadOnlyList<TagSample>> GetRecordedByWebIdAsync(string webId, string startTime = "*-1h", string endTime = "*", int maxCount = 100, CancellationToken ct);
```

---

#### 📄 `Catalog/AppDbContext.cs`
**Purpose:** Entity Framework Core DbContext for SQLite tag catalog.

**Key DbSets:**
- `Tags`: Curated tag metadata (canonical names, aliases, PI identities, business metadata).
- `Aliases`: Tag name aliases.
- `Relationships`: Tag-to-tag relationships (e.g., "B03_STEAM_PRESSURE" related to "B03_STEAM_TEMP").
- `Documentation`: Tag-specific documentation (instructions, warnings, etc.).

**Schema:**
```
Tags
├─ Id (PK)
├─ CanonicalName (unique, searchable)
├─ DisplayName
├─ PiPointName (synced from PI)
├─ PiWebId (synced from PI)
├─ AfAttributePath (AF path if applicable)
├─ DescriptionOverride
├─ EquipmentId, OwnerTeam, Criticality
├─ ExpectedMin, ExpectedMax, Unit
├─ IsSearchable, IsEnabled
└─ Aliases (1:many)

Relationships
├─ FromCanonicalName, ToCanonicalName, Relationship, Description

Documentation
├─ CanonicalName, Title, Body, Source
```

---

#### 📄 `Catalog/TagCatalogRepository.cs`
**Purpose:** Implements `ITagCatalogRepository` using EF Core.

**Key Methods:**
```csharp
Task<TagCatalogItem?> FindByAliasAsync(string alias, CancellationToken ct);
Task<TagCatalogItem?> FindByCanonicalNameAsync(string canonicalName, CancellationToken ct);
Task<IReadOnlyList<TagCatalogItem>> SearchAsync(string query, int maxResults, CancellationToken ct);
Task<IReadOnlyList<TagRelationshipItem>> GetRelationshipsAsync(string canonicalName, CancellationToken ct);
Task<IReadOnlyList<TagDocumentationItem>> GetDocumentationAsync(string canonicalName, CancellationToken ct);
```
- All queries are no-tracked (read-only).
- Uses LIKE wildcards for substring search.

---

#### 📄 `Catalog/TagCatalogSeeder.cs`
**Purpose:** Initializes the SQLite database with demo/production tag metadata.

**Key Methods:**
```csharp
Task SeedAsync(AppDbContext db, CancellationToken ct);
Task EnsureNuGreenCatalogAsync(AppDbContext db, CancellationToken ct);
```
- Runs at app startup via `InitializeInfrastructureAsync()`.
- Seeds tags like `B03_STEAM_PRESSURE`, `B04_STEAM_PRESSURE`, etc.
- Links catalog entries to PI identities and AF paths.

---

#### 📄 `AI/TagAssistant.cs`
**Purpose:** Implements `ITagAssistant` — uses an LLM (Ollama) to answer questions about tags.

**Flow:**
1. User asks a question (e.g., "What is the steam pressure at Boiler 3?").
2. TagAssistant infers intent and searches for related tags.
3. Fetches current/recent values via `ITagValueReader`.
4. Sends context + user question to Ollama.
5. Returns AI answer + tool trace (which tags were queried).

**Key Method:**
```csharp
Task<ChatAskResponse> AskAsync(ChatAskRequest request, CancellationToken ct);
```

---

#### 📄 `Caching/MemoryMetadataCache.cs`
**Purpose:** Implements `IMetadataCache` using `IMemoryCache`.

```csharp
bool TryGet<T>(string key, out T? value);
void Set<T>(string key, T value, TimeSpan ttl);
```
- Lightweight wrapper around ASP.NET Core's `IMemoryCache`.
- Default TTL: 15 minutes.

---

#### 📄 `DependencyInjection.cs`
**Purpose:** Registers Infrastructure services.

**Registration Logic:**
```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
{
	// Register options
	services.AddOptions<PiConnectionOptions>().Bind(configuration.GetSection("PiConnection"));
	services.AddOptions<OllamaOptions>().Bind(configuration.GetSection("Ollama"));

	// Conditional: Demo vs Real PI
	if (pi.UseDemoMode)
	{
		services.AddSingleton<DemoPiDataSource>();
		services.AddSingleton<IPiConnectivity>(sp => sp.GetRequiredService<DemoPiDataSource>());
		// ... (other ports pointing to DemoPiDataSource)
	}
	else
	{
		services.AddHttpClient<PiWebApiDataSource>(...);
		services.AddScoped<IPiConnectivity>(sp => sp.GetRequiredService<PiWebApiDataSource>());
		// ... (other ports pointing to PiWebApiDataSource)
	}

	// Database
	services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString));
	services.AddScoped<ITagCatalogRepository, TagCatalogRepository>();

	// Chat
	services.AddSingleton<IChatClient>(sp => CreateOllamaChatClient(...));
	services.AddScoped<ITagAssistant, TagAssistant>();

	return services;
}
```

---

## Layer 4: API Layer (`src/PiAiAssistant.Api`)

The API layer exposes the business logic via HTTP endpoints.

### Files:

#### 📄 `Program.cs`
**Purpose:** ASP.NET Core app entry point and middleware pipeline.

**Key Setup:**
```csharp
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.ConfigureHttpJsonOptions(o =>
	o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddControllers(); // <-- Controllers (recent change)
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();
await app.Services.InitializeInfrastructureAsync(); // Seed DB, log startup

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
	app.MapOpenApi(); // /openapi/v1.json
	app.MapScalarApiReference(); // /scalar/v1
}

app.MapControllers(); // <-- Route all controllers

app.Run();
```

**Middleware Stack:**
1. Static files (CSS, JS, HTML in `wwwroot/`).
2. OpenAPI (dev only).
3. Controller routing.

---

#### 📄 `Controllers/` (New structure)
**Purpose:** HTTP controllers expose application services.

**Example: TagsController**
```csharp
[ApiController]
[Route("api/tags")]
public sealed class TagsController : ControllerBase
{
	private readonly ITagIntelligenceService _svc;

	[HttpGet("search")]
	public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int? maxResults, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(q))
			return BadRequest(new { message = "Query parameter 'q' is required." });
		return Ok(await _svc.SearchTagsAsync(q, maxResults ?? 10, ct));
	}

	[HttpGet("history")]
	public async Task<IActionResult> History([FromQuery] string reference, [FromQuery] DateTimeOffset? startUtc, [FromQuery] DateTimeOffset? endUtc, [FromQuery] int? maxCount, CancellationToken ct)
	{
		// ... fetch and return tag history
	}

	[HttpGet("{tagReference}")]
	public async Task<IActionResult> GetTag(string tagReference, [FromQuery] bool? includeCurrentValue, ..., CancellationToken ct)
	{
		// ... fetch tag details
	}
}
```

**Example: ChatController**
```csharp
[ApiController]
[Route("api/chat")]
public sealed class ChatController : ControllerBase
{
	[HttpPost]
	public async Task<IActionResult> AskDecisionAssistant([FromBody] ChatAskRequest request, [FromServices] IDecisionAssistant assistant, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(request.Message))
			return BadRequest(new { message = "message is required" });
		return Ok(await assistant.AskAsync(request, ct));
	}

	[HttpPost("ollama")]
	public async Task<IActionResult> ChatOllama([FromBody] ChatAskRequest request, [FromKeyedServices("ollama")] IRawChatService chatService, CancellationToken ct)
	{
		// ... LLM call
	}
}
```

**Other Controllers:**
- `HealthController`: `/health`, `/health/pi`, `/health/ollama` endpoints.
- `FleetController`: `/api/fleet` endpoints (semantic hierarchy: Kingdom → Sectors → Plants → Units).
- `BriefingsController`: `/api/briefings` endpoints (role-driven briefings).
- `HomeController`: Root `/` redirect.

---

#### 📄 `appsettings.json`
**Purpose:** Production configuration (default fallback).

```json
{
  "Logging": {
	"LogLevel": {
	  "Default": "Information",
	  "Microsoft.AspNetCore": "Warning"
	}
  },
  "ConnectionStrings": {
	"TagCatalog": "Data Source=tag-catalog.db"
  },
  "PiConnection": {
	"UseDemoMode": false,
	"BaseUrl": "https://your-real-pi-network-host/piwebapi",
	"AuthMode": "Anonymous",
	"TimeoutSeconds": 30,
	"SampleTagNames": [...]
  },
  "Ollama": {
	"Enabled": true,
	"BaseUrl": "http://localhost:11434/",
	"DefaultModel": "qwen3:8b",
	"Models": { "qwen": "qwen3:8b", "deepseek": "deepseek-r1:5b" }
  },
  "QwenOnline": {
	"Enabled": false,
	"ApiKey": "",
	"Model": "qwen-max"
  }
}
```

---

#### 📄 `appsettings.Development.json`
**Purpose:** Development-specific overrides (auto-loaded when `ASPNETCORE_ENVIRONMENT=Development`).

```json
{
  "Logging": {
	"LogLevel": {
	  "Default": "Debug",
	  "Microsoft.AspNetCore": "Information"
	}
  },
  "PiConnection": {
	"UseDemoMode": false,
	"BaseUrl": "http://localhost:5000/piwebapi",
	"AuthMode": "Anonymous",
	"_comment": "Development uses local PI WebAPI emulator."
  },
  "Ollama": {
	"Enabled": true,
	"BaseUrl": "http://localhost:11434/",
	"DefaultModel": "qwen3:8b"
  }
}
```

---

#### 📄 `appsettings.Production.json`
**Purpose:** Production-specific configuration (auto-loaded when `ASPNETCORE_ENVIRONMENT=Production`).

```json
{
  "PiConnection": {
	"UseDemoMode": false,
	"BaseUrl": "https://your-real-pi-network-host/piwebapi",
	"DataArchiveName": "PIServer1",
	"DefaultAfServer": "AFServer1",
	"DefaultAfDatabase": "NuGreen",
	"AuthMode": "Anonymous",
	"TimeoutSeconds": 30
  },
  "Ollama": {
	"Enabled": true,
	"BaseUrl": "http://localhost:11434/",
	"DefaultModel": "qwen3:8b"
  }
}
```

---

#### 📄 `Properties/launchSettings.json`
**Purpose:** Visual Studio launch profiles.

```json
{
  "profiles": {
	"PiAiAssistant.Api": {
	  "commandName": "Project",
	  "dotnetRunMessages": true,
	  "launchBrowser": true,
	  "launchUrl": "scalar/v1",
	  "applicationUrl": "https://localhost:7001;http://localhost:5000",
	  "environmentVariables": {
		"ASPNETCORE_ENVIRONMENT": "Development"
	  }
	}
  }
}
```

---

#### 📄 `wwwroot/app/` (Frontend)
**Purpose:** Simple SPA for testing tag queries and chat.

**Files:**
- `index.html`: HTML structure.
- `app.js`: JavaScript logic (fetch API calls, event handlers).
- `styles.css`: CSS styling.

**Features:**
- Search tags.
- View tag history and charts.
- Chat with assistants.
- Real-time health status.

---

### Database Files:

#### 📄 `tag-catalog.db`
**Purpose:** SQLite database (created at runtime).

**Tables:**
- `Tags`, `Aliases`, `Relationships`, `Documentation`.
- Seeded by `TagCatalogSeeder` at startup.

---

## Key Patterns & Concepts

### 1. **Dependency Injection (DI) Strategy**

The app uses ASP.NET Core's built-in DI container.

**Key Registrations:**
```
Domain Ports (interfaces)
  ↓ (implemented by)
Infrastructure Implementations (DemoPiDataSource, PiWebApiDataSource, etc.)
  ↓ (injected into)
Application Services (TagResolver, TagIntelligenceService, TagAssistant)
  ↓ (injected into)
API Controllers (TagsController, ChatController, etc.)
```

**Example:**
```csharp
services.AddScoped<IPiConnectivity>(sp => sp.GetRequiredService<PiWebApiDataSource>());
// Later, in TagAssistant:
public TagAssistant(IPiConnectivity pi, ITagValueReader values, ...)
{
	// pi is automatically resolved to PiWebApiDataSource (or DemoPiDataSource if demo mode)
}
```

---

### 2. **Demo vs. Real PI Strategy**

At **composition root** (`DependencyInjection.cs`), the app decides:
- If `PiConnection.UseDemoMode == true` → Register `DemoPiDataSource`.
- Otherwise → Register `PiWebApiDataSource`.

**No code in the middle layers knows which one is active**—they just depend on the interface (`IPiConnectivity`).

This enables:
- Local development without a PI server.
- Seamless switching to production PI.
- Easy testing with mocks.

---

### 3. **Tag Resolution Pipeline**

When a user queries a tag (e.g., "B03_STEAM_PRESSURE"):

1. **TagResolver** checks cache.
2. Tries exact alias match in SQLite catalog.
3. Tries exact canonical name match.
4. Tries fuzzy search in catalog.
5. If it looks like an AF path, queries AF reader (PI).
6. Tries direct PI Point lookup.
7. Returns ambiguous list if multiple candidates.
8. Caches result.

**Result:** Unambiguous `ResolvedTag` with canonical name, PI WebId, AF path.

---

### 4. **Chat Assistant Pipeline**

When a user asks a question (e.g., "What's the temperature at Boiler 3?"):

1. **TagAssistant** parses intent.
2. Searches for related tags (e.g., "B03_STEAM_TEMP").
3. Fetches current value via `ITagValueReader`.
4. Sends to Ollama LLM: "User asked: ... [tag data]. Answer: ..."
5. Ollama returns structured answer.
6. Returns `ChatAskResponse` with answer + tool trace.

---

### 5. **Configuration Hierarchy**

- **Base:** `appsettings.json`
- **Environment:** `appsettings.{ASPNETCORE_ENVIRONMENT}.json` (Development, Production)
- **User Secrets:** (Optional, not checked in)
- **Environment Variables:** (Optional, override all)

**Load Order:**
```
Base (appsettings.json)
  ↓ (merged by)
Environment (appsettings.Development.json or appsettings.Production.json)
  ↓ (overridden by)
Env Vars (PICONNECTION_BASEURL, etc.)
```

---

## Common Workflows

### **Workflow 1: Search for a Tag**
```
User Input: "steam"
  ↓
TagsController.Search() 
  ↓
ITagIntelligenceService.SearchTagsAsync()
  ↓
ITagCatalogRepository.SearchAsync() (SQLite)
  ↓
Returns: [TagCandidate, TagCandidate, ...]
  ↓
HTTP 200 + JSON
```

---

### **Workflow 2: Get Tag History & Chart**
```
User Input: "B03_STEAM_PRESSURE", startUtc: "2025-01-01", endUtc: "2025-01-31"
  ↓
TagsController.Chart()
  ↓
ITagIntelligenceService.GetChartSeriesAsync()
  ↓
TagResolver.ResolveAsync() → "B03_STEAM_PRESSURE" (canonical)
  ↓
ITagValueReader.GetRecordedByNameAsync() (PI Web API or Demo)
  ↓
Aggregate into ChartSeriesPayload (1 series, N points)
  ↓
HTTP 200 + JSON (Chart data ready for UI)
```

---

### **Workflow 3: Chat with TagAssistant**
```
User Input: ChatAskRequest("What is the current steam pressure?")
  ↓
ChatController.AskDecisionAssistant()
  ↓
ITagAssistant.AskAsync()
  ↓
TagAssistant internally:
  1. Infers intent: "steam pressure"
  2. Resolves tag: "B03_STEAM_PRESSURE"
  3. Fetches value: 45.2 bar, @ 2025-01-15 14:30 UTC
  4. Sends to Ollama: "Current steam pressure is 45.2 bar."
  ↓
Returns: ChatAskResponse(
	Answer: "Current steam pressure at Boiler 03 is 45.2 bar.",
	ToolTrace: [{ Tool: "TagSearch", Status: "Success", DurationMs: 25 }],
	Sources: [{ Type: "Tag", CanonicalTagName: "B03_STEAM_PRESSURE", WebId: "..." }]
  )
  ↓
HTTP 200 + JSON
```

---

## Testing Entry Points

### **Unit Tests** (`tests/PiAiAssistant.Tests/`)
- Mock `IPiConnectivity`, `IPiPointReader`, etc. with test doubles.
- Test `TagResolver`, `TagIntelligenceService` in isolation.
- Example: `BriefingServiceTests.cs`.

### **Integration Tests** (Future)
- Use `WebApplicationFactory` to spin up test server.
- Call real endpoints with test configuration (demo PI).
- Verify end-to-end workflows.

### **Manual Testing**
1. Launch app: `dotnet run --project src/PiAiAssistant.Api`.
2. Open Scalar UI: `https://localhost:7001/scalar/v1`.
3. Try endpoints:
   - `GET /health/pi` (check PI connection).
   - `GET /api/tags/search?q=steam&maxResults=10` (search).
   - `POST /api/chat` (ask assistant).

---

## Environment Variables & Secrets

### **Development:**
```bash
set ASPNETCORE_ENVIRONMENT=Development
# appsettings.Development.json is loaded
# PI: http://localhost:5000/piwebapi (demo emulator or local)
# Ollama: http://localhost:11434/
```

### **Production:**
```bash
set ASPNETCORE_ENVIRONMENT=Production
# appsettings.Production.json is loaded
# PI: https://your-pi-server/piwebapi (real production server)
# Ollama: configured endpoint
```

### **Secrets (Optional):**
```bash
dotnet user-secrets init
dotnet user-secrets set "PiConnection:Username" "domain\user"
dotnet user-secrets set "PiConnection:Password" "secret123"
```

---

## File Tree Summary

```
src/
├── PiAiAssistant.Domain/
│   ├── Entities/PiEntities.cs
│   ├── Enums/PiEnums.cs
│   ├── Interfaces/PiPorts.cs
│   └── Exceptions/DomainExceptions.cs
├── PiAiAssistant.Application/
│   ├── Abstractions/IMetadataCache.cs
│   ├── Chat/ChatPorts.cs
│   ├── Tags/TagPorts.cs
│   ├── Tags/TagResolver.cs
│   ├── Tags/TagIntelligenceService.cs
│   ├── Tags/TagDtos.cs
│   └── DependencyInjection.cs
├── PiAiAssistant.Infrastructure/
│   ├── Options/InfrastructureOptions.cs
│   ├── Pi/DemoPiDataSource.cs
│   ├── Pi/PiWebApiDataSource.cs
│   ├── Catalog/AppDbContext.cs
│   ├── Catalog/TagCatalogRepository.cs
│   ├── Catalog/TagCatalogSeeder.cs
│   ├── AI/TagAssistant.cs
│   ├── Caching/MemoryMetadataCache.cs
│   └── DependencyInjection.cs
├── PiAiAssistant.Api/
│   ├── Program.cs
│   ├── Controllers/
│   │   ├── HomeController.cs
│   │   ├── HealthController.cs
│   │   ├── FleetController.cs
│   │   ├── BriefingsController.cs
│   │   ├── TagsController.cs
│   │   └── ChatController.cs
│   ├── wwwroot/app/
│   │   ├── index.html
│   │   ├── app.js
│   │   └── styles.css
│   ├── Properties/launchSettings.json
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── appsettings.Production.json
│   └── PiAiAssistant.Api.http
├── AvevaPi.ConsoleApp/
│   └── Program.cs (CLI tool, separate from API)
tests/
└── PiAiAssistant.Tests/
	└── [unit test files]
```

---

## Quick Answer Guide

**Q: Where do I find the tag search logic?**  
A: `TagResolver.cs` in Application layer. It orchestrates catalog lookup, PI queries, and caching.

**Q: How does the app connect to real PI?**  
A: `PiWebApiDataSource.cs` makes HTTP calls to PI Web API. Registered conditionally in `DependencyInjection.cs` if `UseDemoMode=false`.

**Q: What's the demo PI for?**  
A: `DemoPiDataSource.cs` is a mock implementation used during development. It generates synthetic sine-wave data for testing without a real PI server.

**Q: Where is the curated tag metadata (aliases, descriptions, etc.) stored?**  
A: SQLite database (`tag-catalog.db`), accessed via `TagCatalogRepository.cs` using Entity Framework.

**Q: How does the chat assistant work?**  
A: `TagAssistant.cs` infers intent, resolves tag names, fetches values, and sends context to Ollama LLM for a natural answer.

**Q: What's the difference between /api/tags and /api/chat?**  
A: `/api/tags` returns structured data (history, charts, metadata). `/api/chat` returns an AI-generated narrative answer with tool trace.

**Q: Can I run this without a PI server?**  
A: Yes! Set `PiConnection.UseDemoMode=true` in `appsettings.json`. Demo mode uses `DemoPiDataSource` (always available).

**Q: What LLM providers are supported?**  
A: Ollama (local, default), DeepSeek, QwenOnline (cloud). Configured in `appsettings.json`.

**Q: How is the database initialized?**  
A: `TagCatalogSeeder.cs` runs at app startup via `InitializeInfrastructureAsync()`. It seeds demo tags and ensures the schema exists.

---

## Next Steps for Learning

1. **Read the domain layer first** (`PiAiAssistant.Domain/`): Understand entities and ports.
2. **Read the application layer** (`PiAiAssistant.Application/`): Understand business logic (tag resolution, chat).
3. **Read the infrastructure layer** (`PiAiAssistant.Infrastructure/`): See how ports are implemented (PI, database, AI).
4. **Read the API layer** (`PiAiAssistant.Api/`): See how services are exposed via HTTP.
5. **Run locally**: Launch the app, open Scalar UI, and test endpoints.

---

## Summary

PiAiAssistant is a **clean-architecture web app** that brings AI to industrial data:
- **Domain layer** defines business rules (tag entities, ports).
- **Application layer** orchestrates use cases (tag resolution, chat, intelligence).
- **Infrastructure layer** plugs in real/mock PI, database, and AI.
- **API layer** exposes everything as HTTP endpoints and a SPA.

The architecture enables easy testing, swapping implementations (demo ↔ real), and adding new features (e.g., new LLM providers, new data sources).
