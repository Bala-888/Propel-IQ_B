using Api.Data.Entities;
using Api.Repositories;
using Microsoft.Extensions.Caching.Distributed;

namespace Api.Infrastructure.Auth;

/// <summary>
/// Tracks HTTP 403 responses per source IP using <see cref="IDistributedCache"/>.
/// When the same IP accumulates <c>3</c> or more 403s within a 10-minute window,
/// an <see cref="AdminNotification"/> row with <c>AlertType = "RepeatedUnauthorizedAccess"</c>
/// is inserted via <see cref="IAdminNotificationRepository"/> (AC-005).
/// </summary>
/// <remarks>
/// The <c>AbsoluteExpirationRelativeToNow</c> of 10 minutes means the window is measured
/// from the most recent 403 — the counter resets automatically when 10 minutes have elapsed
/// since the last violation (AC-005).
/// The <see cref="IAdminNotificationRepository"/> concrete implementation and its underlying
/// DB table are provided by task_002 of us_013.
/// </remarks>
public sealed class RepeatedUnauthorizedAccessTracker
{
    private readonly IDistributedCache _cache;
    private readonly IAdminNotificationRepository _notificationRepo;

    public RepeatedUnauthorizedAccessTracker(
        IDistributedCache cache,
        IAdminNotificationRepository notificationRepo)
    {
        _cache           = cache;
        _notificationRepo = notificationRepo;
    }

    /// <summary>
    /// Increments the 403 counter for <paramref name="sourceIp"/> and inserts an
    /// <see cref="AdminNotification"/> when the count reaches the threshold.
    /// </summary>
    public async Task TrackAndAlertAsync(string sourceIp)
    {
        var key      = $"unauth_403:{sourceIp}";
        var countStr = await _cache.GetStringAsync(key);
        var count    = countStr is null ? 0 : int.Parse(countStr);
        count++;

        // Extend (or create) the entry with a fresh 10-minute absolute expiration on every 403.
        await _cache.SetStringAsync(key, count.ToString(), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        });

        if (count >= 3)
        {
            await _notificationRepo.InsertAsync(new AdminNotification
            {
                AlertType = "RepeatedUnauthorizedAccess",
                SourceIp  = sourceIp,
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}
