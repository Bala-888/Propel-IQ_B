namespace Api.Services;

/// <summary>
/// Tracks consecutive failed login attempts per client IP using a distributed cache.
/// Used to enforce the AC-004 requirement: after 5 failures within 15 minutes,
/// subsequent attempts return HTTP 429.
/// </summary>
public interface ILoginRateLimiter
{
    /// <summary>
    /// Returns the current consecutive-failure count for <paramref name="ip"/>.
    /// Returns 0 if no entry exists (first attempt or window has expired).
    /// </summary>
    Task<int> GetFailCountAsync(string ip);

    /// <summary>
    /// Increments the failure counter for <paramref name="ip"/> and resets the
    /// 15-minute absolute expiry window. Called only on credential failures
    /// (wrong password, non-existent email) — NOT on inactive-account rejections.
    /// </summary>
    Task IncrementAsync(string ip);

    /// <summary>
    /// Removes the failure counter for <paramref name="ip"/>. Called after a
    /// successful login so a legitimate user is never blocked by prior failures
    /// from the same IP (AC-005).
    /// Uses <c>RemoveAsync</c> rather than setting to 0 to free cache space immediately.
    /// </summary>
    Task ResetAsync(string ip);
}
