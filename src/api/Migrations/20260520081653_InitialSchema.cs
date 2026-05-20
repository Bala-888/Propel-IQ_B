using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Upacip.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "AppointmentSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ProviderId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentSlots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConflictFlags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    value_a_encrypted = table.Column<string>(type: "text", nullable: false),
                    SourceDocumentAId = table.Column<Guid>(type: "uuid", nullable: false),
                    value_b_encrypted = table.Column<string>(type: "text", nullable: false),
                    SourceDocumentBId = table.Column<Guid>(type: "uuid", nullable: false),
                    Confidence = table.Column<float>(type: "real", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "text", nullable: true),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConflictFlags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InsuranceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "text", nullable: false),
                    MemberIdPattern = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InsuranceRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicalCodeSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeType = table.Column<string>(type: "text", nullable: false),
                    CodeValue = table.Column<string>(type: "text", nullable: false),
                    CodeDescription = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<float>(type: "real", nullable: false),
                    SourceChunkRefsJson = table.Column<string>(type: "text", nullable: false),
                    ReviewStatus = table.Column<string>(type: "text", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorrectedCodeValue = table.Column<string>(type: "text", nullable: true),
                    CorrectedCodeDescription = table.Column<string>(type: "text", nullable: true),
                    SuggestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalCodeSuggestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    phone_encrypted = table.Column<string>(type: "text", nullable: true),
                    dob_encrypted = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorRole = table.Column<string>(type: "text", nullable: true),
                    ActionType = table.Column<string>(type: "text", nullable: false),
                    ResourceType = table.Column<string>(type: "text", nullable: true),
                    ResourceId = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    DetailsJson = table.Column<string>(type: "text", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    insurance_provider_encrypted = table.Column<string>(type: "text", nullable: true),
                    insurance_id_encrypted = table.Column<string>(type: "text", nullable: true),
                    GoogleCalendarStatus = table.Column<string>(type: "text", nullable: false),
                    GoogleCalendarTokenEncrypted = table.Column<string>(type: "text", nullable: true),
                    OutlookCalendarStatus = table.Column<string>(type: "text", nullable: false),
                    OutlookCalendarTokenEncrypted = table.Column<string>(type: "text", nullable: true),
                    EmailNotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SmsNotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Patients_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    InsuranceStatus = table.Column<string>(type: "text", nullable: false),
                    NoShowRiskScore = table.Column<int>(type: "integer", nullable: false),
                    RiskFactorsJson = table.Column<string>(type: "text", nullable: true),
                    PreferredSlotId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsSlotMonitorActive = table.Column<bool>(type: "boolean", nullable: false),
                    ArrivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WalkInPatientName = table.Column<string>(type: "text", nullable: true),
                    WalkInPatientPhone = table.Column<string>(type: "text", nullable: true),
                    CalendarSyncStatus = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookings_AppointmentSlots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "AppointmentSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClinicalDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    MimeType = table.Column<string>(type: "text", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hash = table.Column<string>(type: "text", nullable: false),
                    storage_path_encrypted = table.Column<string>(type: "text", nullable: false),
                    ProcessingStatus = table.Column<string>(type: "text", nullable: false),
                    ProcessingError = table.Column<string>(type: "text", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicalDocuments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntakeRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    data_encrypted = table.Column<string>(type: "text", nullable: true),
                    SessionId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeRecords_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReminderSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReminderType = table.Column<string>(type: "text", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveryStatus = table.Column<string>(type: "text", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReminderSchedules_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChunkEmbeddings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkSequence = table.Column<int>(type: "integer", nullable: false),
                    ChunkRef = table.Column<string>(type: "text", nullable: false),
                    chunk_text_encrypted = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<string>(type: "vector(1536)", nullable: true),
                    TokenCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChunkEmbeddings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChunkEmbeddings_ClinicalDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "ClinicalDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExtractedRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    value_encrypted = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<float>(type: "real", nullable: false),
                    ChunkRef = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsDuplicate = table.Column<bool>(type: "boolean", nullable: false),
                    DuplicateOfId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtractedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractedRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExtractedRecords_ClinicalDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "ClinicalDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSlots_ScheduledAt",
                table: "AppointmentSlots",
                column: "ScheduledAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActionType",
                table: "AuditLogs",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId_OccurredAt",
                table: "AuditLogs",
                columns: new[] { "ActorUserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PatientId_SlotId",
                table: "Bookings",
                columns: new[] { "PatientId", "SlotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_SlotId",
                table: "Bookings",
                column: "SlotId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChunkEmbeddings_DocumentId",
                table: "ChunkEmbeddings",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalDocuments_PatientId_Sha256Hash",
                table: "ClinicalDocuments",
                columns: new[] { "PatientId", "Sha256Hash" });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictFlags_PatientId_Status",
                table: "ConflictFlags",
                columns: new[] { "PatientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedRecords_DocumentId",
                table: "ExtractedRecords",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedRecords_PatientId_EntityType",
                table: "ExtractedRecords",
                columns: new[] { "PatientId", "EntityType" });

            migrationBuilder.CreateIndex(
                name: "IX_InsuranceRecords_ProviderName",
                table: "InsuranceRecords",
                column: "ProviderName");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRecords_PatientId",
                table: "IntakeRecords",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalCodeSuggestions_PatientId_ReviewStatus",
                table: "MedicalCodeSuggestions",
                columns: new[] { "PatientId", "ReviewStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_UserId",
                table: "Patients",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReminderSchedules_BookingId",
                table: "ReminderSchedules",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderSchedules_ScheduledAt_DeliveryStatus",
                table: "ReminderSchedules",
                columns: new[] { "ScheduledAt", "DeliveryStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            // TASK-011: ivfflat cosine similarity index for pgvector RAG queries.
            // lists=100 is appropriate for up to ~1M embeddings; tune for production.
            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS ix_chunk_embeddings_embedding_ivfflat
                ON "ChunkEmbeddings" USING ivfflat (embedding vector_cosine_ops)
                WITH (lists = 100);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ChunkEmbeddings");

            migrationBuilder.DropTable(
                name: "ConflictFlags");

            migrationBuilder.DropTable(
                name: "ExtractedRecords");

            migrationBuilder.DropTable(
                name: "InsuranceRecords");

            migrationBuilder.DropTable(
                name: "IntakeRecords");

            migrationBuilder.DropTable(
                name: "MedicalCodeSuggestions");

            migrationBuilder.DropTable(
                name: "ReminderSchedules");

            migrationBuilder.DropTable(
                name: "ClinicalDocuments");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "AppointmentSlots");

            migrationBuilder.DropTable(
                name: "Patients");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
