using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingStartedAtToDocumentRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "processing_started_at",
                table: "document_records",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");  // back-fills existing rows with current timestamp
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "processing_started_at",
                table: "document_records");
        }
    }
}
