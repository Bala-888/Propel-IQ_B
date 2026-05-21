namespace Api.DTOs;

/// <summary>
/// Request body for <c>POST /intake/manual</c> — complete 5-section manual intake submission
/// (AC-002; AC-003; us_017).
/// All section objects are non-nullable; individual field nullability is checked by
/// <see cref="Api.Services.ManualIntakeValidator"/> so that every validation error is collected
/// before returning 422 rather than stopping at the first failure (AC-003; OWASP A03).
/// Date fields are typed as <c>string?</c> to allow format validation in the validator
/// (<c>YYYY-MM-DD</c> or <c>MM/DD/YYYY</c>); ASP.NET model binding would silently coerce an
/// out-of-range <c>DateOnly</c> and lose the raw value needed for the error message.
/// PHI values in this DTO must never be written to structured logs — only structural IDs are
/// emitted (AIR guardrails; OWASP A09; HIPAA minimum-necessary).
/// </summary>
public sealed class CreateManualIntakeRequest
{
    public DemographicsSection     Demographics   { get; set; } = new();
    public ChiefComplaintSection   ChiefComplaint { get; set; } = new();
    public MedicalHistorySection   MedicalHistory { get; set; } = new();
    public MedicationsSection      Medications    { get; set; } = new();
    public AllergiesSection        Allergies      { get; set; } = new();
}

// ── Section types ─────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Patient demographic fields.
/// <c>FirstName</c>, <c>LastName</c>, <c>DateOfBirth</c>, and <c>Gender</c> are mandatory for a
/// complete submission; <c>Phone</c> and <c>Address</c> are optional (AC-003).
/// </summary>
public sealed class DemographicsSection
{
    public string? FirstName   { get; set; }
    public string? LastName    { get; set; }
    /// <summary>
    /// Date string in <c>yyyy-MM-dd</c> or <c>MM/dd/yyyy</c> format.
    /// Validated by <see cref="Api.Services.ManualIntakeValidator"/> — any other format yields a
    /// per-field 422 error "Please enter a valid date" (Edge: invalid date; AC-003; OWASP A03).
    /// </summary>
    public string? DateOfBirth { get; set; }
    public string? Gender      { get; set; }
    public string? Phone       { get; set; }
    public string? Address     { get; set; }
}

/// <summary>Chief complaint section — <c>Description</c> is mandatory (AC-003).</summary>
public sealed class ChiefComplaintSection
{
    public string? Description { get; set; }
}

/// <summary>
/// Patient medical history.
/// All fields are optional at the section level; if a <see cref="ConditionItem"/> or
/// <see cref="SurgeryItem"/> is included, its <c>Name</c> is mandatory.
/// Date fields in child items are validated for <c>yyyy-MM-dd</c> / <c>MM/dd/yyyy</c> format
/// (Edge: invalid date; OWASP A03).
/// </summary>
public sealed class MedicalHistorySection
{
    public List<ConditionItem>? Conditions      { get; set; }
    public List<SurgeryItem>?   Surgeries       { get; set; }
    /// <summary>Date of last physical exam — optional; format validated if provided.</summary>
    public string?              LastPhysicalExam { get; set; }
}

/// <summary>Medical condition entry within <see cref="MedicalHistorySection"/>.</summary>
public sealed class ConditionItem
{
    public string? Name         { get; set; }
    /// <summary>Diagnosis date string — optional; format validated if non-empty.</summary>
    public string? DiagnosedDate { get; set; }
}

/// <summary>Surgical history entry within <see cref="MedicalHistorySection"/>.</summary>
public sealed class SurgeryItem
{
    public string? Name { get; set; }
    /// <summary>Surgery date string — optional; format validated if non-empty.</summary>
    public string? Date { get; set; }
}

/// <summary>
/// Current medications section.
/// A medication item with a non-empty <c>Name</c> but no <c>Dosage</c> is accepted (not a 422)
/// and produces an advisory warning in the response body:
/// <c>"Consider adding dosage for clarity"</c> (Edge: brand-only medication; AC-002).
/// </summary>
public sealed class MedicationsSection
{
    public List<MedicationItem>? Items { get; set; }
}

/// <summary>Medication entry within <see cref="MedicationsSection"/>.</summary>
public sealed class MedicationItem
{
    public string? Name      { get; set; }
    public string? Dosage    { get; set; }
    public string? Frequency { get; set; }
}

/// <summary>Known allergies section.</summary>
public sealed class AllergiesSection
{
    public List<AllergyItem>? Items { get; set; }
}

/// <summary>Allergy entry within <see cref="AllergiesSection"/>.</summary>
public sealed class AllergyItem
{
    public string? Allergen { get; set; }
    public string? Reaction { get; set; }
}
