using Api.Data.DTOs;

namespace Api.Repositories;

/// <summary>
/// Read interface for patient data with decrypted PHI fields (us_006/AC-002).
/// </summary>
public interface IPatientRepository
{
    /// <summary>
    /// Returns a <see cref="PatientDto"/> with all PHI fields in plaintext,
    /// or <c>null</c> if no patient with <paramref name="patientId"/> exists.
    /// EF Core value converters handle decryption transparently — the repository
    /// does not call <c>IPhiEncryptionService</c> directly (AC-002; separation of concerns).
    /// </summary>
    Task<PatientDto?> GetByIdAsync(int patientId, CancellationToken cancellationToken = default);
}
