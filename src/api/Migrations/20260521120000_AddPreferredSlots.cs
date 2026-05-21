using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPreferredSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // preferred_slots table — at-most-one preferred slot per booking enforced by UNIQUE
            // constraint on booking_id (AC-002; us_024).
            migrationBuilder.CreateTable(
                name: "preferred_slots",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    booking_id = table.Column<int>(type: "integer", nullable: false),
                    slot_id    = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_preferred_slots", x => x.id);
                    table.ForeignKey(
                        name: "fk_preferred_slots_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_preferred_slots_appointment_slots_slot_id",
                        column: x => x.slot_id,
                        principalTable: "appointment_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // UNIQUE index on booking_id — COUNT(*) per booking_id is always ≤ 1 (AC-002)
            migrationBuilder.CreateIndex(
                name: "ix_preferred_slots_booking_id",
                table: "preferred_slots",
                column: "booking_id",
                unique: true);

            // Non-unique index on slot_id for efficient FK look-ups and availability queries
            migrationBuilder.CreateIndex(
                name: "ix_preferred_slots_slot_id",
                table: "preferred_slots",
                column: "slot_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "preferred_slots");
        }
    }
}
