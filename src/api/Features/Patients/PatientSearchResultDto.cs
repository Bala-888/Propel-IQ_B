namespace Api.Features.Patients;

/// <summary>
/// Lightweight search result for <c>GET /patients/search</c> (us_040/AC-001).
/// PHI fields are limited to display-safe values; email and phone are never included (OWASP A02).
/// </summary>
public sealed class PatientSearchResultDto
{
    /// <summary>Patient's internal integer primary key.</summary>
    public int Id { get; set; }

    /// <summary>Concatenated first + last name for display.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Date of birth decrypted by EF Core PHI value converter — ISO-8601 string "yyyy-MM-dd".
    /// Null when the patient record was created without a DOB (Edge: missing PHI).
    /// </summary>
    public string? DateOfBirth { get; set; }

    /// <summary>Synthetic display code, e.g. "P000042" — derived from the integer PK.</summary>
    public string PatientCode { get; set; } = string.Empty;
}
