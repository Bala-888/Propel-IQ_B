namespace Api.Features.Codes;

/// <summary>
/// EF Core entity representing a RAG-generated code suggestion persisted to the
/// <c>code_suggestions</c> table.  <c>Id</c> is a server-generated UUID; <c>PatientId</c>
/// is a scalar FK to <c>patients.id</c> (int, no navigation property required).
/// </summary>
public sealed class CodeSuggestion
{
    public Guid   Id            { get; set; }
    public int    PatientId     { get; set; }
    public string CodeType      { get; set; } = string.Empty;
    public string Code          { get; set; } = string.Empty;
    public string? Description  { get; set; }
    public double Confidence    { get; set; }
    public bool   LowConfidence { get; set; }

    /// <summary>Pending → Accepted | Corrected | Rejected (AC-001).</summary>
    public string ReviewStatus  { get; set; } = "Pending";

    /// <summary>Scalar FK to <c>users.id</c> (int); nullable — set on review (AC-002).</summary>
    public int?              ReviewedBy { get; set; }
    public DateTimeOffset?   ReviewedAt { get; set; }
    public DateTimeOffset    CreatedAt  { get; set; }
}
