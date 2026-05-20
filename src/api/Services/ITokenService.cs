using Api.Data.Entities;

namespace Api.Services;

/// <summary>
/// Generates cryptographic tokens for the authentication flow.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a signed HMAC-SHA256 JWT for <paramref name="user"/>.
    /// Claims: <c>sub = userId</c>, <c>role = user.Role</c>, <c>iat</c>, <c>exp = iat + 900s</c> (AC-001).
    /// Key sourced from <c>JWT_SECRET</c> environment variable (OWASP A02).
    /// </summary>
    string GenerateAccessToken(User user);

    /// <summary>
    /// Generates a cryptographically secure opaque refresh token string (64 random bytes, Base64-encoded).
    /// <c>FamilyId</c> management (new vs. inherited) is the caller's responsibility.
    /// </summary>
    string GenerateRefreshToken();
}
