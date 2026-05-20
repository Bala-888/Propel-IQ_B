# Task - TASK_001

## Requirement Reference
- **User Story:** us_039
- **Story Location:** .propel/context/tasks/EP-007-I/us_039/us_039.md
- **Acceptance Criteria:**
  - AC-001: `GET /documents/{id}/status` returns the current `document_records.status` value for the authenticated patient's document; `GET /documents` returns all documents for the authenticated patient (used by SCR-010 on page load)
  - AC-002: A `PipelineGuardianWorker` `BackgroundService` runs on a 30-second `PeriodicTimer`; documents that have not reached `"EntitiesExtracted"` within 120 seconds of `processing_started_at` are updated to `status = "TimedOut"` and `Log.Error("DocumentProcessingTimeout", documentId)` is emitted per document
  - AC-004: `POST /documents/{id}/retry` resets `status = "Uploaded"`, `processing_started_at = now()`, deletes stale chunks/embeddings/entities, and re-publishes `DocumentUploadedEvent` to restart the pipeline
  - AC-005: `ExtractionFailed` documents are also eligible for retry via `POST /documents/{id}/retry`
- **Edge Cases:**
  - Concurrent retry: if two calls arrive simultaneously for the same `TimedOut` document, the atomic `ExecuteUpdateAsync WHERE status IN ('TimedOut','ExtractionFailed')` ensures only the first update succeeds; the second call finds 0 rows updated and returns HTTP 200 without re-publishing the event — no duplicate pipeline job
  - Complete document: if `status == "EntitiesExtracted"`, `POST /documents/{id}/retry` returns HTTP 409 `{"error": "Document processing is already complete."}` — pipeline is not re-triggered

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes — provides `GET /documents`, `GET /documents/{id}/status`, and `POST /documents/{id}/retry` consumed by task_002 (SCR-010) |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-010-document-processing-status.html |
| **Screen Spec** | SCR-010 (Document Processing Status) |
| **UXR Requirements** | UXR-603 — `POST /documents/{id}/retry` 409 message must be plain text, not a code or stack trace |
| **Design Tokens** | N/A |

---

## AI References
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No — this task does not invoke Ollama or AI models directly |
| **AIR Requirements** | AIR-008 (AI pipeline operational monitoring) — `DocumentProcessingTimeout` log event in Seq feeds operational dashboards; timeout threshold (120s) is the SLA for the AI extraction pipeline |
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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — `DocumentsController` for GET/POST endpoints; `PipelineGuardianWorker : BackgroundService`; `IServiceScopeFactory` for scoped DbContext in guardian (AC-001, AC-002, AC-004) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — `ExecuteUpdateAsync` for status transitions; `ExecuteSqlAsync` for cleanup deletes; ownership-enforced `WHERE patient_id = @jwtPatientId` on all queries (AC-001–005; OWASP A01, A03) |
| Database | PostgreSQL 15.3+ | 15.3+ | TR-007 — `processing_started_at timestamptz` column on `document_records`; partial index on `(status, processing_started_at)` for guardian scan; ON DELETE CASCADE from `document_chunks` to `chunk_embeddings` (AC-002, AC-004) |
| Channel | System.Threading.Channels | .NET 8.0 built-in | TR-009 — `Channel<DocumentUploadedEvent>` singleton (registered in us_035) injected into `DocumentsController`; `TryWrite` on successful retry to restart pipeline (AC-004) |
| Timer | System.Threading.PeriodicTimer | .NET 8.0 built-in | TR-009 — `PeriodicTimer(TimeSpan.FromSeconds(30))` in `PipelineGuardianWorker`; consistent with us_025/us_027 guardian pattern (AC-002; NFR-003) |
| Logging | Serilog + Seq | .NET 8.0 compatible / 2023.4+ | TR-011 — `Log.Error("DocumentProcessingTimeout", documentId)` per timed-out document; no document content or patient PHI in log calls (AC-002; OWASP A02) |

---

## Task Overview

Implement three components: (1) read endpoints `GET /documents` and `GET /documents/{id}/status` in `DocumentsController` returning `DocumentStatusDto`, both ownership-guarded via JWT patientId; (2) `POST /documents/{id}/retry` which atomically cleans up stale pipeline artefacts, resets document status, and re-queues the upload event; (3) `PipelineGuardianWorker` running every 30 seconds to detect and mark documents that have exceeded the 120-second processing SLA. An EF Core migration adds `processing_started_at` to `document_records` and a partial index for efficient guardian scanning. `DocumentUploadController` (us_035) is modified to populate `processing_started_at` on initial upload.

---

## Dependent Tasks
- task_001 (us_035) — `DocumentsController`, `DocumentRecord` entity, `Channel<DocumentUploadedEvent>` singleton, and `document_records` table must exist before these endpoints can be added
- task_001 (us_036) — `document_chunks` and `chunk_embeddings` tables (with FK cascade) must exist for cleanup on retry
- task_001 (us_038) — `patient_entities` table must exist for cleanup on retry

---

## Impacted Components
- `src/api/Controllers/DocumentsController.cs` — modified: add `GET /documents`, `GET /documents/{id}/status`, `POST /documents/{id}/retry` actions; inject `Channel<DocumentUploadedEvent>`
- `src/api/BackgroundServices/PipelineGuardianWorker.cs` — new: `PeriodicTimer(30s)` guardian; query + batch update + per-document Log.Error
- `src/api/Features/Documents/DocumentStatusDto.cs` — new: `{ Guid Id, string FileName, string Status, DateTimeOffset ProcessingStartedAt }`
- `src/api/Features/Documents/DocumentRecord.cs` — modified (us_035 entity): add `ProcessingStartedAt` property
- `src/api/Controllers/DocumentUploadController.cs` — modified (us_035 file): set `ProcessingStartedAt = DateTimeOffset.UtcNow` on new document record
- `src/api/Program.cs` — modified: `AddHostedService<PipelineGuardianWorker>()`
- EF Core migration `AddProcessingStartedAtToDocumentRecords` — new: column + partial index

---

## Implementation Plan
1. `GET /documents/{id}/status` and `GET /documents` in `DocumentsController`: both decorated `[Authorize(Roles = "Patient")]`; patientId extracted from `User.FindFirstValue(ClaimTypes.NameIdentifier)`; status endpoint: `await db.DocumentRecords.Where(dr => dr.Id == id && dr.PatientId == jwtPatientId).Select(dr => new DocumentStatusDto { Id = dr.Id, FileName = dr.FileName, Status = dr.Status, ProcessingStartedAt = dr.ProcessingStartedAt }).FirstOrDefaultAsync(ct)` — returns 404 if null (ownership enforced at query); list endpoint: same WHERE with `OrderByDescending(dr => dr.CreatedAt)` (AC-001; OWASP A01 — ownership at query layer, not just route matching)
2. `POST /documents/{id}/retry` action: `[Authorize(Roles = "Patient")]`; patientId from JWT; first check `await db.DocumentRecords.Where(dr => dr.Id == id && dr.PatientId == jwtPatientId).Select(dr => dr.Status).FirstOrDefaultAsync(ct)` — if null return 404, if `"EntitiesExtracted"` return 409 `{ error = "Document processing is already complete." }` (Edge: complete); else run cleanup + atomic update: `await db.Database.ExecuteSqlAsync($"DELETE FROM patient_entities WHERE document_id = {id}")` then `await db.Database.ExecuteSqlAsync($"DELETE FROM document_chunks WHERE document_id = {id}")` (CASCADE to chunk_embeddings); then `int updated = await db.DocumentRecords.Where(dr => dr.Id == id && dr.PatientId == jwtPatientId && (dr.Status == "TimedOut" || dr.Status == "ExtractionFailed")).ExecuteUpdateAsync(s => s.SetProperty(dr => dr.Status, "Uploaded").SetProperty(dr => dr.ProcessingStartedAt, DateTimeOffset.UtcNow), ct)` — if `updated == 0` return 200 no-op (Edge: concurrent retry); else `_uploadChannel.Writer.TryWrite(new DocumentUploadedEvent { DocumentId = id })` return 200 (AC-004, AC-005; OWASP A01, A03 — parameterised ExecuteSqlAsync FormattableString)
3. `PipelineGuardianWorker : BackgroundService` with `await using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30))` inside `ExecuteAsync(ct)`: per tick, `await using var scope = _scopeFactory.CreateScope()`; `var db = scope.ServiceProvider.GetRequiredService<AppDbContext>()`; `var threshold = DateTimeOffset.UtcNow.AddSeconds(-120)`; `var timedOutIds = await db.DocumentRecords.Where(dr => !new[]{"EntitiesExtracted","TimedOut","ExtractionFailed"}.Contains(dr.Status) && dr.ProcessingStartedAt < threshold).Select(dr => dr.Id).ToListAsync(ct)`; if any: `await db.DocumentRecords.Where(dr => timedOutIds.Contains(dr.Id)).ExecuteUpdateAsync(s => s.SetProperty(dr => dr.Status, "TimedOut"), ct)` then `foreach (var docId in timedOutIds) _logger.LogError("DocumentProcessingTimeout {DocumentId}", docId)` (AC-002; NFR-003 — max 30s detection lag from SLA breach)
4. EF Core migration `AddProcessingStartedAtToDocumentRecords`: `migrationBuilder.AddColumn<DateTimeOffset>("processing_started_at", "document_records", nullable: false, defaultValueSql: "now()")`; then `migrationBuilder.Sql("CREATE INDEX document_records_timeout_scan_idx ON document_records (status, processing_started_at) WHERE status NOT IN ('EntitiesExtracted','TimedOut','ExtractionFailed')")` — partial index covers only active documents, reducing index size and scan cost for the guardian query (AC-002; NFR-003 — query performance for guardian)
5. MODIFY `DocumentUploadController` (us_035 file): add `ProcessingStartedAt = DateTimeOffset.UtcNow` to the `DocumentRecord` initialiser immediately before `db.DocumentRecords.Add(documentRecord)` — the timer begins at upload, not at extraction start, so the 120-second window starts from the moment the file is accepted; also add `ProcessingStartedAt` property (`public DateTimeOffset ProcessingStartedAt { get; set; }`) to `DocumentRecord.cs` entity (AC-002 — threshold reference point set at upload time)
6. `builder.Services.AddHostedService<PipelineGuardianWorker>()` in `Program.cs`; `PipelineGuardianWorker` constructor accepts `IServiceScopeFactory` and `ILogger<PipelineGuardianWorker>` — not `AppDbContext` directly; `Channel<DocumentUploadedEvent>` singleton (already registered by us_035) injected into `DocumentsController` constructor for retry re-publish (OWASP A04 — DI lifetime; no scoped service captured in singleton)

---

## Current Project State
```
src/
└── api/
    ├── Controllers/
    │   ├── DocumentsController.cs              (MODIFY — add GET status, GET list, POST retry)
    │   └── DocumentUploadController.cs         (MODIFY — set ProcessingStartedAt on create)
    ├── BackgroundServices/
    │   └── (PipelineGuardianWorker.cs          — CREATE)
    └── Features/
        └── Documents/
            ├── DocumentRecord.cs               (MODIFY — add ProcessingStartedAt property)
            └── (DocumentStatusDto.cs           — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | src/api/Controllers/DocumentsController.cs | Add GET /documents, GET /documents/{id}/status, POST /documents/{id}/retry |
| MODIFY | src/api/Controllers/DocumentUploadController.cs | Set ProcessingStartedAt = DateTimeOffset.UtcNow on document record creation |
| CREATE | src/api/BackgroundServices/PipelineGuardianWorker.cs | PeriodicTimer(30s) guardian; batch timeout detection and status update |
| CREATE | src/api/Features/Documents/DocumentStatusDto.cs | DTO: { Id, FileName, Status, ProcessingStartedAt } |
| MODIFY | src/api/Features/Documents/DocumentRecord.cs | Add ProcessingStartedAt property |
| MODIFY | src/api/Program.cs | AddHostedService<PipelineGuardianWorker>() |
| CREATE | src/api/Migrations/AddProcessingStartedAtToDocumentRecords.cs | Column + partial index on (status, processing_started_at) |

---

## External References
- https://learn.microsoft.com/en-us/dotnet/api/system.threading.periodictimer (PeriodicTimer — `await timer.WaitForNextTickAsync(ct)` loop pattern; AC-002)
- https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete#executeupdateasync (EF Core `ExecuteUpdateAsync` — batch update without loading entities; AC-002, AC-004)
- https://www.postgresql.org/docs/current/indexes-partial.html (PostgreSQL partial index — `WHERE status NOT IN (...)` for efficient guardian scan; NFR-003)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Call `GET /documents/{id}/status` with JWT for the owning patient; verify correct status returned; call with a different patient's JWT; verify 404 returned — not 403 (OWASP A01 — no information disclosure via status code difference)
- [ ] Set a document's `processing_started_at = now() - 125 seconds` directly in DB; wait for the next guardian tick (≤ 30s); verify `document_records.status = 'TimedOut'` and `Log.Error("DocumentProcessingTimeout", documentId)` appears in Seq (AC-002)
- [ ] Call `POST /documents/{id}/retry` on a `TimedOut` document; verify `status` resets to `'Uploaded'`, `processing_started_at` is updated to approximately now, `document_chunks` rows are deleted, `patient_entities` rows are deleted, and `DocumentUploadedEvent` is published to the upload channel (AC-004)
- [ ] Call `POST /documents/{id}/retry` twice concurrently on the same `TimedOut` document; verify exactly one `DocumentUploadedEvent` is published and `status` is `'Uploaded'` (not processed twice) (Edge: concurrent retry)
- [ ] Call `POST /documents/{id}/retry` on a document with `status = 'EntitiesExtracted'`; verify HTTP 409 with `{"error": "Document processing is already complete."}` (Edge: complete document)
- [ ] Call `POST /documents/{id}/retry` on a document with `status = 'ExtractionFailed'`; verify reset and pipeline restart (AC-005)
- [ ] Verify `Log.Error` calls in `PipelineGuardianWorker` contain only `documentId` (UUID) — no file names, patient names, or document content (OWASP A02)

---

## Implementation Checklist
- [ ] `GET /documents/{id}/status` and `GET /documents` both apply the ownership filter `WHERE patient_id = jwtPatientId` at the EF Core query level — the controller never fetches all rows then filters in memory (OWASP A01 — ownership enforced at DB, not application layer)
- [ ] `POST /documents/{id}/retry` cleanup (`DELETE FROM patient_entities`, `DELETE FROM document_chunks`) uses `ExecuteSqlAsync` with `FormattableString` interpolated parameters — entity `id` is passed as a typed parameter, never string-concatenated into the SQL string (OWASP A03 — parameterised deletes)
- [ ] The `ExecuteUpdateAsync` for retry status reset uses `.Where(... && (dr.Status == "TimedOut" || dr.Status == "ExtractionFailed"))` as the atomic idempotency guard — if the document is already in `'Uploaded'` state (concurrent second call), 0 rows are updated and no `DocumentUploadedEvent` is published (Edge: concurrent retry; AC-004)
- [ ] `409 Conflict` response for `'EntitiesExtracted'` retry is returned **before** any DELETE operations — the cleanup only runs after the 409 guard has passed (Edge: complete document; prevents accidental data deletion on complete documents)
- [ ] `PipelineGuardianWorker` uses `IServiceScopeFactory.CreateScope()` per tick — the scoped `AppDbContext` is resolved fresh on every timer tick and disposed at the end of the tick; `AppDbContext` is never stored as a field (OWASP A04 — DI lifetime)
- [ ] `PipelineGuardianWorker` uses `PeriodicTimer` (`await timer.WaitForNextTickAsync(ct)`) — not `Task.Delay`, `System.Timers.Timer`, or `Thread.Sleep` (consistent with us_025/us_027; NFR-003 — precise 30s cadence)
