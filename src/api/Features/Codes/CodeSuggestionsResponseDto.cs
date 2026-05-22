namespace Api.Features.Codes;

/// <summary>
/// Top-level response envelope for <c>GET /patients/{id}/code-suggestions</c> (us_043/AC-001).
/// </summary>
public sealed class CodeSuggestionsResponseDto
{
    /// <summary>
    /// Up to 10 ICD-10/CPT code suggestions ordered by confidence descending.
    /// Empty array (never null) when insufficient document data or pipeline failure.
    /// </summary>
    public List<CodeSuggestionDto> Suggestions { get; set; } = new();

    /// <summary>
    /// Human-readable explanation when <see cref="Suggestions"/> is empty due to a
    /// non-error condition (e.g. insufficient chunks).  <see langword="null"/> on success.
    /// </summary>
    public string? Message { get; set; }
}
