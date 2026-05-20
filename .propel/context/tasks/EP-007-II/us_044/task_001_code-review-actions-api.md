# Task - TASK_001

## Requirement Reference
- **User Story:** us_044
- **Story Location:** .propel/context/tasks/EP-007-II/us_044/us_044.md
- **Acceptance Criteria:**
  - AC-002: `POST /patients/{id}/medical-codes` with `{"source": "AI", "reviewStatus": "Accepted"}` returns HTTP 201; inserts a `patient_medical_codes` row with `review_status = "Accepted"`, `reviewed_by` (from JWT), `reviewed_at`; audit log `ActionType: MedicalCodeAccepted`
  - AC-003: `PATCH /code-suggestions/{id}` with `{"reviewStatus": "Rejected"}` marks the suggestion `review_status = "Rejected"`; no `patient_medical_codes` row is created; audit log `ActionType: MedicalCodeRejected`
  - AC-004: `POST /patients/{id}/medical-codes` with `{"source": "AI-Corrected", "correctedCode": "<newCode>"}` inserts a `patient_medical_codes` row with the corrected code and `source = "AI-Corrected"`; audit log `ActionType: MedicalCodeCorrected`
  - AC-005: If `source == "AI-Corrected"` and `correctedCode` fails ICD-10/CPT format validation, return HTTP 400 "Invalid code format. ICD-10 codes must match [A-Z][0-9]{2}([...]) and CPT codes must be 5 digits."
- **Edge Cases:**
  - 409 — already reviewed: if `review_status != "Pending"` on the target `code_suggestions` row when either endpoint is called, return HTTP 409 `{"error": "This suggestion has already been reviewed."}` — no duplicate `patient_medical_codes` row must be created

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes — provides `POST /patients/{id}/medical-codes` and `PATCH /code-suggestions/{id}` consumed by task_002 |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-015-medical-code-review.html |
| **Screen Spec** | SCR-015 (Medical Code Review) |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## AI References
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-007 — human-in-loop review: clinician Accept/Reject/Correct actions finalise AI-generated code suggestions; `source = "AI"` records the provenance as AI-generated; `source = "AI-Corrected"` records clinician override of AI output |
| **AI Pattern** | Human-in-loop validation — no new Ollama calls; this task records the human outcome of an AI-generated suggestion |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | `review_status` state machine: Pending → Accepted | Rejected | Corrected; no transition back to Pending; 409 on any re-review attempt (AC-002, AC-003, AC-004 idempotency) |
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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — `MedicalCodesController` for POST; `CodeSuggestionsController` (extended) for PATCH; `[Authorize(Roles)]` RBAC; `reviewedBy` from JWT only (AC-002, AC-003, AC-004; OWASP A01) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — `ExecuteUpdateAsync` for atomic status update with review_status guard; `db.Database.ExecuteSqlAsync(FormattableString)` for ON CONFLICT upsert in GET modification; `AddAsync` for patient_medical_codes insert (AC-002, AC-003; OWASP A04) |
| Database | PostgreSQL 15.3+ | 15.3+ | TR-007 — `code_suggestions` table (UNIQUE on patient_id+code_type+code; review_status state machine); `patient_medical_codes` table; `gen_random_uuid()` for PK default (AC-002, AC-003) |
| Audit | IAuditLogService (us_014) | .NET 8.0 | TR-011 — LogAsync for MedicalCodeAccepted, MedicalCodeRejected, MedicalCodeCorrected; audit Note stores original/corrected codes in DB only — never in ILogger (OWASP A02) |
| Logging | Serilog + Seq | .NET 8.0 compatible / 2023.4+ | TR-011 — only UUID and status values in ILogger; no code values, descriptions, patient names in any log call (OWASP A02) |

---

## Task Overview

Add two new DB tables via EF Core migration. Extend the us_043 `GET /patients/{id}/code-suggestions` endpoint to persist suggestions on each generation and return stable DB `id` values (required for `PATCH /code-suggestions/{id}`). Implement `POST /patients/{id}/medical-codes` for Accept and Correct paths, and `PATCH /code-suggestions/{id}` for the Reject path. All three paths enforce a 409 guard on the suggestion's `review_status`, extract `reviewedBy` from JWT, and write audit log entries via `IAuditLogService`. Code format validation is applied server-side for corrected codes.

---

## Dependent Tasks
- task_001 (us_043) — MODIFIED: `GET /patients/{id}/code-suggestions` implementation must be extended to persist suggestions (item 2 of this task); `CodeSuggestionDto` must be updated to expose `Guid Id` and `string ReviewStatus` fields
- task_001 (us_014) — `IAuditLogService` must exist and be injectable

---

## Impacted Components
- EF Core migration `AddCodeSuggestionsAndMedicalCodesTables` — new
- `src/api/Features/Codes/CodeSuggestion.cs` — new: `CodeSuggestion` EF Core entity
- `src/api/Features/Codes/PatientMedicalCode.cs` — new: `PatientMedicalCode` EF Core entity
- `src/api/Features/Codes/CodeSuggestionDto.cs` — modified (us_043): add `Guid Id`, `string ReviewStatus`
- `src/api/Controllers/CodeSuggestionsController.cs` — modified (us_043): GET action extended to persist + return IDs; PATCH action added
- `src/api/Controllers/MedicalCodesController.cs` — new: POST action for Accept/Correct
- `src/api/Features/Codes/CreateMedicalCodeRequest.cs` — new: POST request DTO

---

## Implementation Plan
1. EF Core migration `AddCodeSuggestionsAndMedicalCodesTables`: create `code_suggestions(id uuid PRIMARY KEY DEFAULT gen_random_uuid(), patient_id uuid NOT NULL REFERENCES patients(id), code_type varchar(10) NOT NULL, code varchar(20) NOT NULL, description text, confidence float8, low_confidence bool NOT NULL DEFAULT false, review_status varchar(20) NOT NULL DEFAULT 'Pending', reviewed_by uuid REFERENCES users(id) ON DELETE SET NULL, reviewed_at timestamptz, created_at timestamptz NOT NULL DEFAULT now())` + `UNIQUE(patient_id, code_type, code)`; create `patient_medical_codes(id uuid PRIMARY KEY DEFAULT gen_random_uuid(), patient_id uuid NOT NULL REFERENCES patients(id), code_type varchar(10) NOT NULL, code varchar(20) NOT NULL, original_code varchar(20) NOT NULL, description text, source varchar(20) NOT NULL, review_status varchar(20) NOT NULL, reviewed_by uuid NOT NULL REFERENCES users(id), reviewed_at timestamptz NOT NULL)`; add `CodeSuggestion` and `PatientMedicalCode` EF Core entities to `DbContext`; add `[Authorize(Roles = "Clinician,Admin")]` note: no migration for additional `GET` data — `CodeSuggestionDto` is extended in code only (AC-002, AC-003, AC-004)
2. Extend `GET /patients/{id}/code-suggestions` (us_043 `CodeSuggestionsController`): after building and validating the top-10 suggestion list, persist each via `db.Database.ExecuteSqlAsync($"INSERT INTO code_suggestions (patient_id, code_type, code, description, confidence, low_confidence) VALUES ({patientId}, {s.CodeType}, {s.Code}, {s.Description}, {s.Confidence}, {s.LowConfidence}) ON CONFLICT (patient_id, code_type, code) DO UPDATE SET id = code_suggestions.id")` (batch; FormattableString; OWASP A03); then fetch persisted rows: `db.CodeSuggestions.AsNoTracking().Where(cs => cs.PatientId == patientId && top10Codes.Contains(cs.Code)).ToDictionaryAsync(cs => cs.Code, ct)` → map `cs.Id` and `cs.ReviewStatus` back to each `CodeSuggestionDto`; update `CodeSuggestionDto` to include `Guid Id` and `string ReviewStatus = "Pending"`; ensures re-calling GET does NOT overwrite an existing `review_status` that has already been set to Accepted/Rejected/Corrected (OWASP A04 — idempotent; AC-003 dependency on stable suggestion IDs)
3. `POST /patients/{id}/medical-codes` in `MedicalCodesController`: `[Authorize(Roles = "Clinician,Admin")]`; `reviewedBy = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)` from JWT (never from request body; OWASP A01); `CreateMedicalCodeRequest { Guid SuggestionId, string CodeType, string Code, string Description, string Source, string ReviewStatus, string? CorrectedCode }` bound from `[FromBody]`; validate `Source ∈ {"AI","AI-Corrected"}`; validate `ReviewStatus` matches `Source` (Accepted↔AI, Corrected↔AI-Corrected); load `CodeSuggestion` by `SuggestionId` → 404 if not found; if `suggestion.ReviewStatus != "Pending"` → 409 `{"error":"This suggestion has already been reviewed."}`; `db.PatientMedicalCodes.AddAsync(new PatientMedicalCode { PatientId = id, CodeType = request.CodeType, Code = source=="AI-Corrected" ? request.CorrectedCode! : request.Code, OriginalCode = request.Code, Description = request.Description, Source = request.Source, ReviewStatus = request.ReviewStatus, ReviewedBy = reviewedBy, ReviewedAt = DateTimeOffset.UtcNow })`; update suggestion status via `ExecuteUpdateAsync`; `await db.SaveChangesAsync(ct)`; return `CreatedAtAction` 201 (AC-002, AC-004; OWASP A04 — status guard)
4. `PATCH /code-suggestions/{id}` for rejection in `CodeSuggestionsController`: `[Authorize(Roles = "Clinician,Admin")]`; `[HttpPatch("{id:guid}")]`; `reviewedBy = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)`; load `code_suggestions` by `id` → 404; if `ReviewStatus != "Pending"` → 409 `{"error":"This suggestion has already been reviewed."}`; `int updated = await db.CodeSuggestions.Where(cs => cs.Id == id && cs.ReviewStatus == "Pending").ExecuteUpdateAsync(s => s.SetProperty(cs => cs.ReviewStatus, "Rejected").SetProperty(cs => cs.ReviewedBy, reviewedBy).SetProperty(cs => cs.ReviewedAt, DateTimeOffset.UtcNow), ct)` — the dual guard `cs.ReviewStatus == "Pending"` prevents TOCTOU race; if `updated == 0` → 409 (AC-003; OWASP A04; Edge: 409 already reviewed)
5. Audit log integration for all three actions: in `MedicalCodesController.Post`: `await _auditLogService.LogAsync(new AuditLogEntry { ActionType = request.ReviewStatus == "Accepted" ? "MedicalCodeAccepted" : "MedicalCodeCorrected", EntityId = request.SuggestionId.ToString(), EntityType = "CodeSuggestion", PerformedBy = reviewedBy.ToString(), Timestamp = DateTimeOffset.UtcNow, Note = request.ReviewStatus == "Corrected" ? $"original:{request.Code} corrected:{request.CorrectedCode}" : null })`; in `CodeSuggestionsController.Patch`: `await _auditLogService.LogAsync(ActionType="MedicalCodeRejected", EntityId=id.ToString(), PerformedBy=reviewedBy.ToString())`; audit Note (for Corrected) goes to audit log DB only — never to `_logger` (AC-002, AC-003, AC-004; OWASP A02 — code values in Note are stored in DB, not in Seq)
6. Corrected code format validation in POST: when `request.Source == "AI-Corrected"` — validate `request.CorrectedCode` is non-null and non-empty; if `request.CodeType == "ICD10"` → test against static `_icd10Regex` (same compiled `^[A-Z][0-9]{2}(\.[0-9A-Z]{1,4})?$` from us_043 pattern — reference the same static field if co-located, or re-declare in `MedicalCodesController` as `private static readonly Regex _icd10Regex = new Regex(@"^[A-Z][0-9]{2}(\.[0-9A-Z]{1,4})?$", RegexOptions.Compiled)`); if `request.CodeType == "CPT"` → test `^\d{5}$`; on failure → return `BadRequest(new { error = "Invalid code format. ICD-10 codes must match [A-Z][0-9]{2}([...]) and CPT codes must be 5 digits." })`; validation occurs BEFORE any DB load — early exit (AC-005; OWASP A03 — validate at boundary)

---

## Current Project State
```
src/
└── api/
    ├── Controllers/
    │   ├── CodeSuggestionsController.cs    (MODIFY — us_043; add PATCH /{id} reject action)
    │   └── (MedicalCodesController.cs      — CREATE)
    └── Features/
        └── Codes/
            ├── (CodeSuggestion.cs          — CREATE: EF Core entity)
            ├── (PatientMedicalCode.cs      — CREATE: EF Core entity)
            ├── (CreateMedicalCodeRequest.cs — CREATE: POST request DTO)
            └── CodeSuggestionDto.cs        (MODIFY — us_043; add Id + ReviewStatus fields)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | EF Core migration AddCodeSuggestionsAndMedicalCodesTables | code_suggestions + patient_medical_codes tables with UNIQUE constraint |
| CREATE | src/api/Features/Codes/CodeSuggestion.cs | EF Core entity for code_suggestions table |
| CREATE | src/api/Features/Codes/PatientMedicalCode.cs | EF Core entity for patient_medical_codes table |
| CREATE | src/api/Features/Codes/CreateMedicalCodeRequest.cs | POST DTO with SuggestionId, CodeType, Code, Source, ReviewStatus, CorrectedCode? |
| CREATE | src/api/Controllers/MedicalCodesController.cs | POST /patients/{id}/medical-codes for Accept and Correct paths |
| MODIFY | src/api/Controllers/CodeSuggestionsController.cs | Extend GET to persist + return IDs; add PATCH /{id} for reject |
| MODIFY | src/api/Features/Codes/CodeSuggestionDto.cs | Add Guid Id and string ReviewStatus fields |

---

## External References
- https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete#executeupdateasync (EF Core `ExecuteUpdateAsync` with WHERE clause guard — TOCTOU race protection for review_status update; OWASP A04)
- https://www.postgresql.org/docs/current/sql-insert.html#SQL-ON-CONFLICT (PostgreSQL ON CONFLICT DO UPDATE — upsert-to-get-ID pattern for suggestion persistence; idempotent GET calls; OWASP A04)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Call `POST /patients/{id}/medical-codes` as Clinician with `{source:"AI", reviewStatus:"Accepted"}` for a Pending suggestion; verify HTTP 201, a `patient_medical_codes` row exists with `source="AI"`, `review_status="Accepted"`, `reviewed_by` matching the JWT user (not any request body field), and audit log `ActionType="MedicalCodeAccepted"` (AC-002)
- [ ] Call `PATCH /code-suggestions/{id}` for a Pending suggestion; verify HTTP 200, `review_status="Rejected"`, no `patient_medical_codes` row created, audit log `ActionType="MedicalCodeRejected"` (AC-003)
- [ ] Call `POST /patients/{id}/medical-codes` with `{source:"AI-Corrected", correctedCode:"D50.0"}` for ICD-10; verify 201 with `code="D50.0"`, `original_code={originalAiCode}`, `source="AI-Corrected"`, audit Note contains both codes (AC-004)
- [ ] Call both endpoints twice on the same suggestion; verify the second call returns HTTP 409 `{"error":"This suggestion has already been reviewed."}` and no duplicate `patient_medical_codes` rows exist (Edge: 409 already reviewed; OWASP A04)
- [ ] Call POST with `{source:"AI-Corrected", correctedCode:"XYZ"}` for ICD-10 type; verify HTTP 400 with the format error message (AC-005)
- [ ] Call GET /patients/{id}/code-suggestions twice; verify suggestion IDs are stable (same UUID for same code on second call); verify a suggestion with `review_status="Accepted"` from a prior POST retains "Accepted" on re-fetch, not reset to "Pending" (OWASP A04 — idempotent upsert)
- [ ] Verify Serilog output contains no code values, descriptions, or corrected codes — only UUIDs and ActionType strings appear in any log (OWASP A02)

---

## Implementation Checklist
- [ ] `reviewedBy` in both endpoints is extracted ONLY from `User.FindFirstValue(ClaimTypes.NameIdentifier)` (JWT); the `CreateMedicalCodeRequest` DTO does NOT contain a `ReviewedBy` field — the clinician cannot self-assign a different userId for the audit trail; if a `reviewedBy` is present in the request body for frontend compatibility, it is ignored by the controller (OWASP A01)
- [ ] Both the `POST` (Accept/Correct) and `PATCH` (Reject) endpoints use a two-level 409 guard: (1) `FindAsync` to check status before the write (human-readable guard), and (2) `ExecuteUpdateAsync` / `AddAsync` with a `WHERE review_status = "Pending"` condition (TOCTOU race guard); if the row count returns 0 after the update/insert → return 409 (OWASP A04)
- [ ] The `ON CONFLICT DO UPDATE SET id = code_suggestions.id` in the GET modification (item 2) is intentionally a no-op update — it exists solely to trigger PostgreSQL's conflict resolution and return the existing row's `id` via the subsequent SELECT; this pattern is documented here so future developers understand the intent (OWASP A04 — idempotent; DRY — single upsert call)
- [ ] Static compiled `Regex` fields (`_icd10Regex`, `_cptRegex`) must be declared as `private static readonly Regex` at class level — not instantiated inside the method on each request; `RegexOptions.Compiled` pre-compiles the automaton at class load time (performance; code anti-patterns — no magic constant re-instantiation)
- [ ] The audit Note for `MedicalCodeCorrected` (`"original:{code} corrected:{correctedCode}"`) is passed to `IAuditLogService.LogAsync` and stored in the `audit_logs` DB table; it is NEVER passed to `_logger.LogInformation` or any `ILogger` method (OWASP A02 — corrected codes are clinical corrections that may carry patient context)
- [ ] The `code_suggestions` UNIQUE constraint is `(patient_id, code_type, code)` — NOT `(patient_id, code)` — because the same numeric code value could theoretically appear in both ICD-10 and CPT code spaces; the three-column key ensures ICD-10 `99213` (if it existed) and CPT `99213` would be separate rows (data integrity; OWASP A04)
