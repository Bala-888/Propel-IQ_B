using Api.Audit;
using Api.Data;
using Api.Data.Entities;
using Api.DTOs;

namespace Api.Features.Patients;

/// <summary>
/// Implements <see cref="IWalkinPatientService"/> for <c>POST /api/patients/walkin-create</c>.
///
/// <para>
/// Creates a minimal <c>Patient</c> row with <c>FirstName</c>, <c>LastName</c>, <c>DateOfBirth</c>
/// (and optionally <c>Phone</c>). No <c>User</c> account, intake form, or insurance record is
/// created — this endpoint exists solely to enable walk-in form pre-fill (AC-004).
/// </para>
/// <para>
/// PHI fields (<c>DateOfBirth</c>, <c>Phone</c>) are encrypted transparently by the EF Core
/// value converters configured in <c>AppDbContext.OnModelCreating</c> (OWASP A02; DR-001).
/// </para>
/// </summary>
public sealed class WalkinPatientService : IWalkinPatientService
{
    private readonly AppDbContext              _db;
    private readonly Api.Services.IAuditLogger _audit;

    public WalkinPatientService(AppDbContext db, Api.Services.IAuditLogger audit)
    {
        _db    = db;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<WalkinPatientCreatedResult> CreateAsync(
        WalkinPatientRequest request,
        string staffId,
        CancellationToken ct = default)
    {
        var patient = new Patient
        {
            FirstName   = request.FirstName,
            LastName    = request.LastName,
            // ISO-8601 string expected by the PHI value converter ("yyyy-MM-dd")
            DateOfBirth = request.DateOfBirth.ToString("yyyy-MM-dd"),
            // Phone is stored as encrypted bytea via EF Core value converter (OWASP A02; DR-001)
            Phone       = request.PhoneNumber,
            CreatedAt   = DateTime.UtcNow,
        };

        _db.Patients.Add(patient);
        await _db.SaveChangesAsync(ct);

        // Audit: staffId + patientId only — no PHI in the log (OWASP A02; AC-004)
        _audit.Log(staffId, AuditActionTypes.WalkinPatientCreated, patient.Id.ToString());

        return new WalkinPatientCreatedResult(patient.Id, patient.FirstName, patient.LastName);
    }
}
