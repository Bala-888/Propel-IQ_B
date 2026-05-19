# Task - TASK_001

## Requirement Reference
- **User Story:** us_016-II
- **Story Location:** .propel/context/tasks/EP-003/us_016-II/us_016-II.md
- **Acceptance Criteria:**
  - AC-001: `GET /intake/ai/summary?sessionId=<id>` returns HTTP 200 with a structured JSON summary containing all 5 collected field groups; the patient's session ownership is validated before the data is returned
  - AC-002: `PATCH /intake/ai/field` with `{"fieldPath": "medications[0].name", "value": "Metformin 500mg"}` updates the named field in the `IntakeSessionState`, persists the change to `IDistributedCache`, and returns the corrected full summary in the response body — no page reload required
  - AC-003: `POST /intake/ai/confirm` returns HTTP 201; an `IntakeRecord` with `status = "Complete"` and `mode = "AI"` is upserted in the database with the intake data encrypted via pgcrypto JSONB; an audit log entry with `ActionType: IntakeCompleted` is written
- **Edge Cases:**
  - Session expired before confirmation: if the `sessionId` key has expired from `IDistributedCache` (TTL elapsed), `POST /intake/ai/confirm` must return HTTP 410 with `{"error": "Session expired. Your draft has been saved."}` — the `IntakeRecord` Draft written by us_016-I auto-saves should be queryable for resume
  - Empty required field via PATCH: if `PATCH /intake/ai/field` sets a required field (e.g., `chiefComplaint`) to an empty string, the API must return HTTP 400 with `{"error": "Chief complaint cannot be empty"}` without saving the empty value to session state

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
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-001 (session state produced by the Ollama dialogue engine is read and finalised here), AIR-002 (structured extraction stored in IntakeSessionState is promoted to a permanent encrypted record) |
| **AI Pattern** | Summary retrieval and correction — no new Ollama inference in this task; session state from us_016-I is read, patched, and confirmed |
| **Prompt Template Path** | N/A (no new model calls in this task) |
| **Guardrails Config** | Corrected field values from PATCH must not be logged in plain text — only structural metadata (sessionId, fieldPath) is written to audit/structured logs |
| **Model Provider** | N/A (no model calls; session state from Ollama dialogue in us_016-I) |

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-002 (`IntakeAiController` extended with summary, field-patch, and confirm endpoints) |
| Backend | Microsoft.Extensions.Caching.Distributed | .NET 8.0 built-in | TR-002 (`IDistributedCache` for `IntakeSessionState` read/write; established in us_016-I) |
| Backend | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-002 (upsert `IntakeRecord` to status Complete on confirm) |
| Backend | BouncyCastle.Cryptography | 2.x | TR-002 (`IPhiEncryptionService.Encrypt` on final JSONB before persistence) |

---

## Task Overview

Extend `IntakeAiController` with three endpoints that cover the summary review phase of the AI intake workflow. `GET /intake/ai/summary` reads the session from cache and returns the structured field data. `PATCH /intake/ai/field` applies a single field correction with required-field validation. `POST /intake/ai/confirm` promotes the Draft record to Complete, encrypts the final JSONB, writes the audit log, evicts the session from cache, and returns 201. The 410 (session expired) and 400 (empty required field) edge cases are handled with typed exceptions caught at the controller level.

---

## Dependent Tasks
- task_001 (us_016-I) — `IntakeSessionService`, `IntakeSessionState`, and `IDistributedCache` session infrastructure must exist; `IntakeRecord` Draft must be written by the auto-save from us_016-I
- task_002 (us_006) — `IPhiEncryptionService` must be available for encrypting the confirmed JSONB
- task_002 (us_014) — `IAuditLogService` with `AuditActionTypes.IntakeCompleted` must be available

---

## Impacted Components
- `src/api/Controllers/IntakeAiController.cs` — modified: add `GET /intake/ai/summary`, `PATCH /intake/ai/field`, `POST /intake/ai/confirm` action methods
- `src/api/AI/IntakeSessionService.cs` — modified: add `PatchFieldAsync(Guid sessionId, string fieldPath, string value)` method
- `src/api/AI/RequiredIntakeFields.cs` — new: static list of required field paths for validation
- `src/api/AI/Exceptions/SessionExpiredException.cs` — new: typed exception for 410 path
- `src/api/Services/IntakeRecordService.cs` — modified: add `ConfirmAsync(Guid patientId, IntakeFieldState fields)` that upserts to status Complete with encrypted JSONB
- `src/api/Audit/AuditActionTypes.cs` — modified: add `IntakeCompleted` constant

---

## Implementation Plan
1. Create `RequiredIntakeFields.cs` as a static class with `public static readonly IReadOnlySet<string> RequiredPaths = new HashSet<string> { "chiefComplaint" }` — used in field-patch validation to determine which field paths cannot be set to empty string (Edge: empty required field; AC-002)
2. Add `PatchFieldAsync(Guid sessionId, Guid requestingPatientId, string fieldPath, string value)` to `IntakeSessionService`: load session from cache; validate ownership; if `RequiredIntakeFields.RequiredPaths.Contains(fieldPath)` and `value.Trim() == ""` → throw `RequiredFieldEmptyException(fieldPath)`; apply the patch to `IntakeSessionState.Fields` using reflection or a switch-based dispatcher; call `UpdateSessionAsync`; return updated `IntakeFieldState` (AC-002; Edge: empty required field)
3. Implement `GET /intake/ai/summary` in `IntakeAiController`: call `IntakeSessionService.GetSessionAsync(sessionId, patientId)` — null → 404; return `Ok(session.Fields)` serialised as the structured summary JSON (AC-001)
4. Implement `PATCH /intake/ai/field` in `IntakeAiController`: call `IntakeSessionService.PatchFieldAsync`; catch `RequiredFieldEmptyException` → 400 with `{"error": "Chief complaint cannot be empty"}` (or the relevant field name from the exception); on success return `Ok(updatedFields)` (AC-002; Edge: empty required field)
5. Add `ConfirmAsync(Guid patientId, IntakeFieldState fields)` to `IntakeRecordService`: serialise fields to JSON string; encrypt via `IPhiEncryptionService.Encrypt`; upsert `IntakeRecord { PatientId, Status = "Complete", Mode = "AI", Data = ciphertext, UpdatedAt = UtcNow }`; call `IAuditLogService.LogAsync(AuditActionTypes.IntakeCompleted, "IntakeRecord", patientId.ToString())` (AC-003; OWASP A09)
6. Implement `POST /intake/ai/confirm` in `IntakeAiController`: call `IntakeSessionService.GetSessionAsync` — null → throw `SessionExpiredException`; catch → return 410 with `{"error": "Session expired. Your draft has been saved."}`; on success call `IntakeRecordService.ConfirmAsync(session.PatientId, session.Fields)`, then `IntakeSessionService.DeleteSessionAsync(sessionId)` to evict from cache, return 201 (AC-003; Edge: session expired)
7. Add `IntakeCompleted` constant to `AuditActionTypes.cs` to satisfy the audit log call in step 5 (AC-003; OWASP A09)

---

## Current Project State
```
src/
└── api/
    ├── Controllers/
    │   └── IntakeAiController.cs                    (MODIFY — add summary, patch-field, confirm endpoints)
    ├── AI/
    │   ├── IntakeSessionService.cs                  (MODIFY — add PatchFieldAsync + DeleteSessionAsync)
    │   ├── RequiredIntakeFields.cs                  (CREATE)
    │   └── Exceptions/
    │       ├── SessionExpiredException.cs           (CREATE)
    │       └── RequiredFieldEmptyException.cs       (CREATE)
    ├── Services/
    │   └── IntakeRecordService.cs                   (MODIFY — add ConfirmAsync)
    └── Audit/
        └── AuditActionTypes.cs                      (MODIFY — add IntakeCompleted)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/api/Controllers/IntakeAiController.cs | Add GET /summary, PATCH /field, POST /confirm action methods |
| MODIFY | src/api/AI/IntakeSessionService.cs | Add PatchFieldAsync (with required-field check) + DeleteSessionAsync |
| CREATE | src/api/AI/RequiredIntakeFields.cs | Static set of required field paths |
| CREATE | src/api/AI/Exceptions/SessionExpiredException.cs | Typed 410 exception |
| CREATE | src/api/AI/Exceptions/RequiredFieldEmptyException.cs | Typed 400 exception with field name |
| MODIFY | src/api/Services/IntakeRecordService.cs | Add ConfirmAsync: encrypt + upsert to status Complete + audit log |
| MODIFY | src/api/Audit/AuditActionTypes.cs | Add IntakeCompleted constant |

---

## External References
- https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-8.0 (IDistributedCache — reading and updating cached session state)
- https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializer (System.Text.Json — serialising IntakeFieldState to JSON before encryption)
- https://www.bouncycastle.org/csharp/ (BouncyCastle.Cryptography 2.x — AES-256 encrypt of final JSONB; established in us_006)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Complete the multi-turn dialogue from us_016-I; call `GET /intake/ai/summary?sessionId=<id>`; verify HTTP 200 with a JSON body containing non-null values for all 5 field groups (AC-001)
- [ ] Call `PATCH /intake/ai/field` with a valid fieldPath and new value; verify HTTP 200 with the corrected value visible in the returned summary; call GET /summary again and confirm the change persisted in session state (AC-002)
- [ ] Call `PATCH /intake/ai/field` with `fieldPath="chiefComplaint"` and `value=""`; verify HTTP 400 with `{"error": "Chief complaint cannot be empty"}` and no change in session state (Edge: empty required field)
- [ ] Call `POST /intake/ai/confirm`; verify HTTP 201; query `intake_records` for the patient; verify `status = "Complete"`, `mode = "AI"`, and `data` column contains ciphertext (not plaintext JSON) (AC-003)
- [ ] Verify an audit log entry with `action_type = "IntakeCompleted"` and the patient's ID exists after confirm (AC-003; OWASP A09)
- [ ] Expire the session manually (delete from cache or wait for TTL); call `POST /intake/ai/confirm`; verify HTTP 410 with the session-expired message; verify the Draft record from auto-save still exists in the database (Edge: session expired)

---

## Implementation Checklist
- [ ] `PatchFieldAsync` validates ownership (`session.PatientId == requestingPatientId`) before applying the field mutation — a patient cannot correct another patient's session via a guessed sessionId (AC-002; OWASP A01)
- [ ] `RequiredIntakeFields.RequiredPaths` is a `IReadOnlySet<string>` — immutable at runtime; adding new required fields requires only a change to this one class (Edge: empty required field; maintainability)
- [ ] `IntakeRecordService.ConfirmAsync` calls `IPhiEncryptionService.Encrypt` on the serialised JSON before `SaveChangesAsync` — the database column never contains plaintext field values (AC-003; OWASP A02; HIPAA §164.312(a)(2)(iv))
- [ ] `IntakeAiController` catches `SessionExpiredException` → 410 and `RequiredFieldEmptyException` → 400; neither exception reaches the global exception middleware (Edge handling; OWASP A05)
- [ ] `DeleteSessionAsync` is called after `ConfirmAsync` succeeds — the session is evicted from cache only on success; if `ConfirmAsync` throws, the session remains in cache so the patient can retry (AC-003 — idempotency on retry)
- [ ] Field values received in `PATCH /intake/ai/field` are never written to `ILogger` or Serilog — only `sessionId` and `fieldPath` are logged as structural metadata; field values may contain PHI (AC-002; OWASP A09; HIPAA minimum-necessary)
- [ ] `AuditActionTypes.IntakeCompleted` is added as a constant string — not a magic string inline in `IntakeRecordService` — consistent with the centralised constants pattern (AC-003; OWASP A09; code maintainability)
