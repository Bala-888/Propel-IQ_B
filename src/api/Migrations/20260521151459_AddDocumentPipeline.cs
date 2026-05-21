using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "reminder_2h_sent_at",
                table: "bookings",
                newName: "reminder2h_sent_at");

            migrationBuilder.RenameColumn(
                name: "reminder_24h_sent_at",
                table: "bookings",
                newName: "reminder24h_sent_at");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<byte[]>(
                name: "data",
                table: "intake_records",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mode",
                table: "intake_records",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "intake_records",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "intake_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "no_show_risk_tier",
                table: "bookings",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Unknown");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "checked_in_at",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by_staff_id",
                table: "bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "priority",
                table: "bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reason_for_visit",
                table: "bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "booking_calendar_syncs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Synced");

            migrationBuilder.CreateTable(
                name: "document_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<int>(type: "integer", nullable: false),
                    original_filename = table.Column<string>(type: "text", nullable: false),
                    mime_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    upload_timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    blob_path = table.Column<string>(type: "text", nullable: false),
                    wrapped_key = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    token_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_chunks", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_chunks_document_records_document_id",
                        column: x => x.document_id,
                        principalTable: "document_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_chunk_embeddings",
                columns: table => new
                {
                    chunk_id = table.Column<Guid>(type: "uuid", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1536)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_chunk_embeddings", x => x.chunk_id);
                    table.ForeignKey(
                        name: "fk_document_chunk_embeddings_document_chunks_chunk_id",
                        column: x => x.chunk_id,
                        principalTable: "document_chunks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_chunks_document_id",
                table: "document_chunks",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_chunks_document_id_chunk_index",
                table: "document_chunks",
                columns: new[] { "document_id", "chunk_index" });

            // HNSW cosine similarity index on document_chunk_embeddings.embedding (AC-003).
            // EF Core does not support HNSW natively; raw SQL is used here so the index is
            // version-controlled and applied automatically on `dotnet ef database update`
            // (checklist — HNSW in migration, not in a raw SQL seed script).
            migrationBuilder.Sql(
                """
                CREATE INDEX document_chunk_embeddings_embedding_hnsw_idx
                ON document_chunk_embeddings
                USING hnsw (embedding vector_cosine_ops)
                WITH (m = 16, ef_construction = 64);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS document_chunk_embeddings_embedding_hnsw_idx;");

            migrationBuilder.DropTable(
                name: "document_chunk_embeddings");

            migrationBuilder.DropTable(
                name: "document_chunks");

            migrationBuilder.DropTable(
                name: "document_records");

            migrationBuilder.DropColumn(
                name: "data",
                table: "intake_records");

            migrationBuilder.DropColumn(
                name: "mode",
                table: "intake_records");

            migrationBuilder.DropColumn(
                name: "status",
                table: "intake_records");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "intake_records");

            migrationBuilder.DropColumn(
                name: "checked_in_at",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "created_by_staff_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "reason_for_visit",
                table: "bookings");

            migrationBuilder.RenameColumn(
                name: "reminder2h_sent_at",
                table: "bookings",
                newName: "reminder_2h_sent_at");

            migrationBuilder.RenameColumn(
                name: "reminder24h_sent_at",
                table: "bookings",
                newName: "reminder_24h_sent_at");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "no_show_risk_tier",
                table: "bookings",
                type: "text",
                nullable: false,
                defaultValue: "Unknown",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "booking_calendar_syncs",
                type: "text",
                nullable: false,
                defaultValue: "Synced",
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
