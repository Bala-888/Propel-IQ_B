using System.Text;
using System.Threading.Channels;
using Api.Data;
using Api.Features.Documents;
using UglyToad.PdfPig;

namespace Api.BackgroundServices;

/// <summary>
/// Singleton <see cref="BackgroundService"/> that consumes <see cref="DocumentUploadedEvent"/>
/// items from the bounded channel, decrypts each blob in-memory, extracts PDF text page-by-page
/// via PdfPig, chunks the text with <see cref="SlidingWindowChunker"/>, and bulk-inserts
/// <c>document_chunks</c> rows in a single database transaction (us_036/AC-001–004).
///
/// <para>
/// <b>DI lifetime:</b> this worker is singleton; <see cref="AppDbContext"/> and
/// <see cref="IDocumentEncryptionService"/> are scoped. A fresh <see cref="IServiceScope"/>
/// is created per event via <see cref="IServiceScopeFactory"/> — scoped services are never
/// captured in constructor fields (OWASP A04; checklist).
/// </para>
///
/// <para>
/// <b>Memory safety:</b> the decrypted <see cref="MemoryStream"/> buffer is zero-filled with
/// <see cref="Array.Clear"/> in a <c>finally</c> block after PdfPig processing completes —
/// plaintext is never written to disk and is removed from the managed heap promptly (AC-001; OWASP A02).
/// </para>
///
/// <para>
/// <b>Heap control:</b> PdfPig's <c>GetPages()</c> is used for page-by-page iteration;
/// <c>GetAllText()</c> is deliberately avoided to keep heap usage bounded for 300+ page
/// documents (AC-001; Edge: 300+ pages; checklist).
/// </para>
/// </summary>
public sealed class DocumentTextExtractionWorker : BackgroundService
{
    private readonly Channel<DocumentUploadedEvent>         _channel;
    private readonly Channel<DocumentChunkedEvent>          _chunkChannel;
    private readonly IServiceScopeFactory                   _scopeFactory;
    private readonly ILogger<DocumentTextExtractionWorker>  _logger;

    public DocumentTextExtractionWorker(
        Channel<DocumentUploadedEvent>         channel,
        Channel<DocumentChunkedEvent>          chunkChannel,
        IServiceScopeFactory                   scopeFactory,
        ILogger<DocumentTextExtractionWorker>  logger)
    {
        _channel      = channel;
        _chunkChannel = chunkChannel;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DocumentTextExtractionWorker started.");

        try
        {
            // ReadAllAsync completes when the channel is marked complete or stoppingToken fires
            await foreach (var ev in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                // Per-event isolation — a single failure must not stall the consumer loop (AC-004; OWASP A09)
                try
                {
                    await ProcessEventAsync(ev, stoppingToken);
                }
                catch (Exception ex)
                {
                    // Outer safety net: structured log with DocumentId only — no BlobPath or WrappedKey (OWASP A02)
                    _logger.LogWarning(ex,
                        "Unhandled exception in extraction pipeline for document {DocumentId}. eventType=ExtractionFailed",
                        ev.DocumentId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown — host is stopping; swallow so the service exits cleanly
            _logger.LogInformation("DocumentTextExtractionWorker stopping.");
        }
    }

    /// <summary>
    /// Full per-event processing pipeline: MIME guard → decrypt → extract → chunk → DB insert.
    /// All exceptions that reach this method are caught and persisted as <c>"ExtractionFailed"</c>
    /// status on the <see cref="DocumentRecord"/> (AC-003, AC-004; OWASP A04).
    /// </summary>
    private async Task ProcessEventAsync(DocumentUploadedEvent ev, CancellationToken ct)
    {
        // ── MIME guard: only PDF is supported in this pipeline (Edge: DOCX arrives; step 3) ─────
        if (ev.MimeType != "application/pdf")
        {
            // Log Warning only — document_records status is NOT modified for non-PDF events
            _logger.LogWarning(
                "Skipping non-PDF document {DocumentId} with MimeType {MimeType}. No status change applied.",
                ev.DocumentId, ev.MimeType);
            return;
        }

        // Create a fresh scope per event — BackgroundService is singleton; AppDbContext is scoped (OWASP A04; checklist)
        await using var scope      = _scopeFactory.CreateAsyncScope();
        var             db         = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var             encryption = scope.ServiceProvider.GetRequiredService<IDocumentEncryptionService>();

        // Load the record early so it is always available for error-path status updates
        var record = await db.DocumentRecords.FindAsync(new object[] { ev.DocumentId }, ct);
        if (record is null)
        {
            _logger.LogWarning(
                "DocumentRecord {DocumentId} not found; skipping extraction.",
                ev.DocumentId);
            return;
        }

        MemoryStream? decryptedStream = null;
        try
        {
            // ── Step 1: In-memory decryption — plaintext never touches disk (AC-001; OWASP A02) ─
            decryptedStream = await encryption.DecryptAsync(ev.BlobPath, ev.WrappedKey, ct);

            // ── Step 2: PdfPig page-by-page text extraction (AC-001; Edge: 300+ pages) ──────────
            // PdfDocument is wrapped in `using` so it is disposed before the next event is
            // processed — prompt disposal prevents heap accumulation (AC-001; checklist).
            // GetAllText() is deliberately NOT called; GetPages() is used for streaming (checklist).
            var sb = new StringBuilder(capacity: 4096);
            using (var pdfDoc = PdfDocument.Open(decryptedStream))
            {
                foreach (var page in pdfDoc.GetPages())
                {
                    // Each page reference becomes GC-eligible after the iteration step (AC-001; Edge: 300+)
                    sb.Append(page.Text);
                    sb.Append(' ');
                }
            }

            // ── Step 3: Empty-text guard → ExtractionFailed (AC-004) ────────────────────────────
            if (sb.Length == 0)
            {
                record.Status        = "ExtractionFailed";
                record.FailureReason = "NoTextContent";
                await db.SaveChangesAsync(ct);

                _logger.LogWarning(
                    "PdfPig returned no text for document {DocumentId}. status=ExtractionFailed reason=NoTextContent",
                    ev.DocumentId);

                // Do NOT re-enqueue the same event — prevents infinite retry (AC-004; checklist)
                return;
            }

            // ── Step 4: Sliding-window chunking (AC-002; AIR-003, AIR-004) ──────────────────────
            var chunks = SlidingWindowChunker.Chunk(sb.ToString());

            // ── Step 5: Bulk insert + "Chunked" status in one transaction (AC-003) ──────────────
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var insertedChunkIds = new List<Guid>(chunks.Count);
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunkId = Guid.NewGuid();
                insertedChunkIds.Add(chunkId);
                db.DocumentChunks.Add(new DocumentChunk
                {
                    Id         = chunkId,
                    DocumentId = ev.DocumentId,
                    ChunkIndex = i,
                    Content    = chunks[i].Content,
                    TokenCount = chunks[i].TokenCount,
                    CreatedAt  = DateTimeOffset.UtcNow,
                });
            }

            record.Status = "Chunked";
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // ── Step 6: Enqueue DocumentChunkedEvent for EmbeddingWorker (us_037/AC-001) ────────
            // TryWrite after CommitAsync — extraction status is already persisted.
            // If channel is full (DropOldest), emit warning; document remains in "Chunked" status
            // and will require manual re-triggering of embedding generation (OWASP A04; checklist).
            if (!_chunkChannel.Writer.TryWrite(new DocumentChunkedEvent(ev.DocumentId, insertedChunkIds.ToArray())))
            {
                _logger.LogWarning(
                    "DocumentChunkedEvent channel full; document {DocumentId} requires manual embedding reprocessing",
                    ev.DocumentId);
            }

            _logger.LogInformation(
                "Document {DocumentId} chunked into {ChunkCount} chunks. status=Chunked",
                ev.DocumentId, chunks.Count);
        }
        catch (Exception ex)
        {
            // Catch-all — prevents unhandled exception from crashing BackgroundService (OWASP A04; AC-003; checklist)
            // Clear all tracked entities (including any partially-added DocumentChunk entries)
            // before the status-update save to avoid re-inserting stale tracked data (AC-003).
            db.ChangeTracker.Clear();

            record.Status        = "ExtractionFailed";
            // Truncate to 500 chars to respect column size constraint (OWASP A04; checklist)
            record.FailureReason = ex.Message[..Math.Min(500, ex.Message.Length)];

            db.DocumentRecords.Update(record);
            try
            {
                await db.SaveChangesAsync(CancellationToken.None); // CancellationToken.None — ct may already be cancelled
            }
            catch (Exception saveEx)
            {
                _logger.LogWarning(saveEx,
                    "Failed to persist ExtractionFailed status for document {DocumentId}", ev.DocumentId);
            }

            _logger.LogWarning(ex,
                "Extraction pipeline failed for document {DocumentId}. status=ExtractionFailed",
                ev.DocumentId);
        }
        finally
        {
            if (decryptedStream is not null)
            {
                // Zero-fill the MemoryStream internal buffer to remove plaintext from managed heap
                // after PdfPig processing completes (AC-001; OWASP A02; checklist).
                try
                {
                    Array.Clear(decryptedStream.GetBuffer(), 0, (int)decryptedStream.Length);
                }
                catch
                {
                    // GetBuffer() throws UnauthorizedAccessException for non-exposable streams;
                    // best-effort zero-fill — swallow to prevent masking the primary exception.
                }
                await decryptedStream.DisposeAsync();
            }
        }
    }
}
