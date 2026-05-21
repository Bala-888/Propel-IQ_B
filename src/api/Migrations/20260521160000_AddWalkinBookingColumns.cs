using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWalkinBookingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── reason_for_visit — free-text intake reason supplied at walk-in desk (us_030/AC-003) ──
            // Nullable: only walk-in bookings populate this column; patient self-service rows stay null.
            migrationBuilder.AddColumn<string>(
                name:     "reason_for_visit",
                table:    "bookings",
                type:     "text",
                nullable: true);

            // ── priority — intake priority tier, e.g. "Normal" | "Urgent" (us_030/AC-003) ──────────
            // Nullable: only populated by walk-in flow; max 20 chars prevents oversized string inserts.
            migrationBuilder.AddColumn<string>(
                name:      "priority",
                table:     "bookings",
                type:      "character varying(20)",
                maxLength: 20,
                nullable:  true);

            // ── created_by_staff_id — JWT sub of the staff actor who created the walk-in booking ─────
            // Stored as string (User.Id.ToString()) for audit traceability without a FK constraint
            // (AC-003; OWASP A02 — staffId only, no PHI). Nullable for backward compatibility.
            migrationBuilder.AddColumn<string>(
                name:     "created_by_staff_id",
                table:    "bookings",
                type:     "text",
                nullable: true);

            // ── Index on (created_by_staff_id) — supports audit queries filtered by staff actor ──────
            migrationBuilder.CreateIndex(
                name:   "ix_bookings_created_by_staff_id",
                table:  "bookings",
                column: "created_by_staff_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name:  "ix_bookings_created_by_staff_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name:  "created_by_staff_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name:  "priority",
                table: "bookings");

            migrationBuilder.DropColumn(
                name:  "reason_for_visit",
                table: "bookings");
        }
    }
}
