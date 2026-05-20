using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Api.Data.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Api.Services;

/// <summary>
/// Implements <see cref="ITokenService"/> using HMAC-SHA256 JWT and cryptographically
/// secure random refresh tokens. The signing key is loaded once at construction from
/// <c>JWT_SECRET</c> — no secret ever touches source control (OWASP A02).
/// </summary>
public sealed class TokenService : ITokenService
{
    private readonly SymmetricSecurityKey _signingKey;

    public TokenService()
    {
        var secret = Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? throw new InvalidOperationException(
                "JWT_SECRET environment variable is not configured");
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }

    /// <inheritdoc />
    public string GenerateAccessToken(User user)
    {
        var now = DateTime.UtcNow;

        // AC-001: sub, role, iat, exp claims — exp = iat + 900s (15 minutes exactly)
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            // Short-form "role" claim (not ClaimTypes.Role URL) so the SPA can read
            // payload.role with atob/JSON.parse without a library (task_003/AC-002..AC-004).
            // RoleClaimType = "role" is set in TokenValidationParameters so [Authorize(Roles="...")]
            // and User.IsInRole() still work (OWASP A01).
            new Claim("role", user.Role),
            // iat as integer — ClaimValueTypes.Integer64 serialises as JSON number, not string
            new Claim(
                JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
        };

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(15), // exp = iat + 900 (AC-001)
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc />
    public string GenerateRefreshToken()
    {
        // 64 cryptographically random bytes → URL-safe Base64 string (OWASP A02)
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }
}
