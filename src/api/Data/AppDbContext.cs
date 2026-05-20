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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Patient PHI value converters (us_006/AC-001, AC-002; DR-001) ────────────────
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
    }
}
