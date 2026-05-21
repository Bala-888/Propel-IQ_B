# Task - TASK_001

## Requirement Reference
- **User Story:** us_033
- **Story Location:** .propel/context/tasks/EP-006/us_033/us_033.md
- **Acceptance Criteria:**
  - AC-001: `QueueEntryAdded` event is broadcast to all connected Staff/Admin clients within 2 seconds of a new booking or walk-in committing
  - AC-002: `QueueEntryUpdated` event is broadcast within 2 seconds of a `MarkArrived` or status-change commit in `QueueService`
  - AC-004: `GET /api/queue?since=<timestamp>` returns all queue entries updated on or after `since`; used by reconnecting clients to replay missed events
  - AC-005: `/hubs/queue` rejects unauthenticated connections with 401 and Patient-role connections with 403
- **Edge Cases:**
  - Hub has 0 connected clients: broadcast is wrapped in try/catch; a failed or no-op broadcast never propagates an exception to the booking commit or status update
  - Broadcast lag > 2 seconds under load: measure latency with `Stopwatch`; emit `Log.Warning` to Seq if > 2000ms; the underlying data mutation is never rolled back due to a broadcast SLA breach

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — `QueueHub` + `IQueueHubService`; `Program.cs` SignalR registration; hub endpoint `/hubs/queue` (AC-001, AC-002, AC-005) |
| SignalR | Microsoft.AspNetCore.SignalR | .NET 8.0 built-in | TR-013 — real-time push to Staff/Admin clients; `IHubContext<QueueHub>` for server-initiated broadcasts (AC-001, AC-002) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — extend `GET /api/queue` with `since` DateTimeOffset filter for reconnection fallback (AC-004) |
| Logging | Serilog + Seq | .NET 8.0 compatible / 2023.4+ | TR-011 — `Log.Warning` for broadcast latency SLA breach; no PHI in log values (Edge: lag > 2s; OWASP A02) |

---

## Task Overview

Implement the SignalR queue hub infrastructure. `QueueHub` inherits from `Hub` and is decorated with `[Authorize(Roles = "Staff,Admin")]`. JWT token extraction for WebSocket connections is configured in `JwtBearerOptions`. The `IQueueHubService` abstraction provides `BroadcastEntryAddedAsync` and `BroadcastEntryUpdatedAsync` methods, each wrapped in a try/catch and measured with a `Stopwatch` for SLA compliance. Both broadcast calls are injected into `WalkinBookingService` (us_030) and `QueueService` (us_031/us_032) and invoked only after a successful DB commit. The `GET /api/queue` endpoint is extended with an optional `since` parameter for reconnection fallback.

---

## Dependent Tasks
- task_001 (us_009) — `Roles.Staff`, `Roles.Admin`, and JWT bearer middleware must be present; `JwtBearerOptions` configurable in `Program.cs`
- task_001 (us_030) — `WalkinBookingService.CreateAsync` is modified to call `BroadcastEntryAddedAsync` post-commit
- task_001 (us_031) — `QueueService.GetQueueAsync` is extended with `since` filter; `QueueEntryDto` is the broadcast payload type
- task_001 (us_032) — `QueueService.MarkArrivedAsync` is modified to call `BroadcastEntryUpdatedAsync` post-save

---

## Impacted Components
- `src/api/Hubs/QueueHub.cs` — new: `QueueHub : Hub` with `[Authorize]` attribute
- `src/api/Features/Queue/IQueueHubService.cs` — new: broadcast service interface
- `src/api/Features/Queue/QueueHubService.cs` — new: implementation using `IHubContext<QueueHub>`; Stopwatch; try/catch
- `src/api/Features/Queue/QueueController.cs` — modified: add `since` DateTimeOffset? param to `GetQueueAsync`
- `src/api/Features/Queue/QueueService.cs` — modified: inject `IQueueHubService`; call `BroadcastEntryUpdatedAsync` in `MarkArrivedAsync` after `SaveChangesAsync`
- `src/api/Features/Bookings/WalkinBookingService.cs` — modified: inject `IQueueHubService`; call `BroadcastEntryAddedAsync` after `CommitAsync`
- `src/api/Program.cs` — modified: `builder.Services.AddSignalR()`; `app.MapHub<QueueHub>("/hubs/queue")`; JWT `OnMessageReceived` event handler for WebSocket query-string token

---

## Implementation Plan
1. `QueueHub : Hub` in `src/api/Hubs/QueueHub.cs`: decorated with `[Authorize(Roles = $"{Roles.Staff},{Roles.Admin}")]`; no override methods needed — hub is used only for server-initiated broadcasts via `IHubContext<QueueHub>`; registered in `Program.cs` with `builder.Services.AddSignalR()` and `app.MapHub<QueueHub>("/hubs/queue")` (AC-005; OWASP A01)
2. JWT WebSocket token extraction in `Program.cs`: inside the `JwtBearer` `OnMessageReceived` event, check `if (context.Request.Path.StartsWithSegments("/hubs/queue"))` and read `context.Token = context.Request.Query["access_token"]` — this is the standard ASP.NET Core pattern required because WebSocket connections cannot set `Authorization` headers (AC-005; OWASP A01)
3. `IQueueHubService` interface with two methods: `Task BroadcastEntryAddedAsync(QueueEntryDto entry)` and `Task BroadcastEntryUpdatedAsync(QueueEntryDto entry)`; registered in `Program.cs` as `AddScoped` (AC-001, AC-002)
4. `QueueHubService` implementation: inject `IHubContext<QueueHub>` and `ILogger<QueueHubService>`; each method starts a `Stopwatch`, calls `await _hubContext.Clients.All.SendAsync("QueueEntryAdded", entry)` or `"QueueEntryUpdated"` inside a `try { ... } catch (Exception ex) { _logger.LogWarning(ex, "QueueHub broadcast failed for {EventType}", eventType) }` block; after the try/catch, check `if (sw.ElapsedMilliseconds > 2000) _logger.LogWarning("QueueHub broadcast latency {LatencyMs}ms exceeds 2s SLA", sw.ElapsedMilliseconds)` (AC-001, AC-002; Edge: 0-clients; Edge: lag > 2s; OWASP A04 — failure cannot propagate)
5. `WalkinBookingService` modification (us_030 addition): after `await transaction.CommitAsync()`, build the `QueueEntryDto` for the new booking and call `await _queueHubService.BroadcastEntryAddedAsync(dto)` — the broadcast is fire-and-forget from the caller's perspective; if it throws internally it is swallowed in `QueueHubService` (AC-001; OWASP A04 — broadcast post-commit only)
6. `QueueService.MarkArrivedAsync` modification (us_032 addition): after `await dbContext.SaveChangesAsync()`, call `await _queueHubService.BroadcastEntryUpdatedAsync(dto)` where `dto` contains the updated booking row; same fire-and-forget contract (AC-002; OWASP A04 — broadcast post-save only)
7. `GET /api/queue` reconnection fallback: add `DateTimeOffset? since` query parameter to `QueueController.GetQueueAsync`; if provided, add `.Where(b => b.UpdatedAt >= since)` to the EF Core query; validate `since` with `DateTimeOffset.TryParse` — default to null if unparseable; the `since` value is used only as a parameterised EF Core predicate, never in raw SQL (AC-004; OWASP A03)

---

## Current Project State
```
src/
└── api/
    ├── Hubs/
    │   └── (QueueHub.cs                    — CREATE)
    └── Features/
        └── Queue/
            ├── (IQueueHubService.cs         — CREATE)
            ├── (QueueHubService.cs          — CREATE)
            ├── QueueController.cs           (MODIFY — add since param)
            ├── QueueService.cs              (MODIFY — inject IQueueHubService)
            └── ../Bookings/
                └── WalkinBookingService.cs  (MODIFY — inject IQueueHubService)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Hubs/QueueHub.cs | QueueHub : Hub with [Authorize] |
| CREATE | src/api/Features/Queue/IQueueHubService.cs | Broadcast service interface |
| CREATE | src/api/Features/Queue/QueueHubService.cs | IHubContext broadcast + Stopwatch + try/catch |
| MODIFY | src/api/Features/Queue/QueueController.cs | Add since DateTimeOffset? query param |
| MODIFY | src/api/Features/Queue/QueueService.cs | Inject IQueueHubService; call BroadcastEntryUpdatedAsync post-save |
| MODIFY | src/api/Features/Bookings/WalkinBookingService.cs | Inject IQueueHubService; call BroadcastEntryAddedAsync post-commit |
| MODIFY | src/api/Program.cs | AddSignalR; MapHub; JWT OnMessageReceived for WebSocket |

---

## External References
- https://learn.microsoft.com/en-us/aspnet/core/signalr/hubs?view=aspnetcore-8.0 (ASP.NET Core 8 SignalR — `Hub` class, `IHubContext<T>` server-initiated push, `MapHub` registration; AC-001, AC-002)
- https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-8.0 (SignalR authentication — JWT `OnMessageReceived` query-string token extraction for WebSocket; AC-005; OWASP A01)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Connect a SignalR test client as Staff; trigger a walk-in booking creation; verify `QueueEntryAdded` message arrives within 2 seconds (AC-001)
- [ ] Call `PATCH /api/queue/{id}/arrived`; verify `QueueEntryUpdated` is pushed to a connected Staff client within 2 seconds (AC-002)
- [ ] Simulate 0 connected clients; trigger a booking; verify no exception is thrown and the booking row is committed to DB (Edge: 0 clients)
- [ ] Stub `SendAsync` to delay 2500ms; trigger a booking; verify a `LogWarning` SLA entry appears in Seq output and the booking commits successfully (Edge: lag > 2s)
- [ ] Attempt to connect to `/hubs/queue` with a Patient-role JWT; verify connection is rejected 403 (AC-005; OWASP A01)
- [ ] Attempt to connect without a JWT; verify 401 (AC-005; OWASP A01)
- [ ] Call `GET /api/queue?since=2026-01-01T00:00:00Z`; verify only entries updated on or after that timestamp are returned (AC-004)

---

## Implementation Checklist
- [x] `QueueHub` is decorated with `[Authorize(Roles = $"{Roles.Staff},{Roles.Admin}")]`; the hub has no methods exposed to clients — it is used exclusively for server-to-client push via `IHubContext<QueueHub>` (AC-005; OWASP A01)
- [x] `JwtBearerOptions.Events.OnMessageReceived` reads `access_token` from the query string only when the request path starts with `/hubs/queue` — this handler is not applied to regular API routes (AC-005; OWASP A01)
- [x] Both `BroadcastEntryAddedAsync` and `BroadcastEntryUpdatedAsync` in `QueueHubService` are wrapped individually in `try/catch`; exceptions are logged as `LogWarning` and swallowed — they never propagate to the calling service (Edge: 0 clients; OWASP A04)
- [x] Broadcast is invoked only after `CommitAsync()` or `SaveChangesAsync()` succeeds — broadcast is never called speculatively before the DB write; if the DB write fails the broadcast is not attempted (OWASP A04; AC-001, AC-002 — data consistency)
- [x] `Stopwatch` measures the duration of `SendAsync`; if elapsed > 2000ms, `_logger.LogWarning("QueueHub broadcast latency {LatencyMs}ms exceeds 2s SLA", elapsed)` is emitted — no exception is thrown, no transaction is rolled back (Edge: lag > 2s; OWASP A02 — no PHI in log values)
- [x] `since` query parameter in `GET /api/queue` is validated with `DateTimeOffset.TryParse` and used only as a parameterised EF Core `.Where` predicate — never interpolated into raw SQL (AC-004; OWASP A03; decision logged: F011)
- [x] `IQueueHubService` is registered as `AddScoped` to share the DI lifetime with the scoped `DbContext` in services that inject both (OWASP A04 — DI lifetime consistency)
