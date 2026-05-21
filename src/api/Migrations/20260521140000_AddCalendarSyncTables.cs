using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarSyncTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── patient_calendar_tokens ──────────────────────────────────────────────────────────
            // Stores encrypted OAuth tokens for connected calendars (us_028; AC-001, AC-002).
            // EncryptedAccessToken/EncryptedRefreshToken are AES-256 bytea — plaintext never stored
            // (OWASP A02; HIPAA minimum-necessary).
            migrationBuilder.CreateTable(
                name: "patient_calendar_tokens",
                columns: table => new
                {
                    id         = table.Column<int>(type: "integer", nullable: false)
                                      .Annotation("Npgsql:ValueGenerationStrategy",
                                                  NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    patient_id = table.Column<int>(type: "integer", nullable: false),
                    provider   = table.Column<string>(type: "text", nullable: false),
                    encrypted_access_token  = table.Column<byte[]>(type: "bytea", nullable: true),
                    encrypted_refresh_token = table.Column<byte[]>(type: "bytea", nullable: true),
                    token_expiry = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patient_calendar_tokens", x => x.id);
                    table.ForeignKey(
                        name:       "fk_patient_calendar_tokens_patients_patient_id",
                        column:     x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // UNIQUE (patient_id, provider) — one token row per patient per provider
            migrationBuilder.CreateIndex(
                name:    "uq_patient_calendar_tokens_patient_provider",
                table:   "patient_calendar_tokens",
                columns: new[] { "patient_id", "provider" },
                unique:  true);

            // ── booking_calendar_syncs ────────────────────────────────────────────────────────────
            // Tracks ExternalEventId and sync status per (booking, provider) pair (us_028; AC-001–AC-005).
            migrationBuilder.CreateTable(
                name: "booking_calendar_syncs",
                columns: table => new
                {
                    id          = table.Column<int>(type: "integer", nullable: false)
                                       .Annotation("Npgsql:ValueGenerationStrategy",
                                                   NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    booking_id        = table.Column<int>(type: "integer",  nullable: false),
                    provider          = table.Column<string>(type: "text",  nullable: false),
                    external_event_id = table.Column<string>(type: "text",  nullable: true),
                    status            = table.Column<string>(type: "text",  nullable: false, defaultValue: "Synced"),
                    created_at        = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at        = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_calendar_syncs", x => x.id);
                    table.ForeignKey(
                        name:       "fk_booking_calendar_syncs_bookings_booking_id",
                        column:     x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // UNIQUE (booking_id, provider) — one sync row per (booking, provider) pair (AC-001, AC-002)
            migrationBuilder.CreateIndex(
                name:    "uq_booking_calendar_syncs_booking_provider",
                table:   "booking_calendar_syncs",
                columns: new[] { "booking_id", "provider" },
                unique:  true);

            // Non-unique index on booking_id for UpdateAsync / DeleteAsync hook look-ups
            migrationBuilder.CreateIndex(
                name:   "ix_booking_calendar_syncs_booking_id",
                table:  "booking_calendar_syncs",
                column: "booking_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "booking_calendar_syncs");
            migrationBuilder.DropTable(name: "patient_calendar_tokens");
        }
    }
}
