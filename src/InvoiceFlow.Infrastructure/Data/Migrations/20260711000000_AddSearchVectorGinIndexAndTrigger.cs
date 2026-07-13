using Microsoft.EntityFrameworkCore.Migrations;

namespace InvoiceFlow.Infrastructure.Data.Migrations;

/// <summary>
/// Adds GIN index on the search_vector tsvector column and a trigger to auto-populate it on INSERT/UPDATE.
/// Also backfills search vectors for existing rows.
/// </summary>
public partial class AddSearchVectorGinIndexAndTrigger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Create GIN index for full-text search queries
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS IX_documents_search_vector ON documents USING GIN (search_vector);");

        // Create a trigger function that auto-updates search_vector on row INSERT/UPDATE
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION documents_search_vector_update() RETURNS trigger AS $$
            BEGIN
                NEW.search_vector := to_tsvector('english',
                    coalesce(NEW.file_name, '') || ' ' ||
                    coalesce(NEW.ocr_text, '') || ' ' ||
                    coalesce(NEW.tags, ''));
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            """);

        // Create the trigger
        migrationBuilder.Sql("""
            CREATE TRIGGER documents_search_vector_trigger
            BEFORE INSERT OR UPDATE ON documents
            FOR EACH ROW EXECUTE FUNCTION documents_search_vector_update();
            """);

        // Backfill existing rows
        migrationBuilder.Sql("""
            UPDATE documents SET search_vector = to_tsvector('english',
                coalesce(file_name, '') || ' ' ||
                coalesce(ocr_text, '') || ' ' ||
                coalesce(tags, ''));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS documents_search_vector_trigger ON documents;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS documents_search_vector_update();");
        migrationBuilder.Sql("DROP INDEX IF EXISTS IX_documents_search_vector;");
    }
}
