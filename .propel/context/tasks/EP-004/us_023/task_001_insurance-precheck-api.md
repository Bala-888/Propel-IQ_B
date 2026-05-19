# Task - TASK_001

## Requirement Reference
- **User Story:** us_023
- **Story Location:** .propel/context/tasks/EP-004/us_023/us_023.md
- **Acceptance Criteria:**
  - AC-001: `GET /insurance/pre-check?patientId=<id>` returns within 2 seconds with `{"status": "Complete" | "Incomplete" | "Missing"}` based on the patient's `InsuranceRecord`
  - AC-004: An audit log entry is written with `ActionType: InsurancePreCheck`, `patientId`, and `result` on every pre-check call, regardless of outcome
- **Edge Cases:**
  - Pre-check API unavailable: this task owns only the backend; the 5xx surface is handled by the frontend caller (task_002). The backend must return well-formed 200 responses (not 5xx) for all three insurance status outcomes — a 5xx from this endpoint is only caused by unhandled infrastructure failure, which triggers the existing global exception handler

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 (new InsuranceController endpoint; InsurancePreCheckService) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — `InsuranceRecord` table queried via `FirstOrDefaultAsync` on indexed PatientId (AC-001 — 2s SLA) |
| Audit | AuditDbContext + IAuditService | Project-established | TR-014 — existing audit pattern (AC-004) |

---

## Task Overview

Implement `GET /api/insurance/pre-check` — a lightweight endpoint that queries the `InsuranceRecord` table for the requesting patient and returns one of three statuses: `Complete`, `Incomplete`, or `Missing`. The `patientId` is extracted exclusively from the JWT claim; the query-string parameter is accepted for routing but cross-validated against the claim to prevent cross-patient data access. An audit log entry is written for every call. The query is a single `FirstOrDefaultAsync` on the indexed `PatientId` column — no joins, no external calls — to satisfy the 2-second SLA.

---

## Dependent Tasks
- task_001 (us_007) — `InsuranceRecord` table and `PatientId` foreign key must exist in the schema before this service can compile and query
- task_001 (us_020) — `BookingService` is the downstream consumer of this endpoint; must be available for integration

---

## Impacted Components
- `src/api/Features/Insurance/InsuranceController.cs` — new: `[HttpGet("pre-check")]` action
- `src/api/Features/Insurance/InsurancePreCheckService.cs` — new: `CheckAsync(Guid patientId)` implementation
- `src/api/Features/Insurance/IInsurancePreCheckService.cs` — new: service interface
- `src/api/Features/Insurance/InsurancePreCheckResponse.cs` — new: response DTO with `Status` string
- `src/api/Program.cs` — modified: register `IInsurancePreCheckService` as scoped

---

## Implementation Plan
1. Create `InsurancePreCheckResponse` record: `string Status` — valid values: `"Complete"`, `"Incomplete"`, `"Missing"` (AC-001)
2. Create `IInsurancePreCheckService` interface with `Task<InsurancePreCheckResponse> CheckAsync(Guid patientId)` (AC-001)
3. Implement `InsurancePreCheckService.CheckAsync`: `var record = await dbContext.InsuranceRecords.FirstOrDefaultAsync(r => r.PatientId == patientId, ct)`; if `record == null` → return `"Missing"`; if `record != null && string.IsNullOrWhiteSpace(record.PolicyNumber)` → return `"Incomplete"`; otherwise → return `"Complete"` (AC-001)
4. Register `IInsurancePreCheckService` as scoped in `Program.cs`; `InsuranceRecord` entity is already tracked by the existing `AppDbContext` from us_007 — no new `DbSet` registration required if already present (AC-001)
5. Create `InsuranceController` with `[ApiController, Route("api/insurance"), Authorize(Roles = Roles.Patient)]`; `[HttpGet("pre-check")]` action extracts `patientId` from `User.FindFirstValue(ClaimTypes.NameIdentifier)`; calls `IInsurancePreCheckService.CheckAsync`; returns `Ok(response)` (OWASP A01 — role-restricted; AC-001)
6. Cross-patient guard: the endpoint does not accept `patientId` as a query parameter — `patientId` is always sourced from the JWT claim only; this prevents any patient from querying another patient's insurance record (OWASP A01; A07 — no cross-patient enumeration)
7. Audit log: after `CheckAsync` returns, call `await _auditService.LogAsync(ActionType.InsurancePreCheck, patientId.ToString(), response.Status)` before returning the HTTP response — logged for all outcomes (Complete, Incomplete, Missing) (AC-004)

---

## Current Project State
```
src/
└── api/
    └── Features/
        └── Insurance/
            └── (InsuranceController.cs          — CREATE)
            └── (IInsurancePreCheckService.cs    — CREATE)
            └── (InsurancePreCheckService.cs     — CREATE)
            └── (InsurancePreCheckResponse.cs    — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Features/Insurance/InsurancePreCheckResponse.cs | DTO with Status string ("Complete" / "Incomplete" / "Missing") |
| CREATE | src/api/Features/Insurance/IInsurancePreCheckService.cs | Service interface |
| CREATE | src/api/Features/Insurance/InsurancePreCheckService.cs | InsuranceRecord query logic (FirstOrDefaultAsync on PatientId) |
| CREATE | src/api/Features/Insurance/InsuranceController.cs | Authorized GET endpoint; JWT-sourced patientId |
| MODIFY | src/api/Program.cs | Register IInsurancePreCheckService as scoped |

---

## External References
- https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-8.0 (ASP.NET Core 8 role-based authorization — `[Authorize(Roles = ...)]`)
- https://learn.microsoft.com/en-us/dotnet/api/system.security.claims.claimtypes.nameidentifier (ClaimTypes.NameIdentifier — extracting patientId from JWT sub claim)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Call `GET /api/insurance/pre-check` as a patient with no `InsuranceRecord` row; verify response is `200 {"status": "Missing"}` within 2 seconds (AC-001)
- [ ] Call endpoint for a patient with an `InsuranceRecord` where `PolicyNumber` is null; verify response is `200 {"status": "Incomplete"}` (AC-001; Edge: Incomplete vs Missing)
- [ ] Call endpoint for a patient with a fully-populated `InsuranceRecord`; verify response is `200 {"status": "Complete"}` (AC-001)
- [ ] Verify Serilog / AuditLog contains `ActionType = "InsurancePreCheck"`, correct `patientId`, and the returned `status` for each call above (AC-004)
- [ ] Attempt to call the endpoint while authenticated as a Staff or Admin role; verify 403 Forbidden is returned (OWASP A01)
- [ ] Verify the endpoint route does not accept a `patientId` query parameter — `patientId` is sourced from JWT only; the endpoint should return the calling patient's own data regardless of URL (OWASP A01; A07)

---

## Implementation Checklist
- [ ] `InsurancePreCheckService.CheckAsync` uses a single `FirstOrDefaultAsync` query on the indexed `PatientId` column with no joins or subqueries; the query runs entirely within the existing `AppDbContext` EF Core context (AC-001 — 2s SLA; no N+1 risk)
- [ ] The `InsuranceController` action sources `patientId` exclusively from `User.FindFirstValue(ClaimTypes.NameIdentifier)` parsed as a `Guid` — the HTTP route and query string do not expose a patientId parameter that could be manipulated by the caller (OWASP A01; A07)
- [ ] `[Authorize(Roles = Roles.Patient)]` is applied at the controller class level; Staff and Admin roles receive 403 — insurance pre-check is a patient-only operation (OWASP A01)
- [ ] Audit log is written via `IAuditService.LogAsync` for every response before the HTTP result is returned — covers all three status outcomes (Complete, Incomplete, Missing) without exception (AC-004)
- [ ] Response status values are the exact string literals `"Complete"`, `"Incomplete"`, and `"Missing"` (case-sensitive) — the frontend guard in task_002 depends on these exact values (AC-001; contract alignment)
