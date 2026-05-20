namespace Api.Exceptions;

/// <summary>
/// Raised by <c>UserManagementService.CreateAsync</c> and <c>WalkInService.CreateWithAccountAsync</c>
/// when the supplied email address is already registered as a Username in the Users table.
/// Mapped to HTTP 409 by <c>AdminUsersController</c> and <c>WalkInsController</c>.
/// The exception message intentionally omits whether the existing account is active or
/// deactivated to prevent account-status enumeration (AC-005; OWASP A07).
/// </summary>
public sealed class DuplicateEmailException : Exception
{
    /// <summary>
    /// The Id of the existing user, when the caller needs to expose it for a link-existing flow.
    /// Null when the context does not require caller to surface the existing account id
    /// (e.g. admin user creation — OWASP A07: avoid unnecessary disclosure).
    /// </summary>
    public int? ExistingUserId { get; }

    public DuplicateEmailException(string email, int? existingUserId = null)
        : base($"A user with this email already exists: {email}")
    {
        ExistingUserId = existingUserId;
    }
}
