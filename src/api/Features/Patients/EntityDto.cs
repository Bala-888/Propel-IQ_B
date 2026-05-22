namespace Api.Features.Patients;

/// <summary>
/// Clinical entity extracted from patient documents for the 360° summary (us_040/AC-002; UXR-105).
/// <c>Confidence</c> and <c>LowConfidence</c> are included so the frontend can render
/// text + icon confidence indicators as required by UXR-105.
/// </summary>
public sealed class EntityDto
{
    public Guid Id { get; set; }

    /// <summary>Entity category — one of Diagnosis, Medication, Allergy, Procedure.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Extracted entity value, e.g. "Type 2 Diabetes".
    /// OWASP A02: this field must never be written to any ILogger call — it may contain PHI.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Ollama confidence in [0.0, 1.0] cast to float for JSON payload compactness (UXR-105).</summary>
    public float Confidence { get; set; }

    /// <summary>True when Confidence &lt; 0.5; drives the warning icon on the frontend (UXR-105).</summary>
    public bool LowConfidence { get; set; }

    /// <summary>UTC timestamp of most-recent extraction for this entity.</summary>
    public DateTimeOffset LastSeenAt { get; set; }
}
