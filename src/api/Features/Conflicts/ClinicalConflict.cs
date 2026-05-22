using Api.Features.Entities;

namespace Api.Features.Conflicts;

/// <summary>
/// EF Core entity for the <c>clinical_conflicts</c> table.
/// Represents a detected clinical conflict between two patient entities (e.g. a drug interaction
/// between a Medication and an Allergy entity) identified by <see cref="Api.BackgroundServices.ConflictDetectionWorker"/>
/// (us_040/AC-004).
/// </summary>
/// <remarks>
/// Idempotency is enforced at the database layer via a UNIQUE constraint on
/// <c>(patient_id, entity_a_id, entity_b_id)</c>.  Re-running detection for the same entity
/// pair does not insert a duplicate row (ON CONFLICT DO NOTHING pattern).
/// </remarks>
public sealed class ClinicalConflict
{
    /// <summary>Surrogate UUID primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>FK to <c>patients.id</c> (int) — the patient whose entities produced this conflict.</summary>
    public int PatientId { get; set; }

    /// <summary>FK to <c>patient_entities.id</c> (UUID) — first entity in the conflict pair.</summary>
    public Guid EntityAId { get; set; }

    /// <summary>FK to <c>patient_entities.id</c> (UUID) — second entity in the conflict pair.</summary>
    public Guid EntityBId { get; set; }

    /// <summary>
    /// Category of conflict.  One of <c>DrugInteraction</c>, <c>DrugAllergyConflict</c>,
    /// <c>DuplicateDiagnosis</c> — validated by ConflictDetectionWorker before any insert (AC-004).
    /// </summary>
    public string ConflictType { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of the conflict.  Max 1000 chars enforced by model config.
    /// OWASP A02: this value is never written to ILogger.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Clinical severity.  One of <c>Low</c>, <c>Medium</c>, <c>High</c> — validated before insert (AC-004).
    /// </summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>Lifecycle status — always <c>Open</c> on insert; downstream workflows may update to <c>Resolved</c>.</summary>
    public string Status { get; set; } = "Open";

    /// <summary>UTC timestamp when the conflict was first detected.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    // ── Resolution fields (us_042/AC-002, AC-003) ──────────────────────────────────────────────────

    /// <summary>
    /// FK to <c>users.id</c> (int) — the authenticated user who resolved or dismissed the conflict.
    /// Null until the conflict is acted upon; <c>SET NULL</c> on user delete preserves audit history.
    /// OWASP A01: populated server-side from JWT sub claim — never accepted from request body.
    /// </summary>
    public int? ResolvedBy { get; set; }

    /// <summary>UTC timestamp when the conflict was resolved or dismissed. Null while status = "Open".</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>
    /// Optional free-text resolution note. Required when <see cref="Status"/> = "Resolved"; optional for "Dismissed".
    /// Max 1,000 chars enforced in the controller before any DB write (OWASP A03).
    /// OWASP A02: never written to any ILogger — stored to audit DB only.
    /// </summary>
    public string? ResolutionNote { get; set; }

    // ── Navigation properties ──────────────────────────────────────────────────────────────────

    /// <summary>Navigation to the first entity in the conflict pair.</summary>
    public PatientEntity EntityA { get; set; } = null!;

    /// <summary>Navigation to the second entity in the conflict pair.</summary>
    public PatientEntity EntityB { get; set; } = null!;
}
