using Api.Data.Entities;

namespace Api.Repositories;

/// <summary>
/// Persistence contract for <see cref="AdminNotification"/> rows.
/// The table is append-only — no UPDATE or DELETE operations are exposed (OWASP A09).
/// </summary>
public interface IAdminNotificationRepository
{
    /// <summary>
    /// Inserts a new <see cref="AdminNotification"/> record in an isolated transaction
    /// so the security alert is persisted regardless of any surrounding DB operation (AC-005).
    /// </summary>
    Task InsertAsync(AdminNotification notification, CancellationToken cancellationToken = default);
}
