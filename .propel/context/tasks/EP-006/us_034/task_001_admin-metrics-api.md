# Task - TASK_001

## Requirement Reference
- **User Story:** us_034
- **Story Location:** .propel/context/tasks/EP-006/us_034/us_034.md
- **Acceptance Criteria:**
  - AC-001: `GET /admin/metrics?date=today` returns `AdminMetricsDto` containing Total Bookings Today, Confirmed Count, Cancelled Count, Walk-ins Count, Average Wait Time (minutes), and No-show Risk Distribution (High/Medium/Low counts) — all for the requested calendar day
  - AC-004: `GET /admin/metrics` is restricted to the Admin role only; Staff and Patient roles receive HTTP 403 `{"error": "Access denied. Admin role required."}` — no metric data is returned to non-Admin callers
- **Edge Cases:**
  - No bookings today: all counts return as zero integers and `averageWaitMinutes` returns `null`; the endpoint returns HTTP 200 with a zero-filled DTO — never an error response for an empty result set
  - Metric aggregation query exceeds 5 seconds: endpoint returns HTTP 503 `{"error": "Metrics temporarily unavailable."}` via `OperationCanceledException` from a `CancellationTokenSource(5s)`; the database connection is released cleanly; no stack trace is exposed to the client

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — `GET /admin/metrics` with `DateOnly` query param, Admin RBAC guard, and `AdminMetricsDto` response (AC-001, AC-004) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — load today's bookings with `Include(b => b.Slot)`; LINQ in-memory aggregation for counts and average; `CancellationToken` passed to `ToListAsync` (AC-001; Edge: timeout) |
| Database | PostgreSQL 15.3+ | 15.3+ | TR-007 — `bookings` table joined with `slots`; `no_show_risk_tier` and `is_walkin` columns aggregated (AC-001) |

---

## Task Overview

Implement `GET /admin/metrics?date=<date>` restricted to the Admin role. The handler loads all bookings for the requested date with their slots, then performs LINQ in-memory aggregation to compute: total count, counts by status (Confirmed, Cancelled), walk-in count, average wait time (minutes from slot start to check-in arrival), and no-show risk tier distribution. A `CancellationTokenSource(5s)` wraps the `ToListAsync` call; `OperationCanceledException` maps to HTTP 503 with a safe error message. The response DTO contains only aggregate integers and a nullable double — no PHI fields.

---

## Dependent Tasks
- task_001 (us_009) — `Roles.Admin` constant and JWT auth middleware must be present
- task_001 (us_019) — `Slot` entity with `StartTime` and `Date` columns
- task_001 (us_021) — `Booking.NoShowRiskTier` column populated by risk scoring job
- task_001 (us_030) — `Booking.IsWalkin` (or `CreatedByStaffId` != null) and `Booking.CheckedInAt` columns must exist

---

## Impacted Components
- `src/api/Features/Admin/AdminMetricsController.cs` — new: `GET /admin/metrics` with Admin RBAC and date param
- `src/api/Features/Admin/AdminMetricsService.cs` — new: aggregation logic + timeout guard
- `src/api/Features/Admin/IAdminMetricsService.cs` — new: service interface
- `src/api/Features/Admin/AdminMetricsDto.cs` — new: response DTO
- `src/api/Program.cs` — modified: register `IAdminMetricsService` as scoped

---

## Implementation Plan
1. `GET /admin/metrics` endpoint in `AdminMetricsController`: `[Authorize(Roles = Roles.Admin)]`; accept `DateOnly? date` query param; validate with `DateOnly.TryParse` — if absent or invalid, default to `DateOnly.FromDateTime(DateTime.UtcNow.Date)`; delegate to `IAdminMetricsService.GetMetricsAsync(date, ct)` (AC-001, AC-004; OWASP A01, A03)
2. Timeout guard: inside `GetMetricsAsync`, create `using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5))`; pass `cts.Token` to `ToListAsync`; wrap in `try { ... } catch (OperationCanceledException) { throw new MetricsTimeoutException(); }` — controller catches `MetricsTimeoutException` and returns `StatusCode(503, new { error = "Metrics temporarily unavailable." })` — no stack trace in the response body (Edge: query > 5s; OWASP A04)
3. Booking load query: `await dbContext.Bookings.Include(b => b.Slot).Where(b => DateOnly.FromDateTime(b.Slot.StartTime.Date) == date).ToListAsync(cts.Token)` — single query; no raw SQL; `cts.Token` enables cancellation after 5 seconds (AC-001; Edge: timeout)
4. LINQ in-memory aggregation: `TotalBookings = bookings.Count`; `Confirmed = bookings.Count(b => b.Status == BookingStatus.Confirmed)`; `Cancelled = bookings.Count(b => b.Status == BookingStatus.Cancelled)`; `WalkIns = bookings.Count(b => b.IsWalkin)`; `HighRisk/MediumRisk/LowRisk = bookings.Count(b => b.NoShowRiskTier == "High"/…)` — all zero-safe (Edge: no bookings returns 0)
5. Average wait time: `AverageWaitMinutes = bookings.Where(b => b.CheckedInAt.HasValue).Select(b => (b.CheckedInAt!.Value - b.Slot.StartTime).TotalMinutes).DefaultIfEmpty().Average()` cast to `double?`; returns `null` if no check-in data exists; the frontend renders `"—"` for null and `"0"` for zero (AC-001)
6. `AdminMetricsDto` record: `record AdminMetricsDto(int TotalBookings, int Confirmed, int Cancelled, int WalkIns, double? AverageWaitMinutes, int HighRisk, int MediumRisk, int LowRisk)` — all fields are non-nullable integers except `AverageWaitMinutes`; no patient name, ID, or PHI included (AC-001; OWASP A02 — aggregate data only)

---

## Current Project State
```
src/
└── api/
    └── Features/
        └── Admin/
            ├── (AdminMetricsController.cs   — CREATE)
            ├── (IAdminMetricsService.cs     — CREATE)
            ├── (AdminMetricsService.cs      — CREATE)
            └── (AdminMetricsDto.cs          — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Features/Admin/AdminMetricsController.cs | GET /admin/metrics with Admin RBAC and date param |
| CREATE | src/api/Features/Admin/IAdminMetricsService.cs | Service interface |
| CREATE | src/api/Features/Admin/AdminMetricsService.cs | Aggregation + timeout guard |
| CREATE | src/api/Features/Admin/AdminMetricsDto.cs | Response DTO record |
| MODIFY | src/api/Program.cs | Register IAdminMetricsService as scoped |

---

## External References
- https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads (CancellationTokenSource — 5-second timeout on EF Core query; Edge: query > 5s; OWASP A04)
- https://learn.microsoft.com/en-us/ef/core/querying/related-data/eager (EF Core 8 — `Include(b => b.Slot)` for single-pass aggregation query; AC-001 performance)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Seed 10 bookings for today with mixed statuses and risk tiers; call `GET /admin/metrics?date=today`; verify all count fields match the seeded data (AC-001)
- [ ] Seed no bookings for today; verify 200 response with all counts = 0 and `averageWaitMinutes = null` (Edge: no bookings)
- [ ] Stub EF Core query to delay 6 seconds; verify 503 `{"error": "Metrics temporarily unavailable."}` with no stack trace (Edge: query > 5s)
- [ ] Call `GET /admin/metrics` authenticated as Staff role; verify 403 (AC-004; OWASP A01)
- [ ] Call `GET /admin/metrics` authenticated as Patient role; verify 403 (AC-004; OWASP A01)
- [ ] Verify `AdminMetricsDto` contains no `patientId`, `patientName`, or other PHI fields (OWASP A02)

---

## Implementation Checklist
- [x] `[Authorize(Roles = Roles.Admin)]` is the sole RBAC decorator — `Roles.Staff` and `Roles.Patient` are not included; any non-Admin JWT receives 403 (AC-004; OWASP A01)
- [x] `CancellationTokenSource` is disposed via `using` even when cancellation is not triggered — no `CancellationTokenSource` instances are left undisposed on success paths (Edge: timeout; resource management)
- [x] The 503 response body is a static string `{"error": "Metrics temporarily unavailable."}` — the `OperationCanceledException` message, stack trace, and inner exception are never serialised to the response (OWASP A04 — no internal detail exposure)
- [x] `AdminMetricsDto` contains only aggregate numeric fields — no `patientId`, `patientName`, or any other PHI-bearing field; the DTO is sufficient to populate all metric cards in the frontend without requiring a second API call (AC-001; OWASP A02)
- [x] All LINQ aggregation operations use `arrivedBookings.Count > 0` guard before `.Average()` which returns null on empty collections — no `InvalidOperationException` on an empty booking set (Edge: no bookings)
- [x] `date` query parameter is validated with `DateOnly.TryParse` before use; the parsed value is passed to a parameterised EF Core `.Where` predicate — never interpolated into raw SQL (OWASP A03; decision logged: F012, F013)
