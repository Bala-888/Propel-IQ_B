using Api.AI;
using Api.DTOs;
using Microsoft.Extensions.Logging;

namespace Api.Services;

/// <summary>
/// Implements the bidirectional intake mode-switch logic for <c>POST /intake/mode-switch</c>
/// (us_018; AC-001 AI→Manual; AC-002 Manual→AI; AC-003 reviewItems).
///
/// <para>
/// AI → Manual (<see cref="SwitchAiToManualAsync"/>):
/// Loads the AI session from <c>IDistributedCache</c>, maps field values via
/// <see cref="IntakeFieldMapper.MapAiToManual"/>, writes a <c>CacheVersion</c> token back to
/// the session, and returns a pre-populated <see cref="SaveDraftRequest"/> plus any unmapped
/// content in <c>reviewItems[]</c>.
/// </para>
///
/// <para>
/// Manual → AI (<see cref="SwitchManualToAiAsync"/>):
/// Loads the patient's Draft record from the database via <see cref="IntakeRecordService.GetDraftAsync"/>,
/// maps structured sections to free-text <see cref="IntakeFieldState"/> blobs via
/// <see cref="IntakeFieldMapper.MapManualToAi"/>, writes a new <see cref="IntakeSessionState"/>
/// to the cache, deletes the manual Draft record to avoid stale state, and returns the new
/// session ID and <c>CacheVersion</c> token.
/// </para>
///
/// <para>
/// PHI transferred between modes stays in memory only; no PHI is written to logs (OWASP A09;
/// AIR guardrails; HIPAA minimum-necessary). OWASP A01 ownership enforcement is delegated to
/// <see cref="IntakeSessionService.GetSessionAsync"/>, which throws
/// <see cref="UnauthorizedAccessException"/> when the session's <c>PatientId</c> does not match
/// the caller.
/// </para>
/// </summary>
public sealed class IntakeModeSwitchService
{
    private readonly IntakeSessionService                  _sessionService;
    private readonly IntakeRecordService                   _recordService;
    private readonly ILogger<IntakeModeSwitchService>      _logger;

    public IntakeModeSwitchService(
        IntakeSessionService             sessionService,
        IntakeRecordService              recordService,
        ILogger<IntakeModeSwitchService> logger)
    {
        _sessionService = sessionService;
        _recordService  = recordService;
        _logger         = logger;
    }

    // ── AI → Manual ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Switches the patient from AI intake mode to manual intake mode (AC-001; AC-003).
    /// </summary>
    /// <param name="sessionId">Active AI intake session GUID.</param>
    /// <param name="patientId">Numeric patient ID from the JWT <c>sub</c> claim.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="ModeSwitchResponse"/> with a pre-populated <see cref="SaveDraftRequest"/> as
    /// <c>MappedFields</c> and labelled AI free-text blobs in <c>ReviewItems</c>.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">
    /// Propagated from <see cref="IntakeSessionService.GetSessionAsync"/> when the session does
    /// not belong to <paramref name="patientId"/> (OWASP A01).
    /// </exception>
    public async Task<ModeSwitchResponse> SwitchAiToManualAsync(
        Guid sessionId, int patientId, CancellationToken ct = default)
    {
        // Throws UnauthorizedAccessException on ownership mismatch (OWASP A01; AC-001)
        var state = await _sessionService.GetSessionAsync(sessionId, patientId, ct);

        // Edge: session expired / no fields collected yet — return empty pre-populated draft
        if (state is null || !HasAnyAiFields(state.Fields))
        {
            _logger.LogInformation(
                "Mode switch AI→Manual (empty session) for patientId={PatientId} sessionId={SessionId}",
                patientId, sessionId);

            return new ModeSwitchResponse
            {
                MappedFields = new SaveDraftRequest(),
                ReviewItems  = Array.Empty<string>(),
                CacheVersion = Guid.NewGuid().ToString(),
            };
        }

        // Map directly-matched fields and surface unmapped content as reviewItems[] (AC-001; AC-003)
        var (draft, reviewItems) = IntakeFieldMapper.MapAiToManual(state.Fields);

        // Write a CacheVersion token so the frontend can gate concurrent draft auto-saves
        // (Edge: concurrent auto-save; OWASP A04; us_018 AC-001)
        var cacheVersion     = Guid.NewGuid().ToString();
        state.CacheVersion   = cacheVersion;
        await _sessionService.UpdateSessionAsync(state, ct);

        _logger.LogInformation(
            "Mode switch AI→Manual completed for patientId={PatientId} sessionId={SessionId}",
            patientId, sessionId);

        return new ModeSwitchResponse
        {
            MappedFields = draft,
            ReviewItems  = reviewItems,
            CacheVersion = cacheVersion,
        };
    }

    // ── Manual → AI ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Switches the patient from manual intake mode to AI intake mode (AC-002).
    /// Creates a new <see cref="IntakeSessionState"/> pre-populated from the patient's
    /// Draft record and deletes the Draft to avoid stale state.
    /// </summary>
    /// <param name="patientId">Numeric patient ID from the JWT <c>sub</c> claim.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="ModeSwitchResponse"/> with the mapped <see cref="IntakeFieldState"/> as
    /// <c>MappedFields</c>, a new <c>NewSessionId</c> for the AI dialogue to use, and a
    /// <c>CacheVersion</c> token for the concurrency guard.
    /// </returns>
    public async Task<ModeSwitchResponse> SwitchManualToAiAsync(
        int patientId, CancellationToken ct = default)
    {
        // Load and decrypt the patient's manual Draft from the DB (AC-002; PHI in memory only)
        var draft = await _recordService.GetDraftAsync(patientId, ct);

        // Edge: no draft exists yet — return an empty AI session the dialogue can immediately start
        if (draft is null || !HasAnyDraftFields(draft))
        {
            _logger.LogInformation(
                "Mode switch Manual→AI (no draft) for patientId={PatientId}", patientId);

            return new ModeSwitchResponse
            {
                MappedFields = new IntakeFieldState(),
                ReviewItems  = Array.Empty<string>(),
                CacheVersion = Guid.NewGuid().ToString(),
            };
        }

        // Map structured manual sections to free-text AI field blobs (AC-002)
        var aiFields = IntakeFieldMapper.MapManualToAi(draft);

        var cacheVersion = Guid.NewGuid().ToString();
        var newSessionId = Guid.NewGuid();

        // Write a new IntakeSessionState to IDistributedCache with pre-populated fields
        // (2-hour TTL is managed by IntakeSessionService.UpdateSessionAsync)
        var newSession = new IntakeSessionState
        {
            SessionId    = newSessionId,
            PatientId    = patientId,
            History      = [],
            Fields       = aiFields,
            CacheVersion = cacheVersion,
        };
        await _sessionService.UpdateSessionAsync(newSession, ct);

        // Delete the manual Draft record so the patient does not see stale manual data
        // if they switch back to Manual mode (AC-002; implementation step 5)
        await _recordService.DeleteManualDraftAsync(patientId, ct);

        _logger.LogInformation(
            "Mode switch Manual→AI completed for patientId={PatientId} newSessionId={NewSessionId}",
            patientId, newSessionId);

        return new ModeSwitchResponse
        {
            MappedFields = aiFields,
            ReviewItems  = Array.Empty<string>(),
            CacheVersion = cacheVersion,
            NewSessionId = newSessionId.ToString(),
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────────

    private static bool HasAnyAiFields(IntakeFieldState f) =>
        f.ChiefComplaint is not null ||
        f.Demographics   is not null ||
        f.MedicalHistory is not null ||
        f.Medications    is not null ||
        f.Allergies      is not null;

    private static bool HasAnyDraftFields(SaveDraftRequest d) =>
        d.ChiefComplaint is not null ||
        d.Demographics   is not null ||
        d.MedicalHistory is not null ||
        d.Medications    is not null ||
        d.Allergies      is not null;
}
