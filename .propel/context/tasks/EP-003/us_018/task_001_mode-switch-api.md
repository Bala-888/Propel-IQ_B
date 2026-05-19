# Task - TASK_001

## Requirement Reference
- **User Story:** us_018
- **Story Location:** .propel/context/tasks/EP-003/us_018/us_018.md
- **Acceptance Criteria:**
  - AC-001: `POST /intake/mode-switch` with `{"from": "AI", "to": "Manual", "sessionId": "<id>"}` returns HTTP 200 and a `mappedFields` payload that pre-populates the manual form sections with AI-collected data
  - AC-002: `POST /intake/mode-switch` with `{"from": "Manual", "to": "AI"}` merges the patient's manual draft data into the AI session state in `IDistributedCache`; subsequent Ollama dialogue turns skip already-filled fields
  - AC-003: AI free-text content that does not map to a named manual field is returned in a `reviewItems[]` array in the response; the array is present even when empty
- **Edge Cases:**
  - No data entered: if the source session is empty (AI session has no collected fields; manual draft has no filled fields), the endpoint returns 200 with empty `mappedFields` and empty `reviewItems[]` — no mapping logic is invoked
  - Concurrent auto-save during mode switch: the mode-switch write must win over a concurrent `POST /intake/draft` auto-save; the response includes a `cacheVersion` token the frontend uses to gate subsequent auto-saves

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
| **AI Pattern** | N/A — this task manipulates existing AI session state in `IDistributedCache`; it does not invoke the Ollama model |
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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 (POST /intake/mode-switch endpoint) |
| Caching | Microsoft.Extensions.Caching.Distributed | .NET 8.0 built-in | AI session state read/write via `IDistributedCache`; `SetAsync` with version stamp for concurrency guard |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-008 (read Draft `IntakeRecord` for Manual→AI mapping) |
| Encryption | BouncyCastle.Cryptography | 2.x | `IPhiEncryptionService` (from us_006) — decrypt AI session data before mapping; encrypt manual draft data when writing to cache |

---

## Task Overview

Build the `POST /intake/mode-switch` endpoint and an `IntakeModeSwitchService` that handles bidirectional field mapping between AI session state and the manual intake form schema. AI→Manual reads the Ollama session from `IDistributedCache`, maps collected field values to `ManualIntakeData` field paths, and collects unmapped free-text content into `reviewItems[]`. Manual→AI reads the patient's Draft `IntakeRecord`, reconstructs a partial `IntakeSessionState`, and writes it back to the cache with filled-field flags so the Ollama dialogue skips those topics. A `cacheVersion` token in the response prevents concurrent auto-save calls from overwriting the switch result.

---

## Dependent Tasks
- task_001 (us_016-I) — `IntakeSessionState` shape and `IDistributedCache` key pattern (`intake:session:{sessionId}`) must be established; `IPhiEncryptionService` registered in DI
- task_001 (us_017) — `ManualIntakeData` DTO and `IntakeRecord` Draft upsert pattern must be available from `IntakeService`

---

## Impacted Components
- `src/api/Features/Intake/ModeSwitchController.cs` — new: POST /intake/mode-switch
- `src/api/Features/Intake/ModeSwitchRequest.cs` — new: request DTO {from, to, sessionId?}
- `src/api/Features/Intake/ModeSwitchResponse.cs` — new: response DTO {mappedFields, reviewItems, cacheVersion}
- `src/api/Features/Intake/IntakeModeSwitchService.cs` — new: AI→Manual and Manual→AI mapping logic; review buffer; concurrency stamp
- `src/api/Features/Intake/IntakeFieldMapper.cs` — new: static mapping table between `IntakeSessionState` field paths and `ManualIntakeData` field paths

---

## Implementation Plan
1. Define `ModeSwitchRequest` record: `string From`, `string To`, `string? SessionId`; validate that `From` and `To` are one of `["AI", "Manual"]` and are not equal; `SessionId` required when `From = "AI"` (OWASP A03 — validate at boundary; AC-001, AC-002)
2. Define `ModeSwitchResponse` record: `object MappedFields` (either `ManualIntakeData` or `AiSessionFields`), `IReadOnlyList<string> ReviewItems`, `string CacheVersion`; `CacheVersion` is a `Guid.NewGuid().ToString()` assigned at mapping time and written into the cache entry for the new session state (AC-001, AC-002, AC-003; Edge: concurrent auto-save)
3. Implement `IntakeFieldMapper`: a static dictionary mapping `IntakeSessionState` named field keys (e.g., `"demographics.firstName"`, `"chiefComplaint"`) to their `ManualIntakeData` property paths; any AI session key not present in the dictionary is treated as unmapped and added to `reviewItems[]`; mapping is bidirectional (AI→Manual and Manual→AI) (AC-001, AC-002, AC-003)
4. Implement AI→Manual mapping in `IntakeModeSwitchService.SwitchAiToManualAsync`: load `IntakeSessionState` from `IDistributedCache` using `sessionId`; if session is null or has no collected fields → return empty `mappedFields` and empty `reviewItems[]` (Edge: no data); iterate session fields against `IntakeFieldMapper`; populate `ManualIntakeData` for mapped fields; collect unmapped free-text into `reviewItems[]`; verify `sessionState.PatientId == claims.Sub` (OWASP A01; AC-001, AC-003)
5. Implement Manual→AI mapping in `IntakeModeSwitchService.SwitchManualToAiAsync`: load patient's Draft `IntakeRecord` from DB via `IntakeService.GetDraftAsync`; decrypt JSONB; iterate filled fields against `IntakeFieldMapper` (reverse direction); reconstruct an `IntakeSessionState` with `FilledFields` set marking which field keys to skip in Ollama dialogue; write back to `IDistributedCache` with new `CacheVersion` stamp and 2h TTL; delete the Draft record to avoid stale state (AC-002; OWASP A01/A02)
6. Concurrency guard: `IDistributedCache.SetAsync` for the reconstructed session state uses the `cacheVersion` value as a logical version; `POST /intake/draft` (from us_017) must check that its payload's `cacheVersion` matches the current cache entry version before overwriting — if mismatch, the auto-save is discarded (Edge: concurrent auto-save; OWASP A04 — insecure design prevention)
7. Build `ModeSwitchController`: `[Authorize(Roles = Roles.Patient)]`; dispatch to `SwitchAiToManualAsync` or `SwitchManualToAiAsync` based on `from/to` values; return 200 with `ModeSwitchResponse`; return 400 if `from == to` or if `SessionId` is missing when required (AC-001, AC-002; OWASP A01/A03)

---

## Current Project State
```
src/
└── api/
    └── Features/
        └── Intake/
            ├── AiIntakeController.cs            (existing — AI intake; do not modify)
            ├── ManualIntakeController.cs         (from us_017 — do not modify)
            ├── IntakeService.cs                  (existing — GetDraftAsync used by Manual→AI path)
            └── (ModeSwitchController.cs          — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Features/Intake/ModeSwitchController.cs | POST /intake/mode-switch |
| CREATE | src/api/Features/Intake/ModeSwitchRequest.cs | Request DTO with from, to, sessionId fields |
| CREATE | src/api/Features/Intake/ModeSwitchResponse.cs | Response DTO with mappedFields, reviewItems, cacheVersion |
| CREATE | src/api/Features/Intake/IntakeModeSwitchService.cs | SwitchAiToManualAsync and SwitchManualToAiAsync |
| CREATE | src/api/Features/Intake/IntakeFieldMapper.cs | Bidirectional field path mapping dictionary |

---

## External References
- https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-8.0 (IDistributedCache SetAsync — cache versioning and session state write)
- https://learn.microsoft.com/en-us/aspnet/core/web-api/action-return-types?view=aspnetcore-8.0 (ASP.NET Core 8 — 200/400 action return types)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] `POST /intake/mode-switch` AI→Manual with a populated AI session → verify 200, `mappedFields.demographics.firstName` equals the AI-collected first name, `reviewItems` is an array (AC-001)
- [ ] `POST /intake/mode-switch` Manual→AI with a populated draft → verify 200; `IDistributedCache` entry for the new session contains `filledFields` that match the submitted manual section data (AC-002)
- [ ] `POST /intake/mode-switch` AI→Manual with AI free-text content not in `IntakeFieldMapper` → verify `reviewItems[]` contains the unmapped text (AC-003)
- [ ] `POST /intake/mode-switch` with empty AI session → verify 200, `mappedFields` is empty, `reviewItems` is `[]` (Edge: no data entered)
- [ ] After a mode switch, call `POST /intake/draft` with the old `cacheVersion` → verify the auto-save is discarded (returns success but does not overwrite the switched session state) (Edge: concurrent auto-save)
- [ ] `POST /intake/mode-switch` with `from == to` → verify 400 response (OWASP A03)
- [ ] `POST /intake/mode-switch` by patient A using session owned by patient B → verify 403 (OWASP A01 — broken access control)

---

## Implementation Checklist
- [ ] `ModeSwitchRequest` is validated at the API boundary: `From` and `To` must each be `"AI"` or `"Manual"` and must not be equal; `SessionId` is required when `From = "AI"`; invalid requests return 400 before any data access (OWASP A03; AC-001, AC-002)
- [ ] `SwitchAiToManualAsync` verifies `sessionState.PatientId == claims.Sub` before reading any field data; returns 403 if the session does not belong to the authenticated patient (OWASP A01 — broken access control; AC-001)
- [ ] `IntakeFieldMapper` provides a static, exhaustive mapping dictionary; any AI session key absent from the dictionary is appended to `reviewItems[]`; the dictionary is the single source of truth for field correspondence (AC-001, AC-002, AC-003; DRY principle)
- [ ] Empty-source guard: if the AI session has zero collected fields (or Draft record has no filled fields), `IntakeModeSwitchService` returns 200 with `MappedFields = null/empty` and `ReviewItems = []` without invoking mapping logic — no null-dereference exception path (Edge: no data entered; OWASP A05 — security misconfiguration / crash prevention)
- [ ] `SwitchManualToAiAsync` writes the reconstructed `IntakeSessionState` to `IDistributedCache` with a freshly generated `CacheVersion` GUID and a 2h absolute TTL matching the existing AI session TTL; `POST /intake/draft` discards writes whose payload `cacheVersion` does not match the current cache entry (Edge: concurrent auto-save; OWASP A04)
- [ ] PHI field values read from `IDistributedCache` or Draft `IntakeRecord` are decrypted in-memory only; re-encrypted by `IPhiEncryptionService` before any write back to cache or database; plaintext PHI never written to Serilog/Seq (OWASP A02; HIPAA at-rest encryption)
- [ ] `ModeSwitchController` is decorated `[Authorize(Roles = Roles.Patient)]`; Staff and Admin roles receive 403 from RBAC middleware; unauthenticated requests receive 401 (AC-001, AC-002; OWASP A01)
