using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPatientId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add patient_id to users so TokenService can embed the patients.id in the
            // JWT 'pid' claim without a round-trip (OWASP A01; us_017/AC-001).
            // IF NOT EXISTS guard makes the migration safe to apply against databases that
            // already have the column from a prior manual ALTER TABLE (dev convenience).
            migrationBuilder.Sql(@"
                ALTER TABLE users
                ADD COLUMN IF NOT EXISTS patient_id INTEGER
                REFERENCES patients(id) ON DELETE SET NULL;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_users_patient_id ON users(patient_id);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_users_patient_id;");

            migrationBuilder.DropColumn(
                name: "patient_id",
                table: "users");
        }
    }
}
