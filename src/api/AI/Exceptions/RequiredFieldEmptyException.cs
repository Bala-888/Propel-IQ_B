namespace Api.AI.Exceptions;

/// <summary>
/// Thrown by <see cref="IntakeSessionService.PatchFieldAsync"/> when the caller attempts to
/// set a required intake field (listed in <see cref="RequiredIntakeFields.RequiredPaths"/>) to
/// an empty or whitespace-only value (AC-002; Edge: empty required field).
/// Caught exclusively in <see cref="Controllers.IntakeAiController.PatchFieldAsync"/> — mapped to
/// HTTP 400; must not reach the global exception handler (OWASP A05; checklist).
/// </summary>
public sealed class RequiredFieldEmptyException : Exception
{
    /// <summary>The camelCase field path that was submitted as empty (e.g., <c>"chiefComplaint"</c>).</summary>
    public string FieldPath { get; }

    public RequiredFieldEmptyException(string fieldPath)
        : base(BuildMessage(fieldPath))
    {
        FieldPath = fieldPath;
    }

    private static string BuildMessage(string fieldPath) =>
        fieldPath.ToLowerInvariant() switch
        {
            "chiefcomplaint" => "Chief complaint cannot be empty",
            "demographics"   => "Demographics cannot be empty",
            "medicalhistory" => "Medical history cannot be empty",
            "medications"    => "Medications cannot be empty",
            "allergies"      => "Allergies cannot be empty",
            _                => $"{fieldPath} cannot be empty",
        };
}
