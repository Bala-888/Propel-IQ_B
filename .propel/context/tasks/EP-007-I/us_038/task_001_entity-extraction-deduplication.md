# Task - TASK_001

## Requirement Reference
- **User Story:** us_038
- **Story Location:** .propel/context/tasks/EP-007-I/us_038/us_038.md
- **Acceptance Criteria:**
  - AC-001: `EntityExtractionWorker` consumes `DocumentEmbeddingsCompleteEvent`; for each document chunk, calls `POST /api/generate` on the Ollama local endpoint with a structured prompt that instructs the model to return only JSON of the form `{"entities": [{"type": "Diagnosis|Medication|Allergy|Procedure", "value": "<string>", "confidence": <0.0-1.0>}]}`; `stream: false` in request body to receive a single response
  - AC-002: Every Ollama response is validated against the entity schema before any DB write; invalid JSON, missing `entities` key, or `type` value outside the four allowed values → `Log.Error("EntityExtractionSchemaError", documentId, chunkId)` + skip that chunk; processing continues to the next chunk
  - AC-003: Upsert logic on `(patient_id, type, value)` unique constraint prevents duplicate `patient_entities` rows; a second extraction for the same patient/type/value updates `last_seen_at` rather than inserting a new row
  - AC-004: All validated entities are persisted with `patient_id`, `document_id`, `type`, `value`, `confidence`, `low_confidence`, `created_at`, and `last_seen_at`
  - AC-005: `document_records.status` is set to `"EntitiesExtracted"` after all chunks for the document are processed (with or without entity results)
- **Edge Cases:**
  - Empty entities array: `{"entities": []}` is a valid Ollama response — no insert, no error; processing continues to next chunk
  - Confidence < 0.5: entity is still inserted/updated but with `low_confidence = true`; entities are never silently dropped on low confidence alone

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
| **AIR Requirements** | AIR-003 (clinical NLP entity extraction — diagnoses, medications, allergies, procedures from document text) |
| **AI Pattern** | Structured prompt → constrained JSON output; extract-validate-deduplicate pipeline feeding patient record enrichment |
| **Prompt Template Path** | Inline in `EntityExtractionWorker.cs` — prompt instructs model: `"Extract medical entities from the following clinical text. Return ONLY valid JSON in this exact format: {\"entities\": [{\"type\": \"Diagnosis|Medication|Allergy|Procedure\", \"value\": \"<string>\", \"confidence\": <0.0-1.0>}]}. Do not include any explanation or prose.\n\nText: <chunk.content>"` |
| **Guardrails Config** | Ollama model: `llama3.1:8b`; `stream: false` enforced; type enum: Diagnosis, Medication, Allergy, Procedure; confidence threshold flag (< 0.5 → `low_confidence = true`, not dropped); schema validation required before any insert |
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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — `EntityExtractionWorker : BackgroundService`; `IServiceScopeFactory` for scoped DbContext per event; channel consumer pattern (AC-001–005) |
| HTTP Client | System.Net.Http (IHttpClientFactory) | .NET 8.0 built-in | TR-006 — Named client `"ollama-generate"` with base URL from `OLLAMA_BASE_URL` env var; `POST /api/generate` with `stream: false` for single-response JSON output (AC-001; OWASP A02) |
| Channel | System.Threading.Channels | .NET 8.0 built-in | TR-009 — `Channel<DocumentEmbeddingsCompleteEvent>.CreateBounded(1000)` singleton; `EmbeddingWorker` publishes after all chunks embedded; `EntityExtractionWorker` consumes (AC-001) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — `document_records.status` update via `ExecuteUpdateAsync`; raw SQL upsert via `ExecuteSqlAsync` for `patient_entities` conflict handling (AC-003–005; OWASP A03) |
| Database | PostgreSQL 15.3+ | 15.3+ | TR-007 — `patient_entities` table with UNIQUE constraint on `(patient_id, type, value)`; `ON CONFLICT DO UPDATE` upsert pattern (AC-003, AC-004) |
| JSON | System.Text.Json | .NET 8.0 built-in | Schema validation of Ollama response; `JsonDocument.Parse`; `TryGetProperty("entities")`; type enum guard (AC-002) |
| AI Runtime | Ollama + Llama 3.1 8B | latest stable | TR-003 — local clinical entity extraction via `/api/generate`; structured prompt ensures JSON-only output (AC-001; AIR-003) |
| Logging | Serilog + Seq | .NET 8.0 compatible / 2023.4+ | TR-011 — `Log.Error` for `EntityExtractionSchemaError`; no entity values or chunk content in logs (AC-002; OWASP A02) |

---

## Task Overview

Implement a clinical entity extraction pipeline as a `BackgroundService`. `EmbeddingWorker` (us_037) is modified to detect when all chunks of a document have embeddings committed and to publish `DocumentEmbeddingsCompleteEvent { DocumentId, PatientId }` to a new `Channel<DocumentEmbeddingsCompleteEvent>` singleton. `EntityExtractionWorker` reads these events, iterates document chunks, calls Ollama `POST /api/generate` per chunk with a JSON-constraining prompt, validates the response against a strict schema, applies the confidence flag rule, and upserts valid entities to `patient_entities` via a parameterised `ON CONFLICT DO UPDATE` SQL statement. On completion, `document_records.status` is set to `"EntitiesExtracted"`. An EF Core migration creates the `patient_entities` table and unique constraint.

---

## Dependent Tasks
- task_001 (us_037) — `EmbeddingWorker` must exist and be modifiable to add the post-commit count check and `DocumentEmbeddingsCompleteEvent` publish logic
- task_001 (us_006) — `document_records` and `document_chunks` tables must exist with `patient_id` FK on `document_records`
- task_001 (us_007) — `patients` table must exist as FK target for `patient_entities.patient_id`

---

## Impacted Components
- `src/api/BackgroundServices/EntityExtractionWorker.cs` — new: `BackgroundService` consuming `Channel<DocumentEmbeddingsCompleteEvent>`; Ollama `/api/generate` per chunk; schema validation; upsert; status update
- `src/api/Features/Documents/DocumentEmbeddingsCompleteEvent.cs` — new: event record `{ Guid DocumentId, Guid PatientId }`
- `src/api/BackgroundServices/EmbeddingWorker.cs` — modified (us_037 file): after each successful `SaveChangesAsync`, count `chunk_embeddings` vs `document_chunks` for document; if equal and > 0, query `document_records` for `patient_id` and publish `DocumentEmbeddingsCompleteEvent`
- `src/api/Features/Entities/PatientEntity.cs` — new: EF Core entity for `patient_entities` table
- `src/api/Program.cs` — modified: singleton `Channel<DocumentEmbeddingsCompleteEvent>.CreateBounded(1000)`; `AddHostedService<EntityExtractionWorker>`; named HTTP client `"ollama-generate"` with `OLLAMA_BASE_URL`
- EF Core migration `AddPatientEntitiesTable` — new: `patient_entities` table + unique constraint on `(patient_id, type, value)`

---

## Implementation Plan
1. `Channel<DocumentEmbeddingsCompleteEvent>` singleton and worker registration in `Program.cs`: `builder.Services.AddSingleton(Channel.CreateBounded<DocumentEmbeddingsCompleteEvent>(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest }))`; `builder.Services.AddHostedService<EntityExtractionWorker>()`; named HTTP client: `builder.Services.AddHttpClient("ollama-generate", c => c.BaseAddress = new Uri(builder.Configuration["OLLAMA_BASE_URL"] ?? "http://ollama:11434"))` — base URL from env var only; this is a second named client distinct from `"ollama-embed"` (AC-001; OWASP A02 — no hardcoded URL)
2. EmbeddingWorker MODIFY — all-chunks-complete detection: after each `await db.SaveChangesAsync(ct)` per chunk in us_037 `EmbeddingWorker`, within the same scoped `db` context, execute `var totalChunks = await db.DocumentChunks.CountAsync(dc => dc.DocumentId == ev.DocumentId, ct)` and `var embeddedChunks = await db.ChunkEmbeddings.CountAsync(ce => ce.Chunk.DocumentId == ev.DocumentId, ct)`; if `embeddedChunks == totalChunks && totalChunks > 0`, query `var doc = await db.DocumentRecords.FindAsync(new object[] { ev.DocumentId }, ct)` and call `_embeddingsCompleteChannel.Writer.TryWrite(new DocumentEmbeddingsCompleteEvent { DocumentId = ev.DocumentId, PatientId = doc!.PatientId })`; on full channel emit `Log.Warning("DocumentEmbeddingsCompleteEvent channel full; document {DocumentId} skipped", ev.DocumentId)` — extraction must be retriggereable manually (AC-001 trigger; OWASP A04 — non-blocking)
3. Per-chunk Ollama `/api/generate` call: for each `DocumentChunk` in the document, compose `var prompt = $"Extract medical entities from the following clinical text. Return ONLY valid JSON in this exact format: {{\"entities\": [{{\"type\": \"Diagnosis|Medication|Allergy|Procedure\", \"value\": \"<string>\", \"confidence\": <0.0-1.0>}}]}}. Do not include any explanation or prose.\n\nText: {chunk.Content}"`; call the `"ollama-generate"` named client with `JsonContent.Create(new { model = "llama3.1:8b", prompt, stream = false })`; read `response.Content`; extract the Ollama wrapper's `"response"` string field via `JsonDocument.Parse`; chunk.Content is never written to any `ILogger` call (AC-001; OWASP A02 — chunk content may contain PHI)
4. JSON schema validation of Ollama response: `JsonDocument.Parse(responseText)` inside a try-catch `JsonException`; verify `RootElement.TryGetProperty("entities", out var entitiesEl) && entitiesEl.ValueKind == JsonValueKind.Array`; for each element verify `type` property exists and its string value is in `_allowedTypes = new HashSet<string> { "Diagnosis", "Medication", "Allergy", "Procedure" }`, and `value` property is a non-empty string, and `confidence` is a number in [0.0, 1.0]; on any failure: `_logger.LogError("EntityExtractionSchemaError for document {DocumentId} chunk {ChunkId}", ev.DocumentId, chunkId)` + `continue` to next chunk; entity `value` string is never included in the log call (AC-002; OWASP A02)
5. Edge case handling within the validated entity loop: if `entitiesEl.GetArrayLength() == 0`, skip without logging and `continue` to next chunk — an empty array is a valid Ollama response (Edge: empty array); for each entity where `entity.confidence < 0.5`, set `lowConfidence = true` and proceed to upsert — do not `continue` or drop the entity (Edge: low confidence; AC-004 — all valid entities persisted)
6. Upsert per validated entity via parameterised raw SQL: `await db.Database.ExecuteSqlAsync($"INSERT INTO patient_entities (id, patient_id, document_id, type, value, confidence, low_confidence, created_at, last_seen_at) VALUES ({Guid.NewGuid()}, {ev.PatientId}, {ev.DocumentId}, {entity.Type}, {entity.Value}, {entity.Confidence}, {lowConfidence}, now(), now()) ON CONFLICT (patient_id, type, value) DO UPDATE SET last_seen_at = now(), document_id = EXCLUDED.document_id, confidence = EXCLUDED.confidence, low_confidence = EXCLUDED.low_confidence")` — EF Core `ExecuteSqlAsync` with interpolated parameters prevents SQL injection (AC-003, AC-004; OWASP A03 — parameterised; entity.Value passed as parameter, never string-concatenated into SQL)
7. After the chunk loop completes (all chunks processed regardless of result), update document status: `await db.DocumentRecords.Where(dr => dr.Id == ev.DocumentId).ExecuteUpdateAsync(s => s.SetProperty(dr => dr.Status, "EntitiesExtracted"), ct)`; `IServiceScopeFactory.CreateScope()` wraps the entire event handler to ensure the scoped `AppDbContext` is disposed after each event — not captured in the singleton worker's constructor (AC-005; OWASP A04 — correct DI lifetime)
8. EF Core migration `AddPatientEntitiesTable`: `patient_entities` table columns — `id uuid PRIMARY KEY DEFAULT gen_random_uuid()`, `patient_id uuid NOT NULL REFERENCES patients(id)`, `document_id uuid NOT NULL REFERENCES document_records(id)`, `type varchar(50) NOT NULL`, `value varchar(500) NOT NULL`, `confidence double precision NOT NULL`, `low_confidence boolean NOT NULL DEFAULT false`, `created_at timestamptz NOT NULL DEFAULT now()`, `last_seen_at timestamptz NOT NULL DEFAULT now()`; `CONSTRAINT patient_entities_patient_id_type_value_key UNIQUE (patient_id, type, value)` — this DB-level constraint is the enforcement layer for AC-003 deduplication (AC-003, AC-004)

---

## Current Project State
```
src/
└── api/
    ├── BackgroundServices/
    │   ├── EntityExtractionWorker.cs           (CREATE)
    │   └── EmbeddingWorker.cs                  (MODIFY — add all-chunks-complete detection + publish)
    └── Features/
        ├── Documents/
        │   └── (DocumentEmbeddingsCompleteEvent.cs  — CREATE)
        └── Entities/
            └── (PatientEntity.cs               — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/BackgroundServices/EntityExtractionWorker.cs | BackgroundService: channel consumer; Ollama /api/generate per chunk; schema validation; upsert; status update |
| CREATE | src/api/Features/Documents/DocumentEmbeddingsCompleteEvent.cs | Event record `{ Guid DocumentId, Guid PatientId }` |
| MODIFY | src/api/BackgroundServices/EmbeddingWorker.cs | Add post-save count check; if all chunks embedded, publish DocumentEmbeddingsCompleteEvent |
| CREATE | src/api/Features/Entities/PatientEntity.cs | EF Core entity for patient_entities table |
| MODIFY | src/api/Program.cs | Channel singleton; AddHostedService<EntityExtractionWorker>; named HTTP client "ollama-generate" |
| CREATE | src/api/Migrations/AddPatientEntitiesTable.cs | EF Core migration: patient_entities table + UNIQUE constraint (patient_id, type, value) |

---

## External References
- https://ollama.com/docs/api#generate-a-completion (Ollama `/api/generate` — request shape: `{"model": "llama3.1:8b", "prompt": "<text>", "stream": false}`; response: `{"response": "<string>", ...}`; AC-001)
- https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete#executesqlasync (EF Core `ExecuteSqlAsync` with interpolated parameters — parameterised SQL injection protection; AC-003, AC-004; OWASP A03)
- https://www.postgresql.org/docs/current/sql-insert.html#SQL-ON-CONFLICT (PostgreSQL `ON CONFLICT DO UPDATE` — upsert semantics for deduplication; AC-003)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Upload a document, complete extraction/chunking (us_035–us_036), complete embedding (us_037); verify `EntityExtractionWorker` receives `DocumentEmbeddingsCompleteEvent` and calls Ollama `/api/generate` once per chunk (AC-001)
- [ ] Stub Ollama to return invalid JSON for one chunk; verify `Log.Error("EntityExtractionSchemaError", ...)` is emitted and no `patient_entities` row is inserted for that chunk; verify next chunk is still processed (AC-002)
- [ ] Stub Ollama to return `{"entities": [{"type": "Diagnosis", "value": "Type 2 Diabetes", "confidence": 0.9}]}` for two different documents belonging to the same patient; verify `SELECT COUNT(*) FROM patient_entities WHERE patient_id = <id> AND type = 'Diagnosis' AND value = 'Type 2 Diabetes'` returns 1; verify `last_seen_at` is updated on the second pass (AC-003)
- [ ] Stub Ollama to return 5 unique valid entities; verify 5 `patient_entities` rows created with correct `patient_id`, `document_id`, `type`, `value`, `confidence`, `created_at`, `last_seen_at` (AC-004)
- [ ] Verify `document_records.status = 'EntitiesExtracted'` after all chunks processed, including when some chunks returned empty arrays or schema errors (AC-005)
- [ ] Stub Ollama to return `{"entities": []}` for a chunk; verify no insert, no error log, pipeline continues (Edge: empty array)
- [ ] Stub Ollama to return an entity with `confidence = 0.3`; verify `patient_entities` row inserted with `low_confidence = true` and the entity is not dropped (Edge: confidence < 0.5)
- [ ] Verify entity `value` field and chunk content never appear in Serilog output; only `documentId` and `chunkId` UUIDs in log entries (OWASP A02 — PHI guard)

---

## Implementation Checklist
- [x] `OLLAMA_BASE_URL` is read exclusively from `IConfiguration` (env var) for the `"ollama-generate"` named HTTP client registration in `Program.cs` — it is not hard-coded anywhere in `EntityExtractionWorker` (OWASP A02)
- [x] The Ollama response wrapper is parsed first (`JsonDocument.Parse(httpResponseContent)` → extract `"response"` field) before the entity schema is parsed from the extracted string — two-level JSON parse to handle Ollama's `/api/generate` response envelope (AC-001; prevents schema validation running against the Ollama wrapper)
- [x] `_allowedTypes` is a `private static readonly HashSet<string>` containing exactly `{ "Diagnosis", "Medication", "Allergy", "Procedure" }` — validated per entity before upsert; any other string value triggers the schema error path (AC-002)
- [x] Empty `entities` array (length 0) is handled by the loop simply not executing — no explicit `if` check needed, no error emitted; the pipeline continues to the next chunk (Edge: empty array; avoids false error logs)
- [x] Confidence threshold check sets `lowConfidence = true` only — it does not `continue`, `break`, or `return`; the upsert proceeds normally with `low_confidence = true` stored in the row (Edge: confidence < 0.5; AC-004 — no silent drops)
- [x] The upsert uses EF Core `ExecuteSqlAsync` with an interpolated `FormattableString` (not raw string concatenation) — each entity value is passed as a typed parameter, preventing SQL injection even if the entity `value` field contains SQL special characters (AC-003, AC-004; OWASP A03)
- [x] `IServiceScopeFactory.CreateScope()` is called at the top of each event handler iteration in `ExecuteAsync` — the scoped `AppDbContext` is resolved inside the `await using var scope = ...` block and is never stored as a field on `EntityExtractionWorker` (OWASP A04 — DI lifetime; matches us_037/us_021 pattern)
- [x] Entity `value` strings and chunk content are never passed to any `ILogger` method — only `documentId` (Guid) and `chunkId` (Guid) appear in log calls within `EntityExtractionWorker`; `entity.Type` (non-PHI) may appear in debug-level logs only if structured as a category, not a value (OWASP A02 — PHI in entity values must not appear in Seq)
