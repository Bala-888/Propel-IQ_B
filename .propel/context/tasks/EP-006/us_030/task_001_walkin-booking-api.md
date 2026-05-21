# Task - TASK_001

## Requirement Reference
- **User Story:** us_030
- **Story Location:** .propel/context/tasks/EP-006/us_030/us_030.md
- **Acceptance Criteria:**
  - AC-002: `GET /patients/search?q=<term>` returns matching patient records within 500ms for queries of 3+ characters; result includes `patientId`, `firstName`, `lastName`, `dateOfBirth`
  - AC-003: `POST /bookings/walkin` returns HTTP 201 with `{"bookingId", "status": "Confirmed", "queuePosition": int}`; assigned slot transitions to `Booked`; patient appears in queue
  - AC-004: `POST /patients/walkin-create` creates a minimal patient record for new patients and returns `{patientId, firstName, lastName}` for pre-fill in the walk-in form
- **Edge Cases:**
  - No same-day slots: if no `Available` slots exist for today, `POST /bookings/walkin` returns 409 `{"error": "NoAvailableSlots"}` as a secondary guard (frontend also disables the button); authoritative check inside the transaction
  - Duplicate walk-in: if a `Confirmed` booking already exists for the same patient today, return 409 `{"error": "DuplicateBookingToday", "existingBookingId": "<uuid>"}` — the frontend uses this to render the override banner and may re-submit with `{"overrideDuplicate": true}` to bypass

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — three new endpoints: patient search, walk-in booking, walk-in patient creation (AC-002, AC-003, AC-004) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — `ILIKE` query for patient search; `SELECT FOR UPDATE` slot lock; queue position count; `IDbContextTransaction` for atomic booking + slot update (AC-002, AC-003) |
| Database | PostgreSQL 15.3+ | 15.3+ | TR-007 — `ILIKE` full-text-style search on patient name/email columns; row-level lock during slot booking (AC-002, AC-003) |
| Audit | AuditDbContext + IAuditService | Project-established | TR-014 — `WalkinBookingCreated` and `WalkinPatientCreated` audit events with `staffId` and `patientId` (OWASP A02; AC-003, AC-004) |

---

## Task Overview

Implement three endpoints for the walk-in booking flow, all restricted to Staff/Admin roles. `GET /patients/search` provides typeahead results using PostgreSQL `ILIKE`. `POST /bookings/walkin` creates a confirmed booking inside an `IDbContextTransaction` with a `SELECT FOR UPDATE` slot lock, a duplicate-today check, and a computed queue position. `POST /patients/walkin-create` creates a minimal patient record for new walk-in patients. All three endpoints perform ownership/role checks via JWT and emit structured audit log entries.

---

## Dependent Tasks
- task_001 (us_009) — `ApplicationUser`, `Roles.Staff`, `Roles.Admin`, and JWT auth middleware must be configured before these endpoints can enforce role-based access
- task_001 (us_019) — `Slot` entity and `SlotStatus` enum must exist for the `SELECT FOR UPDATE` slot availability guard

---

## Impacted Components
- `src/api/Features/Patients/PatientSearchController.cs` — new: `GET /api/patients/search` with 3-char minimum and `ILIKE` query
- `src/api/Features/Bookings/WalkinBookingController.cs` — new: `POST /api/bookings/walkin`
- `src/api/Features/Bookings/WalkinBookingService.cs` — new: duplicate check + slot lock + queue position + transaction commit
- `src/api/Features/Bookings/IWalkinBookingService.cs` — new: service interface
- `src/api/Features/Patients/WalkinPatientController.cs` — new (or extend PatientSearchController): `POST /api/patients/walkin-create`
- `src/api/Features/Patients/WalkinPatientService.cs` — new: minimal patient + user record creation
- `src/api/Program.cs` — modified: register `IWalkinBookingService` and `IWalkinPatientService` as scoped

---

## Implementation Plan
1. `GET /api/patients/search?q=<term>`: `[Authorize(Roles = $"{Roles.Staff},{Roles.Admin}")]`; validate `q.Length >= 3` at boundary → 400 if shorter (OWASP A03; AC-002); query: `dbContext.Patients.Where(p => EF.Functions.ILike(p.FirstName + " " + p.LastName, $"%{term}%") || EF.Functions.ILike(p.Email, $"%{term}%")).Take(10).Select(p => new { p.Id, p.FirstName, p.LastName, p.DateOfBirth })`; index on `(first_name, last_name)` ensures sub-500ms response (AC-002; OWASP A03 — parameterised ILIKE, no raw string injection)
2. `POST /api/bookings/walkin` endpoint: `[Authorize(Roles = $"{Roles.Staff},{Roles.Admin}")]`; body: `WalkinBookingRequest { Guid PatientId, Guid SlotId, string ReasonForVisit, string Priority, bool OverrideDuplicate = false }`; extract `staffId` from JWT — never from request body (OWASP A01; AC-003)
3. Duplicate-today check in `WalkinBookingService.CreateAsync`: `var existing = await dbContext.Bookings.FirstOrDefaultAsync(b => b.PatientId == request.PatientId && b.Slot.Date == today && b.Status == BookingStatus.Confirmed)`; if `existing != null && !request.OverrideDuplicate` → return 409 `{"error": "DuplicateBookingToday", "existingBookingId": existing.Id}` (Edge: duplicate)
4. Slot lock inside `IDbContextTransaction`: `await dbContext.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout='5s'"); var slot = await dbContext.Slots.FromSqlRaw("SELECT * FROM slots WHERE id = {0} FOR UPDATE", request.SlotId).FirstOrDefaultAsync()`; if `slot == null || slot.Status != SlotStatus.Available` → rollback → 409 `{"error": "SlotNoLongerAvailable"}` (AC-003; us_020 TOCTOU pattern; OWASP A04)
5. Walk-in booking writes in transaction: `slot.Status = SlotStatus.Booked`; `dbContext.Bookings.Add(new Booking { PatientId = request.PatientId, SlotId = request.SlotId, Status = BookingStatus.Confirmed, ReasonForVisit = request.ReasonForVisit, Priority = request.Priority, CreatedByStaffId = staffIdFromJwt, CreatedAt = DateTimeOffset.UtcNow })`; `await transaction.CommitAsync()` (AC-003)
6. Queue position: after `CommitAsync()`, compute `queuePosition = await dbContext.Bookings.CountAsync(b => b.Slot.Date == today && b.Status == BookingStatus.Confirmed && b.CreatedAt <= booking.CreatedAt)`; include in 201 response (AC-003)
7. `POST /api/patients/walkin-create`: `[Authorize(Roles = $"{Roles.Staff},{Roles.Admin}")]`; body: `WalkinPatientRequest { string FirstName, string LastName, DateOnly DateOfBirth, string? PhoneNumber }`; create `ApplicationUser` + `Patient` record with `CreatedByStaffId = staffIdFromJwt`; return `{patientId, firstName, lastName}` (AC-004; OWASP A01)
8. Audit log: `_auditService.LogAsync(ActionType.WalkinBookingCreated, staffId, bookingId)` after CommitAsync; `_auditService.LogAsync(ActionType.WalkinPatientCreated, staffId, patientId)` after patient creation — log contains staffId, patientId, bookingId only; no PHI values (OWASP A02; AC-003, AC-004)

---

## Current Project State
```
src/
└── api/
    └── Features/
        ├── Patients/
        │   ├── (PatientSearchController.cs         — CREATE)
        │   ├── (WalkinPatientController.cs         — CREATE)
        │   └── (WalkinPatientService.cs            — CREATE)
        └── Bookings/
            ├── (WalkinBookingController.cs         — CREATE)
            ├── (IWalkinBookingService.cs           — CREATE)
            └── (WalkinBookingService.cs            — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Features/Patients/PatientSearchController.cs | GET /api/patients/search with ILIKE + 3-char guard |
| CREATE | src/api/Features/Patients/WalkinPatientController.cs | POST /api/patients/walkin-create for new walk-in patients |
| CREATE | src/api/Features/Patients/WalkinPatientService.cs | Minimal patient + user record creation |
| CREATE | src/api/Features/Bookings/IWalkinBookingService.cs | Interface with CreateAsync |
| CREATE | src/api/Features/Bookings/WalkinBookingService.cs | Duplicate check + slot lock + queue position + transaction |
| CREATE | src/api/Features/Bookings/WalkinBookingController.cs | POST /api/bookings/walkin |
| MODIFY | src/api/Program.cs | Register IWalkinBookingService and IWalkinPatientService scoped |

---

## External References
- https://www.npgsql.org/efcore/mapping/full-text-search.html (Npgsql EF Core — `EF.Functions.ILike` for case-insensitive pattern matching on PostgreSQL; AC-002 — 500ms SLA via index)
- https://learn.microsoft.com/en-us/ef/core/saving/transactions?view=efcore-8.0 (EF Core 8 IDbContextTransaction — slot lock + booking commit; AC-003 atomicity)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] `GET /api/patients/search?q=Jo` (2 chars) returns 400; `?q=Joh` (3 chars) returns matching patients within 500ms (AC-002; OWASP A03)
- [ ] `POST /api/bookings/walkin` with valid patientId, slotId, reason, priority; verify 201 response with `bookingId`, `status = "Confirmed"`, and `queuePosition >= 1`; verify slot status updated to `Booked` in DB (AC-003)
- [ ] `POST /api/bookings/walkin` for a patient who already has a Confirmed booking today; verify 409 with `error = "DuplicateBookingToday"` and `existingBookingId`; re-submit with `overrideDuplicate = true`; verify 201 (Edge: duplicate)
- [ ] Mark all today's slots as `Booked`; call `POST /api/bookings/walkin`; verify 409 `error = "SlotNoLongerAvailable"` (Edge: no available slots)
- [ ] `POST /patients/walkin-create` with firstName, lastName, DOB; verify 201 with `patientId`; verify Patient row in DB (AC-004)
- [ ] Call `POST /api/bookings/walkin` authenticated as a Patient role; verify 403 (OWASP A01)
- [ ] Verify audit log contains `WalkinBookingCreated` with `staffId` and `bookingId` — no PHI (OWASP A02)

---

## Implementation Checklist
- [x] `GET /patients/search` rejects `q` with fewer than 3 characters with 400 at the controller boundary — no DB query is executed for under-length input (OWASP A03; AC-002 SLA guard)
- [x] `ILIKE` query uses parameterised EF Core `EF.Functions.ILike(column, $"%{term}%")` — the `term` value is never interpolated directly into raw SQL (OWASP A03 — SQL injection prevention)
- [x] `staffId` is extracted exclusively from the JWT claim in both `POST /bookings/walkin` and `POST /patients/walkin-create`; neither endpoint accepts a `staffId` field in the request body (OWASP A01; A07)
- [x] Duplicate-today check runs before the `IDbContextTransaction` opens; if 409 is returned for duplicate, no slot lock is acquired (Edge: duplicate — avoids unnecessary lock hold)
- [x] `SELECT FOR UPDATE` with `SET LOCAL lock_timeout = '5s'` is applied inside the transaction; a `PostgresException` with SqlState `"55P03"` (lock timeout) returns 503 to the caller (AC-003; OWASP A04 — consistent with us_020 pattern)
- [x] `POST /patients/walkin-create` creates a minimal patient record only — no intake form, no insurance record, no full registration flow is triggered from this endpoint (AC-004 — minimum required fields only)
- [x] Both endpoints are decorated with `[Authorize(Roles = $"{Roles.Staff},{Roles.Admin}")]`; Patient role receives 403; unauthenticated callers receive 401 (OWASP A01)
