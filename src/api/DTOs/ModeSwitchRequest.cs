namespace Api.DTOs;

/// <summary>
/// Request body for <c>POST /intake/mode-switch</c> (us_018).
/// <para>
/// Validation rules enforced at the API boundary (OWASP A03):
/// <list type="bullet">
///   <item><c>From</c> and <c>To</c> must each be <c>"AI"</c> or <c>"Manual"</c>.</item>
///   <item><c>From</c> and <c>To</c> must not be equal.</item>
///   <item><c>SessionId</c> is required when <c>From = "AI"</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class ModeSwitchRequest
{
    /// <summary>Source intake mode — <c>"AI"</c> or <c>"Manual"</c>.</summary>
    public string From      { get; set; } = string.Empty;

    /// <summary>Target intake mode — <c>"AI"</c> or <c>"Manual"</c>.</summary>
    public string To        { get; set; } = string.Empty;

    /// <summary>
    /// AI session ID — required when <c>From = "AI"</c> (identifies which
    /// <see cref="Api.AI.IntakeSessionState"/> to read from <c>IDistributedCache</c>).
    /// Null when <c>From = "Manual"</c> (source data is the DB Draft record).
    /// </summary>
    public string? SessionId { get; set; }
}
