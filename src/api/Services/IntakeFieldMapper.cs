using Api.AI;
using Api.DTOs;

namespace Api.Services;

/// <summary>
/// Static bidirectional field-mapping table between <see cref="IntakeSessionState"/> field keys
/// and manual intake field paths (us_018; AC-001; AC-002; AC-003).
///
/// <para>
/// AI → Manual direction: only <c>chiefComplaint</c> has a 1:1 mapping to a named manual field
/// (<c>chiefComplaint.description</c>). The remaining four free-text blobs (<c>demographics</c>,
/// <c>medicalHistory</c>, <c>medications</c>, <c>allergies</c>) have no direct structural
/// mapping and are surfaced as labelled entries in the <c>reviewItems[]</c> array (AC-003).
/// </para>
///
/// <para>
/// Manual → AI direction: each structured section is serialised to a free-text summary that is
/// compatible with the Ollama dialogue context format already stored in
/// <see cref="IntakeFieldState"/> (AC-002).
/// </para>
/// </summary>
public static class IntakeFieldMapper
{
    // ── AI-field key constants ────────────────────────────────────────────────────────────────────

    private const string KeyChiefComplaint = "chiefComplaint";
    private const string KeyDemographics   = "demographics";
    private const string KeyMedicalHistory = "medicalHistory";
    private const string KeyMedications    = "medications";
    private const string KeyAllergies      = "allergies";

    // ── Public mapping tables ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// AI session field key → ManualIntakeData field path for directly-mapped fields.
    /// Keys absent from this dictionary land in <c>reviewItems[]</c> (AC-003).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> AiToManualPaths =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { KeyChiefComplaint, "chiefComplaint.description" },
            // demographics / medicalHistory / medications / allergies have no direct path:
            // the AI stores them as free-text blobs; the manual form expects structured objects.
            // These are therefore unmapped → reviewItems[].
        };

    /// <summary>
    /// AI field keys whose content is unmapped and goes to <c>reviewItems[]</c> (AC-003).
    /// </summary>
    public static readonly IReadOnlySet<string> ReviewFieldKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            KeyDemographics,
            KeyMedicalHistory,
            KeyMedications,
            KeyAllergies,
        };

    // ── AI → Manual ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Maps an AI <see cref="IntakeFieldState"/> to a <see cref="SaveDraftRequest"/> and
    /// collects unmapped content into <c>reviewItems[]</c> (AC-001; AC-003).
    /// </summary>
    /// <param name="fields">AI session field state — all properties are free-text strings.</param>
    /// <returns>
    /// A tuple of:
    /// <list type="bullet">
    ///   <item><c>Draft</c> — <see cref="SaveDraftRequest"/> with directly mapped sections populated.</item>
    ///   <item><c>ReviewItems</c> — labelled strings for content that could not be mapped.</item>
    /// </list>
    /// </returns>
    public static (SaveDraftRequest Draft, IReadOnlyList<string> ReviewItems) MapAiToManual(
        IntakeFieldState fields)
    {
        var draft   = new SaveDraftRequest();
        var reviews = new List<string>();

        // chiefComplaint → chiefComplaint.description (sole direct mapping; AiToManualPaths)
        if (!string.IsNullOrWhiteSpace(fields.ChiefComplaint))
            draft.ChiefComplaint = new ChiefComplaintSection { Description = fields.ChiefComplaint };

        // Unmapped free-text blobs → reviewItems[] (AC-003)
        if (!string.IsNullOrWhiteSpace(fields.Demographics))
            reviews.Add($"Demographics: {fields.Demographics}");

        if (!string.IsNullOrWhiteSpace(fields.MedicalHistory))
            reviews.Add($"Medical history: {fields.MedicalHistory}");

        if (!string.IsNullOrWhiteSpace(fields.Medications))
            reviews.Add($"Medications: {fields.Medications}");

        if (!string.IsNullOrWhiteSpace(fields.Allergies))
            reviews.Add($"Allergies: {fields.Allergies}");

        return (draft, reviews);
    }

    // ── Manual → AI ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Maps a <see cref="SaveDraftRequest"/> to an <see cref="IntakeFieldState"/> for
    /// the Manual→AI switch direction (AC-002).
    /// Structured section objects are serialised to free-text summaries that match the
    /// Ollama extraction format already used by the AI dialogue service.
    /// PHI values are transferred as-is; they are never written to logs here (OWASP A09).
    /// </summary>
    /// <param name="draft">Partially or fully completed manual intake draft.</param>
    /// <returns>
    /// <see cref="IntakeFieldState"/> with populated string fields wherever source data exists.
    /// Null fields are left null so the AI dialogue can re-ask for missing information.
    /// </returns>
    public static IntakeFieldState MapManualToAi(SaveDraftRequest draft)
    {
        var fields = new IntakeFieldState();

        // chiefComplaint.description → ChiefComplaint (direct 1:1)
        if (draft.ChiefComplaint?.Description is { Length: > 0 } cc)
            fields.ChiefComplaint = cc;

        // Demographics section → free-text summary
        if (draft.Demographics is not null)
        {
            var parts = new List<string>(6);
            if (!string.IsNullOrWhiteSpace(draft.Demographics.FirstName))
                parts.Add(draft.Demographics.FirstName!);
            if (!string.IsNullOrWhiteSpace(draft.Demographics.LastName))
                parts.Add(draft.Demographics.LastName!);
            if (!string.IsNullOrWhiteSpace(draft.Demographics.DateOfBirth))
                parts.Add($"DOB: {draft.Demographics.DateOfBirth}");
            if (!string.IsNullOrWhiteSpace(draft.Demographics.Gender))
                parts.Add($"Gender: {draft.Demographics.Gender}");
            if (!string.IsNullOrWhiteSpace(draft.Demographics.Phone))
                parts.Add($"Phone: {draft.Demographics.Phone}");
            if (!string.IsNullOrWhiteSpace(draft.Demographics.Address))
                parts.Add($"Address: {draft.Demographics.Address}");
            if (parts.Count > 0)
                fields.Demographics = string.Join(", ", parts);
        }

        // MedicalHistory section → free-text summary
        if (draft.MedicalHistory is not null)
        {
            var sb = new System.Text.StringBuilder();
            if (draft.MedicalHistory.Conditions?.Count > 0)
            {
                var names = draft.MedicalHistory.Conditions
                    .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => c.DiagnosedDate is { Length: > 0 }
                        ? $"{c.Name} (diagnosed {c.DiagnosedDate})"
                        : c.Name!);
                var joined = string.Join(", ", names);
                if (joined.Length > 0)
                    sb.Append("Conditions: ").Append(joined).Append(". ");
            }
            if (draft.MedicalHistory.Surgeries?.Count > 0)
            {
                var names = draft.MedicalHistory.Surgeries
                    .Where(s => !string.IsNullOrWhiteSpace(s.Name))
                    .Select(s => s.Date is { Length: > 0 }
                        ? $"{s.Name} ({s.Date})"
                        : s.Name!);
                var joined = string.Join(", ", names);
                if (joined.Length > 0)
                    sb.Append("Surgeries: ").Append(joined).Append(". ");
            }
            if (!string.IsNullOrWhiteSpace(draft.MedicalHistory.LastPhysicalExam))
                sb.Append($"Last physical exam: {draft.MedicalHistory.LastPhysicalExam}.");
            var summary = sb.ToString().Trim();
            if (summary.Length > 0)
                fields.MedicalHistory = summary;
        }

        // Medications section → free-text summary
        if (draft.Medications?.Items?.Count > 0)
        {
            var entries = draft.Medications.Items
                .Where(m => !string.IsNullOrWhiteSpace(m.Name))
                .Select(m =>
                {
                    var parts = new List<string> { m.Name! };
                    if (!string.IsNullOrWhiteSpace(m.Dosage))     parts.Add(m.Dosage!);
                    if (!string.IsNullOrWhiteSpace(m.Frequency))  parts.Add(m.Frequency!);
                    return string.Join(" ", parts);
                });
            var summary = string.Join("; ", entries);
            if (summary.Length > 0)
                fields.Medications = summary;
        }

        // Allergies section → free-text summary
        if (draft.Allergies?.Items?.Count > 0)
        {
            var entries = draft.Allergies.Items
                .Where(a => !string.IsNullOrWhiteSpace(a.Allergen))
                .Select(a => a.Reaction is { Length: > 0 }
                    ? $"{a.Allergen} (reaction: {a.Reaction})"
                    : a.Allergen!);
            var summary = string.Join("; ", entries);
            if (summary.Length > 0)
                fields.Allergies = summary;
        }

        return fields;
    }
}
