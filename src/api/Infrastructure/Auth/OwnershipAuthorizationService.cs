using Api.Constants;
using System.Security.Claims;

namespace Api.Infrastructure.Auth;

/// <summary>
/// Per-record access control for patient endpoints.
/// Patients may only view their own record; Staff and Admin bypass the ownership check
/// and may access any patient record (AC-003; OWASP A01).
/// </summary>
public interface IOwnershipAuthorizationService
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="user"/> is permitted to access the patient
    /// record identified by <paramref name="requestedPatientId"/>.
    /// </summary>
    bool CanAccessPatientRecord(ClaimsPrincipal user, int requestedPatientId);
}

/// <inheritdoc />
public sealed class OwnershipAuthorizationService : IOwnershipAuthorizationService
{
    /// <inheritdoc />
    public bool CanAccessPatientRecord(ClaimsPrincipal user, int requestedPatientId)
    {
        var role = user.FindFirstValue("role");

        // Staff and Admin may access any patient record — role bypass (AC-003; OWASP A01).
        if (role is Roles.Staff or Roles.Admin)
            return true;

        // Patient may only access their own record: the JWT sub claim (User.Id) must match
        // the requested Patient.Id.
        var sub = user.FindFirstValue("sub");
        return sub is not null && sub == requestedPatientId.ToString();
    }
}
