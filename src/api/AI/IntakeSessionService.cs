using System.Text.Json;
using Api.AI.Exceptions;
using Microsoft.Extensions.Caching.Distributed;

namespace Api.AI;

/// <summary>
/// Manages <see cref="IntakeSessionState"/> in <see cref="IDistributedCache"/> for the AI
/// intake workflow (AC-001, AC-002; checklist: session lifecycle).
/// Cache key pattern: <c>intake_session:{sessionId}</c>.
/// TTL: 2-hour absolute expiry — expired sessions return <c>null</c> from <see cref="GetSessionAsync"/>
/// and the controller maps this to a 404 "Session expired" response (checklist; OWASP A01).
/// PHI in session state must never be emitted to logs (AIR guardrails; OWASP A09).
/// </summary>
public sealed class IntakeSessionService
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(2);
    private const string KeyPrefix = "intake_session:";

    private readonly IDistributedCache _cache;
    private readonly ILogger<IntakeSessionService> _logger;

    public IntakeSessionService(IDistributedCache cache, ILogger<IntakeSessionService> logger)
    {
        _cache  = cache;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new <see cref="IntakeSessionState"/> for <paramref name="patientId"/>,
    /// stores it in the distributed cache with a 2-hour TTL, and returns it (AC-001).
    /// </summary>
    public async Task<IntakeSessionState> CreateSessionAsync(int patientId, CancellationToken ct = default)
    {
        var state = new IntakeSessionState
        {
            SessionId = Guid.NewGuid(),
            PatientId = patientId,
            History   = new List<OllamaChatMessage>(),
            Fields    = new IntakeFieldState(),
        };

        await PersistAsync(state, ct);

        // Log structural ID only — no PHI (AIR guardrails; OWASP A09; HIPAA minimum-necessary)
        _logger.LogInformation("IntakeSession created: {SessionId}", state.SessionId);

        return state;
    }

    /// <summary>
    /// Retrieves the session identified by <paramref name="sessionId"/> from the cache.
    /// Returns <c>null</c> when the session has expired or does not exist.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the session's <c>PatientId</c> does not match <paramref name="requestingPatientId"/>
    /// — prevents Patient A from continuing Patient B's session (OWASP A01; checklist).
    /// </exception>
    public async Task<IntakeSessionState?> GetSessionAsync(
        Guid sessionId,
        int  requestingPatientId,
        CancellationToken ct = default)
    {
        var json = await _cache.GetStringAsync(CacheKey(sessionId), ct);
        if (json is null)
        {
            _logger.LogInformation(
                "IntakeSession not found or expired: {SessionId}", sessionId);
            return null;
        }

        var state = JsonSerializer.Deserialize<IntakeSessionState>(json);
        if (state is null)
            return null;

        // Cross-patient access guard (OWASP A01; checklist)
        if (state.PatientId != requestingPatientId)
        {
            // Log structural IDs only — no PHI (OWASP A09)
            _logger.LogWarning(
                "IntakeSession ownership mismatch: sessionId={SessionId} ownedBy={OwnerId} requestedBy={RequesterId}",
                sessionId, state.PatientId, requestingPatientId);
            throw new UnauthorizedAccessException(
                "You are not authorised to access this intake session.");
        }

        return state;
    }

    /// <summary>
    /// Persists the updated <paramref name="state"/> back to the cache, resetting the 2-hour TTL
    /// so active sessions do not expire mid-intake (AC-001; checklist: session lifecycle).
    /// </summary>
    public async Task UpdateSessionAsync(IntakeSessionState state, CancellationToken ct = default)
    {
        await PersistAsync(state, ct);
        // Log structural ID only (AIR guardrails; OWASP A09)
        _logger.LogInformation("IntakeSession updated: {SessionId}", state.SessionId);
    }

    /// <summary>
    /// Applies a single field-value patch to the session's <see cref="IntakeSessionState.Fields"/>.
    /// Validates ownership before mutating, and rejects empty values for required fields
    /// (AC-002; OWASP A01; Edge: empty required field; checklist).
    /// Field values received are never logged — only structural sessionId and fieldPath are written
    /// (AIR guardrails; OWASP A09; HIPAA minimum-necessary).
    /// </summary>
    /// <param name="sessionId">Session to patch.</param>
    /// <param name="requestingPatientId">Must match <c>IntakeSessionState.PatientId</c> (OWASP A01).</param>
    /// <param name="fieldPath">camelCase property name on <see cref="IntakeFieldState"/> (e.g., <c>"chiefComplaint"</c>).</param>
    /// <param name="value">New value — empty string rejected for required fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated <see cref="IntakeFieldState"/> after the patch is applied.</returns>
    /// <exception cref="UnauthorizedAccessException">PatientId mismatch (OWASP A01).</exception>
    /// <exception cref="RequiredFieldEmptyException">Required field patched to empty (Edge; AC-002).</exception>
    /// <exception cref="ArgumentException">Unknown fieldPath supplied.</exception>
    public async Task<IntakeFieldState> PatchFieldAsync(
        Guid   sessionId,
        int    requestingPatientId,
        string fieldPath,
        string value,
        CancellationToken ct = default)
    {
        var state = await GetSessionAsync(sessionId, requestingPatientId, ct);
        if (state is null)
            throw new KeyNotFoundException($"Session {sessionId} not found or has expired.");

        // Reject empty values for required fields (Edge: empty required field; AC-002; checklist)
        if (RequiredIntakeFields.RequiredPaths.Contains(fieldPath) && string.IsNullOrWhiteSpace(value))
            throw new RequiredFieldEmptyException(fieldPath);

        // Apply the patch via a switch-based dispatcher — avoids unsafe reflection on PHI-bearing objects
        // Only fieldPath is logged below, not the value — value may contain PHI (OWASP A09; AIR guardrails)
        switch (fieldPath.ToLowerInvariant())
        {
            case "chiefcomplaint":  state.Fields.ChiefComplaint = value; break;
            case "demographics":    state.Fields.Demographics   = value; break;
            case "medicalhistory":  state.Fields.MedicalHistory = value; break;
            case "medications":     state.Fields.Medications    = value; break;
            case "allergies":       state.Fields.Allergies      = value; break;
            default:
                throw new ArgumentException($"Unknown intake field path: '{fieldPath}'", nameof(fieldPath));
        }

        await UpdateSessionAsync(state, ct);

        // Log structural metadata only — fieldPath is safe; value is PHI (OWASP A09)
        _logger.LogInformation(
            "IntakeSession field patched: {SessionId} fieldPath={FieldPath}", sessionId, fieldPath);

        return state.Fields;
    }

    /// <summary>
    /// Removes the session from <see cref="IDistributedCache"/> after a successful confirm (AC-003).
    /// Only called after <c>IntakeRecordService.ConfirmAsync</c> succeeds — if confirm throws, the
    /// session remains in cache so the patient can retry (checklist: idempotency on retry).
    /// </summary>
    public async Task DeleteSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        await _cache.RemoveAsync(CacheKey(sessionId), ct);
        _logger.LogInformation("IntakeSession evicted from cache: {SessionId}", sessionId);
    }

    // ── Private helpers ──────────────────────────────────────────────────────────────────────────

    private async Task PersistAsync(IntakeSessionState state, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(state);
        await _cache.SetStringAsync(
            CacheKey(state.SessionId),
            json,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = SessionTtl,
            },
            ct);
    }

    private static string CacheKey(Guid sessionId) => $"{KeyPrefix}{sessionId}";
}
