using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <summary>
    /// Creates the <c>clinical_conflicts</c> table to store AI-detected clinical conflicts between
    /// patient entity pairs (us_040/AC-004).
    ///
    /// Schema decisions:
    /// <list type="bullet">
    ///   <item><c>id</c> uuid PK — surrogate UUID; gen_random_uuid() called in C# before insert.</item>
    ///   <item><c>patient_id</c> integer FK → <c>patients.id</c> — matches the int PK on the patients table.</item>
    ///   <item><c>entity_a_id</c> / <c>entity_b_id</c> uuid FK → <c>patient_entities.id</c> — cascade delete
    ///     so conflicts are cleaned up when the source entity is removed.</item>
    ///   <item>UNIQUE constraint on <c>(patient_id, entity_a_id, entity_b_id)</c> — enforces idempotency
    ///     for ON CONFLICT DO NOTHING inserts from <c>ConflictDetectionWorker</c> (AC-004).</item>
    ///   <item><c>description</c> varchar(1000) — capped at model layer and truncated in worker (OWASP A04).</item>
    ///   <item><c>status</c> varchar(20) DEFAULT 'Open' — initial lifecycle state.</item>
    /// </list>
    /// </summary>
    public partial class AddClinicalConflictsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clinical_conflicts",
                columns: table => new
                {
                    id             = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id     = table.Column<int>(type: "integer", nullable: false),
                    entity_a_id    = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_b_id    = table.Column<Guid>(type: "uuid", nullable: false),
                    conflict_type  = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description    = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    severity       = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status         = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Open"),
                    created_at     = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clinical_conflicts", x => x.id);

                    table.ForeignKey(
                        name: "fk_clinical_conflicts_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);

                    table.ForeignKey(
                        name: "fk_clinical_conflicts_entity_a",
                        column: x => x.entity_a_id,
                        principalTable: "patient_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);

                    table.ForeignKey(
                        name: "fk_clinical_conflicts_entity_b",
                        column: x => x.entity_b_id,
                        principalTable: "patient_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // UNIQUE constraint: idempotent upsert — ON CONFLICT DO NOTHING on this triplet (AC-004)
            migrationBuilder.CreateIndex(
                name: "uq_clinical_conflicts_patient_entity_pair",
                table: "clinical_conflicts",
                columns: new[] { "patient_id", "entity_a_id", "entity_b_id" },
                unique: true);

            // Index on patient_id: supports RT4 WHERE patient_id = ? query in PatientsController
            migrationBuilder.CreateIndex(
                name: "ix_clinical_conflicts_patient_id",
                table: "clinical_conflicts",
                column: "patient_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "clinical_conflicts");
        }
    }
}
