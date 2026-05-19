# Task - TASK_001

## Requirement Reference
- **User Story:** us_019
- **Story Location:** .propel/context/tasks/EP-004/us_019/us_019.md
- **Acceptance Criteria:**
  - AC-001: `GET /slots?available=true` returns HTTP 200 with a JSON array containing only slots where `status = "Available"` — Booked and Blocked slots are excluded
  - AC-002: `GET /slots?available=true&page=1&pageSize=10` returns exactly 10 slots and a `pagination` object with `{"total": 50, "page": 1, "pageSize": 10, "totalPages": 5}` when 50 available slots exist
- **Edge Cases:**
  - No available slots: the endpoint returns 200 with an empty `slots` array and a `pagination` object where `total = 0`, `totalPages = 0` — not a 404

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 (GET /slots endpoint with query filtering and pagination) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-008 (query `appointment_slots` table with WHERE + SKIP/TAKE; depends on us_005 schema) |
| Database | PostgreSQL | 15.3+ | TR-007 (`appointment_slots` table with `status` column; existing schema from us_005) |

---

## Task Overview

Build the `GET /slots` endpoint that returns paginated available appointment slots. When `available=true`, only rows with `status = "Available"` are returned. The response includes a `slots` array of `SlotDto` objects and a `pagination` metadata object. Input bounds are validated at the API boundary. No patient identifiers are returned — `SlotDto` contains only schedule data.

---

## Dependent Tasks
- task_002 (us_005) — `appointment_slots` table with `id`, `date`, `start_time`, `duration_minutes`, `status` columns must exist in the database schema

---

## Impacted Components
- `src/api/Features/Slots/SlotsController.cs` — new: GET /slots with `available`, `page`, `pageSize` query parameters
- `src/api/Features/Slots/GetSlotsQuery.cs` — new: query object with `Available`, `Page`, `PageSize` properties
- `src/api/Features/Slots/SlotDto.cs` — new: response DTO (id, date, startTime, durationMinutes, status)
- `src/api/Features/Slots/SlotsResponse.cs` — new: wrapper with `slots: SlotDto[]` and `pagination` object
- `src/api/Features/Slots/SlotsService.cs` — new: EF Core query with Available filter and SKIP/TAKE pagination

---

## Implementation Plan
1. Define `GetSlotsQuery` record with `bool Available` (default `true`), `int Page` (default `1`), `int PageSize` (default `10`); validate at the endpoint: `Page >= 1`, `PageSize >= 1`, `PageSize <= 100` — return 400 `ValidationProblemDetails` if any bound is violated (OWASP A03 — validate at boundary; AC-002)
2. Define `SlotDto` record: `Guid Id`, `DateOnly Date`, `TimeOnly StartTime`, `int DurationMinutes`, `string Status`; define `PaginationMeta` record: `int Total`, `int Page`, `int PageSize`, `int TotalPages`; define `SlotsResponse` record: `IReadOnlyList<SlotDto> Slots`, `PaginationMeta Pagination` (AC-002, AC-003)
3. Implement `SlotsService.GetAvailableSlotsAsync`: EF Core query on `appointment_slots` — `WHERE status = 'Available'` when `query.Available = true`; execute `CountAsync()` first for `Total`; apply `.OrderBy(s => s.Date).ThenBy(s => s.StartTime)` then `.Skip((page - 1) * pageSize).Take(pageSize)`; compute `TotalPages = (int)Math.Ceiling((double)total / pageSize)`; return `SlotsResponse` (AC-001, AC-002)
4. Build `SlotsController.GetAsync`: `[Authorize]` (all authenticated roles — Patient, Staff, Admin may view slots); bind `[FromQuery] GetSlotsQuery query`; call `SlotsService.GetAvailableSlotsAsync`; return 200 with `SlotsResponse`; return empty `slots: []` and `total: 0, totalPages: 0` when no slots match (AC-001, AC-002; Edge: no available slots; OWASP A01)
5. Ensure `SlotDto` contains no patient identifiers — `booked_by_patient_id` and any patient-linked foreign key columns on `appointment_slots` must not be mapped into the DTO; response surface area is limited to schedule data only (OWASP A02 — minimum data exposure; HIPAA — no PHI in slot listings)

---

## Current Project State
```
src/
└── api/
    └── Features/
        └── Slots/
            └── (SlotsController.cs   — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Features/Slots/SlotsController.cs | GET /slots endpoint with filtering and pagination |
| CREATE | src/api/Features/Slots/GetSlotsQuery.cs | Query object with Available, Page, PageSize |
| CREATE | src/api/Features/Slots/SlotDto.cs | Schedule-data-only response DTO |
| CREATE | src/api/Features/Slots/SlotsResponse.cs | Wrapper with slots array and pagination metadata |
| CREATE | src/api/Features/Slots/SlotsService.cs | EF Core Available filter + SKIP/TAKE pagination |

---

## External References
- https://learn.microsoft.com/en-us/ef/core/querying/pagination?view=efcore-8.0 (EF Core 8 — SKIP/TAKE pagination)
- https://learn.microsoft.com/en-us/aspnet/core/web-api/action-return-types?view=aspnetcore-8.0 (ASP.NET Core 8 — 200/400 return types)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] `GET /slots?available=true` with a mix of Available, Booked, and Blocked rows in the DB → verify response contains only Available slots; no Booked or Blocked entries in the `slots` array (AC-001)
- [ ] `GET /slots?available=true&page=1&pageSize=10` with 50 available slots → verify exactly 10 slots returned, `pagination.total = 50`, `pagination.totalPages = 5` (AC-002)
- [ ] `GET /slots?available=true` with zero available slots → verify 200 with `slots: []` and `pagination.total = 0` — not a 404 (Edge: no available slots)
- [ ] `GET /slots?available=true&pageSize=200` → verify 400 response (pageSize > 100 guard; OWASP A03)
- [ ] `GET /slots?available=true&page=0` → verify 400 response (page < 1 guard; OWASP A03)
- [ ] Verify `SlotDto` response contains no `patientId`, `bookedByPatientId`, or any patient-linked identifier (OWASP A02)
- [ ] Unauthenticated request → verify 401 response (OWASP A01)

---

## Implementation Checklist
- [ ] `GetSlotsQuery` is validated at the API boundary before any DB access: `Page >= 1`, `PageSize >= 1 && <= 100`; invalid inputs return 400 `ValidationProblemDetails` before `SlotsService` is called (OWASP A03 — validate at boundary; AC-002)
- [ ] EF Core query applies `WHERE status = 'Available'` filter when `query.Available = true`; Booked and Blocked rows are excluded at the query level — not filtered in application code after retrieval (AC-001; performance — database-side filter)
- [ ] `TotalPages` is computed server-side as `(int)Math.Ceiling((double)total / pageSize)`; when `total = 0`, `TotalPages = 0`; the `pagination` object is always present in the response (AC-002; Edge: no available slots)
- [ ] `SlotDto` is mapped from `AppointmentSlot` entity using only the fields `Id`, `Date`, `StartTime`, `DurationMinutes`, `Status`; no patient-linked foreign key (`BookedByPatientId` or equivalent) is projected into the DTO (OWASP A02 — minimum data exposure; HIPAA)
- [ ] `GET /slots` is decorated `[Authorize]`; unauthenticated requests receive 401; all authenticated roles (Patient, Staff, Admin) are permitted to call this endpoint (OWASP A01 — broken access control; AC-001)
