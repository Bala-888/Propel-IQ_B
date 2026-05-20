# Task - TASK_001

## Requirement Reference
- **User Story:** us_040
- **Story Location:** .propel/context/tasks/EP-007-II/us_040/us_040.md
- **Acceptance Criteria:**
  - AC-001: `GET /patients/search?q=<term>` returns ≤20 matching patients (name, DOB, patient ID) within 500ms when term is ≥3 characters; rejects requests with fewer than 3 characters with HTTP 400
  - AC-002: `GET /patients/{id}/summary` returns an aggregated payload (`demographics`, `entities`, `activeBookings`, `documents`) within 2 seconds for a patient with up to 50 documents; documents are paginated (default page size 20)
  - AC-003: Summary aggregation uses no more than 3 database round trips (demographics + entities via Include/JOIN in RT1; active bookings in RT2; documents + count in RT3) — no N+1 queries
  - AC-004: Both endpoints require `Staff`, `Admin`, or `Clinician` role; `Patient` role receives HTTP 403 `{"error": "Access denied."}`
- **Edge Cases:**
  - No extracted entities: `PatientSummaryDto.Entities` is always an array (never null); an empty array is returned when `patient_entities` has no rows for the patient — the frontend is responsible for the empty state message
  - 100+ documents: the documents list is paginated at the query layer (`SKIP/TAKE`) — all document rows are never loaded into EF Core change tracking simultaneously

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes — both endpoints consumed by task_002 (SCR-013 search, SCR-014 summary view) |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-014-360-patient-view.html |
| **Screen Spec** | SCR-013 (Patient Search), SCR-014 (360° Patient View) |
| **UXR Requirements** | UXR-105 — `EntityDto` must include `Confidence` (float) and `LowConfidence` (bool) so the frontend can render text + icon confidence indicators |
| **Design Tokens** | N/A |

---

## AI References
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No — this task does not invoke Ollama or AI models |
| **AIR Requirements** | N/A — entity data (populated by EP-007-I pipeline) is read-only from this endpoint |
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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — `PatientsController` with search and summary actions; `[Authorize(Roles)]` for RBAC; linked `CancellationTokenSource` for 2s SLA guard (AC-001–004) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — `EF.Functions.ILike` for ILIKE prefix search; `Include(p => p.PatientEntities)` for RT1 JOIN; `Skip/Take` for documents pagination; `IsolationLevel.ReadCommitted` transaction wrapping RT1–RT3 (AC-001–003; OWASP A03) |
| Database | PostgreSQL 15.3+ | 15.3+ | TR-007 — `text_pattern_ops` B-tree index on `(lower(last_name), lower(first_name))` for ILIKE prefix search; 500ms SLA cannot be met without this index on large patient tables (AC-001; NFR-003) |
| Logging | Serilog + Seq | .NET 8.0 compatible / 2023.4+ | TR-011 — only patient UUID logged in error paths; search term `q`, demographics, and entity values never in ILogger calls (OWASP A02 — PHI guard) |

---

## Task Overview

Implement `PatientsController` with two endpoints. `GET /patients/search` validates the minimum term length, escapes LIKE metacharacters, executes a ILIKE prefix query against an indexed `patients` table, and returns a DTO list. `GET /patients/{id}/summary` uses a three-round-trip aggregation strategy wrapped in a `ReadCommitted` transaction and guarded by a 2-second `CancellationTokenSource`; it returns a flat `PatientSummaryDto` with all four sections. Both endpoints are gated by `[Authorize(Roles = "Staff,Admin,Clinician")]`. An EF Core migration adds the search index. PHI fields are never logged.

---

## Dependent Tasks
- task_001 (us_007) — `patients` table must exist with `first_name`, `last_name`, `date_of_birth`, `insurance_id` columns
- task_001 (us_038) — `patient_entities` table must exist and be populated by the extraction pipeline
- task_001 (us_019) — `appointments` table must exist with `status` and `patient_id` columns for active bookings query
- task_001 (us_035) — `document_records` table must exist for documents list query

---

## Impacted Components
- `src/api/Controllers/PatientsController.cs` — new: `GET /patients/search` and `GET /patients/{id}/summary` actions
- `src/api/Features/Patients/PatientSearchResultDto.cs` — new: `{ Guid Id, string FullName, DateOnly DateOfBirth, string PatientCode }`
- `src/api/Features/Patients/PatientSummaryDto.cs` — new: flat DTO with Demographics, Entities[], ActiveBookings[], Documents[], TotalDocumentCount, CurrentPage, PageSize
- `src/api/Features/Patients/EntityDto.cs` — new: `{ Guid Id, string Type, string Value, float Confidence, bool LowConfidence, DateTimeOffset LastSeenAt }`
- `src/api/Program.cs` — no change required (PatientsController auto-discovered by convention)
- EF Core migration `AddPatientSearchIndex` — new: B-tree index on lower(last_name, first_name) with text_pattern_ops

---

## Implementation Plan
1. `GET /patients/search?q=<term>` action in `PatientsController`: `[Authorize(Roles = "Staff,Admin,Clinician")]`; if `q.Length < 3` → return `BadRequest(new { error = "Search term must be at least 3 characters." })`; escape LIKE metacharacters: `var escaped = q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")`; query: `await db.Patients.Where(p => EF.Functions.ILike(p.LastName, escaped + "%") || EF.Functions.ILike(p.FirstName + " " + p.LastName, escaped + "%")).AsNoTracking().Take(20).Select(p => new PatientSearchResultDto { Id = p.Id, FullName = p.FirstName + " " + p.LastName, DateOfBirth = p.DateOfBirth, PatientCode = p.PatientCode }).ToListAsync(ct)` — `q` is never written to any ILogger call (AC-001; OWASP A02 — search term may be partial patient name; OWASP A03 — parameterised via EF.Functions.ILike)
2. `GET /patients/{id}/summary?page=1&pageSize=20` action: `[Authorize(Roles = "Staff,Admin,Clinician")]`; parse `page` (min 1) and `pageSize` (min 1, max 50) from query string with `Math.Clamp`; wrap entire aggregation with `using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)); var linkedCt = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, ct).Token`; on `OperationCanceledException` → return `StatusCode(503, new { error = "Summary temporarily unavailable." })` (AC-002; NFR-003 — 2s SLA guard; matches us_034 pattern)
3. Three-round-trip aggregation inside `await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, linkedCt)`: RT1 = `var patient = await db.Patients.Include(p => p.PatientEntities).AsNoTracking().Where(p => p.Id == id).FirstOrDefaultAsync(linkedCt)` — if null → `NotFound()`; RT2 = `var bookings = await db.Appointments.AsNoTracking().Where(a => a.PatientId == id && a.Status == "Scheduled").OrderBy(a => a.SlotStart).Select(BookingSummaryDto.Selector).ToListAsync(linkedCt)`; RT3 = `var totalDocs = await db.DocumentRecords.CountAsync(dr => dr.PatientId == id, linkedCt)` + `var docs = await db.DocumentRecords.AsNoTracking().Where(dr => dr.PatientId == id).OrderByDescending(dr => dr.CreatedAt).Skip((page-1)*pageSize).Take(pageSize).Select(DocumentSummaryDto.Selector).ToListAsync(linkedCt)`; commit transaction; assemble `PatientSummaryDto` (AC-002, AC-003 — ≤3 round trips)
4. `PatientSummaryDto` assembled: `Entities = patient.PatientEntities.Select(pe => new EntityDto { Id=pe.Id, Type=pe.Type, Value=pe.Value, Confidence=(float)pe.Confidence, LowConfidence=pe.LowConfidence, LastSeenAt=pe.LastSeenAt }).ToArray()` — never null; `AsNoTracking()` used on all queries to avoid change tracker overhead for read-only summary (Edge: no entities → empty array; OWASP A04 — AsNoTracking prevents unintentional writes; AC-003 — DTO shape)
5. RBAC 403 body: ASP.NET Core's default `[Authorize]` returns 403 without a body; add an `IAuthorizationMiddlewareResultHandler` or `AuthorizationFailureHandler` that returns `{ error = "Access denied." }` as JSON for all 403 responses — or add explicit `if (!User.IsInRole("Staff") && !User.IsInRole("Admin") && !User.IsInRole("Clinician")) return StatusCode(403, new { error = "Access denied." })` at the top of each action as defence-in-depth (AC-004; OWASP A01)
6. EF Core migration `AddPatientSearchIndex`: `migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS patients_name_search_idx ON patients USING btree (lower(last_name) text_pattern_ops, lower(first_name) text_pattern_ops)")` — `text_pattern_ops` operator class enables PostgreSQL to use this B-tree index for `ILIKE 'term%'` prefix patterns; without this, query planner performs a sequential scan which cannot meet the 500ms SLA on large tables; include in `Down` method: `migrationBuilder.Sql("DROP INDEX IF EXISTS patients_name_search_idx")` (AC-001 — 500ms SLA; NFR-003)
7. PHI logging discipline across all new `PatientsController` methods: `_logger.LogError("PatientSummaryFailed for {PatientId}", id)` — only UUID logged; `p.LastName`, `p.FirstName`, `p.Email`, `p.Phone`, entity `Value` strings, and search term `q` are never passed to any `ILogger` method anywhere in the controller; `[ResponseCache(Duration = 0, NoStore = true)]` on both endpoints to prevent CDN/proxy caching of PHI responses (OWASP A02 — PHI in demographics and entity values must not appear in Seq logs or HTTP caches)

---

## Current Project State
```
src/
└── api/
    ├── Controllers/
    │   └── (PatientsController.cs              — CREATE)
    └── Features/
        └── Patients/
            ├── (PatientSearchResultDto.cs      — CREATE)
            ├── (PatientSummaryDto.cs           — CREATE)
            └── (EntityDto.cs                   — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Controllers/PatientsController.cs | GET /patients/search and GET /patients/{id}/summary actions |
| CREATE | src/api/Features/Patients/PatientSearchResultDto.cs | { Id, FullName, DateOfBirth, PatientCode } |
| CREATE | src/api/Features/Patients/PatientSummaryDto.cs | Flat 360° aggregate DTO with all four sections + pagination metadata |
| CREATE | src/api/Features/Patients/EntityDto.cs | { Id, Type, Value, Confidence, LowConfidence, LastSeenAt } |
| CREATE | src/api/Migrations/AddPatientSearchIndex.cs | B-tree index on lower(last_name, first_name) with text_pattern_ops |

---

## External References
- https://www.postgresql.org/docs/current/indexes-opclass.html (`text_pattern_ops` operator class — enables B-tree LIKE/ILIKE prefix search; required for AC-001 500ms SLA)
- https://learn.microsoft.com/en-us/ef/core/querying/related-data/eager (`Include` with `AsNoTracking` — RT1 LEFT JOIN for patient + entities without change tracker overhead; AC-003)
- https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.ef.functions.ilike (EF.Functions.ILike — PostgreSQL ILIKE with parameterised pattern; OWASP A03)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Search with term `"Mitch"` against a seeded patient "Sarah Mitchell"; verify ≤20 results returned and response time is < 500ms with the search index in place (AC-001; NFR-003)
- [ ] Search with a 2-character term `"Mi"`; verify HTTP 400 with validation error message (AC-001 — min 3 chars)
- [ ] Search with term containing SQL LIKE metacharacters `"Mit%"` and `"Mit_"` ; verify results are returned correctly (not a SQL error or over-broad match) (OWASP A03 — metacharacter escaping)
- [ ] Call `GET /patients/{id}/summary` for a patient with 3 documents; verify response contains `demographics`, `entities`, `activeBookings`, `documents` keys; verify only 3 documents in `documents` array (with `totalDocumentCount = 3`) (AC-002, AC-003)
- [ ] Call `GET /patients/{id}/summary` with a patient JWT; verify HTTP 403 `{"error": "Access denied."}` (AC-004; OWASP A01)
- [ ] Call `GET /patients/{id}/summary` for a patient with 0 entities; verify `entities` key is `[]` not null (Edge: no entities)
- [ ] Call `GET /patients/{id}/summary?page=2&pageSize=20` for a patient with 25 documents; verify 5 documents returned on page 2, `totalDocumentCount = 25` (Edge: 100+ documents — pagination logic)
- [ ] Verify Serilog output in Seq does not contain any patient name, email, phone, entity value, or search term `q` for any log lines emitted by `PatientsController` (OWASP A02 — PHI audit)

---

## Implementation Checklist
- [ ] LIKE metacharacters (`%`, `_`, `\`) are escaped in the search term before passing to `EF.Functions.ILike` — unescaped `%` in a search term would match any characters and return unintended results (OWASP A03; AC-001 — accurate search results)
- [ ] All EF Core queries in the summary endpoint use `AsNoTracking()` — the controller is a read-only path and tracked entities would allocate change-tracking overhead unnecessarily for up to 50 entities + documents (OWASP A04 — avoid unintentional state mutation; performance)
- [ ] `PatientSummaryDto.Entities` is populated as `.ToArray()` (not `.ToList()`), always returns a non-null collection — never `null` even when `patient.PatientEntities` is empty (Edge: no entities — prevents NullReferenceException in frontend deserialization)
- [ ] The 2-second `CancellationTokenSource` is created with `using var` so it is always disposed even if the action throws; the `linkedCt` (merged with the request's `ct`) is passed to all `async` EF Core methods — the timeout fires regardless of whether the HTTP client disconnects first (AC-002 — 2s SLA; NFR-003)
- [ ] `page` and `pageSize` query parameters are clamped: `page = Math.Max(1, page)`, `pageSize = Math.Clamp(pageSize, 1, 50)` — a caller cannot pass `page=0` or `pageSize=0` to cause an exception, or `pageSize=10000` to bypass the pagination limit (Edge: 100+ documents; OWASP A04 — input validation at boundary)
- [ ] `[ResponseCache(Duration = 0, NoStore = true)]` is applied at the controller level (or both action level) — not just individual actions — to ensure no HTTP intermediary caches PHI responses (OWASP A02 — no PHI in HTTP caches)
- [ ] The EF Core migration adds the `text_pattern_ops` index using `migrationBuilder.Sql(...)` because EF Core's `HasIndex` fluent API does not support operator classes natively; the `Down` method drops the index to keep migrations reversible (AC-001 — index is version-controlled; NFR-003)
