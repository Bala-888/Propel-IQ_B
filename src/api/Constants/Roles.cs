namespace Api.Constants;

/// <summary>
/// Role string constants used in <c>[Authorize(Roles = ...)]</c> decorators and RBAC checks.
/// Using constants eliminates magic strings and ensures a single point of change if roles are
/// ever renamed (AC-001, AC-004; OWASP A01 — consistent access control).
/// </summary>
/// <remarks>
/// Role access matrix:
/// <list type="table">
///   <listheader>
///     <term>Role</term>
///     <description>Permitted access</description>
///   </listheader>
///   <item>
///     <term>Patient</term>
///     <description>
///       Own records only — cannot access other patients' data, staff routes, or admin routes.
///       Ownership is validated per-request by <c>IOwnershipAuthorizationService</c>.
///     </description>
///   </item>
///   <item>
///     <term>Staff</term>
///     <description>
///       Queue management and patient data — may access any patient record; cannot access admin routes.
///     </description>
///   </item>
///   <item>
///     <term>Admin</term>
///     <description>
///       Full system access — all patient records, staff functions, and admin management routes.
///     </description>
///   </item>
/// </list>
/// </remarks>
public static class Roles
{
    /// <summary>Patient role — access limited to own records only.</summary>
    public const string Patient = "Patient";

    /// <summary>Staff role — access to queue management and patient data; no admin capabilities.</summary>
    public const string Staff = "Staff";

    /// <summary>Admin role — full system access including user management and admin metrics.</summary>
    public const string Admin = "Admin";

    /// <summary>Clinician role — read access to patient 360° summaries and clinical data.</summary>
    public const string Clinician = "Clinician";
}
