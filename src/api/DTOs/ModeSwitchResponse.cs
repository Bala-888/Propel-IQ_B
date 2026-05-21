namespace Api.DTOs;

/// <summary>
/// Response body for <c>POST /intake/mode-switch</c> (us_018; AC-001; AC-002; AC-003).
///
/// <para>
/// <c>MappedFields</c> shape depends on the switch direction:
/// <list type="bullet">
///   <item>AI → Manual: a <see cref="SaveDraftRequest"/>-shaped object with directly mapped fields
///     pre-populated (e.g., <c>chiefComplaint.description</c>); unmapped AI free-text content appears
///     in <c>ReviewItems</c> (AC-001; AC-003).</item>
///   <item>Manual → AI: an <c>IntakeFieldState</c>-shaped object showing which fields were
///     pre-populated in the new AI session (AC-002).</item>
/// </list>
/// </para>
///
/// <para>
/// <c>ReviewItems</c> is always present (even when empty) — the array holds labelled AI free-text
/// content that could not be mapped to a named manual field path (AC-003).
/// </para>
///
/// <para>
/// <c>CacheVersion</c> is a one-time GUID the frontend stores and compares before making
/// subsequent <c>POST /intake/draft</c> auto-saves; a version mismatch indicates a mode switch
/// has occurred and the auto-save should be suppressed (Edge: concurrent auto-save; OWASP A04).
/// </para>
/// </summary>
public sealed class ModeSwitchResponse
{
    /// <summary>
    /// Pre-populated intake data in the target mode's field shape.
    /// AI → Manual: <c>SaveDraftRequest</c>; Manual → AI: <c>IntakeFieldState</c>.
    /// </summary>
    public object? MappedFields { get; set; }

    /// <summary>
    /// AI free-text content that has no corresponding named manual field path (AC-003).
    /// Always present; empty array when all content was mapped or the source was empty.
    /// </summary>
    public IReadOnlyList<string> ReviewItems { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Logical version stamp (GUID string) written into the cache entry by
    /// <c>IntakeModeSwitchService</c>; the frontend uses this to gate auto-save calls after
    /// a mode switch (Edge: concurrent auto-save; OWASP A04).
    /// </summary>
    public string CacheVersion { get; set; } = string.Empty;

    /// <summary>
    /// New AI session ID — present only for <c>Manual → AI</c> switches, where a new
    /// <c>IntakeSessionState</c> is created in <c>IDistributedCache</c>.
    /// Null for <c>AI → Manual</c> switches.
    /// </summary>
    public string? NewSessionId { get; set; }
}
