namespace Api.Data.DTOs;

/// <summary>
/// Read-model returned by PatientRepository — PHI fields contain decrypted plaintext (AC-002).
/// </summary>
public sealed record PatientDto(
    int Id,
    string FirstName,
    string LastName,
    string? DateOfBirth,
    string? Email,
    string? Phone,
    string? InsuranceProvider,
    string? InsuranceId
);
