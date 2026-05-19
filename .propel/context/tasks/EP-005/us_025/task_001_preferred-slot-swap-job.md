# Task - TASK_001

## Requirement Reference
- **User Story:** us_025
- **Story Location:** .propel/context/tasks/EP-005/us_025/us_025.md
- **Acceptance Criteria:**
  - AC-001: `PreferredSlotMonitorJob` (BackgroundService) runs on a configurable schedule (default every 5 minutes); each execution queries `preferred_slots JOIN bookings JOIN slots WHERE slot.status = 'Available'` ordered by `preferred_slots.created_at ASC` to find eligible swap candidates
  - AC-002: Each eligible candidate is processed in a single ACID transaction: (1) original booking → `Cancelled`, (2) new booking → `Confirmed` on the preferred slot, (3) preferred slot → `Booked`, (4) original slot → `Available`; all four writes commit or all roll back
  - AC-003: When multiple patients prefer the same slot, the one with the earliest `preferred_slots.created_at` is processed first; all other candidates for that slot are skipped for the current cycle (their designations are preserved)
  - AC-004: If the swap transaction rolls back (any constraint or infrastructure failure), both the patient's original booking and the preferred slot record remain in their pre-swap state — no booking is left `Cancelled` without a replacement
  - AC-005: On a successful commit, a `SlotSwapCompletedEvent` is enqueued with `PatientId`, `OldSlotId`, and `NewSlotId` for the us_026 notification worker
- **Edge Cases:**
  - Preferred slot is `Blocked` at job run time: skip the candidate, log a structured warning, and preserve the `preferred_slots` row so it is re-evaluated on the next cycle — the designation is never auto-deleted for a `Blocked` slot
  - 500 eligible candidates in one run: process candidates in batches of 50 (configurable `BatchSize`); each batch runs in its own transaction scope to avoid long-held locks and lock escalation

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — `PreferredSlotMonitorJob : BackgroundService`; `PeriodicTimer` for interval scheduling (AC-001) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — candidate query with joins; `IDbContextTransaction` for ACID swap; `ExecuteUpdateAsync` for bulk field updates (AC-002) |
| Database | PostgreSQL 15.3+ | 15.3+ | TR-007 — `preferred_slots` + `bookings` + `slots` tables; row-level locking inside swap transaction (AC-002, AC-004) |
| Async Queue | System.Threading.Channels | .NET 8.0 built-in | `SlotSwapCompletedEvent` enqueued for us_026 notification worker on successful swap commit (AC-005) |
| Logging | Serilog | Compatible with .NET 8.0 | Structured events: `SlotSwapCompleted`, `SlotSwapSkippedBlocked`, `SlotSwapFailed`, `PreferredSlotMonitorCycleComplete` (AC-001; Edge cases) |

---

## Task Overview

Build the `PreferredSlotMonitorJob` hosted service. The job runs on a `PeriodicTimer` (default 5-minute interval from `PreferredSlotMonitorOptions`). Each tick: (1) queries all candidates where a registered preferred slot is currently `Available`, ordered by `preferred_slots.created_at ASC`; (2) deduplicates by `slot_id` so only the earliest-registrant is selected per slot per cycle; (3) processes candidates in batches of 50, each batch in its own `IDbContextTransaction`; (4) inside each transaction, re-verifies slot status — if `Blocked`, skips and logs without deleting the designation; if still `Available`, executes the four-write ACID swap and commits; (5) on commit, enqueues a `SlotSwapCompletedEvent` for us_026. The `IServiceScopeFactory` pattern is used to resolve a fresh scoped `AppDbContext` per batch cycle inside the singleton `BackgroundService`.

---

## Dependent Tasks
- task_001 (us_024) — `PreferredSlot` entity, `preferred_slots` table, and `PreferredSlotReleasedEvent` Channel must exist before this job can query and process candidates
- task_001 (us_020) — `Booking` entity and `BookingStatus` enum must be defined; `bookings` table must have `status`, `slot_id`, and `patient_id` columns

---

## Impacted Components
- `src/api/Features/PreferredSlots/PreferredSlotMonitorJob.cs` — new: BackgroundService with PeriodicTimer + batch loop
- `src/api/Features/PreferredSlots/PreferredSlotSwapService.cs` — new: ACID swap transaction logic for a single candidate
- `src/api/Features/PreferredSlots/IPreferredSlotSwapService.cs` — new: service interface
- `src/api/Features/PreferredSlots/SlotSwapCompletedEvent.cs` — new: domain event record with PatientId, OldSlotId, NewSlotId
- `src/api/Features/PreferredSlots/PreferredSlotMonitorOptions.cs` — new: configuration record (IntervalMinutes, BatchSize)
- `src/api/Program.cs` — modified: `AddHostedService<PreferredSlotMonitorJob>`; register `IPreferredSlotSwapService` as scoped; bind `PreferredSlotMonitorOptions` from config; register `Channel<SlotSwapCompletedEvent>` singleton

---

## Implementation Plan
1. Create `PreferredSlotMonitorOptions` record: `int IntervalMinutes = 5` and `int BatchSize = 50`; bind from `builder.Configuration.GetSection("PreferredSlotMonitor")` in `Program.cs`; `AddHostedService<PreferredSlotMonitorJob>()` and `AddScoped<IPreferredSlotSwapService, PreferredSlotSwapService>()` (AC-001; Edge: configurable batch size)
2. Implement `PreferredSlotMonitorJob : BackgroundService` using `PeriodicTimer(_options.IntervalMinutes)`: on each tick, create a new `IServiceScope` via `_scopeFactory.CreateScope()` to resolve a fresh `AppDbContext` for each cycle (DI lifetime correctness; OWASP A04)
3. Candidate query inside the cycle scope: `dbContext.PreferredSlots .Include(ps => ps.Booking).Include(ps => ps.Slot) .Where(ps => ps.Slot.Status == SlotStatus.Available && ps.Booking.Status == BookingStatus.Confirmed) .OrderBy(ps => ps.CreatedAt) .ToListAsync(ct)` — returns all eligible candidates ordered earliest-first (AC-001, AC-003)
4. Deduplicate by `SlotId` client-side before batching: `candidates.GroupBy(ps => ps.SlotId).Select(g => g.First())` — ensures only one candidate per preferred slot is processed per cycle; remaining candidates for the same slot are skipped (their rows are NOT deleted) (AC-003)
5. Batch loop: `foreach (var batch in deduplicatedCandidates.Chunk(_options.BatchSize))` — each batch runs in its own scope and transaction; log `PreferredSlotMonitorCycleComplete` with candidate count and batch count after the cycle (Edge: 500 candidates; AC-001)
6. In `PreferredSlotSwapService.SwapAsync(Guid preferredSlotId, CancellationToken ct)`: open `IDbContextTransaction`; re-fetch slot with `SELECT FOR UPDATE` (`SET LOCAL lock_timeout = '5s'`); if `slot.Status == SlotStatus.Blocked` → rollback transaction, log `SlotSwapSkippedBlocked {preferredSlotId}`, return `SwapResult.Skipped` without deleting the `preferred_slots` row (Edge: Blocked slot; AC-004)
7. ACID swap writes inside the committed transaction: (a) `original booking.Status = Cancelled`; (b) `dbContext.Bookings.Add(new Booking { SlotId = preferredSlot.SlotId, PatientId = booking.PatientId, Status = Confirmed, ... })`; (c) `preferredSlot.Status = Booked`; (d) `originalSlot.Status = Available`; (e) `dbContext.PreferredSlots.Remove(preferredSlotRow)`; `await transaction.CommitAsync(ct)` — any exception triggers `await transaction.RollbackAsync()` in the `catch` block; neither the original booking nor the preferred slot row is modified if the catch executes (AC-002, AC-004)
8. On `SwapResult.Success`: `_slotSwapChannel.Writer.TryWrite(new SlotSwapCompletedEvent { PatientId = ..., OldSlotId = ..., NewSlotId = ... })`; log structured `SlotSwapCompleted` event with all three IDs; on `SwapResult.Failed` (caught exception): log `SlotSwapFailed {preferredSlotId}` with exception details — the `preferred_slots` row is preserved for the next cycle (AC-005; AC-004)

---

## Current Project State
```
src/
└── api/
    └── Features/
        └── PreferredSlots/
            ├── PreferredSlot.cs                     (from us_024 — entity only)
            ├── (PreferredSlotMonitorJob.cs           — CREATE)
            ├── (PreferredSlotSwapService.cs          — CREATE)
            ├── (IPreferredSlotSwapService.cs         — CREATE)
            ├── (SlotSwapCompletedEvent.cs            — CREATE)
            └── (PreferredSlotMonitorOptions.cs       — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Features/PreferredSlots/PreferredSlotMonitorOptions.cs | Config record: IntervalMinutes=5, BatchSize=50 |
| CREATE | src/api/Features/PreferredSlots/SlotSwapCompletedEvent.cs | Domain event: PatientId, OldSlotId, NewSlotId |
| CREATE | src/api/Features/PreferredSlots/IPreferredSlotSwapService.cs | Service interface with SwapAsync |
| CREATE | src/api/Features/PreferredSlots/PreferredSlotSwapService.cs | ACID swap: 4-write transaction + Blocked guard + rollback |
| CREATE | src/api/Features/PreferredSlots/PreferredSlotMonitorJob.cs | BackgroundService: PeriodicTimer + scope + candidate query + batch loop |
| MODIFY | src/api/Program.cs | AddHostedService; register IPreferredSlotSwapService scoped; Channel<SlotSwapCompletedEvent> singleton; bind PreferredSlotMonitorOptions |

---

## External References
- https://learn.microsoft.com/en-us/dotnet/api/system.threading.periodictimer?view=net-8.0 (.NET 8 PeriodicTimer — tick-based BackgroundService scheduling; avoids drift vs Task.Delay; preferred over Timer in hosted services)
- https://learn.microsoft.com/en-us/ef/core/saving/transactions?view=efcore-8.0 (EF Core 8 IDbContextTransaction — explicit transaction with RollbackAsync on failure; AC-002, AC-004)
- https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.chunk?view=net-8.0 (Enumerable.Chunk — batch partitioning without custom pagination logic; Edge: 500 candidates in batches of 50)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Set `IntervalMinutes = 1` in test config; create a booking with a preferred slot designation; transition the preferred slot to `Available`; wait one cycle; verify: original booking is `Cancelled`, new booking is `Confirmed` on the preferred slot, preferred slot is `Booked`, original slot is `Available` (AC-002)
- [ ] Register two patients preferring the same slot (Patient A first, Patient B second); transition slot to `Available`; run one cycle; verify Patient A's booking is swapped and Patient B's `preferred_slots` row still exists (AC-003)
- [ ] Inject a constraint failure mid-transaction (e.g., force a duplicate booking conflict); verify both the original booking and the `preferred_slots` row remain in pre-swap state after rollback (AC-004)
- [ ] After a successful swap, verify `SlotSwapCompletedEvent` is enqueued with correct `PatientId`, `OldSlotId`, `NewSlotId` (AC-005)
- [ ] Set slot to `Blocked` before the job runs; verify the `preferred_slots` row is NOT deleted and Serilog contains `SlotSwapSkippedBlocked` (Edge: Blocked slot)
- [ ] Seed 500 preferred slot candidates; run one cycle; verify candidates are processed in batches of 50 (10 transactions, not 1); verify no `LockTimeout` or deadlock exceptions (Edge: 500 candidates; batch processing)
- [ ] Verify `IServiceScopeFactory` creates a new scope per cycle tick — `AppDbContext` is not shared across batch iterations (DI lifetime; OWASP A04)

---

## Implementation Checklist
- [ ] `PreferredSlotMonitorJob` uses `PeriodicTimer` (not `Task.Delay` or `System.Timers.Timer`); tick interval is sourced from `IOptions<PreferredSlotMonitorOptions>.Value.IntervalMinutes` — no magic numbers in the hosted service body (AC-001; maintainability)
- [ ] Candidate query orders by `preferred_slots.created_at ASC` and is deduplicated by `SlotId` (`GroupBy(...).Select(g => g.First())`) before batching — only the earliest-registrant per slot is processed per cycle; other candidates' rows are never deleted within this story (AC-003)
- [ ] Candidates are batched with `Enumerable.Chunk(_options.BatchSize)` (default 50); each batch is processed inside its own `IDbContextTransaction`; no single transaction spans more than 50 rows (Edge: 500 candidates; lock escalation prevention)
- [ ] Inside `PreferredSlotSwapService.SwapAsync`: slot is re-fetched inside the transaction with `SET LOCAL lock_timeout = '5s'`; if `slot.Status == Blocked` → transaction is rolled back, `preferred_slots` row is left untouched, `SwapResult.Skipped` is returned (Edge: Blocked slot; AC-004)
- [ ] All four ACID writes — cancel original booking, create new confirmed booking, update preferred slot to Booked, update original slot to Available — are executed inside a single `IDbContextTransaction`; any exception causes `RollbackAsync()` before the exception is caught and logged as `SwapResult.Failed` (AC-002; AC-004)
- [ ] `SlotSwapCompletedEvent` is enqueued via `TryWrite` only after a successful `CommitAsync()`; it is never enqueued in the catch or finally block — the event is not emitted for rolled-back or skipped swaps (AC-005)
- [ ] `IServiceScopeFactory.CreateScope()` is called once per `PeriodicTimer` tick to resolve a fresh scoped `AppDbContext`; the scope is disposed via `using` after the cycle completes — no scoped service is held across ticks by the singleton `BackgroundService` (DI lifetime correctness; OWASP A04)
- [ ] No PHI (patient name, medical data) is included in Serilog log fields — log events contain only non-PHI identifiers (`preferredSlotId`, `patientId` as opaque Guid, `oldSlotId`, `newSlotId`) (OWASP A02; HIPAA — no PHI in logs)
