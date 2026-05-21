using Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace Api.Infrastructure.Auth;

/// <summary>
/// Validates the JWT role claim against the live DB role on every authenticated request.
/// If the DB role differs from the role in the active JWT (e.g. an admin downgraded the user
/// after the token was issued), the principal's claims are cleared so that the downstream
/// <c>[Authorize]</c> evaluation raises HTTP 401 rather than HTTP 403 — prompting the client
/// to re-authenticate and obtain a fresh token with current claims.
/// (Edge: role changed after token issued; OWASP A01)
/// </summary>
/// <remarks>
/// The DB lookup is cached in <see cref="IMemoryCache"/> with a 60-second sliding expiration
/// to prevent a DB hit on every request while still detecting role changes within a
/// reasonable window (performance; Edge: role changed after token).
/// </remarks>
public sealed class DatabaseRoleClaimsTransformation : IClaimsTransformation
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public DatabaseRoleClaimsTransformation(AppDbContext db, IMemoryCache cache)
    {
        _db    = db;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Only run for authenticated principals — skip anonymous requests.
        if (principal.Identity?.IsAuthenticated is not true)
            return principal;

        var sub = principal.FindFirstValue("sub");
        if (!int.TryParse(sub, out var userId))
            return principal;

        // Cache the DB role lookup with a 60-second sliding expiration (Edge: role changed after token).
        var dbRole = await _cache.GetOrCreateAsync($"user_role:{userId}", async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromSeconds(60);
            return await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => (string?)u.Role)
                .FirstOrDefaultAsync();
        });

        var jwtRole = principal.FindFirstValue("role");

        // If the DB role is absent (user deleted) or differs from the JWT role claim,
        // return an unauthenticated principal so [Authorize] triggers a 401 challenge
        // rather than a 403 — the client must re-authenticate to obtain fresh claims
        // (Edge: role changed after token issued; OWASP A01).
        if (dbRole is null || !string.Equals(dbRole, jwtRole, StringComparison.Ordinal))
            return new ClaimsPrincipal(new ClaimsIdentity()); // unauthenticated → 401

        return principal;
    }
}
