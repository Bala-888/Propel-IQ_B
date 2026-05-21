namespace Api.AI;

/// <summary>
/// Immutable set of field paths in <see cref="IntakeFieldState"/> that must not be set to an
/// empty or whitespace-only string via <c>PATCH /intake/ai/field</c> (AC-002; Edge: empty required field).
/// Adding a new required field requires a change to this one class only (checklist: maintainability).
/// The set is <see cref="IReadOnlySet{T}"/> — immutable at runtime (checklist).
/// </summary>
public static class RequiredIntakeFields
{
    /// <summary>
    /// The set of field paths that are mandatory and must not be patched to empty.
    /// Field paths match property names on <see cref="IntakeFieldState"/> with camelCase convention
    /// (e.g., <c>"chiefComplaint"</c>, <c>"demographics"</c>).
    /// </summary>
    public static readonly IReadOnlySet<string> RequiredPaths =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "chiefComplaint",
        };
}
