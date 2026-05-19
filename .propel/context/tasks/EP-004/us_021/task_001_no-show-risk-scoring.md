# Task - TASK_001

## Requirement Reference
- **User Story:** us_021
- **Story Location:** .propel/context/tasks/EP-004/us_021/us_021.md
- **Acceptance Criteria:**
  - AC-001: A risk score in the range 0.00–1.00 is computed from `no_show_history_count`, `days_to_appointment`, and `booking_lead_time_days` and stored in `bookings.no_show_risk_score` within 5 seconds of booking confirmation
  - AC-002: `no_show_risk_tier` is set to `"Low"` for score < 0.40, `"Medium"` for 0.40–0.69, and `"High"` for ≥ 0.70
  - AC-003: Scoring executes asynchronously; `POST /bookings` returns HTTP 201 without waiting for the score computation — no latency regression
  - AC-004: A scoring exception is logged to Seq with `level = "Error"` and structured property `eventType = "RiskScoringFailed"`; the booking record retains `no_show_risk_score = null` and `no_show_risk_tier = "Unknown"`; no duplicate booking is created
- **Edge Cases:**
  - First-time patient: when `no_show_history_count = 0` and no prior NoShow records exist, the scorer produces a score between 0.00 and 0.40 using only lead-time features — no null result and no divide-by-zero exception
  - Reschedule recomputation: when a booking's slot is changed, `no_show_risk_score` and `no_show_risk_tier` must be recomputed from the new `days_to_appointment` value via `INoShowRiskScoringService.RecomputeAsync` — the previous score must not persist

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 (`BackgroundService` worker; `INoShowRiskScoringService` domain service) |
| Async Queue | System.Threading.Channels | .NET 8.0 built-in | Bounded `Channel<BookingCreatedEvent>` as in-process event queue; `Channel.Writer.TryWrite` from request path; `Channel.Reader.ReadAllAsync` in worker (AC-003) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-008 (COUNT query for `no_show_history_count`; `ExecuteUpdateAsync` for score/tier columns; scoped `DbContext` from `IServiceScopeFactory` inside `BackgroundService`) |
| Database | PostgreSQL | 15.3+ | TR-007 (`bookings.no_show_risk_score`, `no_show_risk_tier`, `no_show_history_count` columns from us_007) |
| Logging | Serilog | Compatible with .NET 8.0 | Structured error log with `eventType = "RiskScoringFailed"` to Seq (AC-004) |

---

## Task Overview

Build the asynchronous no-show risk scoring pipeline triggered by every `BookingCreated` domain event. The pipeline uses a bounded `Channel<BookingCreatedEvent>` as the in-process event queue. After `CommitAsync()` in `BookingService`, the event is enqueued with `TryWrite` — the request returns 201 immediately. A `NoShowRiskScoringWorker` (BackgroundService) dequeues events and calls `INoShowRiskScoringService.ComputeAndPersistAsync`. The scoring service applies a weighted linear formula over three rule-based features, derives the risk tier, and writes both columns via `ExecuteUpdateAsync`. Exceptions are caught and logged; the booking record defaults to null/Unknown. A `RecomputeAsync` method on the service supports the future reschedule flow.

---

## Dependent Tasks
- task_001 (us_020) — `BookingService.CreateBookingAsync` and the `bookings` table must exist; the enqueue call is added after `CommitAsync()` in this task
- task_002 (us_007) — `bookings.no_show_risk_score` (decimal, nullable), `bookings.no_show_risk_tier` (string, default `"Unknown"`), and `no_show_history_count` (or derivable from bookings query) must be present in the database schema

---

## Impacted Components
- `src/api/Features/Bookings/BookingService.cs` — modified: enqueue `BookingCreatedEvent` onto the channel after `CommitAsync()` succeeds
- `src/api/Features/Bookings/BookingCreatedEvent.cs` — new: event record with booking and slot metadata
- `src/api/Features/NoShowRisk/NoShowRiskScoringWorker.cs` — new: `BackgroundService` consumer reading from the channel
- `src/api/Features/NoShowRisk/NoShowRiskScoringService.cs` — new: `ComputeAndPersistAsync` + `RecomputeAsync` with weighted formula and tier derivation
- `src/api/Features/NoShowRisk/INoShowRiskScoringService.cs` — new: interface with `ComputeAndPersistAsync` and `RecomputeAsync` signatures
- `src/api/Program.cs` — modified: register `Channel<BookingCreatedEvent>` as singleton; register `NoShowRiskScoringWorker` via `AddHostedService`; register `INoShowRiskScoringService` as scoped

---

## Implementation Plan
1. Define `BookingCreatedEvent` record: `Guid BookingId`, `Guid PatientId`, `DateOnly SlotDate`, `TimeOnly SlotStartTime`, `DateTime BookingCreatedAt`; register `Channel<BookingCreatedEvent>.CreateBounded(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.Wait })` as a singleton in `Program.cs`; register `NoShowRiskScoringWorker` via `builder.Services.AddHostedService<NoShowRiskScoringWorker>()` (AC-001, AC-003)
2. In `BookingService.CreateBookingAsync`, after `await transaction.CommitAsync()`: construct `BookingCreatedEvent` and call `_channel.Writer.TryWrite(evt)`; if `TryWrite` returns false (channel at capacity), log a structured warning `"BookingCreatedEventDropped: {BookingId}"` and continue — the 201 response is never delayed by the channel write (AC-003; OWASP A05 — crash prevention; graceful degradation)
3. Implement `NoShowRiskScoringWorker : BackgroundService` — `ExecuteAsync(CancellationToken stoppingToken)`: wrap the read loop in `try/catch(OperationCanceledException)` for graceful shutdown; inner loop: `await foreach (var evt in _channel.Reader.ReadAllAsync(stoppingToken))` with a per-event `try/catch(Exception ex)` that calls `logger.LogError(ex, "RiskScoringFailed {BookingId}", evt.BookingId)` and continues to the next event without rethrowing (AC-003, AC-004; OWASP A05)
4. Implement `NoShowRiskScoringService.ComputeAndPersistAsync(BookingCreatedEvent evt)`: create a DI scope via `IServiceScopeFactory.CreateScope()` to resolve a scoped `AppDbContext` (required because `BackgroundService` is singleton); count `no_show_history_count = await dbContext.Bookings.CountAsync(b => b.PatientId == evt.PatientId && b.Status == "NoShow")`; compute `daysToAppt = Math.Max(0, (evt.SlotDate.ToDateTime(evt.SlotStartTime) - DateTime.UtcNow).TotalDays)`; compute `leadDays = Math.Max(0, (evt.BookingCreatedAt - DateTime.UtcNow.AddDays(-daysToAppt)).TotalDays)` (or equate leadDays = daysToAppt for simplicity if booking is made in advance) (AC-001)
5. Score formula: `score = Math.Clamp(0.5 * Math.Min(historyCount / 5.0, 1.0) + 0.3 * Math.Max(0.0, 1.0 - daysToAppt / 30.0) + 0.2 * Math.Max(0.0, 1.0 - leadDays / 14.0), 0.0, 1.0)`; all denominators are positive constants (5, 30, 14) — no divide-by-zero is possible; for `historyCount = 0`, only the lead-time terms contribute, capping the score below 0.50 for appointments booked ≥ 1 day ahead (AC-001; Edge: first-time patient)
6. Tier derivation: `tier = score < 0.40 ? "Low" : score < 0.70 ? "Medium" : "High"`; persist using `await dbContext.Bookings.Where(b => b.Id == evt.BookingId).ExecuteUpdateAsync(s => s.SetProperty(b => b.NoShowRiskScore, score).SetProperty(b => b.NoShowRiskTier, tier))`; `ExecuteUpdateAsync` issues a targeted UPDATE without loading the entity (AC-002; performance — no entity tracking overhead)
7. Error path in `ComputeAndPersistAsync`: the outer `try/catch(Exception ex)` in the worker catches any exception from this method; the `bookings` row is not touched on failure — its `no_show_risk_score` remains `null` (nullable column) and `no_show_risk_tier` remains its default `"Unknown"` (set as column default in the us_007 migration); the structured log entry includes `{"eventType": "RiskScoringFailed", "bookingId": "<id>"}` as enriched properties (AC-004; OWASP A09)
8. Implement `INoShowRiskScoringService.RecomputeAsync(Guid bookingId, DateOnly newSlotDate, TimeOnly newSlotTime)`: reload `historyCount` from DB; recompute `daysToAppt` from `newSlotDate`; run the same formula; call `ExecuteUpdateAsync` to overwrite both columns; designed to be called by the future reschedule event handler — no new formula logic is introduced (Edge: reschedule; DRY principle — single formula definition)

---

## Current Project State
```
src/
└── api/
    ├── Features/
    │   ├── Bookings/
    │   │   ├── BookingService.cs              (from us_020 — MODIFY: add channel enqueue after CommitAsync)
    │   │   └── (BookingCreatedEvent.cs        — CREATE)
    │   └── NoShowRisk/
    │       └── (NoShowRiskScoringWorker.cs    — CREATE)
    │       └── (NoShowRiskScoringService.cs   — CREATE)
    │       └── (INoShowRiskScoringService.cs  — CREATE)
    └── Program.cs                             (MODIFY: register channel singleton + hosted service)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/api/Features/Bookings/BookingService.cs | Enqueue BookingCreatedEvent onto channel after CommitAsync |
| CREATE | src/api/Features/Bookings/BookingCreatedEvent.cs | Domain event record with booking and slot metadata |
| CREATE | src/api/Features/NoShowRisk/INoShowRiskScoringService.cs | Interface: ComputeAndPersistAsync + RecomputeAsync |
| CREATE | src/api/Features/NoShowRisk/NoShowRiskScoringService.cs | Weighted formula, tier derivation, ExecuteUpdateAsync |
| CREATE | src/api/Features/NoShowRisk/NoShowRiskScoringWorker.cs | BackgroundService consuming Channel with per-event error isolation |
| MODIFY | src/api/Program.cs | Register Channel singleton, AddHostedService, scoped INoShowRiskScoringService |

---

## External References
- https://learn.microsoft.com/en-us/dotnet/core/extensions/channels (System.Threading.Channels — BoundedChannel for in-process async event queue)
- https://learn.microsoft.com/en-us/aspnet/core/fundamentals/hosted-services?view=aspnetcore-8.0 (ASP.NET Core 8 BackgroundService — IServiceScopeFactory for scoped DbContext in singleton worker)
- https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete?view=efcore-8.0 (EF Core 8 ExecuteUpdateAsync — targeted UPDATE without entity tracking)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Create a booking; within 5 seconds, query `bookings WHERE id = <bookingId>`; verify `no_show_risk_score` is not null and is in range [0.00, 1.00]; verify `no_show_risk_tier` is one of "Low", "Medium", "High" (AC-001, AC-002)
- [ ] Measure response time of `POST /bookings` with and without the scoring pipeline registered; verify no statistically significant latency increase (AC-003 — async guarantee)
- [ ] Create a booking for a patient with 0 prior bookings; verify `no_show_risk_score` is in [0.00, 0.40] and `no_show_risk_tier = "Low"` (Edge: first-time patient)
- [ ] Simulate a scoring failure by injecting a faulting `INoShowRiskScoringService` implementation in a test; verify: (a) Serilog captures a log entry with `level = "Error"` and `eventType = "RiskScoringFailed"`, (b) `no_show_risk_score` remains null, (c) `no_show_risk_tier` remains "Unknown", (d) no duplicate booking row exists (AC-004)
- [ ] Invoke `RecomputeAsync(bookingId, newSlotDate, newSlotTime)` for a booking; verify both score and tier are updated to values derived from the new `days_to_appointment` (Edge: reschedule recomputation)
- [ ] Verify `no_show_risk_tier` thresholds: a computed score of 0.39 → "Low"; 0.40 → "Medium"; 0.70 → "High" (AC-002 — boundary values)

---

## Implementation Checklist
- [ ] `Channel<BookingCreatedEvent>` is registered as a singleton with `BoundedChannelOptions{Capacity=1000, FullMode=BoundedChannelFullMode.Wait}`; `NoShowRiskScoringWorker` is registered via `AddHostedService`; `INoShowRiskScoringService` is registered as scoped (not singleton) to allow per-event `DbContext` scope creation (AC-001, AC-003; DI correctness)
- [ ] `BookingService` calls `_channel.Writer.TryWrite(evt)` synchronously after `CommitAsync()` — no `await`, no blocking; if the channel is full, logs a warning and continues; the 201 response path never awaits the scoring pipeline (AC-003 — no latency regression)
- [ ] `NoShowRiskScoringWorker` uses `IServiceScopeFactory.CreateScope()` per event to resolve a scoped `AppDbContext`; the scope is disposed after each `ComputeAndPersistAsync` call — prevents scoped service lifetime leaks in the singleton worker (AC-001; OWASP A04 — insecure design prevention)
- [ ] Score formula uses `Math.Clamp(..., 0.0, 1.0)` and all denominators are positive constants (5.0, 30.0, 14.0); `Math.Max(0, ...)` guards ensure no negative sub-scores; `Math.Min(historyCount / 5.0, 1.0)` caps the history factor at 1.0 for patients with ≥ 5 prior no-shows — no divide-by-zero and no out-of-range result possible (AC-001; Edge: first-time patient; OWASP A05)
- [ ] Tier thresholds are applied as `score < 0.40 → "Low"`, `score < 0.70 → "Medium"`, `score >= 0.70 → "High"`; thresholds are defined as `const double` named constants (not magic numbers) in `NoShowRiskScoringService` (AC-002; code-anti-patterns rule — no magic constants)
- [ ] `ExecuteUpdateAsync` is used for the score/tier UPDATE — the entity is not loaded, tracked, or modified in the change tracker; this isolates the scoring write from any concurrent booking transaction (AC-001, AC-002; performance)
- [ ] `catch(Exception ex)` in `NoShowRiskScoringWorker`'s per-event handler logs `logger.LogError(ex, "RiskScoringFailed for BookingId {BookingId}", evt.BookingId)` with an enriched `{"eventType": "RiskScoringFailed"}` property; the booking columns are NOT updated on this path — `null` and `"Unknown"` defaults persist (AC-004; OWASP A09; HIPAA — no PHI in log payload)
- [ ] `INoShowRiskScoringService.RecomputeAsync` is the single entry point for score computation on reschedule; it reuses the identical formula and tier-derivation logic from `ComputeAndPersistAsync` — no formula duplication (Edge: reschedule; DRY principle)
- [ ] `BookingCreatedEvent` contains only scheduling metadata (BookingId, PatientId, SlotDate, SlotStartTime, BookingCreatedAt); no PHI fields (patient name, demographics, intake data) are included in the event record (OWASP A02; HIPAA minimum-necessary)
