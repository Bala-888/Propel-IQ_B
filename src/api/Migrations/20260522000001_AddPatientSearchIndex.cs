using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <summary>
    /// Adds a B-tree index on <c>(lower(last_name), lower(first_name))</c> with <c>text_pattern_ops</c>
    /// operator classes so PostgreSQL can use this index for <c>ILIKE 'term%'</c> prefix patterns.
    ///
    /// Without this index the query planner performs a sequential scan on large <c>patients</c> tables,
    /// which cannot meet the 500ms SLA required by us_040/AC-001/NFR-003.
    ///
    /// <c>text_pattern_ops</c> enables B-tree support for LIKE/ILIKE prefix patterns (<c>term%</c>).
    /// It does NOT help suffix or substring patterns (<c>%term</c>, <c>%term%</c>).
    /// </summary>
    public partial class AddPatientSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS prevents a failure if the index was created manually outside migrations
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS patients_name_search_idx " +
                "ON patients USING btree " +
                "(lower(last_name) text_pattern_ops, lower(first_name) text_pattern_ops)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS patients_name_search_idx");
        }
    }
}
