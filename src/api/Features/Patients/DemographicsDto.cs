namespace Api.Features.Patients;

/// <summary>
/// Demographic slice of the 360° patient summary (us_040/AC-002).
/// PHI fields are returned decrypted by EF Core value converters — accessible only to
/// Staff/Admin/Clinician roles (AC-004; OWASP A01).
/// </summary>
public sealed class DemographicsDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;

    /// <summary>ISO-8601 date string "yyyy-MM-dd" — decrypted PHI; null if not recorded.</summary>
    public string? DateOfBirth { get; set; }

    /// <summary>Insurance provider name — decrypted PHI; null if not recorded.</summary>
    public string? InsuranceProvider { get; set; }

    /// <summary>Insurance policy/member ID — decrypted PHI; null if not recorded.</summary>
    public string? InsuranceId { get; set; }
}
