using System.Globalization;
using Api.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Api.Services;

/// <summary>
/// Validates a <see cref="CreateManualIntakeRequest"/> before any encryption or database write.
/// All errors across all 5 sections are collected before returning — the caller receives a complete
/// <see cref="ValidationProblemDetails"/> with field-level paths rather than only the first failure
/// (AC-003; OWASP A03 — validate at the system boundary).
///
/// <para>Date fields are validated against <c>yyyy-MM-dd</c> and <c>MM/dd/yyyy</c> using
/// <see cref="DateOnly.TryParseExact"/> with <see cref="CultureInfo.InvariantCulture"/> to avoid
/// locale-dependent parsing; any other format produces a per-field error
/// "Please enter a valid date" without affecting other fields (Edge: invalid date; OWASP A03).</para>
///
/// <para>A medication item with a name but no dosage is NOT a validation error — the submission
/// proceeds and the advisory string <c>"Consider adding dosage for clarity"</c> is added to the
/// <paramref name="warnings"/> list for inclusion in the 201 response body
/// (Edge: brand-only medication; AC-002).</para>
///
/// PHI field values are never written to any log in this class (AIR guardrails; OWASP A09;
/// HIPAA minimum-necessary).
/// </summary>
public static class ManualIntakeValidator
{
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "MM/dd/yyyy"];

    /// <summary>
    /// Validates <paramref name="request"/> across all 5 sections.
    /// </summary>
    /// <param name="request">The complete manual intake submission to validate.</param>
    /// <param name="problem">
    /// On failure: a <see cref="ValidationProblemDetails"/> with field-level error paths.
    /// <c>null</c> when validation passes.
    /// </param>
    /// <param name="warnings">
    /// Advisory strings that accompany a successful (201) response, e.g. brand-only medication
    /// notices. Empty when no advisories apply.
    /// </param>
    /// <returns><c>true</c> when validation passes; <c>false</c> when one or more errors exist.</returns>
    public static bool TryValidate(
        CreateManualIntakeRequest request,
        out ValidationProblemDetails? problem,
        out List<string> warnings)
    {
        warnings = [];
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        // ── Demographics mandatory fields ──────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(request.Demographics.FirstName))
            AddError(errors, "demographics.firstName", "First name is required.");

        if (string.IsNullOrWhiteSpace(request.Demographics.LastName))
            AddError(errors, "demographics.lastName", "Last name is required.");

        if (string.IsNullOrWhiteSpace(request.Demographics.DateOfBirth))
        {
            AddError(errors, "demographics.dateOfBirth", "Date of birth is required.");
        }
        else if (!TryParseDate(request.Demographics.DateOfBirth))
        {
            // Edge: invalid date format — per-field error; other fields are not affected (AC-003)
            AddError(errors, "demographics.dateOfBirth", "Please enter a valid date");
        }

        if (string.IsNullOrWhiteSpace(request.Demographics.Gender))
            AddError(errors, "demographics.gender", "Gender is required.");

        // ── Chief Complaint — mandatory ────────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(request.ChiefComplaint.Description))
            AddError(errors, "chiefComplaint.description", "Chief complaint is required.");

        // ── Medical History — optional section; date fields validated when present ───────────
        if (!string.IsNullOrWhiteSpace(request.MedicalHistory.LastPhysicalExam)
            && !TryParseDate(request.MedicalHistory.LastPhysicalExam))
        {
            AddError(errors, "medicalHistory.lastPhysicalExam", "Please enter a valid date");
        }

        if (request.MedicalHistory.Conditions is not null)
        {
            for (int i = 0; i < request.MedicalHistory.Conditions.Count; i++)
            {
                var cond = request.MedicalHistory.Conditions[i];
                if (string.IsNullOrWhiteSpace(cond.Name))
                    AddError(errors, $"medicalHistory.conditions[{i}].name", "Condition name is required.");

                if (!string.IsNullOrWhiteSpace(cond.DiagnosedDate) && !TryParseDate(cond.DiagnosedDate))
                    AddError(errors, $"medicalHistory.conditions[{i}].diagnosedDate", "Please enter a valid date");
            }
        }

        if (request.MedicalHistory.Surgeries is not null)
        {
            for (int i = 0; i < request.MedicalHistory.Surgeries.Count; i++)
            {
                var surgery = request.MedicalHistory.Surgeries[i];
                if (string.IsNullOrWhiteSpace(surgery.Name))
                    AddError(errors, $"medicalHistory.surgeries[{i}].name", "Surgery name is required.");

                if (!string.IsNullOrWhiteSpace(surgery.Date) && !TryParseDate(surgery.Date))
                    AddError(errors, $"medicalHistory.surgeries[{i}].date", "Please enter a valid date");
            }
        }

        // ── Medications — brand-only advisory (not a validation error; AC-002 Edge) ───────────
        if (request.Medications.Items is not null)
        {
            // Detect any medication with a name but no dosage — advisory only, does not block submit
            bool hasBrandOnly = request.Medications.Items.Any(
                m => !string.IsNullOrWhiteSpace(m.Name) && string.IsNullOrWhiteSpace(m.Dosage));

            if (hasBrandOnly)
                warnings.Add("Consider adding dosage for clarity");
        }

        // ── Allergies — allergen name required per item when the list is present ────────────
        if (request.Allergies.Items is not null)
        {
            for (int i = 0; i < request.Allergies.Items.Count; i++)
            {
                var allergy = request.Allergies.Items[i];
                if (string.IsNullOrWhiteSpace(allergy.Allergen))
                    AddError(errors, $"allergies.items[{i}].allergen", "Allergen name is required.");
            }
        }

        if (errors.Count > 0)
        {
            problem = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title  = "One or more validation errors occurred.",
            };
            return false;
        }

        problem = null;
        return true;
    }

    // ── Private helpers ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> when <paramref name="value"/> matches <c>yyyy-MM-dd</c> or
    /// <c>MM/dd/yyyy</c> exactly (invariant culture, no partial matches).
    /// </summary>
    private static bool TryParseDate(string value)
        => DateOnly.TryParseExact(
            value.Trim(),
            DateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);

    private static void AddError(Dictionary<string, string[]> errors, string key, string message)
    {
        if (errors.TryGetValue(key, out var existing))
            errors[key] = [.. existing, message];
        else
            errors[key] = [message];
    }
}
