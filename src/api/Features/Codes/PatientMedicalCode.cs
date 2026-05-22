namespace Api.Features.Codes;

/// <summary>
/// EF Core entity for a clinician-reviewed medical code written to the
/// <c>patient_medical_codes</c> table.  <c>ReviewedBy</c> is a non-nullable scalar FK
/// to <c>users.id</c> (int) — every accepted or corrected code requires an identified reviewer.
/// </summary>
public sealed class PatientMedicalCode
{
    public Guid   Id           { get; set; }
    public int    PatientId    { get; set; }
    public string CodeType     { get; set; } = string.Empty;
    public string Code         { get; set; } = string.Empty;

    /// <summary>
    /// Original AI-suggested code before clinician correction.  Equals <see cref="Code"/> for
    /// accepted suggestions; differs for corrected ones (Source = "AI-Corrected").
    /// </summary>
    public string  OriginalCode  { get; set; } = string.Empty;
    public string? Description   { get; set; }

    /// <summary>"AI" or "AI-Corrected" (AC-001).</summary>
    public string Source         { get; set; } = string.Empty;

    /// <summary>"Accepted" or "Corrected" (AC-001).</summary>
    public string ReviewStatus   { get; set; } = string.Empty;

    /// <summary>Scalar FK to <c>users.id</c> (int); NOT NULL — every code review has an actor.</summary>
    public int           ReviewedBy { get; set; }
    public DateTimeOffset ReviewedAt { get; set; }
    public DateTimeOffset CreatedAt  { get; set; }
}
