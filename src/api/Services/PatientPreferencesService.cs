using Api.Audit;
using Api.Data;
using Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Partial-update service for patient notification and calendar-sync preferences (us_029).
/// Only non-null DTO fields are applied to the entity — prevents unintended mass-assignment of
/// non-preference columns (OWASP A03; AC-002).
/// Audit log records the changed field name without logging the boolean value (OWASP A02; AC-003).
/// Disabling all five channels simultaneously is a valid persisted state — no minimum-one-channel
/// validation is applied (Edge: all channels disabled; task spec).
/// </summary>
public sealed class PatientPreferencesService : IPatientPreferencesService
{
    private readonly AppDbContext   _db;
    private readonly IAuditLogger   _audit;

    public PatientPreferencesService(AppDbContext db, IAuditLogger audit)
    {
        _db    = db;
        _audit = audit;
    }

    /// <inheritdoc/>
    public async Task<PatientPreferencesResponse?> GetAsync(int patientId, CancellationToken ct = default)
    {
        var prefs = await _db.PatientPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PatientId == patientId, ct);

        return prefs is null ? null : ToResponse(prefs);
    }

    /// <inheritdoc/>
    public async Task<PatientPreferencesResponse> PatchAsync(
        int                  patientId,
        PatchPreferencesDto  dto,
        CancellationToken    ct = default)
    {
        var prefs = await _db.PatientPreferences
            .FirstOrDefaultAsync(p => p.PatientId == patientId, ct);

        // Upsert guard: should never be null for existing patients (row created at registration),
        // but handle gracefully to prevent 500 if a legacy account lacks a preferences row.
        if (prefs is null)
        {
            prefs = new Data.Entities.PatientPreferences { PatientId = patientId };
            _db.PatientPreferences.Add(prefs);
        }

        // Apply only non-null DTO fields — log field name (not value) for each changed field.
        // OWASP A03: only these five fields are ever written to; no other entity properties touched.
        // OWASP A02: preference values are NOT included in the audit entry (AC-003).
        if (dto.EmailNotificationsEnabled.HasValue)
        {
            prefs.EmailNotificationsEnabled = dto.EmailNotificationsEnabled.Value;
            _audit.Log(patientId.ToString(), AuditActionTypes.PreferenceUpdated,
                nameof(dto.EmailNotificationsEnabled));
        }

        if (dto.SmsNotificationsEnabled.HasValue)
        {
            prefs.SmsNotificationsEnabled = dto.SmsNotificationsEnabled.Value;
            _audit.Log(patientId.ToString(), AuditActionTypes.PreferenceUpdated,
                nameof(dto.SmsNotificationsEnabled));
        }

        if (dto.SlotSwapNotificationsEnabled.HasValue)
        {
            prefs.SlotSwapNotificationsEnabled = dto.SlotSwapNotificationsEnabled.Value;
            _audit.Log(patientId.ToString(), AuditActionTypes.PreferenceUpdated,
                nameof(dto.SlotSwapNotificationsEnabled));
        }

        if (dto.GoogleCalendarSyncEnabled.HasValue)
        {
            prefs.GoogleCalendarSyncEnabled = dto.GoogleCalendarSyncEnabled.Value;
            _audit.Log(patientId.ToString(), AuditActionTypes.PreferenceUpdated,
                nameof(dto.GoogleCalendarSyncEnabled));
        }

        if (dto.OutlookCalendarSyncEnabled.HasValue)
        {
            prefs.OutlookCalendarSyncEnabled = dto.OutlookCalendarSyncEnabled.Value;
            _audit.Log(patientId.ToString(), AuditActionTypes.PreferenceUpdated,
                nameof(dto.OutlookCalendarSyncEnabled));
        }

        await _db.SaveChangesAsync(ct);

        return ToResponse(prefs);
    }

    // ── Private helpers ───────────────────────────────────────────────────────────────────────

    private static PatientPreferencesResponse ToResponse(Data.Entities.PatientPreferences p) =>
        new()
        {
            EmailNotificationsEnabled    = p.EmailNotificationsEnabled,
            SmsNotificationsEnabled      = p.SmsNotificationsEnabled,
            SlotSwapNotificationsEnabled = p.SlotSwapNotificationsEnabled,
            GoogleCalendarSyncEnabled    = p.GoogleCalendarSyncEnabled,
            OutlookCalendarSyncEnabled   = p.OutlookCalendarSyncEnabled,
        };
}
