namespace Api.DTOs;

/// <summary>
/// Partial update body for <c>PATCH /admin/users/{id}</c>.
/// At least one property must be non-null; the controller validates this before delegating
/// to the service (AC-002, AC-003). Role, when present, is validated against the allowed
/// set in the controller to return <c>{"role": "Invalid role value"}</c> (Edge: invalid role).
/// </summary>
public sealed class PatchUserRequest
{
    /// <summary>
    /// New role assignment. Must be one of <c>Staff</c> or <c>Admin</c>
    /// when present. Validated in the controller before the service is called (AC-002).
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Activation flag. <c>false</c> deactivates the account (AC-003);
    /// <c>true</c> re-enables authentication (Edge: reactivation).
    /// </summary>
    public bool? IsActive { get; set; }
}
