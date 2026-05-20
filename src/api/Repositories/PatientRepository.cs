using Api.Data;
using Api.Data.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Api.Repositories;

/// <summary>
/// Retrieves Patient records with PHI fields decrypted transparently via EF Core value converters.
/// The repository does not call IPhiEncryptionService directly — decryption is handled
/// by the converters configured in AppDbContext (AC-002; separation of concerns).
/// </summary>
public sealed class PatientRepository : IPatientRepository
{
    private readonly AppDbContext _db;

    public PatientRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<PatientDto?> GetByIdAsync(int patientId, CancellationToken cancellationToken = default)
    {
        var patient = await _db.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);

        if (patient is null) return null;

        // PHI fields are already decrypted by EF Core value converters at this point (AC-002)
        return new PatientDto(
            patient.Id,
            patient.FirstName,
            patient.LastName,
            patient.DateOfBirth,
            patient.Email,
            patient.Phone,
            patient.InsuranceProvider,
            patient.InsuranceId);
    }
}
