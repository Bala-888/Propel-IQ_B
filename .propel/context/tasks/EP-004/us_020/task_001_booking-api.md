# Task - TASK_001

## Requirement Reference
- **User Story:** us_020
- **Story Location:** .propel/context/tasks/EP-004/us_020/us_020.md
- **Acceptance Criteria:**
  - AC-001: `POST /bookings` with valid `slotId` returns HTTP 201 with `{"bookingId": "<uuid>", "status": "Confirmed", "slot": {...}}`; the `appointment_slots` row is updated to `status = "Booked"` and the `bookings` row is inserted in a single ACID transaction
  - AC-002: Two concurrent `POST /bookings` calls for the same slot result in exactly one HTTP 201; the second receives HTTP 409 `{"error": "This slot is no longer available.", "alternatives": [<3 SlotDto objects>]}`
  - AC-003: `SELECT FOR UPDATE` on the `appointment_slots` row is used inside the transaction; an xUnit integration test that dispatches 50 concurrent booking requests confirms exactly 1 committed booking and 49 HTTP 409 responses
  - AC-004: If the patient already has a Confirmed booking whose time window overlaps the target slot, the API returns HTTP 409 `{"error": "You already have an active booking for this time window."}`
  - AC-005: If the slot UPDATE succeeds but the booking INSERT fails (simulated constraint violation), the transaction rolls back and the slot reverts to `status = "Available"` — no orphaned booking row exists
- **Edge Cases:**
  - Slot Blocked by admin: if the slot's current status at transaction time is `"Blocked"` (not `"Booked"`), return HTTP 409 `{"error": "This slot has been blocked by the clinic."}` — no `alternatives` array in the response body
  - Lock timeout: if `SELECT FOR UPDATE` cannot be acquired within 10 seconds, return HTTP 503 `{"error": "Booking could not be processed. Please try again."}` — do not leave the connection blocked indefinitely

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 (POST /bookings endpoint; transaction orchestration) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-008 (`BeginTransactionAsync`; `FromSqlRaw` for `SELECT FOR UPDATE`; `SaveChangesAsync` + `CommitAsync`) |
| Database | PostgreSQL | 15.3+ | TR-007 (ACID transaction; `SELECT FOR UPDATE`; `lock_timeout` session variable; `appointment_slots` + `bookings` tables from us_005) |
| Logging | Serilog | Compatible with .NET 8.0 | Audit log `BookingCreated` via `IAuditLogger` after commit |

---

## Task Overview

Build the `POST /bookings` endpoint with a full ACID booking transaction. Inside the transaction: `SET LOCAL lock_timeout = '10s'` is executed first, then `SELECT FOR UPDATE` re-reads the target slot's current status. If the slot is Available, a duplicate time-window check runs against the patient's existing confirmed bookings. If both checks pass, the slot is updated to `Booked` and a `Confirmed` booking row is inserted atomically. Any conflict path rolls back the transaction and returns an appropriate 409. Lock-acquisition timeout returns 503. Alternatives are queried and attached to the 409 body when the conflict is a concurrent booking. An audit log entry is written after successful commit.

---

## Dependent Tasks
- task_001 (us_019) — `appointment_slots` table with `status` column must exist; `SlotsService` and `SlotDto` are reused for the alternatives query
- task_002 (us_005) — `appointment_slots` and `bookings` tables must exist in the database schema from EP-DATA migrations

---

## Impacted Components
- `src/api/Features/Bookings/BookingsController.cs` — new: POST /bookings
- `src/api/Features/Bookings/CreateBookingRequest.cs` — new: DTO with `Guid SlotId` (patientId from JWT claims only)
- `src/api/Features/Bookings/BookingService.cs` — new: transaction orchestration, SELECT FOR UPDATE, conflict checks, atomic writes
- `src/api/Features/Bookings/BookingConflictResponse.cs` — new: 409 response shape `{error: string, alternatives?: SlotDto[]}`
- `src/api/Features/Slots/SlotsService.cs` — modified: expose `GetNearestAvailableSlotsAsync(slot, count: 3)` for alternatives query

---

## Implementation Plan
1. Define `CreateBookingRequest` record with `Guid SlotId` only; extract `patientId` from `HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)` inside the endpoint — the client must never supply its own patientId (OWASP A01/A07 — broken access control and identification failures; AC-001)
2. Begin the transaction: `await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable)`; immediately execute `await dbContext.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '10s'")` so any `SELECT FOR UPDATE` that blocks beyond 10 seconds raises `PostgresException` with `SqlState = "55P03"` (lock_not_available); catch that exception, roll back, and return 503 (Edge: lock timeout; AC-003)
3. Re-read the slot inside the transaction using `SELECT FOR UPDATE`: `dbContext.AppointmentSlots.FromSqlRaw("SELECT * FROM appointment_slots WHERE id = {0} FOR UPDATE", slotId).SingleOrDefaultAsync()`; if null → return 404; if `status = "Booked"` → rollback + 409 `{error: "This slot is no longer available.", alternatives: await GetNearestAvailableSlotsAsync(slot, 3)}`; if `status = "Blocked"` → rollback + 409 `{error: "This slot has been blocked by the clinic."}` (no alternatives) (AC-002, AC-003; Edge: slot Blocked)
4. Duplicate time-window check inside the transaction: query `bookings WHERE patient_id = patientId AND status = 'Confirmed' AND date = slot.Date AND start_time < (slot.StartTime + slot.DurationMinutes minutes) AND (start_time + duration_minutes minutes) > slot.StartTime`; if any match found → rollback + 409 `{error: "You already have an active booking for this time window."}` (AC-004)
5. Atomic writes: set `slot.Status = "Booked"` on the tracked entity; call `dbContext.Bookings.Add(new Booking { Id = Guid.NewGuid(), PatientId = patientId, SlotId = slotId, Status = "Confirmed", CreatedAt = DateTime.UtcNow })`; call `await dbContext.SaveChangesAsync()`; wrap in `try/catch (DbUpdateException)` — on any exception, call `await transaction.RollbackAsync()` and return 500 (the slot change will not persist) (AC-001, AC-005)
6. On `SaveChangesAsync()` success, call `await transaction.CommitAsync()`; after commit, call `IAuditLogger.RecordAsync(actionType: AuditActionTypes.BookingCreated, actorId: patientId, resourceId: bookingId)`; return 201 with `{bookingId, status: "Confirmed", slot: SlotDto}` (AC-001; OWASP A09; AC-005 commit confirmation)
7. `SlotsService.GetNearestAvailableSlotsAsync(AppointmentSlot contestedSlot, int count)`: query `appointment_slots WHERE status = 'Available' ORDER BY ABS(EXTRACT(EPOCH FROM (date + start_time) - ({contested_date} + {contested_start_time}))) LIMIT {count}`; return as `IReadOnlyList<SlotDto>`; used only in the 409 concurrent-conflict branch (AC-002; UXR-602 backend data; OWASP A03 — count bounded to 3)
8. Rollback correctness: `DbUpdateException` catch path calls `await transaction.RollbackAsync()` before returning; the EF Core change tracker discards the pending `slot.Status = "Booked"` mutation; verified in test by re-querying both tables and confirming slot is `Available` and no booking row exists for that slotId (AC-005)

---

## Current Project State
```
src/
└── api/
    └── Features/
        ├── Bookings/
        │   └── (BookingsController.cs    — CREATE)
        └── Slots/
            └── SlotsService.cs           (from us_019 — MODIFY: add GetNearestAvailableSlotsAsync)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Features/Bookings/BookingsController.cs | POST /bookings endpoint |
| CREATE | src/api/Features/Bookings/CreateBookingRequest.cs | DTO with SlotId only |
| CREATE | src/api/Features/Bookings/BookingService.cs | ACID transaction, SELECT FOR UPDATE, conflict checks |
| CREATE | src/api/Features/Bookings/BookingConflictResponse.cs | 409 response shape |
| MODIFY | src/api/Features/Slots/SlotsService.cs | Add GetNearestAvailableSlotsAsync for alternatives query |

---

## External References
- https://www.npgsql.org/efcore/index.html (Npgsql EF Core 8 — FromSqlRaw for SELECT FOR UPDATE; PostgresException SqlState codes)
- https://learn.microsoft.com/en-us/ef/core/saving/transactions?view=efcore-8.0 (EF Core 8 — BeginTransactionAsync, CommitAsync, RollbackAsync)
- https://www.postgresql.org/docs/15/sql-set.html (PostgreSQL 15 SET LOCAL lock_timeout — session-scoped lock acquisition timeout)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] `POST /bookings` with a valid Available slotId → verify 201, booking row exists in DB with `status = "Confirmed"`, slot row has `status = "Booked"` (AC-001)
- [ ] Dispatch 50 concurrent `POST /bookings` for the same slotId via xUnit parallel tasks → verify exactly 1 returns 201; `SELECT COUNT(*) FROM bookings WHERE slot_id = <slotId>` returns 1; 49 requests return 409 (AC-002, AC-003)
- [ ] `POST /bookings` where the patient already has a Confirmed booking overlapping the same time window → verify 409 `"You already have an active booking for this time window."` (AC-004)
- [ ] `POST /bookings` where slot INSERT is forced to fail via a test constraint → verify DB shows slot `status = "Available"` and no booking row exists for that slotId (AC-005)
- [ ] `POST /bookings` where slot status is `Blocked` → verify 409 `"This slot has been blocked by the clinic."` with no `alternatives` field (Edge: slot Blocked)
- [ ] Simulate `lock_timeout` scenario (hold a lock on the slot row for > 10s in a parallel test connection) → verify 503 response within 11 seconds (Edge: lock timeout)
- [ ] `POST /bookings` 409 concurrent-conflict response includes `alternatives` array with exactly 3 SlotDto objects all having `status = "Available"` (AC-002; UXR-602 data)

---

## Implementation Checklist
- [ ] `CreateBookingRequest` contains `Guid SlotId` only; `patientId` is extracted exclusively from `HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)` — the client cannot supply or override its own patientId (OWASP A01/A07 — broken access control)
- [ ] `SET LOCAL lock_timeout = '10s'` is executed as the first statement inside the transaction; `PostgresException` with `SqlState = "55P03"` is caught, the transaction is rolled back, and HTTP 503 is returned — the endpoint never blocks indefinitely (Edge: lock timeout; OWASP A04)
- [ ] `SELECT FOR UPDATE` re-reads the slot's current status inside the transaction; the decision to proceed is based on the locked row value, not the value cached before the transaction began — eliminates TOCTOU race (AC-002, AC-003; OWASP A04 — insecure design)
- [ ] Duplicate time-window check uses a closed-interval overlap query `(start_time < slot_end AND end_time > slot_start)` against Confirmed bookings for the same patient before any write; the check runs inside the same transaction as the writes (AC-004; OWASP A04)
- [ ] Both the `appointment_slots` UPDATE and the `bookings` INSERT are submitted in a single `SaveChangesAsync()` call within the explicit transaction; if `SaveChangesAsync()` throws `DbUpdateException`, `RollbackAsync()` is called before returning 500 — no partial state survives (AC-001, AC-005; OWASP A04)
- [ ] `GetNearestAvailableSlotsAsync` returns at most 3 results (`LIMIT 3` in the query); the count parameter is not exposed to callers as a configurable value — the bound is hard-coded to prevent unbounded alternatives queries (AC-002; OWASP A03 — validate at boundary)
- [ ] Audit log `AuditActionTypes.BookingCreated` is written **after** `CommitAsync()` succeeds; if commit fails, no audit entry is written; the log payload contains `patientId` and `bookingId` only — no slot date/time details that could constitute scheduling PHI (OWASP A09; HIPAA minimum-necessary)
- [ ] `POST /bookings` is decorated `[Authorize(Roles = Roles.Patient)]`; Staff and Admin roles receive 403; unauthenticated requests receive 401 (OWASP A01 — broken access control)
