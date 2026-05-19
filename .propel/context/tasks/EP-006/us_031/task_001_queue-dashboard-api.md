# Task - TASK_001

## Requirement Reference
- **User Story:** us_031
- **Story Location:** .propel/context/tasks/EP-006/us_031/us_031.md
- **Acceptance Criteria:**
  - AC-001: `GET /api/queue?date=today` returns all today's Confirmed and CheckedIn bookings ordered by position; each entry includes `position`, `patientName`, `arrivalTime`, `appointmentTime`, `noShowRiskTier`, and `status`
- **Edge Cases:**
  - Missing risk score: if `Booking.NoShowRiskTier` is null, the DTO must return the string `"Unknown"` — the API never returns a null `noShowRiskTier` field to the client

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — `GET /api/queue` endpoint with query parameter, RBAC guard, and PHI-decrypted response (AC-001) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — filter by `slot.Date` and `BookingStatus`; order by `QueuePosition`; single query projection to DTO (AC-001) |
| Database | PostgreSQL 15.3+ | 15.3+ | TR-007 — `bookings` table join with `slots` table; `no_show_risk_tier` column populated by us_021 background job (AC-001) |
| PHI Encryption | IPhiEncryptionService (BouncyCastle AES-256) | 2.x | Decrypt `PatientName` for authorised Staff/Admin display in `QueueEntryDto` — no PHI returned to Patient role (AC-001; OWASP A01) |

---

## Task Overview

Implement `GET /api/queue?date=today` restricted to Staff and Admin roles. The endpoint queries all bookings for the requested date with `Confirmed` or `CheckedIn` status, orders them by `QueuePosition`, and projects a `QueueEntryDto` per row. `PatientName` is PHI-decrypted inline. `NoShowRiskTier` null values are normalised to the string `"Unknown"` in the DTO — raw null is never returned to the client. The `date` query parameter is validated before use; it is never interpolated into raw SQL.

---

## Dependent Tasks
- task_001 (us_009) — `Roles.Staff` and `Roles.Admin` constants and JWT middleware must be present
- task_001 (us_019) — `Slot` entity with `Date` and `Status` columns must exist for the date-based filter
- task_001 (us_021) — `Booking.NoShowRiskTier` column (populated by risk scoring job) is read by this endpoint
- task_001 (us_030) — `Booking.QueuePosition` and `Booking.CheckedInAt` columns must be present for ordering and arrival time

---

## Impacted Components
- `src/api/Features/Queue/QueueController.cs` — new: `GET /api/queue` with date query param
- `src/api/Features/Queue/QueueService.cs` — new: query, DTO projection, null-risk normalisation
- `src/api/Features/Queue/IQueueService.cs` — new: service interface
- `src/api/Features/Queue/QueueEntryDto.cs` — new: response DTO
- `src/api/Program.cs` — modified: register `IQueueService` as scoped

---

## Implementation Plan
1. `GET /api/queue` endpoint: `[Authorize(Roles = $"{Roles.Staff},{Roles.Admin}")]`; accepts `date` query param (`DateOnly?`); if `date` is null or not parseable, default to `DateOnly.FromDateTime(DateTime.UtcNow.Date)`; validated before use — never interpolated as raw SQL (AC-001; OWASP A03)
2. EF Core query in `QueueService.GetQueueAsync(DateOnly date)`: `dbContext.Bookings.Include(b => b.Slot).Include(b => b.Patient).Where(b => DateOnly.FromDateTime(b.Slot.StartTime.Date) == date && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn)).OrderBy(b => b.QueuePosition).ThenBy(b => b.CreatedAt)` (AC-001)
3. Project to `QueueEntryDto`: `Position = b.QueuePosition`, `PatientName = _phiEncryptionService.Decrypt(b.Patient.FullNameEncrypted)`, `ArrivalTime = b.CheckedInAt`, `AppointmentTime = b.Slot.StartTime`, `NoShowRiskTier = b.NoShowRiskTier ?? "Unknown"`, `Status = b.Status.ToString()` — one LINQ Select call; no second roundtrip (AC-001; Edge: null risk)
4. `NoShowRiskTier` normalisation: use null-coalescing `?? "Unknown"` in the projection — never return a null value for this field to the caller; the frontend contract guarantees one of `"High"`, `"Medium"`, `"Low"`, `"Unknown"` (Edge: missing risk score)
5. PHI handling: `PatientName` is decrypted via `IPhiEncryptionService.Decrypt()` inside the projection for this Staff/Admin-only endpoint; `PatientName` must not appear in any `Log.Information` or `Log.Debug` statement in this class (OWASP A02 — no PHI in structured logs)
6. Register `IQueueService` → `QueueService` as `AddScoped` in `Program.cs`; inject `IPhiEncryptionService` via DI — do not instantiate directly (OWASP A04 — DI lifetime consistency with scoped DbContext)
7. `QueueEntryDto` record: `record QueueEntryDto(int Position, string PatientName, DateTimeOffset? ArrivalTime, DateTimeOffset AppointmentTime, string NoShowRiskTier, string Status)` — all string fields non-nullable in the type; only `ArrivalTime` is nullable (pre-scheduled patients have no check-in time yet) (AC-001; AC-003 — frontend computes wait time from this field)

---

## Current Project State
```
src/
└── api/
    └── Features/
        └── Queue/
            ├── (QueueController.cs     — CREATE)
            ├── (IQueueService.cs       — CREATE)
            ├── (QueueService.cs        — CREATE)
            └── (QueueEntryDto.cs       — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Features/Queue/QueueController.cs | GET /api/queue with date query param and RBAC guard |
| CREATE | src/api/Features/Queue/IQueueService.cs | Service interface |
| CREATE | src/api/Features/Queue/QueueService.cs | Query, projection, PHI decrypt, null-risk normalisation |
| CREATE | src/api/Features/Queue/QueueEntryDto.cs | Response DTO record |
| MODIFY | src/api/Program.cs | Register IQueueService as scoped |

---

## External References
- https://learn.microsoft.com/en-us/ef/core/querying/related-data/eager (EF Core 8 eager loading — `Include(b => b.Slot).Include(b => b.Patient)` for single-query projection; AC-001 performance)
- https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record (C# record — `QueueEntryDto` as immutable response projection)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Seed 5 Confirmed bookings for today with varying risk tiers; call `GET /api/queue?date=today`; verify 200 with 5 rows ordered by `position` ascending (AC-001)
- [ ] Verify one row has `noShowRiskTier = null` in DB; confirm API returns `"Unknown"` for that row — not null (Edge: missing risk)
- [ ] Call `GET /api/queue?date=today` authenticated as Patient role; verify 403 (OWASP A01)
- [ ] Call `GET /api/queue` without `date` param; verify it defaults to today and returns today's queue (AC-001)
- [ ] Call `GET /api/queue?date=not-a-date`; verify it defaults gracefully to today — no 500 error (OWASP A03)
- [ ] Verify Serilog log output for the endpoint call contains no patient name or arrival time (OWASP A02)

---

## Implementation Checklist
- [ ] The `date` query parameter is parsed with `DateOnly.TryParse`; if absent or invalid, the handler defaults to `DateOnly.FromDateTime(DateTime.UtcNow.Date)` — no raw SQL interpolation of user input (OWASP A03; AC-001)
- [ ] Query filters on `BookingStatus.Confirmed || BookingStatus.CheckedIn` only — no other statuses are included regardless of what is in the database (AC-001 scope)
- [ ] `NoShowRiskTier` is mapped as `b.NoShowRiskTier ?? "Unknown"` in the LINQ projection — the `QueueEntryDto.NoShowRiskTier` property is declared as `string` (non-nullable), so the compiler enforces the null-coalescing (Edge: missing risk)
- [ ] `PatientName` is decrypted via injected `IPhiEncryptionService`; it does not appear in any `ILogger` call in `QueueService` or `QueueController` (OWASP A02)
- [ ] `IQueueService` is registered as `AddScoped` — not `AddSingleton` — to avoid a captive dependency with the scoped `AppDbContext` (OWASP A04 — DI lifetime)
- [ ] Endpoint is decorated with `[Authorize(Roles = $"{Roles.Staff},{Roles.Admin}")]`; a Patient-role JWT receives 403; an unauthenticated request receives 401 (OWASP A01; AC-001)
- [ ] `ArrivalTime` is nullable `DateTimeOffset?` in the DTO — pre-scheduled patients who have not yet checked in will have a null value; the frontend handles this by displaying "—" for Wait Time (AC-003 contract)
