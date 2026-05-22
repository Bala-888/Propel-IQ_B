using System.ComponentModel.DataAnnotations;

namespace Api.Features.Codes;

/// <summary>
/// Request body for <c>POST /patients/{id}/medical-codes</c>.
/// Accepts or corrects a RAG-generated code suggestion (AC-001).
/// </summary>
public sealed class CreateMedicalCodeRequest
{
    /// <summary>ID of the <c>code_suggestions</c> row being reviewed.</summary>
    [Required]
    public Guid SuggestionId { get; set; }

    /// <summary>"ICD-10" or "CPT" (AC-002).</summary>
    [Required]
    public string CodeType { get; set; } = string.Empty;

    /// <summary>The code being accepted (equals original) or the corrected code (AC-002).</summary>
    [Required]
    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>"AI" for straight acceptance, "AI-Corrected" for clinician correction (AC-001).</summary>
    [Required]
    public string Source { get; set; } = string.Empty;

    /// <summary>"Accepted" or "Corrected" (AC-001).</summary>
    [Required]
    public string ReviewStatus { get; set; } = string.Empty;

    /// <summary>
    /// Required when <c>Source == "AI-Corrected"</c>; contains the clinician's corrected code.
    /// Validated against the format regex before any DB access (OWASP A03; AC-005).
    /// </summary>
    public string? CorrectedCode { get; set; }
}
