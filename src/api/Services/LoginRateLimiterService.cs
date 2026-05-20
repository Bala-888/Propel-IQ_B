using System.Text;
using Microsoft.Extensions.Caching.Distributed;

namespace Api.Services;

/// <summary>
/// <see cref="ILoginRateLimiter"/> implementation backed by <see cref="IDistributedCache"/>.
/// Counter is stored as a UTF-8 integer string under the key <c>login_fail:{ip}</c>.
///
/// Each call to <see cref="IncrementAsync"/> resets the absolute expiry to 15 minutes from
/// the most recent failure — satisfying AC-004 (15-minute window) and AC-005 (counter
/// auto-expires without a scheduled cleanup job).
///
/// <para>
/// <b>Multi-instance deployments:</b> Register <c>AddStackExchangeRedisCache</c> in
/// <c>Program.cs</c> to share the counter across load-balanced instances (Edge: the
/// in-process <c>AddDistributedMemoryCache</c> used in development is per-instance and
/// would allow bypass by hitting a different node).
/// </para>
/// </summary>
public sealed class LoginRateLimiterService : ILoginRateLimiter
{
    private readonly IDistributedCache _cache;

    public LoginRateLimiterService(IDistributedCache cache)
    {
        _cache = cache;
    }

    // Cache key is intentionally simple; no sensitive data included — the IP is already
    // visible in access logs (not a new data disclosure) (OWASP A02).
    private static string CacheKey(string ip) => $"login_fail:{ip}";

    /// <inheritdoc />
    public async Task<int> GetFailCountAsync(string ip)
    {
        var bytes = await _cache.GetAsync(CacheKey(ip));
        if (bytes is null) return 0;
        // Safe parse: cache was written by IncrementAsync which always stores a valid integer.
        return int.TryParse(Encoding.UTF8.GetString(bytes), out var count) ? count : 0;
    }

    /// <inheritdoc />
    public async Task IncrementAsync(string ip)
    {
        var current  = await GetFailCountAsync(ip);
        var newCount = current + 1;

        // AbsoluteExpirationRelativeToNow restarts the 15-minute window on every failure write
        // (AC-004). Overwriting the entry with SetAsync replaces both value and expiry.
        await _cache.SetAsync(
            CacheKey(ip),
            Encoding.UTF8.GetBytes(newCount.ToString()),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
            });
    }

    /// <inheritdoc />
    public async Task ResetAsync(string ip)
    {
        // RemoveAsync deletes the key entirely — frees cache space immediately rather
        // than leaving a zero-valued entry until expiry (AC-005; checklist: RemoveAsync not Set-to-0).
        await _cache.RemoveAsync(CacheKey(ip));
    }
}
