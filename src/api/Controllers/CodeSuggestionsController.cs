using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Api.Audit;
using Api.Constants;
using Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Codes;

/// <summary>
/// Exposes AI-assisted medical code suggestions via a RAG pipeline (us_043/AC-001–AC-005).
///
/// <para>
/// Access is restricted to <c>Clinician</c> and <c>Admin</c> roles only (AC-005; OWASP A01).
/// <c>Staff</c> and <c>Patient</c> roles receive HTTP 403 without reaching the handler body.
/// </para>
///
/// <para>
/// The RAG pipeline is synchronous within this request:
/// (1) Chunk count guard → early exit when &lt; 3 embedded chunks (OWASP A04).
/// (2) Entity query string → Ollama <c>/api/embeddings</c> → query vector.
/// (3) pgvector cosine similarity search → top-20 <c>document_chunk_embeddings</c> rows.
/// (4) Ollama <c>/api/generate</c> with chunk context → raw suggestion list.
/// (5) ICD-10/CPT regex validation; confidence flagging; chunk ID cross-reference.
/// (6) Return up to 10 suggestions ordered by confidence desc.
/// </para>
///
/// <para>
/// OWASP A02: entity values, chunk texts, and invalid code values are NEVER written to
/// any <see cref="ILogger"/> or Serilog sink.  Only structural IDs and event-type strings appear
/// in log entries (HIPAA minimum-necessary §164.312(b); AC-002, AC-003).
/// </para>
/// </summary>
[ApiController]
[Route("patients")]
[Authorize(Roles = $"{Roles.Clinician},{Roles.Admin}")]
[ResponseCache(Duration = 0, NoStore = true)]
public sealed class CodeSuggestionsController : ControllerBase
{
    private const string ModelName = "llama3.1:8b";

    // ── Compiled regex constants (checklist item 3 — pre-compiled at class load, not per request) ──
    private static readonly Regex IcD10Regex = new(
        @"^[A-Z][0-9]{2}(\.[0-9A-Z]{1,4})?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        matchTimeout: TimeSpan.FromMilliseconds(100));

    private static readonly Regex CptRegex = new(
        @"^\d{5}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        matchTimeout: TimeSpan.FromMilliseconds(100));

    // System prompt for Ollama /api/generate RAG call (AIR-006)
    private const string SystemPrompt =
        "You are a clinical coding assistant. " +
        "Suggest up to 10 ICD-10-CM or CPT codes supported by the following clinical text and patient context. " +
        "Return ONLY a JSON array with no surrounding text, markdown fencing, or explanation. " +
        "Format: [{\"codeType\":\"ICD10\",\"code\":\"...\",\"description\":\"...\",\"confidence\":0.0,\"supportingChunkIds\":[\"uuid1\"]}]";

    private readonly AppDbContext                           _db;
    private readonly IHttpClientFactory                     _httpClientFactory;
    private readonly IAuditLogger                           _auditLogger;
    private readonly ILogger<CodeSuggestionsController>     _logger;

    public CodeSuggestionsController(
        AppDbContext                        db,
        IHttpClientFactory                  httpClientFactory,
        IAuditLogger                        auditLogger,
        ILogger<CodeSuggestionsController>  logger)
    {
        _db                = db;
        _httpClientFactory = httpClientFactory;
        _auditLogger       = auditLogger;
        _logger            = logger;
    }

    /// <summary>
    /// Returns up to 10 ICD-10/CPT code suggestions for the specified patient,
    /// enriched with supporting document chunk evidence.
    /// </summary>
    /// <param name="id">Patient integer primary key.</param>
    /// <param name="ct">Request cancellation token — propagated through all Ollama calls (OWASP A10).</param>
    [HttpGet("{id:int}/code-suggestions")]
    public async Task<IActionResult> GetCodeSuggestionsAsync(int id, CancellationToken ct)
    {
        // OWASP A01: [Authorize(Roles)] is primary gate; no additional ownership check needed
        // — Clinicians access any patient's record by design (documented in ClinicalConflictsController).

        var patientId = id;

        // ── Step 1: Chunk count guard — early exit before any Ollama I/O (OWASP A04; checklist) ──
        // Uses the document_chunk_embeddings table (us_037) which is distinct from
        // the legacy chunk_embeddings table (us_006; see AppDbContext.OnModelCreating comment).
        var chunkCount = await _db.DocumentChunkEmbeddings
            .Join(_db.DocumentChunks,
                ce => ce.ChunkId,
                dc => dc.Id,
                (ce, dc) => new { ce, dc })
            .Join(_db.DocumentRecords,
                x  => x.dc.DocumentId,
                dr => dr.Id,
                (x, dr) => dr.PatientId)
            .CountAsync(pid => pid == patientId, ct);

        if (chunkCount < 3)
        {
            return Ok(new CodeSuggestionsResponseDto
            {
                Suggestions = new List<CodeSuggestionDto>(),
                Message     = "Insufficient document data for code suggestions. Please upload clinical documents first.",
            });
        }

        // ── Step 2: Build query string from patient entities (OWASP A02 — values never logged) ──
        var entities = await _db.PatientEntities
            .Where(pe => pe.PatientId == patientId)
            .AsNoTracking()
            .Select(pe => new { pe.Type, pe.Value })
            .ToListAsync(ct);

        // queryString is PHI — never passed to any _logger call (OWASP A02; checklist item 2)
        var queryString = entities.Count > 0
            ? string.Join("\n", entities.Select(e => $"{e.Type}: {e.Value}"))
            : "clinical notes";

        // ── Step 2b: Ollama /api/embeddings — generate query vector ──────────────────────────────
        var ollamaClient = _httpClientFactory.CreateClient("ollama-suggestions");

        float[]? queryVector;
        try
        {
            var embedResponse = await ollamaClient.PostAsJsonAsync(
                "/api/embeddings",
                new { model = ModelName, input = queryString },
                ct);

            if (!embedResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OllamaEmbeddingFailed StatusCode={StatusCode}", (int)embedResponse.StatusCode);
                return StatusCode(503, new { message = "Code suggestion service is temporarily unavailable." });
            }

            var embedBody = await embedResponse.Content
                .ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: ct);

            queryVector = embedBody?.Embedding;

            if (queryVector is null || queryVector.Length != 1536)
            {
                _logger.LogWarning(
                    "OllamaEmbeddingDimensionMismatch ExpectedDimensions=1536 ActualDimensions={Actual}",
                    queryVector?.Length ?? 0);
                return StatusCode(503, new { message = "Code suggestion service is temporarily unavailable." });
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("OllamaEmbeddingConnectionFailed: {ExceptionType}", ex.GetType().Name);
            return StatusCode(503, new { message = "Code suggestion service is temporarily unavailable." });
        }

        // ── Step 3: pgvector cosine similarity search — top-20 chunks (OWASP A03 — parameterized) ──
        // Column aliases match ChunkRow property names exactly (EF Core SqlQuery mapping by alias).
        // Table: document_chunk_embeddings (us_037) — NOT legacy chunk_embeddings (us_006).
        // Column: dc.content (DocumentChunk.Content with snake_case → "content") aliased as "ChunkText".
        // OWASP A02: chunk texts (ChunkText column) are never written to any ILogger.
        var vectorLiteral = $"[{string.Join(",", queryVector)}]";

        List<ChunkRow> chunks;
        try
        {
            chunks = await _db.Database
                .SqlQuery<ChunkRow>(
                    $@"SELECT ce.chunk_id        AS ""ChunkId"",
                              dc.content          AS ""ChunkText"",
                              dr.original_filename AS ""OriginalFilename""
                       FROM   document_chunk_embeddings ce
                       JOIN   document_chunks           dc ON dc.id         = ce.chunk_id
                       JOIN   document_records          dr ON dr.id         = dc.document_id
                       WHERE  dr.patient_id = {patientId}
                       ORDER  BY ce.embedding <=> {vectorLiteral}::vector
                       LIMIT  20")
                .ToListAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError("VectorSearchFailed PatientId={PatientId} ExceptionType={ExceptionType}",
                patientId, ex.GetType().Name);
            return StatusCode(503, new { message = "Code suggestion service is temporarily unavailable." });
        }

        if (chunks.Count == 0)
        {
            return Ok(new CodeSuggestionsResponseDto
            {
                Suggestions = new List<CodeSuggestionDto>(),
                Message     = "Insufficient document data for code suggestions. Please upload clinical documents first.",
            });
        }

        // ── Step 4: Ollama /api/generate RAG call ─────────────────────────────────────────────────
        // Entity values and chunk texts are PHI — included in the Ollama prompt body but NEVER logged.
        var chunkContext = BuildChunkContext(chunks);
        var entityContext = entities.Count > 0
            ? "\n\nPatient entity context:\n" + string.Join("\n", entities.Select(e => $"- {e.Type}: {e.Value}"))
            : string.Empty;

        var fullPrompt = $"{SystemPrompt}\n\nClinical document excerpts:\n{chunkContext}{entityContext}";

        List<RawSuggestionDto> rawSuggestions;
        try
        {
            var generateResponse = await ollamaClient.PostAsJsonAsync(
                "/api/generate",
                new { model = ModelName, prompt = fullPrompt, stream = false },
                ct);

            if (!generateResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OllamaGenerateFailed StatusCode={StatusCode}", (int)generateResponse.StatusCode);
                return Ok(new CodeSuggestionsResponseDto
                {
                    Suggestions = new List<CodeSuggestionDto>(),
                    Message     = "Unable to parse code suggestions at this time.",
                });
            }

            // Two-level JSON parse: (a) Ollama wrapper → .response string; (b) suggestion array
            var generateBody = await generateResponse.Content
                .ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: ct);

            var responseText = generateBody?.Response?.Trim() ?? string.Empty;

            rawSuggestions = JsonSerializer.Deserialize<List<RawSuggestionDto>>(responseText)
                             ?? new List<RawSuggestionDto>();
        }
        catch (JsonException)
        {
            // OllamaCodeSuggestionParseFailed — no patient data in log (OWASP A02; checklist)
            _logger.LogWarning("OllamaCodeSuggestionParseFailed");
            return Ok(new CodeSuggestionsResponseDto
            {
                Suggestions = new List<CodeSuggestionDto>(),
                Message     = "Unable to parse code suggestions at this time.",
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("OllamaGenerateConnectionFailed: {ExceptionType}", ex.GetType().Name);
            return Ok(new CodeSuggestionsResponseDto
            {
                Suggestions = new List<CodeSuggestionDto>(),
                Message     = "Unable to parse code suggestions at this time.",
            });
        }

        // ── Step 5: Code validation, confidence flagging, chunk ID cross-reference ───────────────
        // validChunkIds: cross-reference set — prevents hallucinated IDs from leaking PHI (OWASP A01)
        var validChunkIds = new HashSet<Guid>(chunks.Select(c => c.ChunkId));
        var chunkLookup   = chunks.ToDictionary(c => c.ChunkId);

        var validated = new List<CodeSuggestionDto>();

        foreach (var raw in rawSuggestions)
        {
            if (string.IsNullOrWhiteSpace(raw.Code) || string.IsNullOrWhiteSpace(raw.CodeType))
                continue;

            bool formatValid = raw.CodeType switch
            {
                "ICD10" => IcD10Regex.IsMatch(raw.Code),
                "CPT"   => CptRegex.IsMatch(raw.Code),
                _       => false,
            };

            if (!formatValid)
            {
                // InvalidCodeFormat log — only EventType and CodeType; never the invalid code value (OWASP A02; AC-002, AC-003)
                _logger.LogWarning("{EventType} {CodeType}", "InvalidCodeFormat", raw.CodeType);
                continue;
            }

            // Cross-reference supporting chunk IDs against actual search results (OWASP A01; checklist item 4)
            var filteredChunkIds = (raw.SupportingChunkIds ?? Enumerable.Empty<string>())
                .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
                .Where(g => g.HasValue && validChunkIds.Contains(g!.Value))
                .Select(g => g!.Value)
                .ToList();

            validated.Add(new CodeSuggestionDto
            {
                CodeType    = raw.CodeType,
                Code        = raw.Code,
                Description = raw.Description ?? string.Empty,
                Confidence  = raw.Confidence,
                LowConfidence = raw.Confidence < 0.3,   // Edge: confidence < 0.3 (AC-004)
                SupportingChunks = filteredChunkIds
                    .Where(id => chunkLookup.ContainsKey(id))
                    .Select(id => new SupportingChunkDto
                    {
                        ChunkId        = id,
                        ChunkText      = chunkLookup[id].ChunkText,
                        SourceFilename = chunkLookup[id].OriginalFilename,
                    })
                    .ToList(),
            });
        }

        // ── Step 6: Sort by confidence desc, return up to 10 (AC-001) ───────────────────────────
        var result = validated
            .OrderByDescending(s => s.Confidence)
            .Take(10)
            .ToList();

        // ── Step 7: Upsert suggestions to code_suggestions for stable IDs (AC-002) ────────────────
        // ON CONFLICT (patient_id, code_type, code) DO UPDATE SET id = code_suggestions.id is a
        // no-op that returns the existing row — idempotent across repeated GET calls.
        // OWASP A03: FormattableString parameters are passed as DbParameters, never interpolated.
        foreach (var s in result)
        {
            await _db.Database.ExecuteSqlAsync(
                $@"INSERT INTO code_suggestions (patient_id, code_type, code, description, confidence, low_confidence)
                   VALUES ({patientId}, {s.CodeType}, {s.Code}, {s.Description}, {s.Confidence}, {s.LowConfidence})
                   ON CONFLICT (patient_id, code_type, code) DO UPDATE SET id = code_suggestions.id", ct);
        }

        // Fetch the persisted rows to read back stable IDs and review statuses
        var persistedRows = await _db.CodeSuggestions
            .AsNoTracking()
            .Where(cs => cs.PatientId == patientId)
            .Select(cs => new { cs.Id, cs.CodeType, cs.Code, cs.ReviewStatus })
            .ToListAsync(ct);

        var persistedLookup = persistedRows
            .ToDictionary(cs => (cs.CodeType, cs.Code));

        foreach (var s in result)
        {
            if (persistedLookup.TryGetValue((s.CodeType, s.Code), out var row))
            {
                s.Id           = row.Id;
                s.ReviewStatus = row.ReviewStatus;
            }
        }

        return Ok(new CodeSuggestionsResponseDto { Suggestions = result });
    }

    /// <summary>
    /// Rejects a pending code suggestion.<br/>
    ///
    /// <para>
    /// Returns HTTP 200 on success.
    /// Returns HTTP 404 when the suggestion does not exist.
    /// Returns HTTP 409 when the suggestion has already been reviewed.
    /// </para>
    ///
    /// <para>
    /// OWASP A04 TOCTOU guard: <c>ExecuteUpdateAsync</c> includes
    /// <c>WHERE review_status = 'Pending'</c>; zero rows updated signals a concurrent
    /// review and returns 409.
    /// </para>
    /// </summary>
    [HttpPatch("/code-suggestions/{id:guid}")]
    public async Task<IActionResult> RejectSuggestionAsync(Guid id, CancellationToken ct)
    {
        // ── 1. Extract actor identity from JWT (OWASP A01 — never from request body) ──────────
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? "unknown";
        var actorRole = User.FindFirstValue(ClaimTypes.Role)
                     ?? User.FindFirstValue("role")
                     ?? string.Empty;

        // ── 2. Load suggestion — 404 if missing (AC-002) ──────────────────────────────────────
        var suggestion = await _db.CodeSuggestions
            .AsNoTracking()
            .FirstOrDefaultAsync(cs => cs.Id == id, ct);

        if (suggestion is null)
            return NotFound(new { error = $"Code suggestion {id} not found." });

        // ── 3. Optimistic status check (fast path before ExecuteUpdateAsync) ─────────────────
        if (suggestion.ReviewStatus != "Pending")
            return Conflict(new { error = "This code suggestion has already been reviewed." });

        // ── 4. Atomic TOCTOU guard: WHERE id=? AND review_status='Pending' (OWASP A04) ───────
        int? reviewedBy = int.TryParse(actorId, out var parsedId) ? parsedId : null;

        var updated = await _db.CodeSuggestions
            .Where(cs => cs.Id == id && cs.ReviewStatus == "Pending")
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(cs => cs.ReviewStatus, "Rejected")
                    .SetProperty(cs => cs.ReviewedBy,   reviewedBy)
                    .SetProperty(cs => cs.ReviewedAt,   DateTimeOffset.UtcNow),
                ct);

        if (updated == 0)
            return Conflict(new { error = "This code suggestion has already been reviewed." });

        // ── 5. Audit — suggestion ID and action type only; no PHI (OWASP A02) ─────────────────
        await _auditLogger.RecordAsync(new AuditEntry(
            ActorId:      actorId,
            ActorRole:    actorRole,
            ActionType:   AuditActionTypes.MedicalCodeRejected,
            ResourceType: "CodeSuggestion",
            ResourceId:   id.ToString(),
            IpAddress:    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent:    Request.Headers.UserAgent.ToString(),
            OccurredAt:   DateTime.UtcNow), ct);

        return Ok();
    }

    // ── Private helpers ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the chunk context string for the Ollama prompt — max 4,000 characters total.
    /// Oldest (lowest-similarity) chunks are truncated first.
    /// OWASP A02: result is used in Ollama prompt body only — never logged.
    /// </summary>
    private static string BuildChunkContext(List<ChunkRow> chunks)
    {
        const int MaxContextChars = 4000;
        var sb = new StringBuilder(MaxContextChars + 200);

        foreach (var chunk in chunks)
        {
            var line = $"[{chunk.OriginalFilename}] {chunk.ChunkText}\n";
            if (sb.Length + line.Length > MaxContextChars) break;
            sb.Append(line);
        }

        return sb.ToString();
    }

    // ── Ollama API response types ─────────────────────────────────────────────────────────────────

    private sealed record OllamaEmbeddingResponse(
        [property: JsonPropertyName("embedding")] float[] Embedding);

    private sealed record OllamaGenerateResponse(
        [property: JsonPropertyName("response")] string? Response);

    /// <summary>
    /// Internal raw deserialization type for one Ollama-returned code suggestion.
    /// Not exposed in the API response — mapped to <see cref="CodeSuggestionDto"/> after validation.
    /// </summary>
    private sealed class RawSuggestionDto
    {
        [JsonPropertyName("codeType")]
        public string? CodeType { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("supportingChunkIds")]
        public List<string>? SupportingChunkIds { get; set; }
    }
}
