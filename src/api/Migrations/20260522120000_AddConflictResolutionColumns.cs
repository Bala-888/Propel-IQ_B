using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <summary>
    /// Adds three resolution columns to <c>clinical_conflicts</c>: <c>resolved_by</c>,
    /// <c>resolved_at</c>, and <c>resolution_note</c> (us_042/AC-002, AC-003).
    ///
    /// Schema decisions:
    /// <list type="bullet">
    ///   <item><c>resolved_by</c> integer FK → <c>users.id</c> with <c>ON DELETE SET NULL</c> —
    ///     preserves the conflict record and audit history if the resolving user is later deleted (AC-003).</item>
    ///   <item><c>resolved_at</c> timestamp with time zone — UTC timestamp set server-side to prevent
    ///     timezone skew (AC-002; HIPAA §164.312(b)).</item>
    ///   <item><c>resolution_note</c> varchar(1000) nullable — required by the application layer when
    ///     resolution = "Resolved"; optional for "Dismissed" (AC-003).
    ///     OWASP A02: controller validates length; stored in DB only, never in any ILogger.</item>
    /// </list>
    /// </summary>
    public partial class AddConflictResolutionColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "resolved_by",
                table: "clinical_conflicts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "resolved_at",
                table: "clinical_conflicts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resolution_note",
                table: "clinical_conflicts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            // FK: resolved_by → users.id with ON DELETE SET NULL.
            // Preserves the conflict record and audit trail when the user account is deleted (AC-003).
            migrationBuilder.AddForeignKey(
                name: "fk_clinical_conflicts_users_resolved_by",
                table: "clinical_conflicts",
                column: "resolved_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_clinical_conflicts_users_resolved_by",
                table: "clinical_conflicts");

            migrationBuilder.DropColumn(
                name: "resolved_by",
                table: "clinical_conflicts");

            migrationBuilder.DropColumn(
                name: "resolved_at",
                table: "clinical_conflicts");

            migrationBuilder.DropColumn(
                name: "resolution_note",
                table: "clinical_conflicts");
        }
    }
}
