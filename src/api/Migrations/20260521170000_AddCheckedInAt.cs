using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckedInAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── checked_in_at — UTC timestamp set when staff marks patient as arrived (us_031/AC-001) ──
            // Nullable: pre-scheduled patients have no check-in time until PATCH /bookings/{id}/status
            // transitions them to CheckedIn. Stored as timestamptz (UTC-aware).
            migrationBuilder.AddColumn<DateTimeOffset>(
                name:     "checked_in_at",
                table:    "bookings",
                type:     "timestamptz",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name:  "checked_in_at",
                table: "bookings");
        }
    }
}
