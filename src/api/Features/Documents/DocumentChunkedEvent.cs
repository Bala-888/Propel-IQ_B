namespace Api.Features.Documents;

/// <summary>
/// Event published by <see cref="Api.BackgroundServices.DocumentTextExtractionWorker"/> after the
/// chunking transaction commits.  Consumed by
/// <see cref="Api.BackgroundServices.EmbeddingWorker"/> to generate and persist vector embeddings.
/// </summary>
/// <param name="DocumentId">FK into <c>document_records</c>.</param>
/// <param name="ChunkIds">Ordered array of newly-inserted <c>document_chunks.id</c> values.</param>
public sealed record DocumentChunkedEvent(Guid DocumentId, Guid[] ChunkIds);
