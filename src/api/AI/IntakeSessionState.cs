using System.Text.Json.Serialization;

namespace Api.AI;

/// <summary>
/// Serialisable chat message compatible with the Ollama /api/chat format (AIR-001).
/// Role values: "system", "user", "assistant" — matched to Ollama spec.
/// </summary>
public sealed class OllamaChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// The five structured field groups collected during an AI-driven intake session (AC-002).
/// Each property is null until the corresponding field group is extracted from patient dialogue.
/// PHI stored here must never be emitted to logs — only structural session IDs are logged
/// (AIR guardrails; OWASP A09; HIPAA minimum-necessary).
/// </summary>
public sealed class IntakeFieldState
{
    public string? Demographics { get; set; }
    public string? MedicalHistory { get; set; }
    public string? Medications { get; set; }
    public string? Allergies { get; set; }
    public string? ChiefComplaint { get; set; }
}

/// <summary>
/// Per-session state stored in <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>
/// under <c>intake_session:{SessionId}</c> with a 2-hour TTL (AC-001; checklist: session lifecycle).
/// PHI in <see cref="Fields"/> and raw patient messages in <see cref="History"/> must never be
/// emitted to structured logs — only structural session IDs are logged (AIR guardrails; OWASP A09).
/// </summary>
public sealed class IntakeSessionState
{
    public Guid SessionId { get; set; }

    /// <summary>
    /// Numeric ID of the owning Patient (matches <c>Patient.Id</c> and the JWT <c>sub</c> claim).
    /// Used in <see cref="IntakeSessionService.GetSessionAsync"/> to prevent cross-patient session
    /// access even when the caller supplies a valid sessionId (OWASP A01).
    /// </summary>
    public int PatientId { get; set; }

    /// <summary>
    /// Chronological list of chat messages exchanged with the Ollama model.
    /// The system prompt is prepended on each call by <see cref="OllamaIntakeClient"/>;
    /// it is not stored here to avoid caching large prompt text with every session entry.
    /// Raw patient messages must not be emitted to logs (AIR guardrails; OWASP A09).
    /// </summary>
    public List<OllamaChatMessage> History { get; set; } = new();

    /// <summary>
    /// Structured extraction state accumulated across successive AI turns (AC-002).
    /// Serialised and PHI-encrypted before persisting to <c>intake_records</c> (AC-005).
    /// </summary>
    public IntakeFieldState Fields { get; set; } = new();

    /// <summary>
    /// Logical version stamp set by <c>IntakeModeSwitchService</c> when a mode switch writes a
    /// new reconstructed session state to the cache (us_018; Edge: concurrent auto-save).
    /// The frontend uses this token to gate subsequent <c>POST /intake/draft</c> auto-saves;
    /// a mismatch means a mode switch has occurred and the auto-save should be suppressed.
    /// Null for sessions created by the standard AI intake flow.
    /// </summary>
    public string? CacheVersion { get; set; }

    /// <summary>
    /// Returns <c>true</c> when all 5 required field groups have been extracted (AC-002).
    /// </summary>
    public bool AllFieldsCollected =>
        Fields.Demographics   is not null &&
        Fields.MedicalHistory is not null &&
        Fields.Medications    is not null &&
        Fields.Allergies      is not null &&
        Fields.ChiefComplaint is not null;
}
