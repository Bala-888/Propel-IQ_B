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
    public DbSet<PreferredSlot> PreferredSlots => Set<PreferredSlot>();
    public DbSet<PatientCalendarToken>  PatientCalendarTokens  => Set<PatientCalendarToken>();
    public DbSet<BookingCalendarSync>   BookingCalendarSyncs   => Set<BookingCalendarSync>();
    public DbSet<PatientPreferences>    PatientPreferences     => Set<PatientPreferences>();
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

        // ── PreferredSlot (us_024) ─────────────────────────────────────────────────
        // FK → bookings: cascade delete cleans up the preferred slot if the booking row is
        // hard-deleted; business-logic cancellation uses ExecuteDeleteAsync in BookingService.
        modelBuilder.Entity<PreferredSlot>()
            .HasOne(ps => ps.Booking)
            .WithMany()
            .HasForeignKey(ps => ps.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK → appointment_slots: Restrict so the slot row cannot be deleted while a preferred
        // slot references it (data integrity).
        modelBuilder.Entity<PreferredSlot>()
            .HasOne(ps => ps.Slot)
            .WithMany()
            .HasForeignKey(ps => ps.SlotId)
            .OnDelete(DeleteBehavior.Restrict);

        // UNIQUE index on BookingId — enforces at-most-one preferred slot per booking (AC-002)
        modelBuilder.Entity<PreferredSlot>()
            .HasIndex(ps => ps.BookingId)
            .IsUnique();

        // Non-unique index on SlotId for FK look-up efficiency
        modelBuilder.Entity<PreferredSlot>()
            .HasIndex(ps => ps.SlotId);

        // ── AppointmentReminderJob (us_027; AC-001, AC-002) ───────────────────────
        // Index on slot_start supports the ±5-minute window range query executed on every tick.
        modelBuilder.Entity<AppointmentSlot>()
            .HasIndex(s => s.SlotStart)
            .HasDatabaseName("ix_appointment_slots_slot_start");

        // ── PatientCalendarToken (us_028; AC-001, AC-002) ─────────────────────────────────────
        // FK → Patient: cascade delete removes token rows when the patient is deleted (data hygiene).
        // EncryptedAccessToken/EncryptedRefreshToken: stored as bytea; CalendarSyncService encrypts
        // via IPhiEncryptionService.Encrypt() before saving (OWASP A02; HIPAA minimum-necessary).
        modelBuilder.Entity<PatientCalendarToken>()
            .HasOne(t => t.Patient)
            .WithMany()
            .HasForeignKey(t => t.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        // UNIQUE (PatientId, Provider) — one token row per patient per provider (Google / Outlook)
        modelBuilder.Entity<PatientCalendarToken>()
            .HasIndex(t => new { t.PatientId, t.Provider })
            .IsUnique()
            .HasDatabaseName("uq_patient_calendar_tokens_patient_provider");

        // ── BookingCalendarSync (us_028; AC-001–AC-005) ───────────────────────────────────────
        // FK → Booking: cascade delete removes sync rows if the booking row is hard-deleted.
        // Business-logic cancellation sets Status = "Deleted" without hard-deleting (AC-004).
        modelBuilder.Entity<BookingCalendarSync>()
            .HasOne(s => s.Booking)
            .WithMany()
            .HasForeignKey(s => s.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        // UNIQUE (BookingId, Provider) — one sync row per (booking, provider) pair (AC-001, AC-002)
        modelBuilder.Entity<BookingCalendarSync>()
            .HasIndex(s => new { s.BookingId, s.Provider })
            .IsUnique()
            .HasDatabaseName("uq_booking_calendar_syncs_booking_provider");

        // Non-unique index on BookingId for efficient look-up in UpdateAsync / DeleteAsync hooks
        modelBuilder.Entity<BookingCalendarSync>()
            .HasIndex(s => s.BookingId)
            .HasDatabaseName("ix_booking_calendar_syncs_booking_id");

        // ── PatientPreferences (us_029; AC-004) ─────────────────────────────────────────────────
        // FK → Patient: cascade delete removes preference row when the patient account is deleted.
        // UNIQUE on PatientId: one preference row per patient enforced at the DB level.
        // HasDefaultValue on all five bool columns so rows inserted outside EF (e.g. seed scripts)
        // also receive the correct opt-in / opt-out starting state (AC-004; task spec).
        modelBuilder.Entity<PatientPreferences>()
            .HasOne(p => p.Patient)
            .WithMany()
            .HasForeignKey(p => p.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PatientPreferences>()
            .HasIndex(p => p.PatientId)
            .IsUnique()
            .HasDatabaseName("uq_patient_preferences_patient_id");

        // Notification channels: opt-in by default (HasDefaultValue(true) → DB column DEFAULT true)
        modelBuilder.Entity<PatientPreferences>()
            .Property(p => p.EmailNotificationsEnabled)
            .HasDefaultValue(true);
        modelBuilder.Entity<PatientPreferences>()
            .Property(p => p.SmsNotificationsEnabled)
            .HasDefaultValue(true);
        modelBuilder.Entity<PatientPreferences>()
            .Property(p => p.SlotSwapNotificationsEnabled)
            .HasDefaultValue(true);

        // Calendar sync channels: opt-out by default (HasDefaultValue(false) → DB column DEFAULT false)
        modelBuilder.Entity<PatientPreferences>()
            .Property(p => p.GoogleCalendarSyncEnabled)
            .HasDefaultValue(false);
        modelBuilder.Entity<PatientPreferences>()
            .Property(p => p.OutlookCalendarSyncEnabled)
            .HasDefaultValue(false);

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
