using Api.Data.Entities;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Api.Data;

public class AppDbContext : DbContext
{
    private readonly IPhiEncryptionService _phiEncryption;

    public AppDbContext(DbContextOptions<AppDbContext> options, IPhiEncryptionService phiEncryption)
        : base(options)
    {
        _phiEncryption = phiEncryption;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<IntakeRecord> IntakeRecords => Set<IntakeRecord>();
    public DbSet<AppointmentSlot> AppointmentSlots => Set<AppointmentSlot>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<ClinicalDocument> ClinicalDocuments => Set<ClinicalDocument>();
    public DbSet<ExtractedRecord> ExtractedRecords => Set<ExtractedRecord>();
    public DbSet<ChunkEmbedding> ChunkEmbeddings => Set<ChunkEmbedding>();
    public DbSet<MedicalCodeSuggestion> MedicalCodeSuggestions => Set<MedicalCodeSuggestion>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ReminderSchedule> ReminderSchedules => Set<ReminderSchedule>();
    public DbSet<InsuranceRecord> InsuranceRecords => Set<InsuranceRecord>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<WalkInBooking> WalkInBookings => Set<WalkInBooking>();
    public DbSet<AdminNotification> AdminNotifications => Set<AdminNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Patient PHI value converters ── PHI COVERAGE — DO NOT REMOVE CONVERTERS ─────
        // (us_006/AC-001, AC-002; HIPAA §164.312(a)(2)(iv); DR-001)
        // Removing any converter below would store plaintext PHI in the database (AC-001; OWASP A02).
        // ValueConverter<string?, byte[]?> transparently encrypts on write and decrypts on read.
        // IPhiEncryptionService.Encrypt/Decrypt guard null inputs — no NULL column is passed to
        // pgp_sym_encrypt (Edge: null PHI field).
        var phiConverter = new ValueConverter<string?, byte[]?>(
            model  => _phiEncryption.Encrypt(model),
            stored => _phiEncryption.Decrypt(stored));

        modelBuilder.Entity<Patient>().Property(p => p.Email)
            .HasConversion(phiConverter).HasColumnType("bytea");
        modelBuilder.Entity<Patient>().Property(p => p.Phone)
            .HasConversion(phiConverter).HasColumnType("bytea");
        modelBuilder.Entity<Patient>().Property(p => p.DateOfBirth)
            .HasConversion(phiConverter).HasColumnType("bytea");
        modelBuilder.Entity<Patient>().Property(p => p.InsuranceProvider)
            .HasConversion(phiConverter).HasColumnType("bytea");
        modelBuilder.Entity<Patient>().Property(p => p.InsuranceId)
            .HasConversion(phiConverter).HasColumnType("bytea");

        // ChunkEmbedding.Embedding — vector(1536) for ivfflat cosine index (AC-003)
        modelBuilder.Entity<ChunkEmbedding>()
            .Property(c => c.Embedding)
            .HasColumnType("vector(1536)");

        // Booking → Patient FK (AC-004)
        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Patient)
            .WithMany(p => p.Bookings)
            .HasForeignKey(b => b.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Booking → AppointmentSlot FK
        modelBuilder.Entity<Booking>()
            .HasOne(b => b.AppointmentSlot)
            .WithMany(s => s.Bookings)
            .HasForeignKey(b => b.AppointmentSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        // IntakeRecord → Patient FK
        modelBuilder.Entity<IntakeRecord>()
            .HasOne(i => i.Patient)
            .WithMany()
            .HasForeignKey(i => i.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        // ClinicalDocument → Patient FK
        modelBuilder.Entity<ClinicalDocument>()
            .HasOne(d => d.Patient)
            .WithMany()
            .HasForeignKey(d => d.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        // ExtractedRecord → ClinicalDocument FK
        modelBuilder.Entity<ExtractedRecord>()
            .HasOne(e => e.ClinicalDocument)
            .WithMany(d => d.ExtractedRecords)
            .HasForeignKey(e => e.ClinicalDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // ChunkEmbedding → ExtractedRecord FK
        modelBuilder.Entity<ChunkEmbedding>()
            .HasOne(c => c.ExtractedRecord)
            .WithMany(e => e.ChunkEmbeddings)
            .HasForeignKey(c => c.ExtractedRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        // MedicalCodeSuggestion → ExtractedRecord FK
        modelBuilder.Entity<MedicalCodeSuggestion>()
            .HasOne(m => m.ExtractedRecord)
            .WithMany(e => e.MedicalCodeSuggestions)
            .HasForeignKey(m => m.ExtractedRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        // ReminderSchedule → Booking FK
        modelBuilder.Entity<ReminderSchedule>()
            .HasOne(r => r.Booking)
            .WithMany()
            .HasForeignKey(r => r.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        // InsuranceRecord → Patient FK
        modelBuilder.Entity<InsuranceRecord>()
            .HasOne(i => i.Patient)
            .WithMany(p => p.InsuranceRecords)
            .HasForeignKey(i => i.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── us_007 schema additions ────────────────────────────────────────────────
        // AC-003: Npgsql maps JsonDocument to json by default; explicit jsonb required for
        //         binary-backed storage, GIN indexing, and containment operators.
        modelBuilder.Entity<Booking>()
            .Property(b => b.RiskFactors)
            .HasColumnType("jsonb");

        // AC-004: DEFAULT 'Pending' emitted in migration DDL via HasDefaultValue;
        //         HasMaxLength(20) caps the column to character varying(20).
        modelBuilder.Entity<MedicalCodeSuggestion>()
            .Property(m => m.ReviewStatus)
            .HasDefaultValue("Pending")
            .HasMaxLength(20);

        // AC-005: SHA-256 hex is always 64 characters; HasMaxLength(64) generates
        //         character varying(64) and prevents over-length writes at the DB level.
        modelBuilder.Entity<ClinicalDocument>()
            .Property(d => d.FileHash)
            .HasMaxLength(64);

        // ── RefreshToken (us_009/task_001) ─────────────────────────────────────────
        // FK → User with cascade delete: tokens are removed when the user is deleted (data integrity).
        // HasOne(rt => rt.User) binds the navigation property so EF does not create a second shadow FK
        // (UserId1 anti-pattern) when the RefreshToken.User nav property was added in task_002.
        modelBuilder.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique index on Token — prevents two rows with the same token string (security invariant)
        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => rt.Token)
            .IsUnique();

        // ── AdminNotification (us_013/task_002) ────────────────────────────────────
        // Append-only table: no navigation properties, no FK constraints, no UPDATE/DELETE
        // operations exposed via IAdminNotificationRepository (AC-005; OWASP A09).
        modelBuilder.Entity<AdminNotification>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.AlertType).HasMaxLength(64).IsRequired();
            // Max 45 chars — full IPv6 length (e.g. 2001:0db8:…:7334) + IPv4-mapped form (OWASP A05)
            e.Property(x => x.SourceIp).HasMaxLength(45).IsRequired();
            // DB-server timestamp — prevents application-side clock-skew manipulation (AC-005; OWASP A09)
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        });

        // ── AuditLog (us_014/task_002) ─────────────────────────────────────────────
        // BIGSERIAL PK: monotonically increasing sequence eliminates B-tree page-splits caused
        // by random UUID inserts under concurrent write load (Edge: 50+ concurrent writes; AC-001).
        // UseIdentityByDefaultColumn() emits the Npgsql identity annotation; EF translates
        // the C# long type to PostgreSQL bigint (BIGSERIAL semantics, AC-001; TR-003).
        // IpAddress varchar(45): max length of IPv6 + IPv4-mapped form; NOT NULL because
        // every authenticated request has a resolvable source address (AC-001; OWASP A03).
        // UserAgent varchar(512): optional — some clients omit the header (AC-001; OWASP A09).
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.Property(x => x.Id).UseIdentityByDefaultColumn();
            e.Property(x => x.IpAddress).HasMaxLength(45).IsRequired(false);
            e.Property(x => x.UserAgent).HasMaxLength(512).IsRequired(false);
        });
    }
}
