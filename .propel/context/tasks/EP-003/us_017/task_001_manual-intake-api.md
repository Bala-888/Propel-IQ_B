# Task - TASK_001

## Requirement Reference
- **User Story:** us_017
- **Story Location:** .propel/context/tasks/EP-003/us_017/us_017.md
- **Acceptance Criteria:**
  - AC-002: `POST /intake/manual` returns HTTP 201; an `IntakeRecord` with `status = "Complete"` and `mode = "Manual"` is persisted with PHI-encrypted JSONB data; an audit log entry `ActionType: IntakeCompleted` is written
  - AC-003: If any mandatory field across the 5 sections is absent, the API returns 422 `ValidationProblemDetails` with field-level error paths; the body of the response does not omit other valid section data
  - AC-004: `POST /intake/draft` saves an `IntakeRecord` with `status = "Draft"` and `mode = "Manual"` using partially encrypted JSONB; returns HTTP 201
  - AC-005: `GET /intake/draft` returns the most recent Draft record for the authenticated patient with decrypted field data; returns 404 if no draft exists
- **Edge Cases:**
  - Invalid date format: if a Medical History date field value does not match `YYYY-MM-DD` or `MM/DD/YYYY`, the API returns 422 with the message "Please enter a valid date" on that specific field path — other fields are not affected
  - Brand-only medication: if a medication entry has a name but no dosage, the API accepts the request and returns 201 with a `"warnings"` array containing `"Consider adding dosage for clarity"` — submission is not blocked

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 (POST /intake/manual, POST /intake/draft, GET /intake/draft endpoints) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-008 (IntakeRecord upsert and query; depends on existing intake_records table from us_005) |
| Database | PostgreSQL + pgcrypto | 15.3+ | TR-007 (encrypted JSONB column storage; pgcrypto extension from us_005) |
| Encryption | BouncyCastle.Cryptography | 2.x | Existing `IPhiEncryptionService` AES-256 (from us_006) — encrypt JSONB before persist; decrypt after read |
| Logging | Serilog | Compatible with .NET 8.0 | Structural audit log via `IAuditLogger` — IntakeCompleted on successful submit |

---

## Task Overview

Build three endpoints for the manual intake flow: `GET /intake/draft` (returns decrypted draft data for form pre-population), `POST /intake/draft` (upserts a partial encrypted Draft record on navigation-away), and `POST /intake/manual` (validates all 5 sections, encrypts, persists a Complete record, and writes an audit log). All endpoints are `[Authorize(Roles = Roles.Patient)]`. The `IPhiEncryptionService` from us_006 handles AES-256 encryption before any JSONB write. The `intake_records` table from us_005 is the sole persistence target — no new migration is needed.

---

## Dependent Tasks
- task_002 (us_005) — `intake_records` table with `status`, `mode`, `data` JSONB columns must exist; pgcrypto extension enabled
- task_002 (us_006) — `IPhiEncryptionService` registered in DI; AES-256 key injected from environment variable

---

## Impacted Components
- `src/api/Features/Intake/ManualIntakeController.cs` — new: POST /intake/manual, POST /intake/draft, GET /intake/draft
- `src/api/Features/Intake/Requests/CreateManualIntakeRequest.cs` — new: DTO with 5 nested section objects and mandatory field annotations
- `src/api/Features/Intake/Requests/SaveDraftRequest.cs` — new: DTO with 5 optional nested section objects for partial draft saves
- `src/api/Features/Intake/Validators/ManualIntakeValidator.cs` — new: mandatory field check, date format validation, medication advisory logic
- `src/api/Features/Intake/IntakeService.cs` — modified: add `SubmitManualAsync`, `SaveDraftAsync`, `GetDraftAsync` methods

---

## Implementation Plan
1. Define `CreateManualIntakeRequest` DTO with five section sub-objects (`Demographics`, `MedicalHistory`, `Medications`, `Allergies`, `ChiefComplaint`); mark mandatory fields with `[Required]`; Medical History date fields are `string` to allow format validation in `ManualIntakeValidator`; define `SaveDraftRequest` with the same structure but all fields optional (AC-002, AC-003, AC-004)
2. Implement `ManualIntakeValidator`: iterate all mandatory field paths across 5 sections; collect any absent fields into a `ModelStateDictionary` and return `ValidationProblemDetails` 422; validate each Medical History date string using `DateOnly.TryParseExact` with formats `["yyyy-MM-dd", "MM/dd/yyyy"]` — add field-level error "Please enter a valid date" on mismatch; detect brand-only medications (name non-empty, dosage null or empty) and collect advisory strings for the response body (AC-003; Edge: invalid date; Edge: brand-only medication; OWASP A03)
3. Implement `POST /intake/manual` in `ManualIntakeController`: `[Authorize(Roles = Roles.Patient)]`; call `ManualIntakeValidator`; on validation failure → return 422 `ValidationProblemDetails`; on valid → call `IPhiEncryptionService.Encrypt(JsonSerializer.Serialize(request))` → persist `IntakeRecord {PatientId, Status=Complete, Mode=Manual, Data=encryptedJsonb, CreatedAt=UtcNow}` via `IntakeService.SubmitManualAsync`; return 201 with optional `warnings` array if medication advisory detected (AC-002; OWASP A01/A02)
4. Write audit log in `IntakeService.SubmitManualAsync` after `SaveChangesAsync` succeeds: call `IAuditLogger.RecordAsync(actionType: AuditActionTypes.IntakeCompleted, actorId: patientId, resourceId: intakeRecordId)` — no PHI fields in the audit payload (AC-002; OWASP A09; HIPAA minimum-necessary)
5. Implement `POST /intake/draft` in `ManualIntakeController`: `[Authorize(Roles = Roles.Patient)]`; no mandatory-field validation; call `IPhiEncryptionService.Encrypt` on the partial payload; call `IntakeService.SaveDraftAsync` which upserts: if an existing Draft record exists for this `PatientId`, update its `Data` column; otherwise insert a new Draft record; return 201 (AC-004; OWASP A01/A02)
6. Implement `GET /intake/draft` in `ManualIntakeController`: `[Authorize(Roles = Roles.Patient)]`; call `IntakeService.GetDraftAsync(patientId)`; if a Draft record is found → decrypt via `IPhiEncryptionService.Decrypt` and return the deserialized DTO; if not found → return 404 (AC-005; OWASP A01/A02)
7. In `IntakeService.SubmitManualAsync`, after the Complete record is saved, delete or soft-delete any existing Draft record for the same `PatientId` — prevents the pre-population endpoint from returning stale draft data after a successful submission (AC-004/AC-005 data consistency; OWASP A04 — insecure design prevention)

---

## Current Project State
```
src/
└── api/
    └── Features/
        └── Intake/
            ├── AiIntakeController.cs           (existing — AI intake flow; do not modify)
            ├── IntakeService.cs                (existing — add manual intake methods)
            └── (ManualIntakeController.cs      — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Features/Intake/ManualIntakeController.cs | POST /intake/manual, POST /intake/draft, GET /intake/draft |
| CREATE | src/api/Features/Intake/Requests/CreateManualIntakeRequest.cs | DTO for complete manual submission (5-section hierarchy) |
| CREATE | src/api/Features/Intake/Requests/SaveDraftRequest.cs | DTO for partial draft save (all fields optional) |
| CREATE | src/api/Features/Intake/Validators/ManualIntakeValidator.cs | Mandatory field + date format + medication advisory validation |
| MODIFY | src/api/Features/Intake/IntakeService.cs | Add SubmitManualAsync, SaveDraftAsync, GetDraftAsync |

---

## External References
- https://learn.microsoft.com/en-us/aspnet/core/web-api/action-return-types?view=aspnetcore-8.0 (ASP.NET Core 8 — ValidationProblemDetails and 422 responses)
- https://learn.microsoft.com/en-us/dotnet/api/system.dateonly.tryparseexact?view=net-8.0 (DateOnly.TryParseExact — multi-format date validation)
- https://learn.microsoft.com/en-us/ef/core/saving/basic?view=efcore-8.0 (EF Core 8 — upsert pattern for draft record)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] `POST /intake/manual` with all 5 sections valid → verify 201, `IntakeRecord.status = "Complete"`, `IntakeRecord.mode = "Manual"`, and the raw `data` column is not plaintext PHI in the database (AC-002; OWASP A02)
- [ ] `POST /intake/manual` with `chiefComplaint.description` absent → verify 422 `ValidationProblemDetails` response with field path `chiefComplaint.description`; other valid section data intact in the response (AC-003)
- [ ] `POST /intake/manual` with Medical History date = `"13/32/2025"` → verify 422 with message "Please enter a valid date" on the date field path; rest of form data not affected (Edge: invalid date)
- [ ] `POST /intake/manual` with medication name = `"Tylenol"` and dosage absent → verify 201 response body contains `"warnings": ["Consider adding dosage for clarity"]`; `IntakeRecord.status = "Complete"` in DB (Edge: brand-only medication)
- [ ] `POST /intake/draft` with partial data (2 of 5 sections filled) → verify 201, `IntakeRecord.status = "Draft"` in DB; calling `POST /intake/draft` a second time for the same patient → verify only one Draft record exists (upsert; AC-004)
- [ ] `GET /intake/draft` after a draft save → verify decrypted fields match what was submitted; raw DB column is ciphertext (AC-005; OWASP A02)
- [ ] `POST /intake/manual` success → then `GET /intake/draft` → verify 404 (stale draft removed; AC-004/AC-005 consistency)
- [ ] Audit log record with `ActionType: IntakeCompleted` exists after `POST /intake/manual` success; record absent after `POST /intake/draft` (AC-002; OWASP A09)

---

## Implementation Checklist
- [ ] `POST /intake/manual` validates all 5 section mandatory fields at the API boundary via `ManualIntakeValidator` before any encryption or database call; returns `ValidationProblemDetails` 422 with field-level paths when any mandatory field is absent (AC-003; OWASP A03 — validate at boundary)
- [ ] Date field validation uses `DateOnly.TryParseExact(value, ["yyyy-MM-dd", "MM/dd/yyyy"], ...)` — any other format returns 422 with per-field error "Please enter a valid date" without affecting other fields in the request (Edge: invalid date; OWASP A03)
- [ ] Brand-only medication (name non-empty, dosage null or empty) does not produce a 422; the 201 response body includes `"warnings": ["Consider adding dosage for clarity"]` alongside the created record — submission is not blocked (Edge: brand-only medication)
- [ ] PHI JSONB is encrypted by `IPhiEncryptionService.Encrypt` before `SaveChangesAsync` for both `POST /intake/manual` and `POST /intake/draft`; decrypted by `IPhiEncryptionService.Decrypt` in `GetDraftAsync`; plaintext PHI is never written to the database `data` column (AC-002; AC-005; OWASP A02; HIPAA at-rest encryption)
- [ ] `POST /intake/draft` uses an upsert: `UPDATE intake_records SET data=... WHERE patient_id=... AND status='Draft'` if a Draft exists; otherwise `INSERT` — prevents unbounded draft record accumulation for a single patient (AC-004; OWASP A04 — insecure design)
- [ ] `IntakeService.SubmitManualAsync` deletes the patient's existing Draft record (if any) after the Complete record is successfully committed; a subsequent `GET /intake/draft` for that patient returns 404 (AC-004/AC-005 data consistency)
- [ ] `IAuditLogger.RecordAsync(IntakeCompleted, patientId, intakeRecordId)` is called only on `POST /intake/manual` success; no audit entry is written for draft saves; the log payload contains only structural IDs — no decrypted PHI field values (AC-002; OWASP A09; HIPAA minimum-necessary)
- [ ] All three endpoints are decorated with `[Authorize(Roles = Roles.Patient)]`; Staff and Admin roles receive 403 from the RBAC middleware; unauthenticated requests receive 401 (AC-002; OWASP A01 — broken access control)
