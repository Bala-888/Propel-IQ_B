using Api.DTOs;

namespace Api.Features.Patients;

/// <summary>
/// Handles <c>POST /api/patients/walkin-create</c> — creates a minimal patient record
/// for new walk-in patients and returns pre-fill data for the front-desk form (us_030/AC-004).
/// </summary>
public interface IWalkinPatientService
{
    /// <summary>
    /// Creates a minimal <c>Patient</c> row from walk-in intake data.
    /// No full registration, intake form, or insurance record is created from this call (AC-004).
    /// </summary>
    /// <param name="request">Minimum required walk-in patient fields.</param>
    /// <param name="staffId">JWT sub of the acting staff member — for audit only (OWASP A01).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new patient's ID plus first/last name for form pre-fill.</returns>
    Task<WalkinPatientCreatedResult> CreateAsync(
        WalkinPatientRequest request,
        string staffId,
        CancellationToken ct = default);
}

/// <summary>Result of a successful walk-in patient creation.</summary>
public sealed record WalkinPatientCreatedResult(int PatientId, string FirstName, string LastName);
