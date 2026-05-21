# Task - TASK_001

## Requirement Reference
- **User Story:** us_036
- **Story Location:** .propel/context/tasks/EP-007-I/us_036/us_036.md
- **Acceptance Criteria:**
  - AC-001: `DocumentTextExtractionWorker` consumes `DocumentUploadedEvent` from a bounded channel; decrypts the document in memory; extracts text using PdfPig page-by-page; updates `document_records.status` to `"TextExtracted"` — decrypted bytes are never written to disk
  - AC-002: `SlidingWindowChunker` produces chunks of ≤500 tokens with 50-token overlap; the invariant `chunks[i].EndToken - chunks[i+1].StartToken == 50` holds for every consecutive pair (using exclusive `EndToken`)
  - AC-003: One `document_chunks` row is inserted per chunk with `document_id`, `chunk_index` (0-based), `content`, `token_count`, `created_at`; `document_records.status` is updated to `"Chunked"` in the same transaction
  - AC-004: PdfPig returning empty text causes `status = "ExtractionFailed"`, `failure_reason = "NoTextContent"`, a `Log.Warning` to Seq; the document is not re-enqueued
- **Edge Cases:**
  - Multi-page PDF with 300+ pages: PdfPig pages enumerated one-at-a-time via `foreach (var page in doc.GetPages())`; `GetAllText()` is never called; each page is GC-eligible after text is appended to `StringBuilder` — heap usage must not spike above 500 MB for a single document
  - DOCX arrives in the pipeline: if `event.MimeType != "application/pdf"`, skip with `Log.Warning` and continue the reading loop; the `document_records` status is not modified

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
| **AIR Requirements** | AIR-003 (PDF text extracted and chunked to ≤500 tokens before downstream embedding), AIR-004 (50-token overlap preserves context boundaries across chunks) |
| **AI Pattern** | Sliding window chunking — fixed token window with overlap |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | Ollama / Llama 3.1 8B (downstream consumer of chunks — not invoked in this task) |

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — `DocumentTextExtractionWorker : BackgroundService`; `IServiceScopeFactory` for scoped DbContext per processing iteration (AC-001–004) |
| PDF Extraction | UglyToad.PdfPig | latest stable | TR-010 — `PdfDocument.Open(memoryStream)` + `GetPages()` enumeration for page-by-page streaming text extraction without full-document memory load (AC-001; Edge: 300+ pages) |
| Channel | System.Threading.Channels | .NET 8.0 built-in | TR-009 — `Channel<DocumentUploadedEvent>.CreateBounded(1000)` singleton; decouples HTTP upload response from background extraction work (AC-001) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — `document_chunks` batch insert; `document_records` status update; `IDbContextTransaction` for atomicity (AC-003) |
| Database | PostgreSQL 15.3+ | 15.3+ | TR-007 — `document_records` (`status`, `failure_reason` columns) and `document_chunks` tables (AC-003, AC-004) |
| Encryption | BouncyCastle.Cryptography | 2.x | TR-008 — `IDocumentEncryptionService.DecryptAsync(blobPath, wrappedKey)` → `MemoryStream`; established in us_035 (AC-001; OWASP A02) |
| Logging | Serilog + Seq | .NET 8.0 compatible / 2023.4+ | TR-011 — `Log.Warning` for empty-text extraction failures, DOCX skips, and channel backpressure; no PHI in log payloads (AC-004; OWASP A02) |

---

## Task Overview

Implement an event-driven PDF text extraction and chunking pipeline as a `BackgroundService`. `DocumentUploadService` (us_035) enqueues `DocumentUploadedEvent` to a `Channel<DocumentUploadedEvent>.CreateBounded(1000)` singleton after `SaveChangesAsync`. `DocumentTextExtractionWorker` reads events via `channel.Reader.ReadAsync`. Per event it: validates MIME type, decrypts the blob to a `MemoryStream` (never disk), opens the PDF with PdfPig and accumulates text page-by-page into a `StringBuilder`, passes the text to `SlidingWindowChunker` (window=500, overlap=50), and bulk-inserts `document_chunks` rows plus updates `document_records.status = "Chunked"` in one transaction. Empty-text and exception paths both set `status = "ExtractionFailed"` with `failure_reason` and emit `Log.Warning`.

---

## Dependent Tasks
- task_001 (us_035) — `DocumentUploadService`, `DocumentRecord` entity, `IDocumentEncryptionService`, and the encrypted blob must be available before this worker can consume events
- task_001 (us_006) — `IDocumentEncryptionService.DecryptAsync` AES-256-GCM decryption must be present

---

## Impacted Components
- `src/api/BackgroundServices/DocumentTextExtractionWorker.cs` — new: `BackgroundService` consuming `Channel<DocumentUploadedEvent>`
- `src/api/Features/Documents/DocumentUploadedEvent.cs` — new: event record `{ Guid DocumentId, Guid PatientId, string BlobPath, byte[] WrappedKey, string MimeType }`
- `src/api/Features/Documents/DocumentUploadService.cs` — modified (us_035 addition): enqueue `DocumentUploadedEvent` to channel after `SaveChangesAsync`
- `src/api/Features/Documents/SlidingWindowChunker.cs` — new: static chunker with `Chunk(text, windowSize: 500, overlap: 50)`
- `src/api/Features/Documents/DocumentChunk.cs` — new: EF Core entity for `document_chunks` table
- `src/api/Program.cs` — modified: singleton `Channel<DocumentUploadedEvent>.CreateBounded(1000)`; `AddHostedService<DocumentTextExtractionWorker>`; add `DocumentChunks` DbSet to `AppDbContext`

---

## Implementation Plan
1. `Channel<DocumentUploadedEvent>` singleton and worker registration in `Program.cs`: `builder.Services.AddSingleton(Channel.CreateBounded<DocumentUploadedEvent>(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest })); builder.Services.AddHostedService<DocumentTextExtractionWorker>()`; the worker injects `IServiceScopeFactory` and `ChannelReader<DocumentUploadedEvent>` — not `AppDbContext` directly (OWASP A04 — scoped DbContext in singleton; matches us_021/us_025 pattern)
2. Event publishing in `DocumentUploadService` (us_035 modification): after `await dbContext.SaveChangesAsync()`, call `_channel.Writer.TryWrite(new DocumentUploadedEvent { DocumentId, PatientId, BlobPath, WrappedKey, MimeType })`; if `TryWrite` returns false (channel full), emit `_logger.LogWarning("DocumentUploadedEvent channel full; document {DocumentId} may require manual reprocessing", documentId)` — the HTTP 201 is still returned; channel backpressure must not block the upload HTTP response (AC-001; OWASP A04)
3. DOCX and MIME guard in `DocumentTextExtractionWorker.ExecuteAsync`: at the top of the `await foreach` or `ReadAsync` loop, check `if (ev.MimeType != "application/pdf") { _logger.LogWarning("Skipping non-PDF document {DocumentId} with mime type {MimeType}", ev.DocumentId, ev.MimeType); continue; }` — no `document_records` status change; no I/O performed (Edge: DOCX arrives)
4. In-memory decryption: `await using var decryptedStream = await _documentEncryptionService.DecryptAsync(ev.BlobPath, ev.WrappedKey)` returning a `MemoryStream`; the stream is passed directly to PdfPig; after the PdfPig `using` block, zero-fill the buffer: `Array.Clear(decryptedStream.GetBuffer(), 0, (int)decryptedStream.Length)` — plaintext is never flushed to disk at any point (AC-001; OWASP A02)
5. PdfPig page-by-page extraction inside `using var pdfDoc = PdfDocument.Open(decryptedStream)`: `var sb = new StringBuilder(capacity: 4096); foreach (var page in pdfDoc.GetPages()) { sb.Append(page.Text); sb.Append(' '); }` — `GetAllText()` is deliberately avoided; each `page` reference becomes GC-eligible after the iteration step (AC-001; Edge: 300+ pages — streaming keeps heap bounded; use `pdfDoc.GetPages()` not `pdfDoc.NumberOfPages` with index access)
6. Empty text guard and `"ExtractionFailed"` status: `if (sb.Length == 0)` → `using var scope = _serviceScopeFactory.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var record = await db.DocumentRecords.FindAsync(ev.DocumentId); record.Status = "ExtractionFailed"; record.FailureReason = "NoTextContent"; await db.SaveChangesAsync(); _logger.LogWarning("PdfPig returned no text for document {DocumentId}", ev.DocumentId); continue;` — do NOT write the same `DocumentUploadedEvent` back to the channel (AC-004 — no infinite retry; OWASP A04)
7. `SlidingWindowChunker.Chunk(string text, int windowSize = 500, int overlap = 50)`: `var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries); var chunks = new List<TextChunk>(); int start = 0; while (start < words.Length) { int end = Math.Min(start + windowSize, words.Length); var slice = words[start..end]; chunks.Add(new TextChunk { Content = string.Join(" ", slice), TokenCount = slice.Length, StartToken = start, EndToken = start + windowSize }); start += windowSize - overlap; }` — `EndToken` is exclusive (past-the-end); invariant: `chunks[i].EndToken - chunks[i+1].StartToken = (s + 500) - (s + 450) = 50` ✓ (AC-002; AIR-003, AIR-004)
8. `document_chunks` batch insert and `"Chunked"` status in one transaction: `using var transaction = await db.Database.BeginTransactionAsync(ct); for (int i = 0; i < chunks.Count; i++) { db.DocumentChunks.Add(new DocumentChunk { DocumentId = ev.DocumentId, ChunkIndex = i, Content = chunks[i].Content, TokenCount = chunks[i].TokenCount, CreatedAt = DateTimeOffset.UtcNow }); } record.Status = "Chunked"; await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);` wrapped in `catch (Exception ex) { record.Status = "ExtractionFailed"; record.FailureReason = ex.Message[..Math.Min(500, ex.Message.Length)]; await db.SaveChangesAsync(); _logger.LogWarning(ex, "Extraction pipeline failed for document {DocumentId}", ev.DocumentId); }` (AC-003; OWASP A04 — catch-all prevents unhandled exception in BackgroundService)

---

## Current Project State
```
src/
└── api/
    ├── BackgroundServices/
    │   └── (DocumentTextExtractionWorker.cs    — CREATE)
    └── Features/
        └── Documents/
            ├── (DocumentUploadedEvent.cs         — CREATE)
            ├── DocumentUploadService.cs          (MODIFY — enqueue event post-SaveChangesAsync)
            ├── (SlidingWindowChunker.cs          — CREATE)
            └── (DocumentChunk.cs                — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/BackgroundServices/DocumentTextExtractionWorker.cs | BackgroundService consuming Channel<DocumentUploadedEvent> |
| CREATE | src/api/Features/Documents/DocumentUploadedEvent.cs | Event record with DocumentId, BlobPath, WrappedKey, MimeType |
| MODIFY | src/api/Features/Documents/DocumentUploadService.cs | Enqueue DocumentUploadedEvent to channel after SaveChangesAsync |
| CREATE | src/api/Features/Documents/SlidingWindowChunker.cs | Chunk(text, 500, 50) with StartToken/EndToken tracking |
| CREATE | src/api/Features/Documents/DocumentChunk.cs | EF Core entity for document_chunks table |
| MODIFY | src/api/Program.cs | Singleton channel; AddHostedService; DocumentChunks DbSet |

---

## External References
- https://github.com/UglyToad/PdfPig (UglyToad.PdfPig — `PdfDocument.Open(stream)`, `GetPages()`, `page.Text`; page-by-page streaming for large document memory safety; AC-001; Edge: 300+ pages)
- https://learn.microsoft.com/en-us/dotnet/core/extensions/channels (System.Threading.Channels — `Channel.CreateBounded`, `BoundedChannelFullMode.DropOldest`, `ChannelReader.ReadAsync`; AC-001 async event pipeline)
- https://learn.microsoft.com/en-us/dotnet/core/extensions/hosted-services (ASP.NET Core 8 BackgroundService — `ExecuteAsync`, `IServiceScopeFactory` for scoped dependencies; OWASP A04 DI lifetime pattern)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Upload a valid 3-page PDF via us_035; wait for background processing; verify `document_records.status = "Chunked"` and `document_chunks` rows exist with `chunk_index` 0, 1, 2, … (AC-001, AC-003)
- [ ] Assert that for a 2,000-word document, every chunk has `TokenCount <= 500` and consecutive chunks satisfy `chunks[i].EndToken - chunks[i+1].StartToken == 50` (AC-002)
- [ ] Upload an image-only scanned PDF (no selectable text); verify `document_records.status = "ExtractionFailed"` with `failure_reason = "NoTextContent"` and no `document_chunks` rows; verify no second event is enqueued (AC-004)
- [ ] Push a `DocumentUploadedEvent` with `MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"` directly to the channel; verify the worker logs a Warning and the `document_records` status is unchanged (Edge: DOCX)
- [ ] Process a 300-page PDF; monitor heap via dotnet-counters; verify no OOM and `status = "Chunked"` after processing (Edge: 300+ pages)
- [ ] Verify `Log.Warning` is emitted for extraction failures and DOCX skips; verify no `WrappedKey` bytes or decrypted text appears in Seq output (OWASP A02)
- [ ] Verify that if `document_chunks` insert fails mid-batch, the `status` is set to `"ExtractionFailed"` and no partial chunk rows remain (AC-003 — transaction rollback)

---

## Implementation Checklist
- [x] `IServiceScopeFactory.CreateScope()` is called inside the `ExecuteAsync` loop for each event — the scoped `AppDbContext` is never captured from the constructor or reused across event processing iterations (OWASP A04 — DI lifetime; matches us_021/us_025 pattern)
- [x] `decryptedStream.GetBuffer()` is zero-filled with `Array.Clear` inside a `finally` block after PdfPig processing completes — plaintext in the `MemoryStream` is not left in memory beyond the processing scope (AC-001; OWASP A02)
- [x] `PdfDocument.Open(stream)` is wrapped in a `using` statement and `GetPages()` is used for iteration — `GetAllText()` is not called; the PdfDocument instance is disposed before the next event is processed (AC-001; Edge: 300+ pages — prompt disposal prevents heap accumulation)
- [x] The empty-text guard calls `continue` after updating `status = "ExtractionFailed"` — it does not call `channel.Writer.TryWrite` with the same or a modified event, preventing infinite retry (AC-004; OWASP A04)
- [x] `SlidingWindowChunker` uses `EndToken = start + windowSize` (exclusive past-the-end); consecutive chunks satisfy `EndToken[i] - StartToken[i+1] = 500 - 450 = 50`; a unit test asserts this invariant on a synthetic 2,000-token string (AC-002; AIR-004)
- [x] The catch-all exception handler in the processing loop truncates `ex.Message` to 500 characters before writing it to `failure_reason` — no unbounded exception messages are stored in the DB column (OWASP A04 — column size constraint respect)
- [x] `WrappedKey` from `DocumentUploadedEvent` is not logged at any level; `BlobPath` is not logged at `Information` or higher — only `DocumentId` (UUID) appears in structured log entries (OWASP A02)
- [x] `BoundedChannelFullMode.DropOldest` is set on the channel so that backpressure drops the oldest unprocessed event rather than blocking the producer HTTP thread; a `LogWarning` is emitted in `TryWrite` failure paths in the publisher (OWASP A04 — no HTTP thread blocking)
