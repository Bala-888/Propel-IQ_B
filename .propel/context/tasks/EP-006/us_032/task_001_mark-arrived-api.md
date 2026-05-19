# Task - TASK_001

## Requirement Reference
- **User Story:** us_032
- **Story Location:** .propel/context/tasks/EP-006/us_032/us_032.md
- **Acceptance Criteria:**
  - AC-002: `PATCH /api/queue/{queueEntryId}/arrived` returns HTTP 200 `{"status": "Arrived", "arrivedAt": "<ISO-8601 UTC timestamp>"}` on success
  - AC-003: `arrived_at` is set to `DateTimeOffset.UtcNow` inside the service handler — never to a timestamp supplied by the client
  - AC-004: The PATCH is idempotent — if the entry is already `Arrived`, the response is HTTP 200 with the original `arrived_at` unchanged and no duplicate audit log entry is written
- **Edge Cases:**
  - Race condition — two staff calling PATCH simultaneously: the second call hits an already-`Arrived` entry and follows the idempotent path (AC-004); no 409 or data inconsistency occurs

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — `PATCH /api/queue/{queueEntryId}/arrived`; idempotent status transition; server-side timestamp (AC-002, AC-003, AC-004) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — load `Booking` by ID; conditional update on `Status` and `CheckedInAt`; `SaveChangesAsync()` only on state transition (AC-002, AC-003, AC-004) |
| Database | PostgreSQL 15.3+ | 15.3+ | TR-007 — `bookings` table; `status` and `checked_in_at` columns updated atomically (AC-003) |
| Audit | AuditDbContext + IAuditService | Project-established | TR-014 — `PatientMarkedArrived` audit event on `Waiting → Arrived` transition only; skipped on idempotent re-call (AC-004; OWASP A02) |

---

## Task Overview

Implement `PATCH /api/queue/{queueEntryId}/arrived` restricted to Staff and Admin roles. The handler loads the `Booking` by `queueEntryId`. If the booking is already `Arrived`, it returns 200 immediately with the existing `CheckedInAt` — no DB write, no audit log (idempotent path). If the booking is `Waiting`, it sets `Status = Arrived` and `CheckedInAt = DateTimeOffset.UtcNow`, saves, emits one `PatientMarkedArrived` audit event, and returns 200 with the new timestamp. The server-side timestamp is the only authoritative source; no timestamp is accepted from the request body.

---

## Dependent Tasks
- task_001 (us_009) — `Roles.Staff`, `Roles.Admin` constants and JWT middleware
- task_001 (us_030) — `Booking` entity must have `Status` (including `Arrived` enum value) and `CheckedInAt` nullable column
- task_001 (us_031) — `GET /api/queue` established that `queueEntryId` maps to `Booking.Id`

---

## Impacted Components
- `src/api/Features/Queue/QueueController.cs` — modified: add `PATCH {queueEntryId}/arrived` action
- `src/api/Features/Queue/IQueueService.cs` — modified: add `MarkArrivedAsync(Guid queueEntryId, Guid staffId)` signature
- `src/api/Features/Queue/QueueService.cs` — modified: implement `MarkArrivedAsync` with idempotent logic
- `src/api/Features/Queue/ArrivedResponseDto.cs` — new: response DTO `{ string Status, DateTimeOffset ArrivedAt }`

---

## Implementation Plan
1. `PATCH /api/queue/{queueEntryId}/arrived` action on `QueueController`: `[Authorize(Roles = $"{Roles.Staff},{Roles.Admin}")]`; `[HttpPatch("{queueEntryId}/arrived")]`; extract `staffId` from JWT claim; delegate to `IQueueService.MarkArrivedAsync(queueEntryId, staffId)` (AC-002; OWASP A01)
2. `MarkArrivedAsync` in `QueueService`: load `Booking` by `queueEntryId`; if not found → return `null` (controller returns 404); if `booking.Status == BookingStatus.Arrived` → return `ArrivedResponseDto { Status = "Arrived", ArrivedAt = booking.CheckedInAt!.Value }` immediately without any DB write (AC-004 idempotent path)
3. State transition for `Waiting → Arrived`: set `booking.Status = BookingStatus.Arrived`; set `booking.CheckedInAt = DateTimeOffset.UtcNow` — the timestamp is assigned in the service, never from the HTTP request body or headers; call `await dbContext.SaveChangesAsync()` (AC-003 — server time only)
4. Audit log after `SaveChangesAsync`: `_auditService.LogAsync(ActionType.PatientMarkedArrived, staffId, booking.Id)` — logged once, only on actual `Waiting → Arrived` transition; the idempotent path (already `Arrived`) must not call `LogAsync` (AC-004; OWASP A02 — no PHI in log)
5. Response shape: `ArrivedResponseDto { string Status, DateTimeOffset ArrivedAt }` serialised as `{"status": "Arrived", "arrivedAt": "<ISO-8601 UTC>"}` — consistent for both the transition and idempotent paths; controller returns `Ok(dto)` in both cases (AC-002, AC-004)
6. `queueEntryId` path parameter validation: if the GUID cannot be parsed or resolves to no matching booking, return 404 `{"error": "QueueEntryNotFound"}`; the GUID is used only as a parameterised EF Core lookup — never in raw SQL string interpolation (OWASP A03)

---

## Current Project State
```
src/
└── api/
    └── Features/
        └── Queue/
            ├── QueueController.cs        (MODIFY — add PATCH action)
            ├── IQueueService.cs          (MODIFY — add MarkArrivedAsync)
            ├── QueueService.cs           (MODIFY — implement MarkArrivedAsync)
            └── (ArrivedResponseDto.cs    — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/api/Features/Queue/QueueController.cs | Add PATCH {queueEntryId}/arrived action |
| MODIFY | src/api/Features/Queue/IQueueService.cs | Add MarkArrivedAsync signature |
| MODIFY | src/api/Features/Queue/QueueService.cs | Implement idempotent MarkArrivedAsync |
| CREATE | src/api/Features/Queue/ArrivedResponseDto.cs | Response record { Status, ArrivedAt } |

---

## External References
- https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-8.0#patch-http-method (ASP.NET Core 8 — `[HttpPatch]` attribute; idempotent HTTP semantics for PATCH; AC-004)
- https://www.rfc-editor.org/rfc/rfc5789 (RFC 5789 — PATCH method semantics; idempotency is expected when the resource end state is deterministic; AC-004 design rationale)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Seed a `Waiting` booking; call `PATCH /api/queue/{id}/arrived`; verify 200 with `status = "Arrived"` and `arrivedAt` within 2 seconds of `DateTimeOffset.UtcNow`; verify `bookings.checked_in_at` updated in DB (AC-002, AC-003)
- [ ] Call `PATCH /api/queue/{id}/arrived` a second time on the same already-`Arrived` booking; verify 200 with unchanged `arrivedAt`; verify no second audit log row for that `bookingId` (AC-004)
- [ ] Call `PATCH /api/queue/{id}/arrived` as Patient role; verify 403 (OWASP A01)
- [ ] Call `PATCH /api/queue/00000000-0000-0000-0000-000000000000/arrived`; verify 404 (OWASP A03)
- [ ] Simulate two concurrent PATCH calls for the same entry using `Task.WhenAll`; verify both return 200 and `arrived_at` is set to one consistent timestamp (Edge: race condition; AC-004)

---

## Implementation Checklist
- [ ] `booking.CheckedInAt` is assigned `DateTimeOffset.UtcNow` exclusively inside `QueueService.MarkArrivedAsync`; the PATCH endpoint accepts no body and no timestamp field — the request body is empty by design (AC-003 — server time only)
- [ ] The idempotent branch returns 200 with the existing `CheckedInAt` value without calling `dbContext.SaveChangesAsync()` or `_auditService.LogAsync()` — exactly zero DB writes and zero audit entries on a re-call for an already-`Arrived` entry (AC-004)
- [ ] `_auditService.LogAsync(ActionType.PatientMarkedArrived, staffId, bookingId)` is called only after a successful `SaveChangesAsync()` on the `Waiting → Arrived` transition — never before the save and never on the idempotent path (OWASP A02; AC-004)
- [ ] `staffId` is extracted from the JWT claim in the controller action; it is not accepted as a query parameter or request body field (OWASP A01; A07)
- [ ] The GUID `queueEntryId` is used as a parameterised EF Core predicate `.Where(b => b.Id == queueEntryId)` — it is never concatenated into a raw SQL string (OWASP A03)
- [ ] Endpoint returns 403 for Patient role and 401 for unauthenticated requests via `[Authorize(Roles = $"{Roles.Staff},{Roles.Admin}")]` attribute (OWASP A01)
