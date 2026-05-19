# Task - TASK_001

## Requirement Reference
- **User Story:** us_024
- **Story Location:** .propel/context/tasks/EP-005/us_024/us_024.md
- **Acceptance Criteria:**
  - AC-001: `POST /bookings/{bookingId}/preferred-slot` with body `{"slotId": "<uuid>"}` returns HTTP 201 with `{"preferredSlotId": "<uuid>", "status": "Registered"}`; the `preferred_slots` table is updated with the booking-slot pair
  - AC-002: Re-posting with a different `slotId` replaces the previous record; `SELECT COUNT(*) FROM preferred_slots WHERE booking_id = <id>` returns 1 at all times
  - AC-003: If `slotId` equals the booking's currently booked slot, the endpoint returns HTTP 400 with `{"error": "Preferred slot cannot be the same as the active booking."}`
  - AC-004: If the booking status is not `Confirmed`, the endpoint returns HTTP 409 with `{"error": "Preferred slot selection is only available for active confirmed bookings."}`
- **Edge Cases:**
  - Slot unavailable before POST: if the chosen slot has transitioned away from `Available` before the API call, return HTTP 409 with `{"error": "The selected slot is no longer available."}`
  - Booking cancelled while preferred slot registered: when `BookingService.CancelBookingAsync` executes, delete the `preferred_slots` row and enqueue a `PreferredSlotReleasedEvent` for the us_025 monitoring worker

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — new endpoint on BookingsController or PreferredSlotController (AC-001) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — `PreferredSlot` entity; UNIQUE index on `BookingId`; `ExecuteDeleteAsync` for upsert delete step (AC-001, AC-002) |
| Database | PostgreSQL 15.3+ | 15.3+ | TR-007 — `preferred_slots` table with UNIQUE constraint on `booking_id` (AC-002 — COUNT = 1) |
| Async Queue | System.Threading.Channels | .NET 8.0 built-in | `PreferredSlotReleasedEvent` enqueued when booking cancelled; consumed by us_025 monitoring worker (Edge: booking cancelled) |

---

## Task Overview

Implement `POST /bookings/{bookingId}/preferred-slot` to register or replace a patient's preferred alternative slot. A dedicated `PreferredSlot` entity with a UNIQUE constraint on `BookingId` enforces the at-most-one invariant. The upsert is implemented as `ExecuteDeleteAsync` on the existing row followed by `Add` + `SaveChangesAsync`, which guarantees `COUNT(*) = 1` at all times. Guards are applied in order: ownership → status → same-slot → availability. When a booking is cancelled, `CancelBookingAsync` deletes the preferred slot row and enqueues a `PreferredSlotReleasedEvent` as an upstream hook for the us_025 monitoring job.

---

## Dependent Tasks
- task_001 (us_020) — `Booking` entity, `BookingService.CancelBookingAsync`, and `bookings` table must exist before this task can add the cancellation release hook
- task_001 (us_019) — `Slot` entity and `SlotStatus` enum must be defined for the availability guard

---

## Impacted Components
- `src/api/Features/Bookings/PreferredSlotController.cs` — new: POST endpoint with ownership + status + same-slot + availability guards
- `src/api/Features/Bookings/PreferredSlotService.cs` — new: `SetPreferredSlotAsync` orchestrating all guards and upsert
- `src/api/Features/Bookings/IPreferredSlotService.cs` — new: service interface
- `src/api/Domain/Entities/PreferredSlot.cs` — new: entity with UNIQUE BookingId constraint
- `src/api/Features/Bookings/BookingService.cs` — modified: cancel path deletes preferred slot row and enqueues `PreferredSlotReleasedEvent`
- `src/api/Infrastructure/Persistence/AppDbContext.cs` — modified: add `DbSet<PreferredSlot>` and UNIQUE index configuration
- `src/api/Program.cs` — modified: register `IPreferredSlotService` as scoped; register `Channel<PreferredSlotReleasedEvent>` singleton

---

## Implementation Plan
1. Create `PreferredSlot` entity: `Guid Id` (PK), `Guid BookingId` (FK → bookings, UNIQUE), `Guid SlotId` (FK → slots), `DateTimeOffset CreatedAt`; configure UNIQUE index via `modelBuilder.Entity<PreferredSlot>().HasIndex(ps => ps.BookingId).IsUnique()` in `AppDbContext.OnModelCreating`; generate EF Core migration (AC-001, AC-002)
2. Create `POST /api/bookings/{bookingId}/preferred-slot` action in `PreferredSlotController`; apply `[Authorize(Roles = Roles.Patient)]`; extract `patientId` from `User.FindFirstValue(ClaimTypes.NameIdentifier)` — never from request body (OWASP A01; AC-001)
3. Ownership guard in `PreferredSlotService.SetPreferredSlotAsync`: load booking via `dbContext.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId)`; if booking is null or `booking.PatientId != patientIdFromJwt` → return 403 Forbidden (OWASP A01; A07 — no booking enumeration)
4. Status guard: if `booking.Status != BookingStatus.Confirmed` → throw `BusinessRuleException` → 409 `{"error": "Preferred slot selection is only available for active confirmed bookings."}` (AC-004)
5. Same-slot guard: if `request.SlotId == booking.SlotId` → throw `BusinessRuleException` → 400 `{"error": "Preferred slot cannot be the same as the active booking."}` (AC-003)
6. Slot availability guard: `await dbContext.Slots.FirstOrDefaultAsync(s => s.Id == request.SlotId && s.Status == SlotStatus.Available)`; if null → throw `BusinessRuleException` → 409 `{"error": "The selected slot is no longer available."}` (Edge: slot unavailable)
7. Upsert: `await dbContext.PreferredSlots.Where(ps => ps.BookingId == bookingId).ExecuteDeleteAsync()` then `dbContext.PreferredSlots.Add(new PreferredSlot { ... })`; `await dbContext.SaveChangesAsync()`; return `201 Created` with `{"preferredSlotId": slotId, "status": "Registered"}` (AC-001, AC-002)
8. Cancellation release hook in `BookingService.CancelBookingAsync`: after `booking.Status = BookingStatus.Cancelled` and before `SaveChangesAsync`, call `await dbContext.PreferredSlots.Where(ps => ps.BookingId == bookingId).ExecuteDeleteAsync()`; then `_preferredSlotReleasedChannel.Writer.TryWrite(new PreferredSlotReleasedEvent { BookingId = bookingId })` for the us_025 monitoring worker (Edge: booking cancelled)

---

## Current Project State
```
src/
└── api/
    ├── Domain/
    │   └── Entities/
    │       └── (PreferredSlot.cs                  — CREATE)
    ├── Features/
    │   └── Bookings/
    │       ├── BookingService.cs                   (from us_020 — MODIFY: cancellation release hook)
    │       ├── (IPreferredSlotService.cs           — CREATE)
    │       ├── (PreferredSlotService.cs            — CREATE)
    │       └── (PreferredSlotController.cs         — CREATE)
    └── Infrastructure/
        └── Persistence/
            └── AppDbContext.cs                     (MODIFY: add DbSet<PreferredSlot> + UNIQUE index)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Domain/Entities/PreferredSlot.cs | Entity with UNIQUE BookingId index |
| CREATE | src/api/Features/Bookings/IPreferredSlotService.cs | Service interface |
| CREATE | src/api/Features/Bookings/PreferredSlotService.cs | Guards + upsert logic |
| CREATE | src/api/Features/Bookings/PreferredSlotController.cs | POST endpoint with JWT-sourced patientId |
| MODIFY | src/api/Infrastructure/Persistence/AppDbContext.cs | Add DbSet<PreferredSlot>; UNIQUE index in OnModelCreating |
| MODIFY | src/api/Features/Bookings/BookingService.cs | Cancellation path: delete preferred slot + enqueue PreferredSlotReleasedEvent |
| MODIFY | src/api/Program.cs | Register IPreferredSlotService scoped; Channel<PreferredSlotReleasedEvent> singleton |

---

## External References
- https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete?view=efcore-8.0 (EF Core 8 ExecuteDeleteAsync — bulk delete without loading entity into memory; used for upsert delete step)
- https://learn.microsoft.com/en-us/ef/core/modeling/indexes?view=efcore-8.0 (EF Core HasIndex + IsUnique — enforce at-most-one preferred slot per booking at the database level)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] `POST /bookings/{bookingId}/preferred-slot` with a valid available slot returns 201 `{"preferredSlotId": "...", "status": "Registered"}`; `preferred_slots` table row is created (AC-001)
- [ ] Second POST with a different valid `slotId` returns 201; `SELECT COUNT(*) FROM preferred_slots WHERE booking_id = <id>` returns 1 — old row replaced, not appended (AC-002)
- [ ] POST with `slotId` equal to the booking's active slot returns 400 `{"error": "Preferred slot cannot be the same as the active booking."}` (AC-003)
- [ ] POST for a cancelled booking returns 409 `{"error": "Preferred slot selection is only available for active confirmed bookings."}` (AC-004)
- [ ] POST with a slot whose status is `Booked` or `Blocked` returns 409 `{"error": "The selected slot is no longer available."}` (Edge: slot unavailable)
- [ ] Cancel a booking that has a preferred slot registered; verify `preferred_slots` row is deleted and `PreferredSlotReleasedEvent` is enqueued (Edge: booking cancelled)
- [ ] POST as a patient for another patient's bookingId returns 403 (OWASP A01)

---

## Implementation Checklist
- [ ] `PreferredSlot` entity is configured with `HasIndex(ps => ps.BookingId).IsUnique()` in `AppDbContext.OnModelCreating`; the database-level UNIQUE constraint ensures `COUNT(*) = 1` is enforced even under concurrent requests (AC-002; OWASP A04)
- [ ] All four guards (ownership → status → same-slot → availability) are applied in order before the upsert; each guard throws a typed `BusinessRuleException` that is translated to the correct HTTP status code in the global exception handler (AC-003, AC-004; Edge: slot unavailable; OWASP A01)
- [ ] `patientId` is sourced from `User.FindFirstValue(ClaimTypes.NameIdentifier)` only; ownership check compares JWT `patientId` against `booking.PatientId` — request path contains only `bookingId` (OWASP A01; A07)
- [ ] Upsert uses `ExecuteDeleteAsync` (no entity load into memory) followed by `Add` + `SaveChangesAsync`; this is not a read-modify-write and does not require `SELECT FOR UPDATE` because the UNIQUE constraint handles concurrent duplicate inserts at the DB level (AC-002; performance)
- [ ] `BookingService.CancelBookingAsync` calls `ExecuteDeleteAsync` on `preferred_slots` and `TryWrite` on the `Channel<PreferredSlotReleasedEvent>` as part of the cancellation transaction — preferred slot release is atomic with booking status update (Edge: booking cancelled; upstream hook for us_025)
