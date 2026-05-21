namespace Api.Features.Documents;

/// <summary>
/// Event published by <see cref="Api.BackgroundServices.EmbeddingWorker"/> after all chunks of a
/// document have had their embeddings successfully committed.  Consumed by
/// <see cref="Api.BackgroundServices.EntityExtractionWorker"/> to trigger clinical entity extraction
/// (us_038/AC-001).
/// </summary>
/// <param name="DocumentId">FK into <c>document_records</c>.</param>
/// <param name="PatientId">Patient's numeric ID — sourced from <c>document_records.patient_id</c>
/// (matches JWT <c>sub</c> claim type; consistent with <see cref="Api.Features.Documents.DocumentRecord.PatientId"/>).</param>
public sealed record DocumentEmbeddingsCompleteEvent(Guid DocumentId, int PatientId);
