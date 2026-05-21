# Task - TASK_001

## Requirement Reference
- **User Story:** us_037
- **Story Location:** .propel/context/tasks/EP-007-I/us_037/us_037.md
- **Acceptance Criteria:**
  - AC-001: `EmbeddingWorker` consumes `DocumentChunkedEvent` and calls `POST /api/embeddings` on the Ollama local endpoint with `{"model": "llama3.1:8b", "input": "<chunk.content>"}` for each chunk
  - AC-002: A `chunk_embeddings` row is inserted per chunk with `chunk_id`, `embedding vector(1536)`, and `created_at` — no duplicate rows
  - AC-003: An HNSW cosine similarity index (`vector_cosine_ops`) exists on `chunk_embeddings.embedding`; a `GetSimilarChunks(queryVector, limit)` method executes the cosine similarity query in < 200ms for a 10,000-chunk corpus
  - AC-004: Ollama 5xx on a chunk → wait 10 seconds → retry once → if retry fails, skip that chunk + `Log.Error("EmbeddingFailed", chunkId)`; `document_records.status` remains `"Chunked"`
- **Edge Cases:**
  - Ollama offline (connection refused): stop processing the batch; re-enqueue the `DocumentChunkedEvent` to the channel after a 5-minute `Task.Delay` backoff — not dropped silently
  - Vector dimension mismatch: if Ollama returns a vector with length != 1,536, validate at application layer; `Log.Error("EmbeddingDimensionMismatch", chunkId, actualDimension)` and skip the insert — never attempt to write a malformed vector to pgvector

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
| **AIR Requirements** | AIR-001 (vector embeddings stored in pgvector for semantic similarity search), AIR-004 (chunk-level granularity enables context-preserving retrieval) |
| **AI Pattern** | Retrieval-Augmented Generation (RAG) — embedding generation pipeline feeding pgvector similarity index |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | Ollama model: `llama3.1:8b`; expected vector dimension: 1,536; max retries: 1 per chunk |
| **Model Provider** | Ollama (local) — `llama3.1:8b` via `POST http://{OLLAMA_BASE_URL}/api/embeddings` |

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — `EmbeddingWorker : BackgroundService`; `IServiceScopeFactory` for scoped DbContext; `IHttpClientFactory` for Ollama calls (AC-001–004) |
| HTTP Client | System.Net.Http (IHttpClientFactory) | .NET 8.0 built-in | TR-006 — Named client `"ollama-embed"` with base URL from `OLLAMA_BASE_URL` env var; per-chunk POST to Ollama embeddings endpoint (AC-001) |
| Channel | System.Threading.Channels | .NET 8.0 built-in | TR-009 — `Channel<DocumentChunkedEvent>.CreateBounded(1000)` singleton; `EmbeddingWorker` consumes events via `channel.Reader.ReadAsync` (AC-001) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — `chunk_embeddings` table insert via EF Core; `UseVector()` extension configured on `NpgsqlDataSourceBuilder`; raw SQL for HNSW cosine search (AC-002, AC-003) |
| Database | PostgreSQL 15.3+ with pgvector | 15.3+ | TR-007 — `chunk_embeddings` table with `vector(1536)` column; HNSW index `vector_cosine_ops`; cosine distance operator `<=>` (AC-002, AC-003) |
| Vector | Pgvector | latest stable | TR-004 — `Vector` type for `ChunkEmbedding.Embedding` EF Core property mapping; required for pgvector interop in .NET (AC-002) |
| AI Runtime | Ollama + Llama 3.1 8B | latest stable | TR-003 — local embedding generation via `llama3.1:8b` model; no external API call (AC-001) |
| Logging | Serilog + Seq | .NET 8.0 compatible / 2023.4+ | TR-011 — `Log.Error` for `EmbeddingFailed` and `EmbeddingDimensionMismatch`; `Log.Warning` for Ollama offline re-enqueue; no chunk content in logs (AC-004; OWASP A02) |

---

## Task Overview

Implement an event-driven embedding generation pipeline as a `BackgroundService`. `DocumentTextExtractionWorker` (us_036) publishes `DocumentChunkedEvent` to a `Channel<DocumentChunkedEvent>.CreateBounded(1000)` singleton after the chunking transaction commits. `EmbeddingWorker` reads events via `channel.Reader.ReadAsync`, loads `document_chunks` rows for the document, and calls the Ollama `POST /api/embeddings` endpoint per chunk. Connection-refused errors trigger a 5-minute re-enqueue; 5xx errors trigger one 10-second retry. Vector dimension is validated before insert. HNSW cosine similarity index is created via EF Core migration. A `GetSimilarChunks` query method is provided for downstream search.

---

## Dependent Tasks
- task_001 (us_036) — `DocumentTextExtractionWorker` must enqueue `DocumentChunkedEvent` after `transaction.CommitAsync()`; `document_chunks` rows must exist before embedding can be generated
- task_001 (us_006) — pgvector extension must be enabled (`HasPostgresExtension("vector")`) in the EF Core model

---

## Impacted Components
- `src/api/BackgroundServices/EmbeddingWorker.cs` — new: `BackgroundService` consuming `Channel<DocumentChunkedEvent>`
- `src/api/Features/Documents/DocumentChunkedEvent.cs` — new: event record `{ Guid DocumentId, Guid[] ChunkIds }`
- `src/api/BackgroundServices/DocumentTextExtractionWorker.cs` — modified (us_036 addition): publish `DocumentChunkedEvent` to channel after `transaction.CommitAsync()`
- `src/api/Features/Embeddings/ChunkEmbedding.cs` — new: EF Core entity for `chunk_embeddings` table
- `src/api/Features/Embeddings/EmbeddingSearchService.cs` — new: `GetSimilarChunks(float[] queryVector, int limit)` raw SQL cosine search
- `src/api/Program.cs` — modified: singleton `Channel<DocumentChunkedEvent>.CreateBounded(1000)`; `AddHostedService<EmbeddingWorker>`; named HTTP client `"ollama-embed"`; `NpgsqlDataSourceBuilder.UseVector()`; `ChunkEmbeddings` DbSet in `AppDbContext`; add `Pgvector` package reference

---

## Implementation Plan
1. `Channel<DocumentChunkedEvent>` singleton and worker registration in `Program.cs`: `builder.Services.AddSingleton(Channel.CreateBounded<DocumentChunkedEvent>(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest }))`; `builder.Services.AddHostedService<EmbeddingWorker>()`; named HTTP client: `builder.Services.AddHttpClient("ollama-embed", c => c.BaseAddress = new Uri(builder.Configuration["OLLAMA_BASE_URL"] ?? "http://ollama:11434"))` — base URL from env var only (OWASP A02; matches us_028 IHttpClientFactory pattern)
2. Event publishing in `DocumentTextExtractionWorker` (us_036 modification): after `await transaction.CommitAsync(ct)`, enqueue `new DocumentChunkedEvent { DocumentId = ev.DocumentId, ChunkIds = insertedChunkIds.ToArray() }` to the `Channel<DocumentChunkedEvent>` via `TryWrite`; if channel full, emit `Log.Warning("DocumentChunkedEvent channel full; document {DocumentId} requires manual embedding reprocessing", documentId)` — the extraction status `"Chunked"` is already committed (AC-001; OWASP A04 — non-blocking)
3. Ollama offline guard in `EmbeddingWorker.ExecuteAsync`: wrap the per-document chunk loop in `try { ... } catch (HttpRequestException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused })`: emit `_logger.LogWarning("Ollama offline for document {DocumentId}; re-enqueuing after 5-minute back-off", ev.DocumentId)`; `await Task.Delay(TimeSpan.FromMinutes(5), ct)`; `_channel.Writer.TryWrite(ev)`; `continue` — event is re-queued, not dropped (Edge: Ollama offline)
4. Per-chunk Ollama call with one retry: for each `chunkId` in `ev.ChunkIds`, load `chunk.Content` from DB; call `POST /api/embeddings` on the named client with `JsonContent.Create(new { model = "llama3.1:8b", input = chunk.Content })`; on `HttpResponseMessage.StatusCode >= 500`: `await Task.Delay(TimeSpan.FromSeconds(10), ct)`; retry once; if second attempt also >= 500 → `_logger.LogError("EmbeddingFailed for chunk {ChunkId} after retry", chunkId)`; `continue` to next chunk (AC-004)
5. Vector dimension validation before insert: deserialise Ollama response `{"embedding": [float, ...]}` to `float[]`; `if (vector.Length != 1536) { _logger.LogError("EmbeddingDimensionMismatch for chunk {ChunkId}: expected 1536, got {Actual}", chunkId, vector.Length); continue; }` — never pass a malformed vector to `dbContext.ChunkEmbeddings.Add(...)` (Edge: dimension mismatch; OWASP A04 — prevents pgvector cast exception)
6. `chunk_embeddings` insert per validated chunk: `dbContext.ChunkEmbeddings.Add(new ChunkEmbedding { ChunkId = chunkId, Embedding = new Vector(vector), CreatedAt = DateTimeOffset.UtcNow }); await dbContext.SaveChangesAsync(ct)` — called inside the per-chunk loop so each chunk is independently committed; a failure on one chunk does not roll back previously saved embeddings (AC-002 — one row per chunk, no duplicates because `ChunkId` is a unique FK)
7. EF Core migration for `chunk_embeddings` table and HNSW index: `CREATE TABLE chunk_embeddings (chunk_id uuid PRIMARY KEY REFERENCES document_chunks(id), embedding vector(1536) NOT NULL, created_at timestamptz NOT NULL DEFAULT now())`; `CREATE INDEX chunk_embeddings_embedding_hnsw_idx ON chunk_embeddings USING hnsw (embedding vector_cosine_ops) WITH (m = 16, ef_construction = 64)` — HNSW parameters tuned for 10,000-chunk corpus with < 200ms search SLA; `HasPostgresExtension("vector")` in `AppDbContext.OnModelCreating` (AC-002, AC-003)
8. `EmbeddingSearchService.GetSimilarChunksAsync(float[] queryVector, int limit = 5)`: executes `SELECT ce.chunk_id, dc.content, 1 - (ce.embedding <=> $1::vector) AS similarity FROM chunk_embeddings ce JOIN document_chunks dc ON dc.id = ce.chunk_id ORDER BY ce.embedding <=> $1::vector LIMIT $2` via `dbContext.Database.SqlQueryRaw`; `queryVector` is validated as length 1536 before the call; used by downstream AI search features; `IServiceScopeFactory` injected — not a singleton DbContext (AC-003; AIR-001; OWASP A03 — parameterised query, no interpolation)

---

## Current Project State
```
src/
└── api/
    ├── BackgroundServices/
    │   ├── EmbeddingWorker.cs                    (CREATE)
    │   └── DocumentTextExtractionWorker.cs       (MODIFY — publish DocumentChunkedEvent post-commit)
    └── Features/
        ├── Documents/
        │   └── (DocumentChunkedEvent.cs           — CREATE)
        └── Embeddings/
            ├── (ChunkEmbedding.cs                 — CREATE)
            └── (EmbeddingSearchService.cs         — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/BackgroundServices/EmbeddingWorker.cs | BackgroundService consuming Channel<DocumentChunkedEvent>; Ollama calls; retry; dimension guard |
| CREATE | src/api/Features/Documents/DocumentChunkedEvent.cs | Event record { DocumentId, ChunkIds[] } |
| MODIFY | src/api/BackgroundServices/DocumentTextExtractionWorker.cs | Publish DocumentChunkedEvent to channel after transaction.CommitAsync |
| CREATE | src/api/Features/Embeddings/ChunkEmbedding.cs | EF Core entity for chunk_embeddings (Pgvector Vector type) |
| CREATE | src/api/Features/Embeddings/EmbeddingSearchService.cs | GetSimilarChunksAsync cosine similarity query |
| MODIFY | src/api/Program.cs | Channel singleton; AddHostedService; named HTTP client "ollama-embed"; UseVector(); ChunkEmbeddings DbSet |
| CREATE | src/api/Migrations/AddChunkEmbeddingsTable.cs | EF Core migration: chunk_embeddings table + HNSW index |

---

## External References
- https://github.com/pgvector/pgvector-dotnet (Pgvector .NET — `Vector` type, `UseVector()` Npgsql configuration, EF Core mapping for `vector(1536)` column; AC-002)
- https://ollama.com/blog/embedding-models (Ollama embedding API — `POST /api/embeddings` with `{"model": "llama3.1:8b", "input": "<text>"}` request shape; AC-001)
- https://github.com/pgvector/pgvector#hnsw (pgvector HNSW — `CREATE INDEX USING hnsw (embedding vector_cosine_ops)` with `m` and `ef_construction` parameters; AC-003 < 200ms SLA)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Upload a 5-chunk document via us_035; complete us_036 extraction; verify `EmbeddingWorker` generates 5 `chunk_embeddings` rows with `embedding` non-null and `LENGTH(embedding) = 1536` (AC-001, AC-002)
- [ ] Execute the cosine similarity query with a test vector against 10,000 seeded embeddings; verify query completes within 200ms and returns 5 rows (AC-003; HNSW index performance)
- [ ] Stub Ollama to return 503 for one chunk; verify the chunk is retried once after 10 seconds; if still 503, verify `Log.Error("EmbeddingFailed", chunkId)` and next chunk is processed (AC-004)
- [ ] Stub Ollama to return connection refused; verify the `DocumentChunkedEvent` is re-enqueued after 5-minute delay; verify `Log.Warning` is emitted and no chunk_embeddings rows are inserted (Edge: Ollama offline)
- [ ] Stub Ollama to return a 512-dimension vector; verify `Log.Error("EmbeddingDimensionMismatch", ...)` and no `chunk_embeddings` insert for that chunk (Edge: dimension mismatch)
- [ ] Verify `chunk.Content` does not appear in any Serilog log output; only `chunkId` (UUID) appears in error log entries (OWASP A02)
- [ ] Verify `OLLAMA_BASE_URL` is read from environment configuration; hard-coding the URL in code produces a test failure (OWASP A02 — no hardcoded endpoints)

---

## Implementation Checklist
- [x] `OLLAMA_BASE_URL` is read exclusively from `IConfiguration` (env var); it is not hard-coded in the named HTTP client registration or anywhere in `EmbeddingWorker` (OWASP A02)
- [x] The Ollama offline catch block re-enqueues the `DocumentChunkedEvent` via `TryWrite` and then calls `await Task.Delay(TimeSpan.FromMinutes(5), ct)` before processing the next event — the delay is inside the catch block so other events queued after this one are not blocked for the full 5 minutes (Edge: Ollama offline; OWASP A04 — backpressure is bounded)
- [x] Per-chunk retry uses exactly one retry attempt (`int attempt = 0; while (attempt < 2)`) — the loop does not retry indefinitely; after `attempt == 2` the chunk is skipped and `Log.Error` is emitted (AC-004 — max 1 retry)
- [x] `vector.Length != 1536` guard is evaluated immediately after deserialising the Ollama response body, before any call to `dbContext.ChunkEmbeddings.Add` — a malformed vector cannot reach the EF Core insert path (Edge: dimension mismatch; OWASP A04)
- [x] `ChunkEmbedding.Embedding` is declared as `Vector` (Pgvector type) with EF Core property configuration `.HasColumnType("vector(1536)")`; `NpgsqlDataSourceBuilder.UseVector()` is called in `Program.cs` before the connection pool is created (AC-002 — correct pgvector type registration)
- [x] HNSW index is created in an EF Core migration, not in a raw SQL seed script, so it is version-controlled and applied automatically during `dotnet ef database update` (AC-003 — reproducible index provisioning)
- [x] `IServiceScopeFactory.CreateScope()` is used inside the processing loop to resolve `AppDbContext` for each event — the scoped DbContext is never captured in the singleton `EmbeddingWorker` constructor (OWASP A04 — DI lifetime; matches us_036/us_021 pattern)
- [x] `chunk.Content` is sent to Ollama as the request body `input` field; it is never written to any `ILogger` call — only `chunkId` (UUID) appears in log entries for this service (AC-001; OWASP A02 — PHI in chunk content must not appear in Seq)
