# Task - TASK_001

## Requirement Reference
- **User Story:** us_014
- **Story Location:** .propel/context/tasks/EP-002/us_014/us_014.md
- **Acceptance Criteria:**
  - AC-001: `PostgresAuditLogger` middleware intercepts every request post-authorization and inserts a row into `audit_logs` with `actorId`, `actorRole`, `actionType`, `resourceType`, `resourceId`, `ipAddress`, `userAgent`, and `occurredAt` (UTC) within 100 ms of the response being sent
  - AC-002: If `IAuditLogger.RecordAsync` fails (PostgreSQL unavailable), the API returns HTTP 503 with `{"error": "Action could not be completed. Audit logging unavailable."}` and the underlying action (e.g., patient data read) is NOT completed
  - AC-003: On every successful audit log write, a Serilog structured event with properties `EventType: "AuditLog"`, `ActionType`, `ActorId`, `ResourceId`, and `OccurredAt` is emitted to the Seq sink and is queryable within 5 seconds
  - AC-005: All 10 auditable action types are capturable through the middleware: `LoginSuccess`, `LoginFailure`, `Registration`, `PatientDataAccess`, `BookingCreate`, `BookingModify`, `DocumentUpload`, `CodeSuggestionAccept`, `CodeSuggestionReject`, `AdminUserCRUD`, `UnauthorizedAccess`
- **Edge Cases:**
  - System-initiated actions: background jobs and hosted services must write audit entries with `ActorId = "system"` and `ActorRole = "System"` — never null — to ensure complete HIPAA audit coverage
  - Concurrent writes: under 50 simultaneous requests all audit INSERTs must succeed; no deadlock or constraint violation; the BIGSERIAL sequence-based primary key (configured in task_002) makes concurrent inserts safe — no `Guid.NewGuid()` contention on the primary index

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-002 (ASP.NET Core middleware pipeline; `IMiddleware` for `AuditMiddleware`) |
| Backend | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-002 (inserting `AuditLog` entity via a dedicated scoped `AuditDbContext` to isolate the insert from the request DbContext) |
| Logging | Serilog | compatible with .NET 8.0 | TR-004 (structured event emission; `Log.ForContext(...).Information(...)`) |
| Logging | Seq | 2023.4+ | TR-004 (structured log sink; `WriteTo.Seq(seqUrl)` configured at startup; established in us_004) |

---

## Task Overview

Implement the audit logging backbone required for HIPAA §164.312(b) compliance. `AuditMiddleware` intercepts every authenticated request after authorization, extracts actor context from JWT claims and route data, and calls `IAuditLogger.RecordAsync`. `PostgresAuditLogger` inserts the `AuditLog` entity and emits a Serilog structured event to Seq. If the DB insert throws, the middleware short-circuits with a 503 — the underlying action is never completed. A `SystemAuditContext` factory serves background jobs. `AuditActionTypes` constants enumerate all 10 required action types.

---

## Dependent Tasks
- task_002 (us_014) — `AuditLog` entity with BIGSERIAL Id, `ip_address`, and `user_agent` columns must be migrated before `PostgresAuditLogger` can insert rows
- task_001 (us_004) — Seq server must be running and the Serilog Seq sink must be configured; Seq URL must be available as an environment variable
- task_002 (us_006) — `audit_logs` table with INSERT-only `app_user` privilege must exist in PostgreSQL

---

## Impacted Components
- `src/api/Audit/IAuditLogger.cs` — new interface
- `src/api/Audit/PostgresAuditLogger.cs` — new implementation
- `src/api/Audit/AuditEntry.cs` — new DTO/record
- `src/api/Audit/AuditActionTypes.cs` — new constants class (10 action types)
- `src/api/Audit/SystemAuditContext.cs` — new static factory for background-job audit entries
- `src/api/Middleware/AuditMiddleware.cs` — new ASP.NET Core middleware
- `src/api/Program.cs` — modified: register `IAuditLogger`, add `AuditMiddleware` to pipeline, configure Seq URL

---

## Implementation Plan
1. Create `AuditActionTypes.cs` with `public static class AuditActionTypes` containing 10 `public const string` fields: `LoginSuccess`, `LoginFailure`, `Registration`, `PatientDataAccess`, `BookingCreate`, `BookingModify`, `DocumentUpload`, `CodeSuggestionAccept`, `CodeSuggestionReject`, `AdminUserCRUD`, `UnauthorizedAccess` — used by all callers to prevent magic strings (AC-005)
2. Create `AuditEntry` record with properties: `string ActorId`, `string ActorRole`, `string ActionType`, `string ResourceType`, `string ResourceId`, `string IpAddress`, `string UserAgent`, `DateTime OccurredAt`; add a static factory `SystemAuditContext.For(string actionType, string resourceType, string resourceId)` that returns an `AuditEntry` with `ActorId = "system"`, `ActorRole = "System"`, `OccurredAt = DateTime.UtcNow` (Edge: system-initiated; AC-001)
3. Create `IAuditLogger` interface: `Task RecordAsync(AuditEntry entry, CancellationToken ct = default)`; create `PostgresAuditLogger : IAuditLogger` — map `AuditEntry` to `AuditLog` entity, call `_auditContext.AuditLogs.Add(entity); await _auditContext.SaveChangesAsync(ct)`; on `NpgsqlException` or `DbUpdateException` throw `AuditLogUnavailableException` (AC-001, AC-002)
4. In `PostgresAuditLogger.RecordAsync`, after a successful `SaveChangesAsync`, emit the Serilog structured event: `Log.ForContext("EventType", "AuditLog").ForContext("ActionType", entry.ActionType).ForContext("ActorId", entry.ActorId).ForContext("ResourceId", entry.ResourceId).ForContext("OccurredAt", entry.OccurredAt).Information("Audit event recorded")` — the Seq sink flushes this within 5 seconds (AC-003)
5. Implement `AuditMiddleware : IMiddleware` with `InvokeAsync(HttpContext context, RequestDelegate next)`: extract `actorId` from `context.User.FindFirstValue(ClaimTypes.NameIdentifier)`, `actorRole` from `context.User.FindFirstValue(ClaimTypes.Role)`, `resourceType` and `resourceId` from `context.Request.RouteValues`; call `await _auditLogger.RecordAsync(entry, context.RequestAborted)` **before** calling `next(context)` — audit must precede the action; on `AuditLogUnavailableException` write HTTP 503 with JSON body and return without calling `next` (AC-001, AC-002 — audit-before-action ordering)
6. Apply `AuditMiddleware` selectively to routes requiring audit coverage: add `app.UseWhen(ctx => ctx.User.Identity?.IsAuthenticated == true, branch => branch.UseMiddleware<AuditMiddleware>())` in `Program.cs` after `UseAuthentication` and `UseAuthorization`; unauthenticated routes (e.g., `POST /auth/login`) are audited by explicit `IAuditLogger.RecordAsync` calls in their controllers (AC-001, AC-005)
7. Configure Seq sink in `Program.cs`: `Log.Logger = new LoggerConfiguration().WriteTo.Seq(Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://seq:5341").Enrich.FromLogContext().CreateLogger()`; OWASP A02: Seq URL comes from an environment variable, never hardcoded (AC-003; OWASP A02)

---

## Current Project State
```
src/
└── api/
    ├── Audit/
    │   ├── IAuditLogger.cs                          (CREATE)
    │   ├── PostgresAuditLogger.cs                   (CREATE)
    │   ├── AuditEntry.cs                            (CREATE)
    │   ├── AuditActionTypes.cs                      (CREATE)
    │   └── SystemAuditContext.cs                    (CREATE)
    ├── Middleware/
    │   └── AuditMiddleware.cs                       (CREATE)
    └── Program.cs                                   (MODIFY — register IAuditLogger + AuditMiddleware + Seq)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Audit/IAuditLogger.cs | Interface: RecordAsync |
| CREATE | src/api/Audit/PostgresAuditLogger.cs | DB insert + Serilog structured event emission |
| CREATE | src/api/Audit/AuditEntry.cs | Record DTO + SystemAuditContext factory |
| CREATE | src/api/Audit/AuditActionTypes.cs | 10 action type constants |
| CREATE | src/api/Audit/SystemAuditContext.cs | Static factory for system-initiated audit entries |
| CREATE | src/api/Middleware/AuditMiddleware.cs | Post-auth interceptor; 503 on audit failure; audit-before-action |
| MODIFY | src/api/Program.cs | Register IAuditLogger; add AuditMiddleware; configure Seq URL from env var |

---

## External References
- https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-8.0 (ASP.NET Core 8 middleware pipeline — IMiddleware, UseWhen)
- https://serilog.net/ (Serilog — structured logging; ForContext chaining for audit event properties)
- https://docs.datalust.co/docs/using-serilog (Seq + Serilog — WriteTo.Seq sink configuration and structured event querying)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Authenticate as Staff; call `GET /patients/42/view`; query `audit_logs` within 100 ms of the response; verify a row exists with `actor_role = "Staff"`, `action_type = "PatientDataAccess"`, `resource_id = "42"`, `ip_address` and `user_agent` populated, `occurred_at` in UTC (AC-001)
- [ ] Stop the PostgreSQL container; attempt any authenticated API call; verify HTTP 503 with body `{"error": "Action could not be completed. Audit logging unavailable."}` and no data returned (AC-002)
- [ ] Perform an auditable action; open the Seq UI within 5 seconds; filter by `EventType = "AuditLog"`; verify the event has `ActionType`, `ActorId`, `ResourceId`, and `OccurredAt` properties (AC-003)
- [ ] Perform each of the 10 action types in sequence; query `audit_logs`; verify all 10 `action_type` values appear with non-null `actor_id`, `action_type`, and `occurred_at` (AC-005)
- [ ] Trigger an action from a background job stub that uses `SystemAuditContext.For`; verify the audit row has `actor_id = "system"` and `actor_role = "System"` (Edge: system-initiated)
- [ ] Send 50 concurrent authenticated requests; verify all 50 audit rows are present in `audit_logs` with no duplicate or missing entries (Edge: concurrent writes)

---

## Implementation Checklist
- [ ] `AuditMiddleware.InvokeAsync` calls `_auditLogger.RecordAsync` **before** `next(context)` — the underlying data action is never executed if the audit write fails; this is the audit-before-action ordering required by AC-002 and HIPAA §164.312(b) (AC-002; OWASP A09)
- [ ] `PostgresAuditLogger` uses a dedicated `AuditDbContext` (separate scoped context) distinct from the request's main `AppDbContext` — prevents the audit INSERT from being rolled back if the request's unit-of-work transaction is rolled back (AC-001 — transaction isolation)
- [ ] `AuditEntry.OccurredAt` is always set to `DateTime.UtcNow` at the call site (not database-default) to ensure the timestamp reflects the moment of the action, not the moment the DB processes the insert (AC-001 — 100 ms requirement; AC-003 — OccurredAt in Seq event)
- [ ] Serilog `Log.ForContext` calls do not log PHI fields (e.g., patient name, DOB) — only structural IDs are emitted to Seq; PHI remains only in the encrypted DB columns (AC-003; OWASP A09; HIPAA minimum-necessary principle)
- [ ] `AuditActionTypes` constants are used at every `IAuditLogger.RecordAsync` call site — no inline string literals for action types anywhere in the codebase; enforced by a code review checklist item (AC-005; maintainability)
- [ ] `AuditLogUnavailableException` is a typed domain exception caught exclusively in `AuditMiddleware` — it must not propagate to the global exception handler, which would return a generic 500 instead of the required 503 (AC-002; OWASP A05)
- [ ] Seq URL is loaded from `Environment.GetEnvironmentVariable("SEQ_URL")` — never from `appsettings.json` committed to source control; falls back to `http://seq:5341` (Docker Compose service name) for local development only (AC-003; OWASP A02)
