namespace Upacip.Api.Tests.Services.Intake;

/// <summary>
/// Test plan for EP-003: Patient Intake (AI Conversational and Manual).
/// Stories: US-009 (AI intake), US-010 (manual intake), US-011 (mode switch),
///          US-012 (validation failure and draft save).
///
/// Implementation target: Sprint 3.
/// </summary>
public sealed class IntakeServiceTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // US-010 — Manual Intake Validation
    // ═══════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-INT-001: Submit with missing required field returns field-level error (not generic)")]
    public void ManualIntake_MissingRequiredField_ReturnsFieldLevelError()
        => throw new NotImplementedException("Sprint 3 — IntakeValidationService");

    [Fact(DisplayName = "TC-INT-002: Submit with invalid DOB format returns correction hint")]
    public void ManualIntake_InvalidDobFormat_ReturnsCorrectionHint()
        => throw new NotImplementedException("Sprint 3");

    [Fact(DisplayName = "TC-INT-003: Failed submission does not clear entered data (all values preserved)")]
    public void ManualIntake_FailedSubmit_DataPreserved()
        => throw new NotImplementedException("Sprint 3");

    [Fact(DisplayName = "TC-INT-004: Successful submission creates IntakeRecord with mode=Manual, status=Complete")]
    public void ManualIntake_ValidSubmit_CreatesCompleteRecord()
        => throw new NotImplementedException("Sprint 3");

    [Fact(DisplayName = "TC-INT-005: IntakeRecord.DataEncrypted is stored as ciphertext not plaintext")]
    public void ManualIntake_DataIsEncrypted()
        => throw new NotImplementedException("Sprint 3 — PHI encryption verification");

    // ═══════════════════════════════════════════════════════════════════════
    // US-011 — Intake Mode Switch
    // ═══════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-INT-006: Switching from AI to Manual pre-populates 3 collected AI fields")]
    public void ModeSwitch_AiToManual_PrePopulatesFields()
        => throw new NotImplementedException("Sprint 3 — IntakeModeSwitchService");

    [Fact(DisplayName = "TC-INT-007: Switching from Manual to AI pre-populates AI summary")]
    public void ModeSwitch_ManualToAi_PrePopulatesAiSummary()
        => throw new NotImplementedException("Sprint 3");

    [Fact(DisplayName = "TC-INT-008: Mode switch does not create new session or change sessionId")]
    public void ModeSwitch_PreservesSessionId()
        => throw new NotImplementedException("Sprint 3");

    [Fact(DisplayName = "TC-INT-009: Mode switch writes INTAKE_MODE_SWITCHED audit log entry")]
    public void ModeSwitch_WritesAuditLog()
        => throw new NotImplementedException("Sprint 3");

    // ═══════════════════════════════════════════════════════════════════════
    // US-012 — Draft Save
    // ═══════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-INT-010: POST /intake/draft saves partial data with status=Draft")]
    public void DraftSave_PartialData_StatusIsDraft()
        => throw new NotImplementedException("Sprint 3");

    [Fact(DisplayName = "TC-INT-011: Draft is stored with PHI fields encrypted")]
    public void DraftSave_PhiFieldsAreEncrypted()
        => throw new NotImplementedException("Sprint 3");

    [Fact(DisplayName = "TC-INT-012: Submit incomplete form → no IntakeRecord with status=Complete is created")]
    public void ManualIntake_IncompleteSubmit_NoCompleteRecordCreated()
        => throw new NotImplementedException("Sprint 3");
}
