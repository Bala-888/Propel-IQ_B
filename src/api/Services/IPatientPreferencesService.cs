using Api.DTOs;

namespace Api.Services;

/// <summary>
/// Partial update contract for patient notification/calendar-sync preferences (us_029).
/// </summary>
public interface IPatientPreferencesService
{
    /// <summary>
    /// Applies only the non-null fields in <paramref name="dto"/> to the existing
    /// <c>PatientPreferences</c> row for <paramref name="patientId"/>, persists the changes,
    /// and returns the full updated row as a <see cref="PatientPreferencesResponse"/> (AC-002).
    /// </summary>
    Task<PatientPreferencesResponse> PatchAsync(
        int                  patientId,
        PatchPreferencesDto  dto,
        CancellationToken    ct = default);

    /// <summary>
    /// Returns the current preference row for <paramref name="patientId"/> (AC-002 GET support).
    /// </summary>
    Task<PatientPreferencesResponse?> GetAsync(
        int               patientId,
        CancellationToken ct = default);
}
