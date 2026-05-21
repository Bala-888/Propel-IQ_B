using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Api.Data;
using Api.Features.Documents;
using Api.Features.Embeddings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace Api.BackgroundServices;

/// <summary>
/// Singleton <see cref="BackgroundService"/> that consumes <see cref="DocumentChunkedEvent"/>
/// events published by <see cref="DocumentTextExtractionWorker"/> after the chunking transaction
/// commits, and generates + persists pgvector embeddings for each <c>document_chunks</c> row
/// (us_037/AC-001 – AC-004; AIR-001, AIR-004).
/// </summary>
/// <remarks>
/// Design invariants:
/// <list type="bullet">
///   <item>All <see cref="AppDbContext"/> instances are resolved via <see cref="IServiceScopeFactory"/>
///     per event — the scoped DbContext is never captured in the singleton constructor (OWASP A04).</item>
///   <item><see cref="IHttpClientFactory"/> is singleton-safe and injected directly.</item>
///   <item><c>chunk.Content</c> is sent to Ollama but never written to any logger;
///     only <c>chunkId</c> (UUID) appears in log entries (AC-001; OWASP A02 — PHI guard).</item>
/// </list>
/// </remarks>
public sealed class EmbeddingWorker : BackgroundService
{
    private readonly Channel<DocumentChunkedEvent>            _channel;
    private readonly Channel<DocumentEmbeddingsCompleteEvent> _embeddingsCompleteChannel;
    private readonly IServiceScopeFactory                     _scopeFactory;
    private readonly IHttpClientFactory                       _httpClientFactory;
    private readonly ILogger<EmbeddingWorker>                 _logger;

    public EmbeddingWorker(
        Channel<DocumentChunkedEvent>            channel,
        Channel<DocumentEmbeddingsCompleteEvent> embeddingsCompleteChannel,
        IServiceScopeFactory                     scopeFactory,
        IHttpClientFactory                       httpClientFactory,
        ILogger<EmbeddingWorker>                 logger)
    {
        _channel                   = channel;
        _embeddingsCompleteChannel = embeddingsCompleteChannel;
        _scopeFactory              = scopeFactory;
        _httpClientFactory         = httpClientFactory;
        _logger                    = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var ev in _channel.Reader.ReadAllAsync(ct))
        {
            await ProcessEventAsync(ev, ct);
        }
    }

    /// <summary>
    /// Full per-event processing pipeline: load chunks → call Ollama → validate → insert embeddings.
    /// Ollama connection-refused errors re-enqueue the event with a 5-minute back-off.
    /// Per-chunk 5xx errors trigger one 10-second retry; on second failure the chunk is skipped.
    /// </summary>
    private async Task ProcessEventAsync(DocumentChunkedEvent ev, CancellationToken ct)
    {
        await using var scope  = _scopeFactory.CreateAsyncScope();
        var             db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var             client = _httpClientFactory.CreateClient("ollama-embed");

        try
        {
            foreach (var chunkId in ev.ChunkIds)
            {
                // ── Load chunk — only Content is used; never logged (AC-001; OWASP A02) ─────────
                var chunk = await db.DocumentChunks.FindAsync(new object[] { chunkId }, ct);
                if (chunk is null)
                {
                    _logger.LogWarning(
                        "Chunk {ChunkId} not found in document_chunks; skipping embedding.", chunkId);
                    continue;
                }

                // ── Per-chunk Ollama call with one retry on 5xx (AC-004) ──────────────────────
                // attempt 0 = first call; attempt 1 = single retry after 10s delay.
                float[]? vector = null;
                int attempt = 0;
                while (attempt < 2)
                {
                    if (attempt > 0)
                        await Task.Delay(TimeSpan.FromSeconds(10), ct);

                    var response = await client.PostAsJsonAsync(
                        "/api/embeddings",
                        new { model = "llama3.1:8b", input = chunk.Content },
                        ct);

                    attempt++;

                    if ((int)response.StatusCode >= 500)
                    {
                        if (attempt >= 2)
                        {
                            // Second attempt also returned 5xx — log error and skip (AC-004)
                            // Only chunkId (UUID) in log — never chunk.Content (OWASP A02)
                            _logger.LogError(
                                "EmbeddingFailed for chunk {ChunkId} after retry. StatusCode={StatusCode}",
                                chunkId, (int)response.StatusCode);
                        }
                        // attempt < 2: loop will retry; attempt >= 2: loop exits
                        continue;
                    }

                    // Success or non-5xx: deserialise and break out of retry loop
                    var body = await response.Content
                        .ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: ct);
                    vector = body?.Embedding;
                    break;
                }

                if (vector is null)
                    continue; // all retry attempts exhausted or deserialization returned null

                // ── Dimension validation before any EF Core insert (Edge: dimension mismatch) ───
                // OWASP A04 — prevents a malformed vector from causing a pgvector cast exception.
                if (vector.Length != 1536)
                {
                    _logger.LogError(
                        "EmbeddingDimensionMismatch for chunk {ChunkId}: expected 1536, got {Actual}",
                        chunkId, vector.Length);
                    continue;
                }

                // ── Insert one row per validated chunk; each chunk independently committed (AC-002) ─
                // ChunkId is the PK + FK — uniqueness is enforced at DB level (no duplicate rows).
                db.DocumentChunkEmbeddings.Add(new DocumentChunkEmbedding
                {
                    ChunkId   = chunkId,
                    Embedding = new Vector(vector),
                    CreatedAt = DateTimeOffset.UtcNow,
                });
                await db.SaveChangesAsync(ct);

                // ── All-chunks-complete detection (us_038/AC-001 trigger) ──────────────────
                // After each successful commit, check whether every chunk now has an embedding.
                // Only publish when embeddedCount == totalCount > 0 to avoid spurious events
                // on partially-processed documents (AC-001; OWASP A04 — non-blocking TryWrite).
                var totalChunks = await db.DocumentChunks
                    .CountAsync(dc => dc.DocumentId == ev.DocumentId, ct);
                var embeddedChunks = await (
                    from ce in db.DocumentChunkEmbeddings
                    join dc in db.DocumentChunks on ce.ChunkId equals dc.Id
                    where dc.DocumentId == ev.DocumentId
                    select ce).CountAsync(ct);

                if (embeddedChunks == totalChunks && totalChunks > 0)
                {
                    var doc = await db.DocumentRecords.FindAsync(new object[] { ev.DocumentId }, ct);
                    if (doc is not null)
                    {
                        if (!_embeddingsCompleteChannel.Writer.TryWrite(
                                new DocumentEmbeddingsCompleteEvent(ev.DocumentId, doc.PatientId)))
                        {
                            _logger.LogWarning(
                                "DocumentEmbeddingsCompleteEvent channel full; document {DocumentId} skipped",
                                ev.DocumentId);
                        }
                    }
                }
            }
        }
        catch (HttpRequestException ex)
            when (ex.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused })
        {
            // ── Ollama offline: re-enqueue event, then back off 5 minutes (Edge: Ollama offline) ─
            // TryWrite first so the event is not lost; Task.Delay before processing the next event
            // so other queued events are not also sent immediately to an offline Ollama (OWASP A04).
            _logger.LogWarning(
                "Ollama offline for document {DocumentId}; re-enqueuing after 5-minute back-off",
                ev.DocumentId);
            _channel.Writer.TryWrite(ev);
            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }

    /// <summary>Minimal DTO for the Ollama <c>POST /api/embeddings</c> response.</summary>
    private sealed record OllamaEmbeddingResponse(
        [property: JsonPropertyName("embedding")] float[] Embedding);
}
