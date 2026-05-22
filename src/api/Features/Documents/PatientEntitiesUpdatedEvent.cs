namespace Api.Features.Documents;

/// <summary>
/// Event published by <see cref="Api.BackgroundServices.EntityExtractionWorker"/> after all
/// patient entities have been persisted and the document status has been set to "EntitiesExtracted".
/// Consumed by <see cref="Api.BackgroundServices.ConflictDetectionWorker"/> to trigger clinical
/// conflict detection for the patient (us_040/AC-004).
/// </summary>
/// <param name="PatientId">Patient's numeric ID — sourced from the originating document_records row.
/// Matches <see cref="Api.Features.Entities.PatientEntity.PatientId"/> type (int).</param>
public sealed record PatientEntitiesUpdatedEvent(int PatientId);
