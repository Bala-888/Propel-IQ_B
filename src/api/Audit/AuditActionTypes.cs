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
}
