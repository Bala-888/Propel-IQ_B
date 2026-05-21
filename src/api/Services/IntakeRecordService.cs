using System.Text.Json;
using Api.AI;
using Api.Data;
using Api.Data.Entities;
using Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Upserts an <see cref="IntakeRecord"/> row with <c>Status = "Draft"</c> after each AI intake turn
/// (AC-005; us_016-I).
/// The serialised <see cref="IntakeFieldState"/> JSON is PHI-encrypted via
/// <see cref="IPhiEncryptionService"/> before persistence — the <c>Data</c> column contains only
/// AES-256 ciphertext at rest (OWASP A02; HIPAA §164.312(a)(2)(iv); checklist).
/// PHI must never be emitted to <see cref="ILogger"/> in this service (AIR guardrails; OWASP A09).
/// </summary>
public sealed class IntakeRecordService
{
    private readonly AppDbContext         _db;
    private readonly IPhiEncryptionService _phi;
    private readonly IAuditLogger         _auditLogger;
    private readonly ILogger<IntakeRecordService> _logger;

    public IntakeRecordService(
        AppDbContext          db,
        IPhiEncryptionService phi,
        IAuditLogger          auditLogger,
        ILogger<IntakeRecordService> logger)
    {
        _db          = db;
        _phi         = phi;
        _auditLogger = auditLogger;
        _logger      = logger;
    }

    /// <summary>
    /// Upserts the patient's draft intake record with the current partial field state.
    /// If a Draft record already exists for <paramref name="patientId"/>, its <c>Data</c>
    /// and <c>UpdatedAt</c> columns are updated in place.
    /// If no Draft record exists, a new row is inserted (AC-005).
    /// </summary>
    /// <param name="patientId">Numeric patient ID (matches <c>Patient.Id</c> and JWT sub claim).</param>
    /// <param name="fields">Partial or complete field state — serialised and encrypted before write.</param>
    /// <param name="ct">Cancellation token forwarded from the controller action.</param>
    public async Task UpsertDraftAsync(int patientId, IntakeFieldState fields, CancellationToken ct = default)
    {
        // Serialise and PHI-encrypt the fields JSON — Data column contains only ciphertext (OWASP A02)
        var fieldsJson     = JsonSerializer.Serialize(fields);
        var encryptedData  = _phi.Encrypt(fieldsJson);

        var existing = await _db.IntakeRecords
            .FirstOrDefaultAsync(
                r => r.PatientId == patientId && r.Status == "Draft",
                ct);

        if (existing is not null)
        {
            existing.Data      = encryptedData;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var record = new IntakeRecord
            {
                PatientId      = patientId,
                Status         = "Draft",
                Data           = encryptedData,
                RecordedAt     = DateTime.UtcNow,
                UpdatedAt      = DateTime.UtcNow,
                ChiefComplaint = string.Empty,  // populated when intake is finalised
            };
            _db.IntakeRecords.Add(record);
        }

        await _db.SaveChangesAsync(ct);

        // Log structural IDs only — no PHI (AIR guardrails; OWASP A09; HIPAA minimum-necessary)
        _logger.LogInformation(
            "IntakeRecord draft upserted for patientId={PatientId}", patientId);
    }

    /// <summary>
    /// Promotes the patient's intake record from <c>Draft</c> to <c>Complete</c> and writes the
    /// final audit log entry (AC-003; us_016-II).
    /// The serialised <see cref="IntakeFieldState"/> JSON is PHI-encrypted via
    /// <see cref="IPhiEncryptionService"/> before persistence — the database column never contains
    /// plaintext field values (OWASP A02; HIPAA §164.312(a)(2)(iv); checklist).
    /// Field values are never logged — only structural IDs are written (OWASP A09; checklist).
    /// </summary>
    /// <param name="patientId">Numeric patient ID.</param>
    /// <param name="fields">Final complete field state — serialised and encrypted before write.</param>
    /// <param name="ct">Cancellation token forwarded from the controller action.</param>
    public async Task ConfirmAsync(
        int    patientId,
        IntakeFieldState fields,
        CancellationToken ct = default)
    {
        // Serialise and PHI-encrypt — Data column contains only ciphertext (OWASP A02; checklist)
        var fieldsJson    = JsonSerializer.Serialize(fields);
        var encryptedData = _phi.Encrypt(fieldsJson);

        var existing = await _db.IntakeRecords
            .FirstOrDefaultAsync(
                r => r.PatientId == patientId && r.Status == "Draft",
                ct);

        if (existing is not null)
        {
            existing.Status        = "Complete";
            existing.Mode          = "AI";
            existing.Data          = encryptedData;
            existing.ChiefComplaint = fields.ChiefComplaint ?? string.Empty;
            existing.UpdatedAt     = DateTime.UtcNow;
        }
        else
        {
            // No prior Draft (edge case — confirm without prior auto-save): insert a Complete row
            var record = new IntakeRecord
            {
                PatientId      = patientId,
                Status         = "Complete",
                Mode           = "AI",
                Data           = encryptedData,
                ChiefComplaint = fields.ChiefComplaint ?? string.Empty,
                RecordedAt     = DateTime.UtcNow,
                UpdatedAt      = DateTime.UtcNow,
            };
            _db.IntakeRecords.Add(record);
        }

        await _db.SaveChangesAsync(ct);

        // Audit log — structural IDs only; no PHI in the audit entry (AC-003; OWASP A09; checklist)
        _auditLogger.Log(
            actorId:    patientId.ToString(),
            actionType: Api.Audit.AuditActionTypes.IntakeCompleted,
            resourceId: patientId.ToString());

        _logger.LogInformation(
            "IntakeRecord confirmed (Complete) for patientId={PatientId}", patientId);
    }

    // ── Manual intake methods (us_017) ────────────────────────────────────────────────────────

    /// <summary>
    /// Persists a complete manual intake submission as an <see cref="IntakeRecord"/> with
    /// <c>Status = "Complete"</c> and <c>Mode = "Manual"</c>.
    ///
    /// <para>Execution order (AC-002; OWASP A02; OWASP A09):</para>
    /// <list type="number">
    ///   <item>Serialise the full <paramref name="request"/> to JSON.</item>
    ///   <item>Encrypt the JSON via <see cref="IPhiEncryptionService.Encrypt"/> — the
    ///         <c>Data</c> column never contains plaintext PHI.</item>
    ///   <item>Upsert: if a Draft record (any mode) exists for this patient, promote it to
    ///         Complete; otherwise insert a new Complete row.</item>
    ///   <item>Delete all remaining Draft records for this patient so that a subsequent
    ///         <c>GET /intake/draft</c> returns 404 (AC-004/AC-005 data consistency).</item>
    ///   <item>Write an <c>IntakeCompleted</c> audit log entry with structural IDs only —
    ///         no PHI in the audit payload (AC-002; OWASP A09; HIPAA minimum-necessary).</item>
    /// </list>
    ///
    /// PHI values are never written to <see cref="ILogger"/> in this method (AIR guardrails).
    /// </summary>
    /// <param name="patientId">Numeric patient ID from the JWT <c>sub</c> claim.</param>
    /// <param name="request">Validated complete manual intake request.</param>
    /// <param name="ct">Cancellation token forwarded from the controller.</param>
    /// <returns>The <c>Id</c> of the persisted <see cref="IntakeRecord"/>.</returns>
    public async Task<int> SubmitManualAsync(
        int                       patientId,
        CreateManualIntakeRequest request,
        CancellationToken         ct = default)
    {
        // Serialise and PHI-encrypt — the data column contains only ciphertext (OWASP A02; checklist)
        var requestJson   = JsonSerializer.Serialize(request);
        var encryptedData = _phi.Encrypt(requestJson);

        var chiefComplaint = request.ChiefComplaint.Description ?? string.Empty;

        // Upsert: reuse the existing Draft record if one exists; otherwise insert a Complete row
        var existing = await _db.IntakeRecords
            .FirstOrDefaultAsync(r => r.PatientId == patientId && r.Status == "Draft", ct);

        IntakeRecord record;
        if (existing is not null)
        {
            existing.Status         = "Complete";
            existing.Mode           = "Manual";
            existing.Data           = encryptedData;
            existing.ChiefComplaint = chiefComplaint;
            existing.UpdatedAt      = DateTime.UtcNow;
            record = existing;
        }
        else
        {
            record = new IntakeRecord
            {
                PatientId      = patientId,
                Status         = "Complete",
                Mode           = "Manual",
                Data           = encryptedData,
                ChiefComplaint = chiefComplaint,
                RecordedAt     = DateTime.UtcNow,
                UpdatedAt      = DateTime.UtcNow,
            };
            _db.IntakeRecords.Add(record);
        }

        await _db.SaveChangesAsync(ct);

        // Remove any remaining Draft records so GET /intake/draft returns 404 (AC-004/AC-005)
        var staleDrafts = await _db.IntakeRecords
            .Where(r => r.PatientId == patientId && r.Status == "Draft")
            .ToListAsync(ct);

        if (staleDrafts.Count > 0)
        {
            _db.IntakeRecords.RemoveRange(staleDrafts);
            await _db.SaveChangesAsync(ct);
        }

        // Audit log — structural IDs only; no PHI (AC-002; OWASP A09; HIPAA minimum-necessary)
        _auditLogger.Log(
            actorId:    patientId.ToString(),
            actionType: Api.Audit.AuditActionTypes.IntakeCompleted,
            resourceId: record.Id.ToString());

        _logger.LogInformation(
            "Manual IntakeRecord submitted (Complete) for patientId={PatientId} recordId={RecordId}",
            patientId, record.Id);

        return record.Id;
    }

    /// <summary>
    /// Upserts a partial manual intake record with <c>Status = "Draft"</c> and
    /// <c>Mode = "Manual"</c>.
    ///
    /// <para>Upsert logic: if a Draft record with <c>Mode = "Manual"</c> already exists for this
    /// patient, its <c>Data</c> and <c>UpdatedAt</c> columns are updated in place; otherwise a new
    /// Draft row is inserted (AC-004; OWASP A04 — prevents unbounded draft accumulation).</para>
    ///
    /// No mandatory-field validation is applied — a save with only one of five sections is
    /// accepted (AC-004).
    /// PHI values are never written to <see cref="ILogger"/> (AIR guardrails; OWASP A09).
    /// </summary>
    /// <param name="patientId">Numeric patient ID from the JWT <c>sub</c> claim.</param>
    /// <param name="request">Partial intake data — all sections and fields are nullable.</param>
    /// <param name="ct">Cancellation token forwarded from the controller.</param>
    public async Task SaveDraftAsync(
        int              patientId,
        SaveDraftRequest request,
        CancellationToken ct = default)
    {
        // Serialise and PHI-encrypt — the data column contains only ciphertext (OWASP A02; checklist)
        var requestJson   = JsonSerializer.Serialize(request);
        var encryptedData = _phi.Encrypt(requestJson);

        var existing = await _db.IntakeRecords
            .FirstOrDefaultAsync(
                r => r.PatientId == patientId && r.Status == "Draft" && r.Mode == "Manual",
                ct);

        if (existing is not null)
        {
            existing.Data      = encryptedData;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.IntakeRecords.Add(new IntakeRecord
            {
                PatientId      = patientId,
                Status         = "Draft",
                Mode           = "Manual",
                Data           = encryptedData,
                ChiefComplaint = string.Empty,  // populated on final submission
                RecordedAt     = DateTime.UtcNow,
                UpdatedAt      = DateTime.UtcNow,
            });
        }

        await _db.SaveChangesAsync(ct);

        // No audit entry for draft saves — only complete submissions are audited (AC-002; OWASP A09)
        _logger.LogInformation(
            "Manual IntakeRecord draft upserted for patientId={PatientId}", patientId);
    }

    /// <summary>
    /// Returns the most recent manual-mode Draft record for <paramref name="patientId"/> with all
    /// PHI fields decrypted and deserialised into a <see cref="SaveDraftRequest"/> for form
    /// pre-population (AC-005).
    /// Returns <c>null</c> when no Draft record with <c>Mode = "Manual"</c> exists — the
    /// controller returns 404 in that case.
    /// </summary>
    /// <param name="patientId">Numeric patient ID from the JWT <c>sub</c> claim.</param>
    /// <param name="ct">Cancellation token forwarded from the controller.</param>
    public async Task<SaveDraftRequest?> GetDraftAsync(int patientId, CancellationToken ct = default)
    {
        var record = await _db.IntakeRecords
            .Where(r => r.PatientId == patientId && r.Status == "Draft" && r.Mode == "Manual")
            .OrderByDescending(r => r.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (record is null)
            return null;

        // Decrypt and deserialise — PHI values remain in memory only; never logged (OWASP A09)
        var plaintext = _phi.Decrypt(record.Data);
        if (string.IsNullOrWhiteSpace(plaintext))
            return null;

        return JsonSerializer.Deserialize<SaveDraftRequest>(plaintext);
    }

    /// <summary>
    /// Deletes all <c>Draft</c> records with <c>Mode = "Manual"</c> belonging to
    /// <paramref name="patientId"/>. Called by <c>IntakeModeSwitchService</c> after a
    /// Manual → AI mode switch to avoid stale draft state (us_018; AC-002).
    /// A no-op if no such records exist.
    /// </summary>
    /// <param name="patientId">Numeric patient ID from the JWT <c>sub</c> claim.</param>
    /// <param name="ct">Cancellation token forwarded from the service.</param>
    public async Task DeleteManualDraftAsync(int patientId, CancellationToken ct = default)
    {
        var drafts = await _db.IntakeRecords
            .Where(r => r.PatientId == patientId && r.Status == "Draft" && r.Mode == "Manual")
            .ToListAsync(ct);

        if (drafts.Count == 0)
            return;

        _db.IntakeRecords.RemoveRange(drafts);
        await _db.SaveChangesAsync(ct);
    }
}
