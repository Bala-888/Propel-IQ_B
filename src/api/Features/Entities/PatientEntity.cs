namespace Api.Features.Entities;

/// <summary>
/// EF Core entity for the <c>patient_entities</c> table.
/// Stores deduplicated clinical entities (diagnoses, medications, allergies, procedures) extracted
/// from patient documents by <see cref="Api.BackgroundServices.EntityExtractionWorker"/>
/// (us_038/AC-003, AC-004; AIR-003).
/// </summary>
/// <remarks>
/// Deduplication is enforced at the database layer via a UNIQUE constraint on
/// <c>(patient_id, type, value)</c>. A second extraction of the same entity updates
/// <c>last_seen_at</c> rather than inserting a new row (AC-003 — ON CONFLICT DO UPDATE pattern).
/// </remarks>
public sealed class PatientEntity
{
    /// <summary>Surrogate UUID primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// FK to <c>patients.id</c> (int).  Matches <see cref="Api.Features.Documents.DocumentRecord.PatientId"/>
    /// type so that the same patient numeric ID ties documents to extracted entities.
    /// </summary>
    public int PatientId { get; set; }

    /// <summary>FK to <c>document_records.id</c> — the source document for the latest extraction.</summary>
    public Guid DocumentId { get; set; }

    /// <summary>
    /// Entity category.  One of <c>Diagnosis</c>, <c>Medication</c>, <c>Allergy</c>,
    /// <c>Procedure</c> — validated by <see cref="Api.BackgroundServices.EntityExtractionWorker"/>
    /// before any insert (AC-002).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Extracted entity value, e.g. <c>"Type 2 Diabetes"</c>.  Max 500 chars enforced by
    /// EF Core model configuration; truncation guard prevents oversized inserts (OWASP A04).
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Ollama-reported confidence in [0.0, 1.0].</summary>
    public double Confidence { get; set; }

    /// <summary>
    /// <see langword="true"/> when <see cref="Confidence"/> &lt; 0.5.
    /// Low-confidence entities are stored, never silently dropped (AC-004; Edge: low confidence).
    /// </summary>
    public bool LowConfidence { get; set; }

    /// <summary>UTC timestamp of first extraction.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>UTC timestamp of most-recent extraction for this (patient, type, value) triplet.</summary>
    public DateTimeOffset LastSeenAt { get; set; }
}
