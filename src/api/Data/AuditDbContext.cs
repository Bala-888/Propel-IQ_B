using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

/// <summary>
/// Dedicated EF Core context used exclusively for audit log INSERTs.
/// Kept separate from <see cref="AppDbContext"/> so the audit write operates in its own
/// transaction scope — a rollback on the request's main unit-of-work never suppresses
/// the audit record (AC-001; HIPAA §164.312(b) — tamper-evident append-only log).
/// No PHI encryption is applied — audit logs contain only structural IDs (OWASP A09).
/// </summary>
public class AuditDbContext : DbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options) { }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
}
