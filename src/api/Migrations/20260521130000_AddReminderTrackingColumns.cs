using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderTrackingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── reminder_24h_sent_at — deduplication guard for the 24-hour reminder tier (us_027; AC-001, AC-002)
            // Null = reminder not yet sent; non-null = sent at the recorded UTC timestamp.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name:     "reminder_24h_sent_at",
                table:    "bookings",
                type:     "timestamp with time zone",
                nullable: true);

            // ── reminder_2h_sent_at — deduplication guard for the 2-hour reminder tier (us_027; AC-002)
            migrationBuilder.AddColumn<DateTimeOffset>(
                name:     "reminder_2h_sent_at",
                table:    "bookings",
                type:     "timestamp with time zone",
                nullable: true);

            // ── Index on appointment_slots.slot_start ─────────────────────────────────────────────
            // Supports the ±5-minute window range scan executed by AppointmentReminderJob on every tick.
            // Without this index, each tick performs a full sequential scan of appointment_slots (AC-001).
            migrationBuilder.CreateIndex(
                name:    "ix_appointment_slots_slot_start",
                table:   "appointment_slots",
                column:  "slot_start");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name:  "ix_appointment_slots_slot_start",
                table: "appointment_slots");

            migrationBuilder.DropColumn(
                name:  "reminder_2h_sent_at",
                table: "bookings");

            migrationBuilder.DropColumn(
                name:  "reminder_24h_sent_at",
                table: "bookings");
        }
    }
}
