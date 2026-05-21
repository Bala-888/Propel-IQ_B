using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNoShowRiskTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add no_show_risk_tier column — defaults to "Unknown" until the async scoring pipeline
            // writes the computed tier after booking (us_021; AC-002; AC-004).
            migrationBuilder.AddColumn<string>(
                name: "no_show_risk_tier",
                table: "bookings",
                type: "text",
                nullable: false,
                defaultValue: "Unknown");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "no_show_risk_tier",
                table: "bookings");
        }
    }
}
