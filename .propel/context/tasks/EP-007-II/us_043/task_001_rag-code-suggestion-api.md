# Task - TASK_001

## Requirement Reference
- **User Story:** us_043
- **Story Location:** .propel/context/tasks/EP-007-II/us_043/us_043.md
- **Acceptance Criteria:**
  - AC-001: `GET /patients/{id}/code-suggestions` performs pgvector similarity search over patient `chunk_embeddings` using the entity list as query context, calls Ollama RAG, returns up to 10 `{codeType, code, description, confidence, supportingChunkIds}` objects within 10 seconds
  - AC-002: ICD-10 codes validated against `^[A-Z][0-9]{2}(\.[0-9A-Z]{1,4})?$`; non-matching codes rejected and logged with `eventType = "InvalidCodeFormat"`
  - AC-003: CPT codes validated against `^\d{5}$`; non-matching codes rejected and logged with `eventType = "InvalidCodeFormat"`
  - AC-004: `supportingChunkIds` (enriched to `supportingChunks: [{chunkId, chunkText, sourceFilename}]`) included in each suggestion so the frontend can render the "View Evidence" panel without a second API call
  - AC-005: HTTP 403 for any user whose role is not `Clinician` or `Admin`
- **Edge Cases:**
  - Patient has fewer than 3 document chunks: return HTTP 200 `{"suggestions": [], "message": "Insufficient document data for code suggestions. Please upload clinical documents first."}` — not an empty array without explanation
  - Confidence < 0.3: include suggestion in response with `"lowConfidence": true` — do not drop it

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes — provides `GET /patients/{id}/code-suggestions` consumed by task_002 (SCR-015) |
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
| **AIR Requirements** | AIR-006 — RAG-based code suggestion: pgvector retrieves top-20 semantically relevant document chunks; Ollama Llama 3.1 8B generates ICD-10/CPT suggestions from retrieved context |
| **AI Pattern** | RAG (Retrieval-Augmented Generation) — query embedding via Ollama `/api/embeddings`; HNSW cosine similarity retrieval from `chunk_embeddings`; generation via Ollama `/api/generate` with chunk texts as context |
| **Prompt Template Path** | Inline in controller (no external file) — system prompt instructs Llama to return only a JSON array of code suggestions; includes top chunk texts + patient entity list |
| **Guardrails Config** | ICD-10/CPT regex validation; confidence threshold flag at < 0.3; max 10 suggestions returned; Ollama chunk ID cross-reference against similarity search result set (OWASP A01) |
| **Model Provider** | Ollama / Llama 3.1 8B (`OLLAMA_BASE_URL` env var; named IHttpClientFactory client) |

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
| Backend | ASP.NET Core Web API (C#) | .NET 8.0 | TR-005 — `CodeSuggestionsController` with `GET /patients/{id}/code-suggestions`; `[Authorize(Roles)]` RBAC; `[ResponseCache(NoStore = true)]` — clinical data must not be cached at any layer (AC-001, AC-005) |
| ORM | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | TR-006 — `db.Database.SqlQuery<ChunkRow>(FormattableString)` for parameterized pgvector query with JOIN; AsNoTracking for read-only path (AC-001, AC-004; OWASP A03) |
| Database | PostgreSQL 15.3+ with pgvector | 15.3+ | TR-007 — HNSW index `vector_cosine_ops` on `chunk_embeddings.embedding` (established in us_037) accelerates `<=>` cosine similarity ORDER BY; cosine similarity expression `embedding <=> $1::vector` (AC-001) |
| AI | Ollama + Llama 3.1 8B | latest stable | TR-009 — two synchronous Ollama calls per request: (1) `/api/embeddings` for query vector; (2) `/api/generate` for code suggestion; `stream: false`; named IHttpClientFactory client; `OLLAMA_BASE_URL` env var; 10-second SLA (AC-001; AIR-006) |
| HTTP | System.Net.Http (IHttpClientFactory) | .NET 8.0 built-in | TR-008 — named IHttpClientFactory client for Ollama API calls; CancellationToken from HttpContext request cancellation propagated to all async operations; timeout enforced via request pipeline (AC-001; OWASP A10) |
| Logging | Serilog + Seq | .NET 8.0 compatible / 2023.4+ | TR-011 — `eventType` and `codeType` values only in LogWarning; no invalid code values, chunk texts, entity values, or patient identifiers in any ILogger call (OWASP A02) |

---

## Task Overview

Implement `GET /patients/{id}/code-suggestions` in `CodeSuggestionsController`. The endpoint is a **synchronous** RAG pipeline (not a background worker): it loads patient entities, generates a query embedding via Ollama `/api/embeddings`, runs a pgvector cosine similarity search for top-20 relevant chunks, sends the retrieved chunk texts and entity context to Ollama `/api/generate`, validates each code suggestion against ICD-10/CPT regex patterns, flags low-confidence suggestions, validates Ollama-returned chunk IDs against the actual search results, and returns up to 10 enriched suggestions. The response includes `supportingChunks: [{chunkId, chunkText, sourceFilename}]` to enable the frontend "View Evidence" panel without a second API call.

---

## Dependent Tasks
- task_001 (us_037) — `chunk_embeddings` table and HNSW index must exist; Ollama embedding infrastructure and `OLLAMA_BASE_URL` env var must be configured

---

## Impacted Components
- `src/api/Controllers/CodeSuggestionsController.cs` — new: `GET /patients/{id}/code-suggestions`
- `src/api/Features/Codes/CodeSuggestionsResponseDto.cs` — new: response shape with `List<CodeSuggestionDto>` + `string? Message`
- `src/api/Features/Codes/CodeSuggestionDto.cs` — new: `{ string CodeType, string Code, string Description, double Confidence, bool LowConfidence, List<SupportingChunkDto> SupportingChunks }`
- `src/api/Features/Codes/SupportingChunkDto.cs` — new: `{ Guid ChunkId, string ChunkText, string SourceFilename }`
- `src/api/Features/Codes/ChunkRow.cs` — new: internal projection record for raw SQL result `{ Guid ChunkId, string ChunkText, string OriginalFilename }`

---

## Implementation Plan
1. `GET /patients/{id}/code-suggestions` endpoint in `CodeSuggestionsController`: `[Authorize(Roles = "Clinician,Admin")]` — 403 for Patient/Staff handled by middleware without reaching handler body; `[HttpGet("/patients/{id:guid}/code-suggestions")]`; `[ResponseCache(NoStore = true)]` — patient-specific clinical data must not be cached; chunk count guard: `int chunkCount = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) FROM chunk_embeddings ce JOIN document_chunks dc ON dc.id = ce.chunk_id JOIN document_records dr ON dr.id = dc.document_record_id WHERE dr.patient_id = {0}", patientId).FirstAsync(ct)` — if `chunkCount < 3` → return `Ok(new CodeSuggestionsResponseDto { Suggestions = [], Message = "Insufficient document data for code suggestions. Please upload clinical documents first." })` (AC-001, AC-005; Edge: < 3 chunks; OWASP A01)
2. Patient entity loading and query embedding: `var entities = await db.PatientEntities.Where(pe => pe.PatientId == patientId && !pe.IsDeleted).AsNoTracking().Select(pe => new { pe.Type, pe.Value }).ToListAsync(ct)` → if no entities → build a generic query string "clinical notes"; else concatenate `string.Join("\n", entities.Select(e => $"{e.Type}: {e.Value}"))` — note: entity `Value` is not logged anywhere (OWASP A02 — entity values are PHI); call Ollama `/api/embeddings` via named `IHttpClientFactory` client (`OLLAMA_BASE_URL`): `POST {baseUrl}/api/embeddings` with `{ "model": "llama3.1:8b", "prompt": queryString }`; validate `embedding.Length == 1536` — if Ollama returns non-200, connection refused, or dimension mismatch → return `StatusCode(503, new { message = "Code suggestion service is temporarily unavailable" })` (AC-001; OWASP A10 — CancellationToken propagated through all awaits)
3. pgvector similarity search: using query vector from item 2, execute parameterized raw SQL via `db.Database.SqlQuery<ChunkRow>($"SELECT ce.chunk_id AS \"ChunkId\", dc.chunk_text AS \"ChunkText\", dr.original_filename AS \"OriginalFilename\" FROM chunk_embeddings ce JOIN document_chunks dc ON dc.id = ce.chunk_id JOIN document_records dr ON dr.id = dc.document_record_id WHERE dr.patient_id = {patientId} ORDER BY ce.embedding <=> {queryVector}::vector LIMIT 20").ToListAsync(ct)` — FormattableString interpolation (OWASP A03 — parameterized); stores result as `List<ChunkRow>` for use in item 4 (Ollama prompt) and item 6 (chunk ID cross-reference); `ChunkText` from this result is never written to ILogger (OWASP A02 — chunk text may contain PHI) (AC-001, AC-004)
4. Ollama `/api/generate` RAG call: build system prompt — `"You are a clinical coding assistant. Suggest up to 10 ICD-10-CM or CPT codes supported by the following clinical text and patient context. Return ONLY a JSON array with no surrounding text or explanation. Format: [{\"codeType\":\"ICD10\",\"code\":\"...\",\"description\":\"...\",\"confidence\":0.0,\"supportingChunkIds\":[\"uuid1\"]}]"` — join top chunk texts (max 4,000 characters total, truncate oldest chunks if needed) + entity type-value pairs into the user content; `POST {baseUrl}/api/generate` with `{ "model": "llama3.1:8b", "prompt": fullPrompt, "stream": false }`; two-level JSON parse: (a) deserialize Ollama wrapper → extract `.response` string; (b) `JsonSerializer.Deserialize<List<RawSuggestionDto>>(responseString)` — if either parse fails with `JsonException` → catch, `_logger.LogWarning("OllamaCodeSuggestionParseFailed")` (no patient data in log), return `Ok(new CodeSuggestionsResponseDto { Suggestions = [], Message = "Unable to parse code suggestions at this time." })` (AC-001; AIR-006; OWASP A02 — prompt content and chunk texts never in ILogger)
5. Code format validation + confidence flagging: build `HashSet<Guid> validChunkIds` from the similarity search result (item 3); iterate `List<RawSuggestionDto>` from Ollama parse — for each dto: (a) if `dto.CodeType == "ICD10"` → validate `Regex.IsMatch(dto.Code, @"^[A-Z][0-9]{2}(\.[0-9A-Z]{1,4})?$", RegexOptions.None)` (compiled regex static field `_icd10Regex`); (b) if `dto.CodeType == "CPT"` → validate `Regex.IsMatch(dto.Code, @"^\d{5}$")` (static field `_cptRegex`); if validation fails → emit `_logger.LogWarning("{EventType} {CodeType}", "InvalidCodeFormat", dto.CodeType)` — only EventType string and CodeType string are logged, not the invalid code value (OWASP A02); skip to next; if valid → set `dto.LowConfidence = dto.Confidence < 0.3`; filter `dto.SupportingChunkIds` to only IDs in `validChunkIds` — prevents Ollama from hallucinating chunk IDs from other patients (OWASP A01; AC-002, AC-003; Edge: confidence < 0.3)
6. Response construction and return: from valid, non-rejected suggestions → `OrderByDescending(s => s.Confidence).Take(10)` → map to `CodeSuggestionDto` with `SupportingChunks` built by joining each filtered `supportingChunkId` against the item 3 `List<ChunkRow>` dictionary (keyed by `ChunkId`) → `SupportingChunkDto { ChunkId = chunkId, ChunkText = chunkRow.ChunkText, SourceFilename = chunkRow.OriginalFilename }` — enriches the AC-001 `supportingChunkIds` spec with text and filename needed for AC-004 "View Evidence" display; return `Ok(new CodeSuggestionsResponseDto { Suggestions = mappedList, Message = null })` (AC-001, AC-004)

---

## Current Project State
```
src/
└── api/
    ├── Controllers/
    │   └── (CodeSuggestionsController.cs      — CREATE)
    └── Features/
        └── Codes/
            ├── (CodeSuggestionsResponseDto.cs  — CREATE)
            ├── (CodeSuggestionDto.cs           — CREATE)
            ├── (SupportingChunkDto.cs          — CREATE)
            └── (ChunkRow.cs                    — CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | src/api/Controllers/CodeSuggestionsController.cs | GET /patients/{id}/code-suggestions with RBAC, RAG pipeline, validation |
| CREATE | src/api/Features/Codes/CodeSuggestionsResponseDto.cs | Response DTO: { List<CodeSuggestionDto> Suggestions, string? Message } |
| CREATE | src/api/Features/Codes/CodeSuggestionDto.cs | Suggestion DTO: { CodeType, Code, Description, Confidence, LowConfidence, SupportingChunks } |
| CREATE | src/api/Features/Codes/SupportingChunkDto.cs | Chunk DTO: { ChunkId, ChunkText, SourceFilename } |
| CREATE | src/api/Features/Codes/ChunkRow.cs | Internal projection for SqlQuery result: { ChunkId, ChunkText, OriginalFilename } |

---

## External References
- https://learn.microsoft.com/en-us/ef/core/querying/sql-queries (EF Core `SqlQuery<T>` with FormattableString — parameterized raw SQL for pgvector ORDER BY; OWASP A03)
- https://github.com/pgvector/pgvector#querying (pgvector cosine similarity `<=>` operator; HNSW index for fast `ORDER BY ... LIMIT` queries; established in us_037)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] Call `GET /patients/{id}/code-suggestions` as Clinician with a patient who has ≥ 3 embedded chunks; verify HTTP 200, response has up to 10 suggestions, each with `code`, `description`, `confidence`, and at least one `supportingChunk` with `chunkText` and `sourceFilename` (AC-001, AC-004)
- [ ] Call as a user with role `Staff`; verify HTTP 403 response (AC-005)
- [ ] Call with a patient who has fewer than 3 embedded chunks; verify HTTP 200 with `suggestions: []` and `message: "Insufficient document data..."` — not an empty array without message (Edge: < 3 chunks)
- [ ] Simulate Ollama returning an ICD-10 code `"ZZZZ"` and a CPT code `"1234"` (both invalid formats); verify both are absent from the response and `"InvalidCodeFormat"` is logged exactly twice; verify valid suggestions in the same response are still returned (AC-002, AC-003)
- [ ] Verify a suggestion with confidence `0.25` appears in the response with `lowConfidence: true`; verify a suggestion with confidence `0.31` appears with `lowConfidence: false` (Edge: confidence < 0.3)
- [ ] Verify Serilog Seq output contains no patient data, chunk text, entity values, or code values — only `EventType` and `CodeType` appear in any LogWarning call (OWASP A02)
- [ ] Simulate Ollama returning a `supportingChunkIds` entry that is a valid UUID but does not appear in the similarity search results; verify that chunk ID is excluded from the response's `supportingChunks` array (OWASP A01 — hallucinated chunk ID cross-reference)

---

## Implementation Checklist
- [x] The chunk count guard (item 1) is executed BEFORE any Ollama call — if count < 3 the handler returns immediately without making a network call to Ollama; this prevents unnecessary I/O on the hot path and respects the 10-second SLA (OWASP A04 — early exit)
- [x] The `queryString` built from entity values (item 2) is passed only to the Ollama API call body; it is NEVER passed to any `_logger` method; entity values are PHI (OWASP A02 — entity values logged at zero places)
- [x] Two static readonly `Regex` fields (`IcD10Regex`, `CptRegex`) are used for code validation (item 5) — `RegexOptions.Compiled` for performance; these are pre-compiled at class load time, not re-compiled per request (performance; code anti-patterns — no magic string re-use)
- [x] Ollama-returned `supportingChunkIds` are cross-referenced against the `HashSet<Guid>` built from the actual similarity search results (item 5) — any ID not in this set is silently filtered out; this prevents a compromised or hallucinatory Ollama response from leaking chunk IDs or texts from other patients (OWASP A01)
- [x] `[ResponseCache(Duration = 0, NoStore = true)]` is present on the controller — code suggestions are patient-specific clinical data and must not be cached by any intermediary proxy, CDN, or browser (OWASP A02 — sensitive data exposure)
- [x] The `patientId` route parameter is typed as `int` (`{id:int}` constraint) — task spec said `Guid` but `patients.id` is an `int` PK (consistent with all other patient endpoints); non-integer values are rejected at routing level before reaching the handler (OWASP A03 — input type safety)
