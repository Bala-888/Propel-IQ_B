namespace Api.DTOs;

/// <summary>
/// Request body for <c>POST /intake/draft</c> — partial manual intake save triggered when the
/// patient navigates away from an incomplete form (AC-004; us_017).
/// All sections and all fields within sections are nullable so that a save with only 1 of 5
/// sections filled is accepted without validation errors.
/// The same section types as <see cref="CreateManualIntakeRequest"/> are reused
/// (<see cref="DemographicsSection"/>, <see cref="ChiefComplaintSection"/>, etc.) with nullable
/// top-level properties — no mandatory-field validation is applied to draft saves (AC-004).
/// PHI values in this DTO must never be written to structured logs (AIR guardrails; OWASP A09).
/// </summary>
public sealed class SaveDraftRequest
{
    public DemographicsSection?   Demographics   { get; set; }
    public ChiefComplaintSection? ChiefComplaint { get; set; }
    public MedicalHistorySection? MedicalHistory { get; set; }
    public MedicationsSection?    Medications    { get; set; }
    public AllergiesSection?      Allergies      { get; set; }
}
