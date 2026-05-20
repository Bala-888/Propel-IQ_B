using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;
using Upacip.Api.Domain.Entities;
using Upacip.Api.Infrastructure.Encryption;

namespace Upacip.Api.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IConfiguration configuration) : DbContext(options)
{
    // ─── DbSets ──────────────────────────────────────────────────────────────
    public DbSet<AppUser> Users => Set<AppUser>();
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
    public DbSet<ConflictFlag> ConflictFlags => Set<ConflictFlag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ─── PHI AES-256 value converter ─────────────────────────────────────
        // Applied to every column named *_encrypted. The key is loaded from
        // IConfiguration["Phi:EncryptionKey"] (Base64-encoded 32-byte AES-256 key).
        // Falls back to a zeroed key for EF design-time migration scaffolding.
        // Load PHI encryption key. Falls back to a zero key ONLY for EF design-time
        // tooling (dotnet ef migrations add). At runtime, a missing or malformed key
        // is a fatal startup error.
        byte[] phiKey;
        var phiKeyBase64 = configuration["Phi:EncryptionKey"];
        if (string.IsNullOrEmpty(phiKeyBase64) || phiKeyBase64.StartsWith("REPLACE"))
        {
            // Design-time / unconfigured fallback — zeroed key, never touches real PHI.
            phiKey = new byte[32];
        }
        else
        {
            try { phiKey = Convert.FromBase64String(phiKeyBase64); }
            catch (FormatException ex)
            {
                throw new InvalidOperationException(
                    "Phi:EncryptionKey is not valid Base64.", ex);
            }
            if (phiKey.Length != 32)
                throw new InvalidOperationException(
                    $"Phi:EncryptionKey must decode to 32 bytes (got {phiKey.Length}).");
        }
        var phiConverter = new AesPhiConverter(phiKey);

        // Enable pgvector extension
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("pgcrypto");

        // ─── AppUser ──────────────────────────────────────────────────────────
        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.FullName).HasMaxLength(256).IsRequired();
            e.Property(u => u.Role).HasConversion<string>();

            // PHI columns — annotated; actual encryption via AesPhiConverter
            e.Property(u => u.Phone).HasColumnName("phone_encrypted").HasConversion(phiConverter);
            e.Property(u => u.DateOfBirth).HasColumnName("dob_encrypted").HasConversion(phiConverter);
        });

        // ─── Patient ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Patient>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasOne(p => p.User)
             .WithOne(u => u.Patient)
             .HasForeignKey<Patient>(p => p.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(p => p.GoogleCalendarStatus).HasConversion<string>();
            e.Property(p => p.OutlookCalendarStatus).HasConversion<string>();
            e.Property(p => p.InsuranceProvider).HasColumnName("insurance_provider_encrypted").HasConversion(phiConverter);
            e.Property(p => p.InsuranceId).HasColumnName("insurance_id_encrypted").HasConversion(phiConverter);
            e.Property(p => p.GoogleCalendarTokenEncrypted).HasColumnName("google_calendar_token_encrypted").HasConversion(phiConverter);
            e.Property(p => p.OutlookCalendarTokenEncrypted).HasColumnName("outlook_calendar_token_encrypted").HasConversion(phiConverter);
        });

        // ─── IntakeRecord ─────────────────────────────────────────────────────
        modelBuilder.Entity<IntakeRecord>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasOne(i => i.Patient)
             .WithMany(p => p.IntakeRecords)
             .HasForeignKey(i => i.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(i => i.Mode).HasConversion<string>();
            e.Property(i => i.Status).HasConversion<string>();
            e.Property(i => i.DataEncrypted).HasColumnName("data_encrypted").HasConversion(phiConverter);
        });

        // ─── AppointmentSlot ──────────────────────────────────────────────────
        modelBuilder.Entity<AppointmentSlot>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.ScheduledAt);
            e.Property(s => s.Status).HasConversion<string>();
        });

        // ─── Booking ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Booking>(e =>
        {
            e.HasKey(b => b.Id);

            // Composite unique: one active booking per patient per slot
            e.HasIndex(b => new { b.PatientId, b.SlotId }).IsUnique();

            e.HasOne(b => b.Patient)
             .WithMany(p => p.Bookings)
             .HasForeignKey(b => b.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(b => b.Slot)
             .WithOne(s => s.Booking)
             .HasForeignKey<Booking>(b => b.SlotId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(b => b.Status).HasConversion<string>();
            e.Property(b => b.Channel).HasConversion<string>();
            e.Property(b => b.InsuranceStatus).HasConversion<string>();
            e.Property(b => b.WalkInPatientName).HasColumnName("walk_in_patient_name_encrypted").HasConversion(phiConverter);
            e.Property(b => b.WalkInPatientPhone).HasColumnName("walk_in_patient_phone_encrypted").HasConversion(phiConverter);
        });

        // ─── ClinicalDocument ─────────────────────────────────────────────────
        modelBuilder.Entity<ClinicalDocument>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => new { d.PatientId, d.Sha256Hash });
            e.HasOne(d => d.Patient)
             .WithMany(p => p.ClinicalDocuments)
             .HasForeignKey(d => d.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(d => d.ProcessingStatus).HasConversion<string>();
            e.Property(d => d.StoragePathEncrypted).HasColumnName("storage_path_encrypted").HasConversion(phiConverter);
        });

        // ─── ExtractedRecord ──────────────────────────────────────────────────
        modelBuilder.Entity<ExtractedRecord>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => new { r.PatientId, r.EntityType });
            e.HasOne(r => r.Document)
             .WithMany(d => d.ExtractedRecords)
             .HasForeignKey(r => r.DocumentId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(r => r.EntityType).HasConversion<string>();
            e.Property(r => r.Status).HasConversion<string>();
            e.Property(r => r.ValueEncrypted).HasColumnName("value_encrypted").HasConversion(phiConverter);
        });

        // ─── ChunkEmbedding ───────────────────────────────────────────────────
        modelBuilder.Entity<ChunkEmbedding>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasOne(c => c.Document)
             .WithMany(d => d.ChunkEmbeddings)
             .HasForeignKey(c => c.DocumentId)
             .OnDelete(DeleteBehavior.Restrict);

            // pgvector column: vector(1536).
            // Explicit string converter so EF Core design-time tools can scaffold
            // the migration without needing a live Npgsql Vector type resolver.
            // At runtime, pgcrypto accepts the pgvector text format "[0.1,0.2,...]".
            var vectorConverter = new ValueConverter<Vector, string>(
                v => v.ToString()!,
                s => new Vector(
                    s.Trim('[', ']')
                     .Split(',', StringSplitOptions.TrimEntries)
                     .Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture))
                     .ToArray())
            );

            e.Property(c => c.Embedding)
             .HasColumnType("vector(1536)")
             .HasConversion(vectorConverter);

            // ivfflat cosine similarity index — added via raw SQL in migration (TASK-011)
            e.Property(c => c.ChunkTextEncrypted).HasColumnName("chunk_text_encrypted").HasConversion(phiConverter);
        });

        // ─── MedicalCodeSuggestion ────────────────────────────────────────────
        modelBuilder.Entity<MedicalCodeSuggestion>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.PatientId, s.ReviewStatus });
            e.Property(s => s.CodeType).HasConversion<string>();
            e.Property(s => s.ReviewStatus).HasConversion<string>();
        });

        // ─── AuditLog ─────────────────────────────────────────────────────────
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => new { a.ActorUserId, a.OccurredAt });
            e.HasIndex(a => a.ActionType);

            // Soft FK — no cascade; logs must persist if user is deactivated
            e.HasOne(a => a.ActorUser)
             .WithMany(u => u.AuditLogs)
             .HasForeignKey(a => a.ActorUserId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ─── ReminderSchedule ─────────────────────────────────────────────────
        modelBuilder.Entity<ReminderSchedule>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => new { r.ScheduledAt, r.DeliveryStatus });
            e.HasOne(r => r.Booking)
             .WithMany(b => b.Reminders)
             .HasForeignKey(r => r.BookingId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(r => r.ReminderType).HasConversion<string>();
            e.Property(r => r.DeliveryStatus).HasConversion<string>();
        });

        // ─── InsuranceRecord ──────────────────────────────────────────────────
        modelBuilder.Entity<InsuranceRecord>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasIndex(i => i.ProviderName);
        });

        // ─── ConflictFlag ─────────────────────────────────────────────────────
        modelBuilder.Entity<ConflictFlag>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => new { c.PatientId, c.Status });
            e.Property(c => c.EntityType).HasConversion<string>();
            e.Property(c => c.Status).HasConversion<string>();
            e.Property(c => c.ValueAEncrypted).HasColumnName("value_a_encrypted").HasConversion(phiConverter);
            e.Property(c => c.ValueBEncrypted).HasColumnName("value_b_encrypted").HasConversion(phiConverter);
        });
    }
}
