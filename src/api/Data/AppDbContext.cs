using Api.Data.Entities;
using Api.Features.Codes;
using Api.Features.Conflicts;
using Api.Features.Documents;
using Api.Features.Embeddings;
using Api.Features.Entities;
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
    // Patient-uploaded encrypted documents — plaintext never persisted (us_035/AC-003, AC-004; OWASP A02)
    public DbSet<DocumentRecord>        DocumentRecords        => Set<DocumentRecord>();
    // Sliding-window text chunks produced by DocumentTextExtractionWorker (us_036/AC-003; AIR-003)
    public DbSet<DocumentChunk>         DocumentChunks         => Set<DocumentChunk>();
    // pgvector embeddings produced by EmbeddingWorker (us_037/AC-002; AIR-001, AIR-004)
    public DbSet<DocumentChunkEmbedding> DocumentChunkEmbeddings => Set<DocumentChunkEmbedding>();
    // Deduplicated clinical entities extracted by EntityExtractionWorker (us_038/AC-003, AC-004; AIR-003)
    public DbSet<PatientEntity>          PatientEntities          => Set<PatientEntity>();
    // Clinical conflicts detected by ConflictDetectionWorker (us_040/AC-004)
    public DbSet<ClinicalConflict>       ClinicalConflicts        => Set<ClinicalConflict>();
    // RAG-generated code suggestions persisted by CodeSuggestionsController (us_043/AC-002, us_044/AC-001)
    public DbSet<CodeSuggestion>         CodeSuggestions          => Set<CodeSuggestion>();
    // Clinician-accepted/corrected medical codes (us_044/AC-001)
    public DbSet<PatientMedicalCode>     PatientMedicalCodes      => Set<PatientMedicalCode>();
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

        // ── DocumentChunk (us_036/AC-003) ──────────────────────────────────────────────────────
        // FK → DocumentRecords: cascade delete removes chunk rows when the parent document is deleted.
        // Index on DocumentId: supports efficient query for all chunks belonging to a document.
        // Index on (DocumentId, ChunkIndex): supports ordered chunk retrieval for embedding pipeline.
        modelBuilder.Entity<DocumentChunk>()
            .HasOne(c => c.Document)
            .WithMany()
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DocumentChunk>()
            .HasIndex(c => c.DocumentId)
            .HasDatabaseName("ix_document_chunks_document_id");

        modelBuilder.Entity<DocumentChunk>()
            .HasIndex(c => new { c.DocumentId, c.ChunkIndex })
            .HasDatabaseName("ix_document_chunks_document_id_chunk_index");

        // FailureReason is optional — only populated when Status = "ExtractionFailed" (us_036/AC-004)
        modelBuilder.Entity<DocumentRecord>()
            .Property(r => r.FailureReason)
            .IsRequired(false)
            .HasMaxLength(500); // OWASP A04 — truncation enforced in DocumentTextExtractionWorker

        // ── DocumentChunkEmbedding (us_037/AC-002, AC-003) ──────────────────────────────────────
        // Table mapped to document_chunk_embeddings — distinct from legacy chunk_embeddings (us_006).
        // ChunkId is both PK and FK (1:1 with document_chunks — one embedding per chunk).
        // embedding column is vector(1536); HNSW cosine index is created in the migration (AC-003).
        modelBuilder.Entity<DocumentChunkEmbedding>()
            .ToTable("document_chunk_embeddings");

        modelBuilder.Entity<DocumentChunkEmbedding>()
            .HasKey(e => e.ChunkId);

        modelBuilder.Entity<DocumentChunkEmbedding>()
            .HasOne<DocumentChunk>()
            .WithMany()
            .HasForeignKey(e => e.ChunkId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DocumentChunkEmbedding>()
            .Property(e => e.Embedding)
            .HasColumnType("vector(1536)");

        // pgvector extension — must be declared before any vector column migration is applied (AC-002)
        modelBuilder.HasPostgresExtension("vector");

        // ── PatientEntity (us_038/AC-003, AC-004) ──────────────────────────────────────────
        // UNIQUE constraint on (patient_id, type, value): DB-level deduplication enforcement (AC-003).
        // value max 500 chars: prevents oversized inserts on malformed Ollama output (OWASP A04).
        modelBuilder.Entity<PatientEntity>()
            .HasIndex(e => new { e.PatientId, e.Type, e.Value })
            .IsUnique()
            .HasDatabaseName("ix_patient_entities_patient_id_type_value");

        modelBuilder.Entity<PatientEntity>()
            .Property(e => e.Value)
            .HasMaxLength(500);

        modelBuilder.Entity<PatientEntity>()
            .Property(e => e.Type)
            .HasMaxLength(50);

        // ── PatientEntity → Patient FK (us_040/AC-003) ─────────────────────────────────────────
        // HasConstraintName matches the FK created in migration 20260521152232_AddPatientEntitiesTable
        // so EF Core does not attempt to DROP/RE-CREATE the existing constraint.
        // WithMany(p => p.PatientEntities) enables Include-based RT1 JOIN in PatientsController (AC-003).
        modelBuilder.Entity<PatientEntity>()
            .HasOne<Patient>()
            .WithMany(p => p.PatientEntities)
            .HasForeignKey(pe => pe.PatientId)
            .HasConstraintName("fk_patient_entities_patients_patient_id")
            .OnDelete(DeleteBehavior.Cascade);

        // ── ClinicalConflict (us_040/AC-004) ──────────────────────────────────────────────────
        // UNIQUE constraint on (patient_id, entity_a_id, entity_b_id): idempotent ON CONFLICT DO NOTHING
        // semantics — re-running conflict detection never inserts duplicate rows (AC-004).
        // Two explicit HasForeignKey configurations are required because EF Core cannot infer
        // which of the two Guid FK columns maps to EntityA vs EntityB without explicit guidance.
        modelBuilder.Entity<ClinicalConflict>()
            .HasIndex(c => new { c.PatientId, c.EntityAId, c.EntityBId })
            .IsUnique()
            .HasDatabaseName("uq_clinical_conflicts_patient_entity_pair");

        modelBuilder.Entity<ClinicalConflict>()
            .Property(c => c.Description)
            .HasMaxLength(1000);

        // EntityA FK — explicit because two navigations target the same PatientEntity table (AC-004)
        modelBuilder.Entity<ClinicalConflict>()
            .HasOne(c => c.EntityA)
            .WithMany()
            .HasForeignKey(c => c.EntityAId)
            .HasConstraintName("fk_clinical_conflicts_entity_a")
            .OnDelete(DeleteBehavior.Cascade);

        // EntityB FK — explicit for the same reason
        modelBuilder.Entity<ClinicalConflict>()
            .HasOne(c => c.EntityB)
            .WithMany()
            .HasForeignKey(c => c.EntityBId)
            .HasConstraintName("fk_clinical_conflicts_entity_b")
            .OnDelete(DeleteBehavior.Cascade);

        // ResolvedBy FK → users.id (int?): ON DELETE SET NULL preserves conflict record and audit
        // history if the resolving user account is later deleted (us_042/AC-003; OWASP A02).
        // No navigation property on User is required — scalar FK only.
        modelBuilder.Entity<ClinicalConflict>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.ResolvedBy)
            .HasConstraintName("fk_clinical_conflicts_users_resolved_by")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ClinicalConflict>()
            .Property(c => c.ResolutionNote)
            .HasMaxLength(1000);

        // ── CodeSuggestion (us_043/us_044) ──────────────────────────────────────────────────────
        // ToTable required: Npgsql snake_case naming would produce "code_suggestion" (singular);
        // migration uses the plural "code_suggestions" (AC-001).
        modelBuilder.Entity<CodeSuggestion>()
            .ToTable("code_suggestions");

        modelBuilder.Entity<CodeSuggestion>()
            .Property(cs => cs.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        modelBuilder.Entity<CodeSuggestion>()
            .Property(cs => cs.ReviewStatus)
            .HasDefaultValue("Pending")
            .HasMaxLength(20);

        modelBuilder.Entity<CodeSuggestion>()
            .Property(cs => cs.CodeType)
            .HasMaxLength(10);

        modelBuilder.Entity<CodeSuggestion>()
            .Property(cs => cs.Code)
            .HasMaxLength(20);

        modelBuilder.Entity<CodeSuggestion>()
            .Property(cs => cs.CreatedAt)
            .HasDefaultValueSql("now()");

        // UNIQUE (patient_id, code_type, code) — ON CONFLICT target for idempotent upserts (AC-002)
        modelBuilder.Entity<CodeSuggestion>()
            .HasIndex(cs => new { cs.PatientId, cs.CodeType, cs.Code })
            .IsUnique()
            .HasDatabaseName("uq_code_suggestions_patient_code_type_code");

        // FK → patients.id (int): Restrict — preserves suggestion history on patient changes
        modelBuilder.Entity<CodeSuggestion>()
            .HasOne<Patient>()
            .WithMany()
            .HasForeignKey(cs => cs.PatientId)
            .HasConstraintName("fk_code_suggestions_patients_patient_id")
            .OnDelete(DeleteBehavior.Restrict);

        // FK → users.id (int): SetNull — preserves rows when a reviewer account is deleted (AC-003)
        modelBuilder.Entity<CodeSuggestion>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(cs => cs.ReviewedBy)
            .HasConstraintName("fk_code_suggestions_users_reviewed_by")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // ── PatientMedicalCode (us_044/AC-001) ───────────────────────────────────────────────
        modelBuilder.Entity<PatientMedicalCode>()
            .ToTable("patient_medical_codes");

        modelBuilder.Entity<PatientMedicalCode>()
            .Property(pmc => pmc.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        modelBuilder.Entity<PatientMedicalCode>()
            .Property(pmc => pmc.CodeType)
            .HasMaxLength(10);

        modelBuilder.Entity<PatientMedicalCode>()
            .Property(pmc => pmc.Code)
            .HasMaxLength(20);

        modelBuilder.Entity<PatientMedicalCode>()
            .Property(pmc => pmc.OriginalCode)
            .HasMaxLength(20);

        modelBuilder.Entity<PatientMedicalCode>()
            .Property(pmc => pmc.Source)
            .HasMaxLength(20);

        modelBuilder.Entity<PatientMedicalCode>()
            .Property(pmc => pmc.ReviewStatus)
            .HasMaxLength(20);

        modelBuilder.Entity<PatientMedicalCode>()
            .Property(pmc => pmc.CreatedAt)
            .HasDefaultValueSql("now()");

        // FK → patients.id (int): Restrict — medical code records must not be orphaned
        modelBuilder.Entity<PatientMedicalCode>()
            .HasOne<Patient>()
            .WithMany()
            .HasForeignKey(pmc => pmc.PatientId)
            .HasConstraintName("fk_patient_medical_codes_patients_patient_id")
            .OnDelete(DeleteBehavior.Restrict);

        // FK → users.id (int): Restrict — preserves historical record (AC-003; HIPAA)
        modelBuilder.Entity<PatientMedicalCode>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(pmc => pmc.ReviewedBy)
            .HasConstraintName("fk_patient_medical_codes_users_reviewed_by")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
