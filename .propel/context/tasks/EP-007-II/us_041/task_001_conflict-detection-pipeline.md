# Task - TASK_001

## Requirement Reference
- **User Story:** us_041
- **Story Location:** .propel/context/tasks/EP-007-II/us_041/us_041.md
- **Acceptance Criteria:**
  - AC-001: `ConflictDetectionWorker` consumes `PatientEntitiesUpdatedEvent`; calls `POST /api/generate` on the Ollama local endpoint with a structured prompt listing all patient entity IDs, types, and values; analysis completes within 30 seconds for a patient with up to 200 entities
  - AC-002: Each conflict returned by Ollama is persisted to `clinical_conflicts` with `patient_id`, `entity_a_id`, `entity_b_id`, `conflict_type`, `description`, `severity`, and `status = "Open"`
  - AC-003: `GET /patients/{id}/summary` response is extended to include a `conflicts` array of open conflicts with `entityA`, `entityB`, `conflictType`, `description`, and `severity`
  - AC-004: Conflict detection is idempotent — `INSERT ... ON CONFLICT (patient_id, entity_a_id, entity_b_id) DO NOTHING` ensures no duplicate rows for the same entity pair
- **Edge Cases:**
  - Fewer than 2 entities: exit early without calling Ollama; log `ConflictDetectionSkipped` with `Reason=InsufficientEntities`
  - Ollama returns unparseable conflict output: log `ConflictDetectionSchemaError` and return without persisting any conflicts — no partial data written to `clinical_conflicts`

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes — extends `GET /patients/{id}/summary` response shape consumed by task_002 (SCR-014) |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-014-360-patient-view.html |
| **Screen Spec** | SCR-014 (360° Patient View — conflict banner in Extracted data panel) |
| **UXR Requirements** | UXR-105 — `ConflictDto` must include `severity` string so the frontend can render text + icon severity indicators |
| **Design Tokens** | N/A |

---

## AI References
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-005 (AI-driven clinical conflict detection — drug interactions, drug-allergy conflicts, duplicate diagnoses identified from extracted entity set) |
| **AI Pattern** | Structured prompt → constrained JSON output; entity ID list enables model to reference specific entities by UUID in the response |
| **Prompt Template Path** | Inline in `ConflictDetectionWorker.cs` — prompt: `"Review the following clinical entities for patient record. Identify clinical conflicts (drug interactions, drug-allergy conflicts, duplicate diagnoses). Return ONLY valid JSON: {\"conflicts\": [{\"entityAId\": \"<uuid>\", \"entityBId\": \"<uuid>\", \"conflictType\": \"DrugInteraction|DrugAllergyConflict|DuplicateDiagnosis\", \"description\": \"<string>\", \"severity\": \"Low|Medium|High\"}]}. Return {\"conflicts\": []} if none found.\n\nEntities:\n{entityList}"` |
| **Guardrails Config** | Ollama model: `llama3.1:8b`; `stream: false`; 30-second timeout; severity enum: Low/Medium/High; conflictType enum: DrugInteraction/DrugAllergyConflict/DuplicateDiagnosis; schema validation required before any insert |
| **Model Provider** | Ollama (local) — `llama3.1:8b` via `POST http://{OLLAMA_BASE_URL}/api/generate` |

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — `ConflictDetectionWorker : BackgroundService`; `IServiceScopeFactory` for scoped DbContext; `IHttpClientFactory` for Ollama calls; modified `EntityExtractionWorker` for event publish (AC-001–004) |
| HTTP Client | System.Net.Http (IHttpClientFactory) | .NET 8.0 built-in | TR-006 — Named client `"ollama-conflicts"` with base URL from `OLLAMA_BASE_URL` env var; `POST /api/generate` with `stream: false`; 30-second `CancellationTokenSource` (AC-001; OWASP A02) |
| Channel | System.Threading.Channels | .NET 8.0 built-in | TR-009 — `Channel<PatientEntitiesUpdatedEvent>.CreateBounded(1000)` singleton; `EntityExtractionWorker` publishes post-commit; `ConflictDetectionWorker` consumes (AC-001) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — `ExecuteSqlAsync` parameterised INSERT ON CONFLICT DO NOTHING; `AsNoTracking()` query for summary conflicts extension (AC-002–004; OWASP A03) |
| Database | PostgreSQL 15.3+ | 15.3+ | TR-007 — `clinical_conflicts` table with UNIQUE constraint on `(patient_id, entity_a_id, entity_b_id)` for idempotent upsert (AC-002, AC-004) |
| JSON | System.Text.Json | .NET 8.0 built-in | Ollama response schema validation; `JsonDocument.Parse`; `TryGetProperty("conflicts")`; severity enum guard; Guid parseable validation for entityAId/entityBId (Edge: invalid JSON; AC-001) |
| AI Runtime | Ollama + Llama 3.1 8B | latest stable | TR-003 — local clinical conflict detection via `llama3.1:8b`; entity values in prompt body, never in logs (AC-001; AIR-005) |
| Logging | Serilog + Seq | .NET 8.0 compatible / 2023.4+ | TR-011 — `Log.Error("ConflictDetectionSchemaError")`, `Log.Error("ConflictDetectionTimeout")`, `Log.Information("ConflictDetectionSkipped")`; entity values and conflict descriptions never in logs (OWASP A02) |

---

## Task Overview

Implement an event-driven clinical conflict detection pipeline. `EntityExtractionWorker` (us_038) is modified to publish `PatientEntitiesUpdatedEvent { PatientId }` after setting document status to `"EntitiesExtracted"`. `ConflictDetectionWorker` reads events, guards against insufficient entities, loads all patient entities, calls Ollama `/api/generate` with a structured prompt, validates the response schema, and inserts validated conflicts via idempotent `ON CONFLICT DO NOTHING` SQL. `GET /patients/{id}/summary` is extended with a 4th round trip for open conflicts. An EF Core migration creates the `clinical_conflicts` table with a unique constraint.

---

## Dependent Tasks
- task_001 (us_038) — `EntityExtractionWorker`, `PatientEntity` entity, `patient_entities` table, and `PatientId` on the worker event must exist for modification and for loading entity data
- task_001 (us_040) — `PatientsController.GetSummary` and `PatientSummaryDto` must exist for the conflicts extension in item 6
- task_001 (us_007) — `patients` table must exist as FK target

---

## Impacted Components
- `src/api/BackgroundServices/ConflictDetectionWorker.cs` — new: `BackgroundService` consuming `Channel<PatientEntitiesUpdatedEvent>`
- `src/api/Features/Documents/PatientEntitiesUpdatedEvent.cs` — new: event record `{ Guid PatientId }`
- `src/api/BackgroundServices/EntityExtractionWorker.cs` — modified (us_038 file): publish `PatientEntitiesUpdatedEvent` after `ExecuteUpdateAsync` to "EntitiesExtracted"
- `src/api/Features/Conflicts/ClinicalConflict.cs` — new: EF Core entity with navigation properties `EntityA` and `EntityB`
- `src/api/Features/Patients/ConflictDto.cs` — new: `{ Guid Id, EntitySummaryDto EntityA, EntitySummaryDto EntityB, string ConflictType, string Description, string Severity, string Status }`
- `src/api/Features/Patients/PatientSummaryDto.cs` — modified (us_040 file): add `ConflictDto[] Conflicts` property
- `src/api/Controllers/PatientsController.cs` — modified (us_040 file): add RT4 conflicts query in `GetSummary`
- `src/api/Program.cs` — modified: channel singleton; `AddHostedService<ConflictDetectionWorker>`; named HTTP client `"ollama-conflicts"`
- EF Core migration `AddClinicalConflictsTable` — new

---

## Implementation Plan
1. `Channel<PatientEntitiesUpdatedEvent>` singleton and worker registration in `Program.cs`: `builder.Services.AddSingleton(Channel.CreateBounded<PatientEntitiesUpdatedEvent>(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest }))`; `builder.Services.AddHostedService<ConflictDetectionWorker>()`; named HTTP client: `builder.Services.AddHttpClient("ollama-conflicts", c => c.BaseAddress = new Uri(builder.Configuration["OLLAMA_BASE_URL"] ?? "http://ollama:11434"))` — URL from env var only; MODIFY `EntityExtractionWorker` (us_038 file): after `await db.DocumentRecords.Where(dr => dr.Id == ev.DocumentId).ExecuteUpdateAsync(s => s.SetProperty(dr => dr.Status, "EntitiesExtracted"), ct)`, call `_entitiesUpdatedChannel.Writer.TryWrite(new PatientEntitiesUpdatedEvent { PatientId = ev.PatientId })`; `Log.Warning` if channel full (AC-001; OWASP A02 — URL from env; A04 — non-blocking)
2. Early-exit guard in `ConflictDetectionWorker.ExecuteAsync`: within `await using var scope = _scopeFactory.CreateScope()`, after reading event via `await _channel.Reader.ReadAsync(ct)`, execute `var entityCount = await db.PatientEntities.CountAsync(pe => pe.PatientId == ev.PatientId, ct)` — if `entityCount < 2` → `_logger.LogInformation("ConflictDetectionSkipped PatientId={PatientId} Reason=InsufficientEntities", ev.PatientId)` + `continue`; only patient UUID logged, never entity values (Edge: < 2 entities; OWASP A02)
3. Ollama `/api/generate` call with 30-second timeout: `var entities = await db.PatientEntities.AsNoTracking().Where(pe => pe.PatientId == ev.PatientId).ToListAsync(ct)`; build `var entityList = string.Join("\n", entities.Select(e => $"- ID: {e.Id} | Type: {e.Type} | Value: {e.Value}"))`; build prompt inline (as described in AI References section above); call `"ollama-conflicts"` named client with `JsonContent.Create(new { model = "llama3.1:8b", prompt, stream = false })` using `using var cts30 = new CancellationTokenSource(TimeSpan.FromSeconds(30)); using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts30.Token, ct)`; on `OperationCanceledException` → `_logger.LogError("ConflictDetectionTimeout PatientId={PatientId}", ev.PatientId)` + `continue` to next event; entity values in prompt body — never in ILogger (AC-001; AIR-005; OWASP A02)
4. Two-level JSON schema validation: Level 1 — parse Ollama wrapper: `JsonDocument.Parse(httpContent)` → extract `"response"` string field; Level 2 — parse entity JSON: `JsonDocument.Parse(responseText)` in `try-catch JsonException` → verify `RootElement.TryGetProperty("conflicts", out var conflictsEl) && conflictsEl.ValueKind == JsonValueKind.Array`; for each element: verify `entityAId` and `entityBId` parse as `Guid`, `conflictType` is non-empty, `description` is non-empty, `severity` is in `_validSeverities = new HashSet<string>{"Low","Medium","High"}`; on any failure → `_logger.LogError("ConflictDetectionSchemaError PatientId={PatientId}", ev.PatientId)` + `continue` to next event without any DB write; `conflictType` and `description` values are never logged (Edge: invalid JSON; OWASP A02 — descriptions may contain entity values)
5. Idempotent INSERT per validated conflict: if `conflictsEl.GetArrayLength() == 0` → no inserts, log nothing; else for each validated conflict: `await db.Database.ExecuteSqlAsync($"INSERT INTO clinical_conflicts (id, patient_id, entity_a_id, entity_b_id, conflict_type, description, severity, status, created_at) VALUES ({Guid.NewGuid()}, {ev.PatientId}, {entityAId}, {entityBId}, {conflictType}, {description}, {severity}, 'Open', now()) ON CONFLICT (patient_id, entity_a_id, entity_b_id) DO NOTHING")` — each insert uses a `FormattableString` so EF Core generates `@p0..@pN` parameters; description and conflictType are parameterised and never string-interpolated into the SQL (AC-002, AC-004; OWASP A03)
6. MODIFY `GET /patients/{id}/summary` in `PatientsController` (us_040 file): add RT4 for open conflicts: `var conflicts = await db.ClinicalConflicts.AsNoTracking().Where(cc => cc.PatientId == id && cc.Status == "Open").Include(cc => cc.EntityA).Include(cc => cc.EntityB).ToListAsync(linkedCt)` — this adds a 4th round trip beyond the ≤3 established in us_040 AC-003; this extension is explicitly required by us_041 AC-003 and supersedes the us_040 constraint for this response shape; map to `ConflictDto[]` and add to `PatientSummaryDto`; an empty array is returned when no open conflicts exist (AC-003; OWASP A01 — endpoint already [Authorize(Roles="Staff,Admin,Clinician")])
7. EF Core migration `AddClinicalConflictsTable`: `id uuid PK DEFAULT gen_random_uuid()`, `patient_id uuid NOT NULL REFERENCES patients(id)`, `entity_a_id uuid NOT NULL REFERENCES patient_entities(id)`, `entity_b_id uuid NOT NULL REFERENCES patient_entities(id)`, `conflict_type varchar(100) NOT NULL`, `description text NOT NULL`, `severity varchar(20) NOT NULL`, `status varchar(20) NOT NULL DEFAULT 'Open'`, `created_at timestamptz NOT NULL DEFAULT now()`; `CONSTRAINT clinical_conflicts_patient_entity_a_b_key UNIQUE (patient_id, entity_a_id, entity_b_id)` — unique constraint is the DB-level guarantee for AC-004 idempotency; `ClinicalConflict` EF Core entity with `PatientEntity EntityA` and `PatientEntity EntityB` navigation properties (AC-002, AC-004)

---

## Current Project State
```
src/
└── api/
    ├── BackgroundServices/
    │   ├── ConflictDetectionWorker.cs          (CREATE)
    │   └── EntityExtractionWorker.cs           (MODIFY — publish PatientEntitiesUpdatedEvent post-status-update)
    └── Features/
        ├── Documents/
        │   └── (PatientEntitiesUpdatedEvent.cs — CREATE)
        ├── Conflicts/
        │   └── (ClinicalConflict.cs            — CREATE)
        └── Patients/
            ├── ConflictDto.cs                  (CREATE)
            └── PatientSummaryDto.cs            (MODIFY — add Conflicts property)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/BackgroundServices/ConflictDetectionWorker.cs | BackgroundService: channel consumer; early-exit guard; Ollama call; schema validation; idempotent INSERT |
| CREATE | src/api/Features/Documents/PatientEntitiesUpdatedEvent.cs | Event record `{ Guid PatientId }` |
| MODIFY | src/api/BackgroundServices/EntityExtractionWorker.cs | Publish PatientEntitiesUpdatedEvent after "EntitiesExtracted" status update |
| CREATE | src/api/Features/Conflicts/ClinicalConflict.cs | EF Core entity for clinical_conflicts table with EntityA/EntityB navigation |
| CREATE | src/api/Features/Patients/ConflictDto.cs | DTO for serialised conflict in summary response |
| MODIFY | src/api/Features/Patients/PatientSummaryDto.cs | Add ConflictDto[] Conflicts property |
| MODIFY | src/api/Controllers/PatientsController.cs | Add RT4 conflicts query in GetSummary |
| MODIFY | src/api/Program.cs | Channel singleton; AddHostedService; named HTTP client "ollama-conflicts" |
| CREATE | src/api/Migrations/AddClinicalConflictsTable.cs | clinical_conflicts table + UNIQUE constraint |

---

## External References
- https://ollama.com/docs/api#generate-a-completion (Ollama `/api/generate` — `stream: false`; response: `{"response": "<string>"}` wrapper; AC-001)
- https://www.postgresql.org/docs/current/sql-insert.html#SQL-ON-CONFLICT (PostgreSQL `ON CONFLICT DO NOTHING` — idempotent insert; AC-004)
- https://learn.microsoft.com/en-us/ef/core/querying/related-data/eager (EF Core `Include` with navigation properties — `EntityA` and `EntityB` loaded in RT4 for summary; AC-003)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [x] Run the full pipeline (us_035–us_038) for a patient with a Medication (Amoxicillin) and an Allergy (Penicillin); verify `ConflictDetectionWorker` receives `PatientEntitiesUpdatedEvent` and inserts a `DrugAllergyConflict` row in `clinical_conflicts` with `status = 'Open'` (AC-001, AC-002)
- [x] Run the conflict detection job twice for the same patient; verify `SELECT COUNT(*) FROM clinical_conflicts WHERE patient_id = <id> AND entity_a_id = <x> AND entity_b_id = <y>` returns 1 (AC-004 — idempotency)
- [x] Call `GET /patients/{id}/summary` after a conflict is detected; verify `conflicts` array contains the conflict with `entityA`, `entityB`, `conflictType`, `description`, and `severity` (AC-003)
- [x] Stub Ollama to return invalid JSON for conflict analysis; verify `Log.Error("ConflictDetectionSchemaError")` is emitted and zero rows are inserted in `clinical_conflicts` (Edge: invalid JSON — no partial data)
- [x] Run conflict detection for a patient with exactly 1 entity; verify `Log.Information("ConflictDetectionSkipped", reason=InsufficientEntities)` is emitted and Ollama is not called (Edge: < 2 entities)
- [x] Verify entity values and conflict descriptions do not appear in any Serilog log output; only `PatientId` UUID appears in log entries for `ConflictDetectionWorker` (OWASP A02 — PHI audit)
- [x] Verify `OLLAMA_BASE_URL` is read from IConfiguration only; hard-coded URL in `"ollama-conflicts"` client causes test failure (OWASP A02)

---

## Implementation Checklist
- [ ] `OLLAMA_BASE_URL` for the `"ollama-conflicts"` named HTTP client is read exclusively from `IConfiguration` in `Program.cs` — it is not hard-coded anywhere in `ConflictDetectionWorker` (OWASP A02)
- [ ] The early-exit guard (`entityCount < 2`) runs before any Ollama HTTP call — no network request is made when there are insufficient entities; the Ollama call is never attempted for a single-entity patient (Edge: < 2 entities; AC-001 — Ollama not called unnecessarily)
- [ ] The schema validation in item 4 returns (via `continue`) from the event handler **before any** `ExecuteSqlAsync` is called — if any entity in the conflicts array fails validation, zero rows are written for that entire conflict analysis result (Edge: invalid JSON; AC-004 — no partial data corruption)
- [ ] The `ExecuteSqlAsync` upsert in item 5 uses `FormattableString` interpolation (C# `$"..."`) — EF Core converts each interpolated variable to a `@p0`-style SQL parameter; this pattern is explicitly different from raw string concatenation and is not vulnerable to SQL injection even if `conflictType` or `description` contain SQL special characters (OWASP A03 — parameterised insert)
- [ ] `IServiceScopeFactory.CreateScope()` is called per event in `ExecuteAsync` — the scoped `AppDbContext` is resolved fresh for each event and disposed at the end of each iteration; `AppDbContext` is never stored as a field on `ConflictDetectionWorker` (OWASP A04 — DI lifetime; consistent with us_037/us_038 pattern)
- [ ] Entity `value` strings, conflict `description` text, and `conflictType` are never passed to any `ILogger` method — only `PatientId` (UUID) appears in log calls within `ConflictDetectionWorker`; this applies to both the error path and the happy path (OWASP A02 — entity values and conflict descriptions may contain PHI)
- [ ] The modification to `PatientsController.GetSummary` (item 6) adds the RT4 conflicts query inside the existing `linkedCt` scope — if the 2-second timeout fires, the conflicts query is also cancelled and a 503 is returned; the timeout guard from us_040 covers all four round trips (AC-003; NFR-003 — 2s SLA still enforced)
