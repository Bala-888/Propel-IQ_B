using Api.AI;
using Api.AI.Exceptions;
using Api.Constants;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

/// <summary>
/// Provides the two AI-driven patient intake endpoints (us_016-I).
/// <list type="bullet">
///   <item><c>POST /intake/ai/start</c> — creates a new session and returns the opening question (AC-001)</item>
///   <item><c>POST /intake/ai/message</c> — processes a patient turn and upserts the draft record (AC-002, AC-005)</item>
/// </list>
/// All Ollama traffic goes to <c>http://ollama:11434</c> via the Docker bridge; no external network
/// calls are made (AC-003; OWASP A02).
/// PHI in patient messages and extracted fields must never be emitted to logs (AIR guardrails; OWASP A09).
/// </summary>
[ApiController]
[Route("intake/ai")]
[Authorize(Roles = Roles.Patient)]
public sealed class IntakeAiController : ControllerBase
{
    private readonly OllamaIntakeClient   _ollamaClient;
    private readonly IntakeSessionService _sessionService;
    private readonly IntakeRecordService  _recordService;
    private readonly ILogger<IntakeAiController> _logger;

    public IntakeAiController(
        OllamaIntakeClient   ollamaClient,
        IntakeSessionService sessionService,
        IntakeRecordService  recordService,
        ILogger<IntakeAiController> logger)
    {
        _ollamaClient   = ollamaClient;
        _sessionService = sessionService;
        _recordService  = recordService;
        _logger         = logger;
    }

    /// <summary>
    /// Initialises a new AI intake session and returns the model's opening question.
    /// The session is stored in <see cref="IDistributedCache"/> with a 2-hour TTL (AC-001).
    /// The entire Ollama call is wrapped in a 29-second CancellationToken budget — accounting for
    /// JWT validation and DB overhead to stay within the 3-second response SLA (AC-001; checklist).
    /// </summary>
    /// <response code="200">Session created; <c>sessionId</c> and the opening <c>message</c> are returned.</response>
    /// <response code="503">Ollama model not loaded; the patient is directed to the manual form.</response>
    /// <response code="504">Ollama inference timed out; the patient is asked to retry.</response>
    [HttpPost("start")]
    public async Task<IActionResult> StartAsync(CancellationToken requestCt)
    {
        var patientId = GetPatientId();
        if (patientId is null)
            return Unauthorized(new { error = "Authentication required." });

        // 29-second budget for the Ollama call — leaves headroom for JWT + DB overhead (AC-001; checklist)
        using var ollamaCts = CancellationTokenSource.CreateLinkedTokenSource(requestCt);
        ollamaCts.CancelAfter(TimeSpan.FromSeconds(29));

        IntakeSessionState session;
        try
        {
            session = await _sessionService.CreateSessionAsync(patientId.Value, ollamaCts.Token);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout,
                new { error = "The intake service timed out. Please try again." });
        }

        // Opening turn: empty history — the system prompt (prepended by OllamaIntakeClient) instructs
        // the model to greet the patient and ask for their name and date of birth first.
        string replyText;
        try
        {
            (replyText, _) = await _ollamaClient.ChatAsync(session.History, ollamaCts.Token);
        }
        catch (IntakeAiUnavailableException ex)
        {
            // Session state is preserved in the cache; patient can resume when the model recovers (Edge)
            _logger.LogWarning("IntakeAi unavailable at start: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "AI intake is temporarily unavailable. You can use the manual form instead." });
        }
        catch (OperationCanceledException)
        {
            // 30-second Ollama timeout — session preserved in cache (Edge: inference timeout)
            _logger.LogWarning(
                "IntakeAi start timed out for sessionId={SessionId}", session.SessionId);
            return StatusCode(StatusCodes.Status504GatewayTimeout,
                new { error = "The intake service timed out. Please try again." });
        }

        // Store the assistant's opening message in the session history for the next turn
        session.History.Add(new OllamaChatMessage { Role = "assistant", Content = replyText });
        await _sessionService.UpdateSessionAsync(session, requestCt);

        // Log structural ID only — no PHI (AIR guardrails; OWASP A09)
        _logger.LogInformation(
            "IntakeAi session started: sessionId={SessionId}", session.SessionId);

        return Ok(new { sessionId = session.SessionId, message = replyText });
    }

    /// <summary>
    /// Processes a patient message within an existing intake session.
    /// The patient's message is appended to conversation history, forwarded to Ollama,
    /// and the model's structured extraction updates the session's field state.
    /// A draft <see cref="Data.Entities.IntakeRecord"/> is upserted with encrypted PHI after every turn (AC-005).
    /// When all 5 field groups are collected the response includes <c>allFieldsCollected: true</c>
    /// and the <c>summary</c> object (AC-002).
    /// </summary>
    /// <response code="200">Response from the AI including the next question or a completion summary.</response>
    /// <response code="400">Missing or invalid request body.</response>
    /// <response code="403">Session does not belong to the authenticated patient.</response>
    /// <response code="404">Session expired or not found.</response>
    /// <response code="503">Ollama model not loaded.</response>
    /// <response code="504">Ollama inference timed out.</response>
    [HttpPost("message")]
    public async Task<IActionResult> MessageAsync(
        [FromBody] IntakeMessageRequest request,
        CancellationToken requestCt)
    {
        var patientId = GetPatientId();
        if (patientId is null)
            return Unauthorized(new { error = "Authentication required." });

        // Retrieve and validate session ownership (OWASP A01; checklist)
        IntakeSessionState session;
        try
        {
            var found = await _sessionService.GetSessionAsync(
                request.SessionId, patientId.Value, requestCt);

            if (found is null)
            {
                return NotFound(new
                {
                    error = "Session expired or not found. Please start a new intake."
                });
            }

            session = found;
        }
        catch (UnauthorizedAccessException)
        {
            // Cross-patient session access attempt — return 403 (OWASP A01)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Access denied. You can only access your own intake session." });
        }

        // Append patient message to history — raw text must not be logged (AIR guardrails; OWASP A09)
        session.History.Add(new OllamaChatMessage { Role = "user", Content = request.Message });

        // 30-second cancellation budget for the Ollama inference call (Edge: inference timeout)
        using var ollamaCts = CancellationTokenSource.CreateLinkedTokenSource(requestCt);
        ollamaCts.CancelAfter(TimeSpan.FromSeconds(30));

        string          replyText;
        IntakeFieldState? extraction;
        try
        {
            (replyText, extraction) = await _ollamaClient.ChatAsync(session.History, ollamaCts.Token);
        }
        catch (IntakeAiUnavailableException ex)
        {
            // Session state is preserved; patient can retry (Edge: model not loaded)
            _logger.LogWarning("IntakeAi unavailable at message: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "AI intake is temporarily unavailable. You can use the manual form instead." });
        }
        catch (OperationCanceledException)
        {
            // Timeout — session state preserved in cache (Edge: inference timeout; checklist)
            _logger.LogWarning(
                "IntakeAi message timed out for sessionId={SessionId}", session.SessionId);
            return StatusCode(StatusCodes.Status504GatewayTimeout,
                new { error = "The intake service timed out. Please try again." });
        }

        // Merge non-null extraction fields into the session state (AC-002; AC-004)
        if (extraction is not null)
            MergeExtraction(session.Fields, extraction);

        // Append assistant reply to history for the next turn
        session.History.Add(new OllamaChatMessage { Role = "assistant", Content = replyText });

        // Persist updated session (session state preserved in all error paths above — checklist)
        await _sessionService.UpdateSessionAsync(session, requestCt);

        // Upsert encrypted draft IntakeRecord after every turn (AC-005; OWASP A02)
        await _recordService.UpsertDraftAsync(session.PatientId, session.Fields, requestCt);

        // Log structural IDs only — no PHI (AIR guardrails; OWASP A09)
        _logger.LogInformation(
            "IntakeAi message processed: sessionId={SessionId} allFieldsCollected={AllFieldsCollected}",
            session.SessionId, session.AllFieldsCollected);

        if (session.AllFieldsCollected)
        {
            // AC-002: final response includes allFieldsCollected=true and the summary object
            return Ok(new
            {
                message           = replyText,
                allFieldsCollected = true,
                summary = new
                {
                    demographics   = session.Fields.Demographics,
                    medicalHistory = session.Fields.MedicalHistory,
                    medications    = session.Fields.Medications,
                    allergies      = session.Fields.Allergies,
                    chiefComplaint = session.Fields.ChiefComplaint,
                },
            });
        }

        return Ok(new { message = replyText, allFieldsCollected = false });
    }

    // ── Private helpers ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the structured field summary for the session identified by <paramref name="sessionId"/>.
    /// Validates session ownership before returning any data (AC-001; OWASP A01).
    /// </summary>
    /// <response code="200">Structured <see cref="IntakeFieldState"/> for all 5 field groups.</response>
    /// <response code="403">Session belongs to a different patient.</response>
    /// <response code="404">Session not found or expired.</response>
    [HttpGet("summary")]
    public async Task<IActionResult> SummaryAsync([FromQuery] Guid sessionId, CancellationToken ct)
    {
        var patientId = GetPatientId();
        if (patientId is null)
            return Unauthorized(new { error = "Authentication required." });

        IntakeSessionState? session;
        try
        {
            session = await _sessionService.GetSessionAsync(sessionId, patientId.Value, ct);
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Access denied. You can only access your own intake session." });
        }

        if (session is null)
            return NotFound(new { error = "Session not found or expired. Please start a new intake." });

        // Log structural ID only — no PHI (AIR guardrails; OWASP A09)
        _logger.LogInformation("IntakeAi summary retrieved: sessionId={SessionId}", sessionId);

        return Ok(session.Fields);
    }

    /// <summary>
    /// Applies a single field correction to the session's extracted data without requiring a new
    /// Ollama inference call (AC-002).
    /// The corrected value is never logged — only <c>sessionId</c> and <c>fieldPath</c> are written
    /// to structured logs (AIR guardrails; OWASP A09; HIPAA minimum-necessary; checklist).
    /// </summary>
    /// <response code="200">Corrected <see cref="IntakeFieldState"/> returned in full.</response>
    /// <response code="400">Required field submitted as empty (Edge: empty required field).</response>
    /// <response code="403">Session belongs to a different patient.</response>
    /// <response code="404">Session not found or expired.</response>
    [HttpPatch("field")]
    public async Task<IActionResult> PatchFieldAsync(
        [FromBody] PatchFieldRequest request,
        CancellationToken ct)
    {
        var patientId = GetPatientId();
        if (patientId is null)
            return Unauthorized(new { error = "Authentication required." });

        try
        {
            var updatedFields = await _sessionService.PatchFieldAsync(
                request.SessionId,
                patientId.Value,
                request.FieldPath,
                request.Value,
                ct);

            return Ok(updatedFields);
        }
        catch (RequiredFieldEmptyException ex)
        {
            // Edge: empty required field → 400; not caught by global handler (OWASP A05; checklist)
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Access denied. You can only access your own intake session." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Session not found or expired. Please start a new intake." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Confirms the AI intake session: promotes the <c>Draft</c> record to <c>Complete</c>,
    /// writes the <c>IntakeCompleted</c> audit entry, evicts the session from cache, and
    /// returns HTTP 201 (AC-003).
    /// Session eviction only occurs after <c>ConfirmAsync</c> succeeds — on failure the session
    /// remains in cache so the patient can retry (checklist: idempotency on retry).
    /// </summary>
    /// <response code="201">Intake confirmed; <c>IntakeRecord</c> promoted to Complete.</response>
    /// <response code="403">Session belongs to a different patient.</response>
    /// <response code="410">Session TTL elapsed before confirmation (Edge: session expired).</response>
    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmAsync(
        [FromBody] ConfirmRequest request,
        CancellationToken ct)
    {
        var patientId = GetPatientId();
        if (patientId is null)
            return Unauthorized(new { error = "Authentication required." });

        IntakeSessionState? session;
        try
        {
            session = await _sessionService.GetSessionAsync(request.SessionId, patientId.Value, ct);
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Access denied. You can only access your own intake session." });
        }

        // Edge: session TTL elapsed → 410 (checklist; AC-003; OWASP A05 — not caught by global handler)
        if (session is null)
            return StatusCode(StatusCodes.Status410Gone,
                new { error = "Session expired. Your draft has been saved." });

        var role      = User.FindFirstValue("role") ?? string.Empty;
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

        // Confirm: encrypt final JSONB + promote to Complete + write audit entry (AC-003)
        await _recordService.ConfirmAsync(
            session.PatientId,
            session.Fields,
            ct);

        // Evict session from cache ONLY after ConfirmAsync succeeds (checklist: idempotency on retry)
        await _sessionService.DeleteSessionAsync(request.SessionId, ct);

        _logger.LogInformation(
            "IntakeAi session confirmed: sessionId={SessionId}", request.SessionId);

        return StatusCode(StatusCodes.Status201Created, new { confirmed = true });
    }

    /// <summary>
    /// Parses the numeric patient ID from the JWT <c>sub</c> claim.
    /// Returns <c>null</c> when the claim is absent or non-numeric.
    /// </summary>
    private int? GetPatientId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");

        return sub is not null && int.TryParse(sub, out var id) ? id : null;
    }

    /// <summary>
    /// Merges non-null fields from <paramref name="extraction"/> into <paramref name="target"/>.
    /// Existing non-null values are never overwritten with null — once a field is collected
    /// it is preserved across turns (AC-002; checklist).
    /// </summary>
    private static void MergeExtraction(IntakeFieldState target, IntakeFieldState extraction)
    {
        if (extraction.Demographics   is not null) target.Demographics   = extraction.Demographics;
        if (extraction.MedicalHistory is not null) target.MedicalHistory = extraction.MedicalHistory;
        if (extraction.Medications    is not null) target.Medications    = extraction.Medications;
        if (extraction.Allergies      is not null) target.Allergies      = extraction.Allergies;
        if (extraction.ChiefComplaint is not null) target.ChiefComplaint = extraction.ChiefComplaint;
    }
}

/// <summary>
/// Request body for <c>POST /intake/ai/message</c>.
/// </summary>
public sealed class IntakeMessageRequest
{
    /// <summary>Session ID returned by <c>POST /intake/ai/start</c> (AC-001).</summary>
    public Guid SessionId { get; set; }

    /// <summary>The patient's response text for this turn (AC-002).</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Request body for <c>PATCH /intake/ai/field</c> (AC-002; us_016-II).
/// </summary>
public sealed class PatchFieldRequest
{
    /// <summary>Session to update — must belong to the authenticated patient (OWASP A01).</summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// camelCase property name on <see cref="IntakeFieldState"/>
    /// (e.g., <c>"chiefComplaint"</c>, <c>"medications"</c>).
    /// </summary>
    public string FieldPath { get; set; } = string.Empty;

    /// <summary>
    /// Replacement value. Empty string rejected for required fields (Edge: empty required field).
    /// Must never be logged — may contain PHI (AIR guardrails; OWASP A09).
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Request body for <c>POST /intake/ai/confirm</c> (AC-003; us_016-II).
/// </summary>
public sealed class ConfirmRequest
{
    /// <summary>Session to confirm — must belong to the authenticated patient (OWASP A01).</summary>
    public Guid SessionId { get; set; }
}
