using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <summary>
    /// Adds two tables for the RAG medical-code review pipeline (us_044/AC-001–AC-005).
    ///
    /// <para><b>code_suggestions</b> — persisted RAG-generated suggestions produced by the
    /// <c>GET /patients/{id}/code-suggestions</c> pipeline.  Each row has a server-generated UUID PK;
    /// the triple <c>(patient_id, code_type, code)</c> is unique so repeated GET calls upsert
    /// idempotently without creating duplicate rows (AC-002).</para>
    ///
    /// <para><b>patient_medical_codes</b> — accepted/corrected codes written by a Clinician or Admin.
    /// <c>reviewed_by</c> is NOT NULL — every saved code has an identified actor.  There is no
    /// ON DELETE cascade here; if a user account is deleted, historical code records are preserved
    /// (HIPAA audit trail, AC-003).</para>
    ///
    /// Schema decisions:
    /// <list type="bullet">
    ///   <item><c>patient_id integer NOT NULL REFERENCES patients(id)</c> — <c>patients.id</c> is int
    ///     in this codebase; the task spec's "uuid" type is incorrect (AC-001).</item>
    ///   <item><c>reviewed_by integer [NOT NULL] REFERENCES users(id)</c> — <c>users.id</c> is also
    ///     int; nullable in <c>code_suggestions</c> (review pending), NOT NULL in
    ///     <c>patient_medical_codes</c> (review complete).</item>
    ///   <item><c>id uuid DEFAULT gen_random_uuid()</c> — UUIDs for stable cross-service references
    ///     without exposing sequential integers (OWASP A01).</item>
    ///   <item><c>created_at timestamptz DEFAULT now()</c> — server-side timestamp prevents
    ///     application clock-skew manipulation (HIPAA §164.312(b)).</item>
    /// </list>
    /// </summary>
    public partial class AddCodeSuggestionsAndMedicalCodesTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── code_suggestions ──────────────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "code_suggestions",
                columns: table => new
                {
                    id = table.Column<Guid>(
                        type: "uuid",
                        nullable: false,
                        defaultValueSql: "gen_random_uuid()"),

                    patient_id = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    code_type = table.Column<string>(
                        type: "character varying(10)",
                        maxLength: 10,
                        nullable: false),

                    code = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: false),

                    description = table.Column<string>(
                        type: "text",
                        nullable: true),

                    confidence = table.Column<double>(
                        type: "double precision",
                        nullable: false),

                    low_confidence = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false),

                    review_status = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: false,
                        defaultValue: "Pending"),

                    // Nullable: set only when a clinician reviews the suggestion (AC-002)
                    reviewed_by = table.Column<int>(
                        type: "integer",
                        nullable: true),

                    reviewed_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true),

                    created_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_code_suggestions", x => x.id);

                    // FK → patients.id (int) — Restrict: preserves suggestion history
                    table.ForeignKey(
                        name: "fk_code_suggestions_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);

                    // FK → users.id (int) — SET NULL: preserves the row when the reviewer is deleted (AC-003)
                    table.ForeignKey(
                        name: "fk_code_suggestions_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            // UNIQUE (patient_id, code_type, code) — ON CONFLICT target for idempotent upserts (AC-002)
            migrationBuilder.CreateIndex(
                name: "uq_code_suggestions_patient_code_type_code",
                table: "code_suggestions",
                columns: new[] { "patient_id", "code_type", "code" },
                unique: true);

            // Supports efficient look-up of all suggestions for a patient
            migrationBuilder.CreateIndex(
                name: "ix_code_suggestions_patient_id",
                table: "code_suggestions",
                column: "patient_id");

            // ── patient_medical_codes ─────────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "patient_medical_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(
                        type: "uuid",
                        nullable: false,
                        defaultValueSql: "gen_random_uuid()"),

                    patient_id = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    code_type = table.Column<string>(
                        type: "character varying(10)",
                        maxLength: 10,
                        nullable: false),

                    code = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: false),

                    // Equals code for straight accepts; holds the original AI code for corrections (AC-001)
                    original_code = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: false),

                    description = table.Column<string>(
                        type: "text",
                        nullable: true),

                    // "AI" | "AI-Corrected" (AC-001)
                    source = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: false),

                    // "Accepted" | "Corrected" (AC-001)
                    review_status = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: false),

                    // NOT NULL — every final code record has an identified reviewer (AC-003)
                    reviewed_by = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    reviewed_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false),

                    created_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patient_medical_codes", x => x.id);

                    // FK → patients.id — Restrict: medical code records must not be orphaned
                    table.ForeignKey(
                        name: "fk_patient_medical_codes_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);

                    // FK → users.id — Restrict: preserves historical record (AC-003; HIPAA)
                    table.ForeignKey(
                        name: "fk_patient_medical_codes_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Supports efficient patient-scoped look-up
            migrationBuilder.CreateIndex(
                name: "ix_patient_medical_codes_patient_id",
                table: "patient_medical_codes",
                column: "patient_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "patient_medical_codes");
            migrationBuilder.DropTable(name: "code_suggestions");
        }
    }
}
