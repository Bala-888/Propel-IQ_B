namespace Api.Audit;

/// <summary>
/// String constants for all auditable action types required by HIPAA §164.312(b) and AC-005.
/// Use these constants at every <c>IAuditLogger.RecordAsync</c> call site —
/// no inline string literals for action types anywhere in the codebase (AC-005; maintainability).
/// </summary>
public static class AuditActionTypes
{
    public const string LoginSuccess          = "LoginSuccess";
    public const string LoginFailure          = "LoginFailure";
    public const string Registration          = "Registration";
    public const string PatientDataAccess     = "PatientDataAccess";
    public const string BookingCreate         = "BookingCreate";
    public const string BookingModify         = "BookingModify";
    public const string DocumentUpload        = "DocumentUpload";
    public const string CodeSuggestionAccept  = "CodeSuggestionAccept";
    public const string CodeSuggestionReject  = "CodeSuggestionReject";
    public const string AdminUserCRUD         = "AdminUserCRUD";
    public const string UnauthorizedAccess    = "UnauthorizedAccess";
    // AI intake confirmation audit event (us_016-II/AC-003; OWASP A09)
    public const string IntakeCompleted       = "IntakeCompleted";
    // Insurance pre-check audit event (us_023/AC-004; OWASP A09)
    public const string InsurancePreCheck     = "InsurancePreCheck";
    // Notification/calendar-sync preference updated (us_029/AC-003; OWASP A02 — field name only, no value)
    public const string PreferenceUpdated     = "PreferenceUpdated";
    // Walk-in booking created by staff (us_030/AC-003; OWASP A02 — staffId + bookingId only, no PHI)
    public const string WalkinBookingCreated  = "WalkinBookingCreated";
    // Walk-in patient record created by staff (us_030/AC-004; OWASP A02 — staffId + patientId only, no PHI)
    public const string WalkinPatientCreated  = "WalkinPatientCreated";
    // Patient marked as arrived by staff (us_032/AC-002; OWASP A02 — staffId + bookingId only, no PHI)
    public const string PatientMarkedArrived  = "PatientMarkedArrived";
    // Clinical conflict resolved by staff/clinician (us_042/AC-002; OWASP A02 — conflictId only, no PHI)
    public const string ConflictResolved      = "ConflictResolved";
    // Clinical conflict dismissed by staff/clinician (us_042/AC-003; OWASP A02 — conflictId only, no PHI)
    public const string ConflictDismissed     = "ConflictDismissed";
    // RAG-generated code suggestion accepted by clinician (us_044/AC-001; OWASP A02 — suggestionId only, no PHI)
    public const string MedicalCodeAccepted   = "MedicalCodeAccepted";
    // RAG-generated code suggestion rejected by clinician (us_044/AC-001; OWASP A02 — suggestionId only, no PHI)
    public const string MedicalCodeRejected   = "MedicalCodeRejected";
    // RAG-generated code suggestion corrected by clinician (us_044/AC-001; OWASP A02 — suggestionId only, no PHI)
    public const string MedicalCodeCorrected  = "MedicalCodeCorrected";
}
