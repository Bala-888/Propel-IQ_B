using Api.Data;
using Api.Data.Entities;

namespace Api.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAdminNotificationRepository"/>.
/// Each <see cref="InsertAsync"/> call uses its own <c>SaveChangesAsync</c> — the insert
/// is not batched with any other DB operation so a failure in a surrounding transaction
/// cannot suppress the security alert (AC-005; OWASP A09 — audit record must persist).
/// No UPDATE or DELETE operations are exposed; the table is append-only (OWASP A09).
/// </summary>
public sealed class AdminNotificationRepository : IAdminNotificationRepository
{
    private readonly AppDbContext _db;

    public AdminNotificationRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task InsertAsync(AdminNotification notification, CancellationToken cancellationToken = default)
    {
        _db.AdminNotifications.Add(notification);
        // Isolated SaveChangesAsync — persists the alert independently of any outer UoW (AC-005).
        await _db.SaveChangesAsync(cancellationToken);
    }
}
