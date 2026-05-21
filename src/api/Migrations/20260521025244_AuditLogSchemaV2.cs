using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AuditLogSchemaV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "audit_logs",
                newName: "resource_type");

            migrationBuilder.RenameColumn(
                name: "entity_type",
                table: "audit_logs",
                newName: "resource_id");

            migrationBuilder.RenameColumn(
                name: "entity_id",
                table: "audit_logs",
                newName: "actor_role");

            migrationBuilder.RenameColumn(
                name: "action",
                table: "audit_logs",
                newName: "action_type");

            migrationBuilder.AlterColumn<string>(
                name: "ip_address",
                table: "audit_logs",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "id",
                table: "audit_logs",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<string>(
                name: "actor_id",
                table: "audit_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "user_agent",
                table: "audit_logs",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            // AC-004: re-enforce append-only constraint at the DB layer.
            // Applied here so the permission restriction is version-controlled and applied
            // atomically with the schema change (OWASP A09; HIPAA §164.312(b); checklist).
            // Re-issuing REVOKE is idempotent — safe even if the privilege was already absent.
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON audit_logs FROM app_user;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "actor_id",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "user_agent",
                table: "audit_logs");

            migrationBuilder.RenameColumn(
                name: "resource_type",
                table: "audit_logs",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "resource_id",
                table: "audit_logs",
                newName: "entity_type");

            migrationBuilder.RenameColumn(
                name: "actor_role",
                table: "audit_logs",
                newName: "entity_id");

            migrationBuilder.RenameColumn(
                name: "action_type",
                table: "audit_logs",
                newName: "action");

            migrationBuilder.AlterColumn<string>(
                name: "ip_address",
                table: "audit_logs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(45)",
                oldMaxLength: 45,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "audit_logs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            // AC-004 rollback: restore UPDATE/DELETE to app_user so earlier migrations that may
            // rely on those privileges are not left in a broken state (checklist: reversibility).
            migrationBuilder.Sql("GRANT UPDATE, DELETE ON audit_logs TO app_user;");
        }
    }
}
