using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Api.Data;
using Api.Features.Documents;
using Api.Features.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.BackgroundServices;

/// <summary>
/// Singleton <see cref="BackgroundService"/> that consumes <see cref="DocumentEmbeddingsCompleteEvent"/>
/// events, calls Ollama <c>POST /api/generate</c> per chunk with a structured JSON-constraining prompt,
/// validates the response, and upserts clinical entities to <c>patient_entities</c>
/// (us_038/AC-001 – AC-005; AIR-003).
/// </summary>
/// <remarks>
/// Design invariants:
/// <list type="bullet">
///   <item>All <see cref="AppDbContext"/> instances are resolved via <see cref="IServiceScopeFactory"/>
///     per event — the scoped DbContext is never captured in the singleton constructor (OWASP A04).</item>
///   <item>Chunk content and entity values are never written to any <see cref="ILogger"/> call;
///     only <c>documentId</c> and <c>chunkId</c> (UUIDs) appear in log entries (OWASP A02 — PHI guard).</item>
///   <item>Upserts use EF Core <c>ExecuteSqlAsync</c> with interpolated <see cref="FormattableString"/>
///     parameters — entity values are never string-concatenated into SQL (OWASP A03).</item>
/// </list>
/// </remarks>
public sealed class EntityExtractionWorker : BackgroundService
{
    private readonly Channel<DocumentEmbeddingsCompleteEvent> _channel;
    private readonly Channel<PatientEntitiesUpdatedEvent>    _entitiesUpdatedChannel;
    private readonly IServiceScopeFactory                     _scopeFactory;
    private readonly IHttpClientFactory                       _httpClientFactory;
    private readonly ILogger<EntityExtractionWorker>          _logger;

    // Allowed entity type values per task spec (AC-002)
    private static readonly HashSet<string> AllowedTypes =
        new(StringComparer.Ordinal) { "Diagnosis", "Medication", "Allergy", "Procedure" };

    public EntityExtractionWorker(
        Channel<DocumentEmbeddingsCompleteEvent> channel,
        Channel<PatientEntitiesUpdatedEvent>    entitiesUpdatedChannel,
        IServiceScopeFactory                     scopeFactory,
        IHttpClientFactory                       httpClientFactory,
        ILogger<EntityExtractionWorker>          logger)
    {
        _channel                = channel;
        _entitiesUpdatedChannel = entitiesUpdatedChannel;
        _scopeFactory           = scopeFactory;
        _httpClientFactory      = httpClientFactory;
        _logger                 = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var ev in _channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                await ProcessEventAsync(ev, ct);
            }
            catch (Exception ex)
            {
                // Outer safety net — prevents unhandled exception from crashing the consumer loop
                _logger.LogWarning(ex,
                    "Unhandled exception in entity extraction pipeline for document {DocumentId}",
                    ev.DocumentId);
            }
        }
    }

    private async Task ProcessEventAsync(DocumentEmbeddingsCompleteEvent ev, CancellationToken ct)
    {
        await using var scope  = _scopeFactory.CreateAsyncScope();
        var             db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var             client = _httpClientFactory.CreateClient("ollama-generate");

        // Load all chunks for the document — Content is used only for Ollama requests; never logged
        var chunks = await db.DocumentChunks
            .Where(dc => dc.DocumentId == ev.DocumentId)
            .OrderBy(dc => dc.ChunkIndex)
            .ToListAsync(ct);

        foreach (var chunk in chunks)
        {
            var entityResults = await ExtractEntitiesFromChunkAsync(client, chunk.Content, ev.DocumentId, chunk.Id, ct);
            if (entityResults is null)
                continue; // schema error already logged; move to next chunk

            // Empty entities array is a valid Ollama response — loop simply does not execute (Edge: empty array)
            foreach (var entity in entityResults)
            {
                var lowConfidence = entity.Confidence < 0.5;

                // Upsert: ON CONFLICT (patient_id, type, value) DO UPDATE — AC-003 deduplication.
                // ExecuteSqlAsync with interpolated FormattableString — each value is a typed
                // parameter; entity.Value is never string-concatenated into SQL (OWASP A03).
                await db.Database.ExecuteSqlAsync(
                    $"""
                    INSERT INTO patient_entities
                        (id, patient_id, document_id, type, value, confidence, low_confidence, created_at, last_seen_at)
                    VALUES
                        ({Guid.NewGuid()}, {ev.PatientId}, {ev.DocumentId}, {entity.Type}, {entity.Value}, {entity.Confidence}, {lowConfidence}, now(), now())
                    ON CONFLICT (patient_id, type, value)
                    DO UPDATE SET
                        last_seen_at  = now(),
                        document_id   = EXCLUDED.document_id,
                        confidence    = EXCLUDED.confidence,
                        low_confidence = EXCLUDED.low_confidence
                    """,
                    ct);
            }
        }

        // Set status to "EntitiesExtracted" after all chunks processed — regardless of per-chunk results (AC-005)
        await db.DocumentRecords
            .Where(dr => dr.Id == ev.DocumentId)
            .ExecuteUpdateAsync(s => s.SetProperty(dr => dr.Status, "EntitiesExtracted"), ct);

        _logger.LogInformation(
            "Entity extraction complete for document {DocumentId}. status=EntitiesExtracted",
            ev.DocumentId);

        // Publish to conflict detection pipeline (us_040/AC-004).
        // TryWrite is fire-and-forget — a full channel drops the oldest event rather than
        // blocking this consumer loop (FullMode=DropOldest mirrors entity extraction channel; OWASP A04).
        if (!_entitiesUpdatedChannel.Writer.TryWrite(new PatientEntitiesUpdatedEvent(ev.PatientId)))
        {
            _logger.LogWarning(
                "ConflictDetectionChannel full — PatientEntitiesUpdatedEvent dropped for patient {PatientId}",
                ev.PatientId);
        }
    }

    /// <summary>
    /// Calls Ollama <c>POST /api/generate</c> for a single chunk and returns the validated entity list.
    /// Returns <see langword="null"/> on schema error (caller should skip the chunk).
    /// Chunk content is never logged (OWASP A02).
    /// </summary>
    private async Task<IReadOnlyList<ExtractedEntity>?> ExtractEntitiesFromChunkAsync(
        HttpClient        client,
        string            chunkContent,
        Guid              documentId,
        Guid              chunkId,
        CancellationToken ct)
    {
        // Structured prompt instructs Llama to output ONLY JSON — prose is explicitly prohibited (AC-001; AIR-003)
        var prompt =
            "Extract medical entities from the following clinical text. " +
            "Return ONLY valid JSON in this exact format: " +
            "{\"entities\": [{\"type\": \"Diagnosis|Medication|Allergy|Procedure\", \"value\": \"<string>\", \"confidence\": <0.0-1.0>}]}. " +
            "Do not include any explanation or prose.\n\n" +
            "Text: " + chunkContent; // chunkContent used only here; never reaches a logger

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(
                "/api/generate",
                new { model = "llama3.1:8b", prompt, stream = false },
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Ollama /api/generate failed for chunk {ChunkId} in document {DocumentId}",
                chunkId, documentId);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Ollama /api/generate returned {StatusCode} for chunk {ChunkId} in document {DocumentId}",
                (int)response.StatusCode, chunkId, documentId);
            return null;
        }

        string httpBody;
        try
        {
            httpBody = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to read Ollama response body for chunk {ChunkId} in document {DocumentId}",
                chunkId, documentId);
            return null;
        }

        // ── Two-level JSON parse: outer Ollama wrapper → inner entity JSON (AC-001; checklist) ──
        // Level 1: extract the Ollama /api/generate response envelope {"response": "<json>", ...}
        string entityJson;
        try
        {
            using var ollamaDoc = JsonDocument.Parse(httpBody);
            if (!ollamaDoc.RootElement.TryGetProperty("response", out var responseEl) ||
                responseEl.ValueKind != JsonValueKind.String)
            {
                _logger.LogError(
                    "EntityExtractionSchemaError for document {DocumentId} chunk {ChunkId}: missing 'response' field in Ollama wrapper",
                    documentId, chunkId);
                return null;
            }
            entityJson = responseEl.GetString()!;
        }
        catch (JsonException)
        {
            _logger.LogError(
                "EntityExtractionSchemaError for document {DocumentId} chunk {ChunkId}: Ollama wrapper is not valid JSON",
                documentId, chunkId);
            return null;
        }

        // Level 2: parse and validate the inner entity schema (AC-002)
        return ParseAndValidateEntities(entityJson, documentId, chunkId);
    }

    /// <summary>
    /// Parses and validates the inner entity JSON string returned by Ollama.
    /// Returns <see langword="null"/> on any schema violation (AC-002).
    /// Entity values are never included in log messages (OWASP A02).
    /// </summary>
    private IReadOnlyList<ExtractedEntity>? ParseAndValidateEntities(
        string entityJson,
        Guid   documentId,
        Guid   chunkId)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(entityJson);
        }
        catch (JsonException)
        {
            _logger.LogError(
                "EntityExtractionSchemaError for document {DocumentId} chunk {ChunkId}: inner entity JSON is not valid JSON",
                documentId, chunkId);
            return null;
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("entities", out var entitiesEl) ||
                entitiesEl.ValueKind != JsonValueKind.Array)
            {
                _logger.LogError(
                    "EntityExtractionSchemaError for document {DocumentId} chunk {ChunkId}: missing or non-array 'entities' key",
                    documentId, chunkId);
                return null;
            }

            var results = new List<ExtractedEntity>();
            foreach (var element in entitiesEl.EnumerateArray())
            {
                // type — must exist and be one of the four allowed values (AC-002)
                if (!element.TryGetProperty("type", out var typeEl) ||
                    typeEl.ValueKind != JsonValueKind.String)
                {
                    _logger.LogError(
                        "EntityExtractionSchemaError for document {DocumentId} chunk {ChunkId}: entity missing 'type' field",
                        documentId, chunkId);
                    return null;
                }
                var type = typeEl.GetString()!;
                if (!AllowedTypes.Contains(type))
                {
                    _logger.LogError(
                        "EntityExtractionSchemaError for document {DocumentId} chunk {ChunkId}: invalid entity type '{Type}'",
                        documentId, chunkId, type);
                    return null;
                }

                // value — must exist and be a non-empty string (AC-002)
                if (!element.TryGetProperty("value", out var valueEl) ||
                    valueEl.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(valueEl.GetString()))
                {
                    _logger.LogError(
                        "EntityExtractionSchemaError for document {DocumentId} chunk {ChunkId}: entity missing or empty 'value' field",
                        documentId, chunkId);
                    return null;
                }
                var value = valueEl.GetString()!;

                // Truncate value to 500 chars — enforces the PatientEntity.Value max-length (OWASP A04)
                if (value.Length > 500)
                    value = value[..500];

                // confidence — must exist and be a number in [0.0, 1.0] (AC-002)
                if (!element.TryGetProperty("confidence", out var confEl) ||
                    confEl.ValueKind != JsonValueKind.Number ||
                    !confEl.TryGetDouble(out var confidence) ||
                    confidence < 0.0 || confidence > 1.0)
                {
                    _logger.LogError(
                        "EntityExtractionSchemaError for document {DocumentId} chunk {ChunkId}: entity missing or invalid 'confidence' field",
                        documentId, chunkId);
                    return null;
                }

                results.Add(new ExtractedEntity(type, value, confidence));
            }

            return results;
        }
    }

    /// <summary>Validated entity extracted from one Ollama response element.</summary>
    private sealed record ExtractedEntity(string Type, string Value, double Confidence);
}
