# Task - TASK_003

## Requirement Reference
- **User Story:** us_004
- **Story Location:** .propel/context/tasks/EP-TECH/us_004/us_004.md
- **Acceptance Criteria:**
  - AC-003: After any API endpoint call, a structured log event with `SourceContext`, `RequestPath`, `StatusCode`, and `Elapsed` properties appears in Seq within 5 seconds, searchable via `http://localhost:5341`
  - AC-004: `IAuditLogger.Log(...)` writes a Seq event with `EventType: AuditLog`, `ActorId`, `ActionType`, `ResourceId`, and `OccurredAt` (UTC) properties within 2 seconds of the auditable action
- **Edge Cases:**
  - Seq not ready at API startup: If the Seq sink fails to connect, Serilog must buffer up to 500 events in memory and retry the connection — the API process must not crash and no log events must be silently dropped during the retry window

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## AI References
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

---

## Mobile References
| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version | Justification |
|-------|------------|---------|---------------|
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-002 (backend runtime) |
| Backend | Serilog | 3.x (latest stable) | TR-016 (structured log pipeline), NFR-006 (searchable audit log) |
| Backend | Serilog.Sinks.Seq | 6.x (latest stable) | TR-016 (Seq sink), NFR-006 (Seq as log aggregator) |
| Backend | Serilog.AspNetCore | 8.x (latest stable) | TR-016 (HTTP request logging enrichment) |
| Infrastructure | Seq | 2023.4+ | TR-016 (structured log aggregation), NFR-006 (audit log accessibility) |

---

## Task Overview

Integrate Serilog into the .NET 8 API (scaffolded in us_003) as the structured logging provider, replacing the default `ILogger` console sink. Configure the Seq sink in `appsettings.json` with an in-memory buffer of 500 events and durable retry so the API is resilient to Seq startup lag. Add `UseSerilogRequestLogging()` to the middleware pipeline to enrich every request with `RequestPath`, `StatusCode`, and `Elapsed` as first-class log properties. Define `IAuditLogger` and `AuditLoggerService` that write structured Serilog events with fixed audit-log property keys to Seq.

---

## Dependent Tasks
- task_001 (us_003) — `Program.cs` middleware pipeline and `appsettings.json` must exist before Serilog is wired in
- task_001 (us_001) — `docker-compose.yml` `seq` service must be running on port 5341

---

## Impacted Components
- `src/api/Api.csproj` — modified to add Serilog NuGet references
- `src/api/Program.cs` — modified to call `UseSerilog()` and `UseSerilogRequestLogging()`
- `src/api/appsettings.json` — modified to add `Serilog` configuration block with Seq sink
- `src/api/Services/IAuditLogger.cs` — new interface
- `src/api/Services/AuditLoggerService.cs` — new implementation writing fixed-schema audit events to Serilog

---

## Implementation Plan
1. Add NuGet packages to `Api.csproj`: `Serilog.AspNetCore` (8.x), `Serilog.Sinks.Seq` (6.x), `Serilog.Enrichers.Environment`, `Serilog.Enrichers.Thread`
2. In `Program.cs`, call `builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration))` before `builder.Build()` so configuration drives the sink setup
3. Add `app.UseSerilogRequestLogging(opts => { opts.EnrichDiagnosticContext = (dc, ctx) => { dc.Set("RequestPath", ctx.Request.Path); dc.Set("StatusCode", ctx.Response.StatusCode); }; })` in the middleware pipeline (before `UseRouting`)
4. In `appsettings.json`, add `"Serilog": { "Using": ["Serilog.Sinks.Seq"], "MinimumLevel": "Information", "WriteTo": [{ "Name": "Seq", "Args": { "serverUrl": "http://seq:5341", "bufferBaseFilename": null, "bufferSizeLimitBytes": null, "retainedInvalidPayloadsLimitBytes": null } }], "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"] }` — use `http://seq:5341` (Docker internal hostname)
5. Add in-memory resilience: set `"bufferQueueCapacity": 500` in the Seq sink args; Serilog's Seq sink buffers in-memory by default when the server is unreachable and retries automatically — confirm no additional explicit retry needed beyond the queue
6. Create `src/api/Services/IAuditLogger.cs` declaring `void Log(string actorId, string actionType, string resourceId)` returning `void`
7. Create `src/api/Services/AuditLoggerService.cs` implementing `IAuditLogger`; use `ILogger<AuditLoggerService>` to emit: `Log.ForContext("EventType", "AuditLog").ForContext("ActorId", actorId).ForContext("ActionType", actionType).ForContext("ResourceId", resourceId).Information("Audit event: {ActionType} on {ResourceId} by {ActorId} at {OccurredAt}", actionType, resourceId, actorId, DateTime.UtcNow)` — `OccurredAt` always UTC
8. Register `services.AddSingleton<IAuditLogger, AuditLoggerService>()` in `Program.cs`

---

## Current Project State
```
src/
└── api/
    ├── Api.csproj               (MODIFY — add Serilog NuGet refs)
    ├── Program.cs               (MODIFY — UseSerilog + UseSerilogRequestLogging)
    ├── appsettings.json         (MODIFY — add Serilog config block)
    └── Services/
        ├── IAuditLogger.cs      (CREATE)
        └── AuditLoggerService.cs (CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/api/Api.csproj | Add `Serilog.AspNetCore`, `Serilog.Sinks.Seq`, `Serilog.Enrichers.Environment`, `Serilog.Enrichers.Thread` NuGet references |
| MODIFY | src/api/Program.cs | Add `builder.Host.UseSerilog(...)` and `app.UseSerilogRequestLogging(...)` middleware |
| MODIFY | src/api/appsettings.json | Add `Serilog` configuration block with Seq sink pointing to `http://seq:5341` and in-memory buffer |
| CREATE | src/api/Services/IAuditLogger.cs | `IAuditLogger` interface with `void Log(string actorId, string actionType, string resourceId)` |
| CREATE | src/api/Services/AuditLoggerService.cs | Implementation emitting fixed-schema structured log events to Serilog with `EventType: AuditLog` and UTC `OccurredAt` |

---

## External References
- https://github.com/serilog/serilog-aspnetcore (Serilog.AspNetCore 8.x — UseSerilog, UseSerilogRequestLogging)
- https://github.com/serilog/serilog-sinks-seq (Serilog.Sinks.Seq 6.x — Seq sink configuration)
- https://docs.datalust.co/docs/getting-started-with-docker (Seq 2023.4 Docker setup — API key and ingestion port 5341)
- https://github.com/serilog/serilog/wiki/Enrichment (Serilog enrichers — FromLogContext, WithMachineName)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] `docker compose up --wait` then call `GET /api/health`; open Seq at `http://localhost:5341` and verify a log event with `RequestPath=/health`, `StatusCode=200`, and `Elapsed` fields appears within 5 seconds (AC-003)
- [ ] Inject `IAuditLogger` in a test controller action, call `Log("user1", "PatientDataAccess", "patient-42")`; verify Seq shows event with `EventType=AuditLog`, `ActorId=user1`, `ActionType=PatientDataAccess`, `ResourceId=patient-42`, `OccurredAt` in UTC format (AC-004)

---

## Implementation Checklist
- [ ] `Api.csproj` references `Serilog.AspNetCore` (8.x), `Serilog.Sinks.Seq` (6.x), `Serilog.Enrichers.Environment`, and `Serilog.Enrichers.Thread` (AC-003, AC-004)
- [ ] `builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration))` is called in `Program.cs` before `builder.Build()`, making `ILogger<T>` injection use Serilog (AC-003)
- [ ] `app.UseSerilogRequestLogging(...)` is placed before `UseRouting()` in the pipeline and enriches each request with `RequestPath`, `StatusCode`, and `Elapsed` via `EnrichDiagnosticContext` (AC-003)
- [ ] `appsettings.json` `Serilog.WriteTo` entry uses `serverUrl: http://seq:5341` (Docker internal hostname, not `localhost`) and the Seq sink's default in-memory queue buffers up to the permitted capacity — API does not crash if Seq is unreachable at startup (Edge: Seq not ready)
- [ ] `IAuditLogger.Log(actorId, actionType, resourceId)` is a void method on the interface; the implementation uses `ForContext` to attach `EventType: AuditLog`, `ActorId`, `ActionType`, and `ResourceId` before writing (AC-004)
- [ ] `AuditLoggerService` sets `OccurredAt` to `DateTime.UtcNow` — not `DateTime.Now` or `DateTimeOffset.UtcNow.LocalDateTime` — ensuring the property is always UTC regardless of server timezone (AC-004)
- [ ] `services.AddSingleton<IAuditLogger, AuditLoggerService>()` is registered in `Program.cs`; all controllers that need audit logging receive it via constructor injection, not via `new` (AC-004, OWASP A09 — centralised audit logging)
