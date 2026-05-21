using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── patient_preferences (us_029; AC-004) ─────────────────────────────────────────────
            // One row per patient; created at registration with correct opt-in/opt-out defaults.
            // HasDefaultValue applied to all five bool columns so rows inserted via raw SQL or
            // seed scripts also receive the privacy-by-default starting state (AC-004; task spec).
            // Notification channels default TRUE (email, sms, slot-swap) — patients are opted in.
            // Calendar sync channels default FALSE (google, outlook) — patients must explicitly enable.
            migrationBuilder.CreateTable(
                name: "patient_preferences",
                columns: table => new
                {
                    id         = table.Column<int>(type: "integer", nullable: false)
                                      .Annotation("Npgsql:ValueGenerationStrategy",
                                                  NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    patient_id = table.Column<int>(type: "integer", nullable: false),

                    // Opt-in notification channels — DB DEFAULT true
                    email_notifications_enabled     = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sms_notifications_enabled       = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    slot_swap_notifications_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),

                    // Opt-out calendar sync channels — DB DEFAULT false
                    google_calendar_sync_enabled  = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    outlook_calendar_sync_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patient_preferences", x => x.id);
                    table.ForeignKey(
                        name:            "fk_patient_preferences_patients_patient_id",
                        column:          x => x.patient_id,
                        principalTable:  "patients",
                        principalColumn: "id",
                        onDelete:        ReferentialAction.Cascade);
                });

            // UNIQUE on patient_id — one preference row per patient enforced at DB level (AC-004)
            migrationBuilder.CreateIndex(
                name:   "uq_patient_preferences_patient_id",
                table:  "patient_preferences",
                column: "patient_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "patient_preferences");
        }
    }
}
