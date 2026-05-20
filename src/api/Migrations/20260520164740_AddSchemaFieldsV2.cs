using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSchemaFieldsV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contact_email",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "contact_phone",
                table: "patients");

            migrationBuilder.AlterColumn<byte[]>(
                name: "date_of_birth",
                table: "patients",
                type: "bytea",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<byte[]>(
                name: "email",
                table: "patients",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "insurance_id",
                table: "patients",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "insurance_provider",
                table: "patients",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "phone",
                table: "patients",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_status",
                table: "medical_code_suggestions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "file_hash",
                table: "clinical_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "no_show_risk_score",
                table: "bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "risk_factors",
                table: "bookings",
                type: "jsonb",
                nullable: true);

            // AC-002: enforce 0–100 range at the DB level; EF Core Fluent API has no parameterised
            // range-check API so raw SQL is required (no_show_risk_score may also be NULL — CHECK
            // only fires on non-NULL values, which is correct for an optional score)
            migrationBuilder.Sql(
                "ALTER TABLE bookings ADD CONSTRAINT chk_no_show_risk_score " +
                "CHECK (no_show_risk_score BETWEEN 0 AND 100);");

            // AC-004 / Edge: reject any review_status value outside the permitted workflow set
            // so invalid states can never be silently persisted
            migrationBuilder.Sql(
                "ALTER TABLE medical_code_suggestions ADD CONSTRAINT chk_review_status " +
                "CHECK (review_status IN ('Pending', 'Accepted', 'Rejected'));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop CHECK constraints BEFORE dropping columns — IF EXISTS ensures safe re-entrant rollback
            migrationBuilder.Sql(
                "ALTER TABLE bookings DROP CONSTRAINT IF EXISTS chk_no_show_risk_score;");
            migrationBuilder.Sql(
                "ALTER TABLE medical_code_suggestions DROP CONSTRAINT IF EXISTS chk_review_status;");

            migrationBuilder.DropColumn(
                name: "email",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "insurance_id",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "insurance_provider",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "review_status",
                table: "medical_code_suggestions");

            migrationBuilder.DropColumn(
                name: "file_hash",
                table: "clinical_documents");

            migrationBuilder.DropColumn(
                name: "no_show_risk_score",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "risk_factors",
                table: "bookings");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "date_of_birth",
                table: "patients",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_email",
                table: "patients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_phone",
                table: "patients",
                type: "text",
                nullable: true);
        }
    }
}
