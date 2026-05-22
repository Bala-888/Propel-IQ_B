using System.ComponentModel.DataAnnotations;

namespace Api.Features.Conflicts;

/// <summary>
/// Request body for <c>PATCH /clinical-conflicts/{id}/resolve</c> (us_042/AC-002, AC-003).
/// Data annotations provide model-state validation at the ASP.NET Core binding layer (OWASP A03).
/// </summary>
public sealed class ResolveConflictRequest
{
    /// <summary>
    /// Resolution outcome.  Must be <c>"Resolved"</c> or <c>"Dismissed"</c>.
    /// Manual enum check in the action runs before any DB round trip (AC-002, AC-003; OWASP A03).
    /// </summary>
    [Required]
    public string Resolution { get; set; } = string.Empty;

    /// <summary>
    /// Optional free-text note.  Required when <see cref="Resolution"/> = <c>"Resolved"</c>.
    /// Capped at 1,000 characters; enforced by the action before any DB write (OWASP A03).
    /// OWASP A02: this value is stored in the audit DB only — never written to any ILogger.
    /// </summary>
    [MaxLength(1000)]
    public string? Note { get; set; }
}
