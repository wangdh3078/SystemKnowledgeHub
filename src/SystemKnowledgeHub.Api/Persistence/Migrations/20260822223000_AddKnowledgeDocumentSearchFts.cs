using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations;

[DbContext(typeof(KnowledgeHubDbContext))]
[Migration("20260822223000_AddKnowledgeDocumentSearchFts")]
public partial class AddKnowledgeDocumentSearchFts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE VIRTUAL TABLE knowledge_documents_fts USING fts5(title, summary, body_text, tokenize = 'unicode61');");
        migrationBuilder.Sql("""
            WITH RECURSIVE source(id, field, value) AS (
                SELECT id, 'title', title FROM knowledge_documents
                UNION ALL SELECT id, 'summary', COALESCE(summary, '') FROM knowledge_documents
                UNION ALL SELECT id, 'body_text', body_markdown FROM knowledge_documents
            ), characters(id, field, value, position, normalized) AS (
                SELECT id, field, value, 1, '' FROM source
                UNION ALL
                SELECT id, field, value, position + 1,
                    normalized || CASE
                        WHEN unicode(substr(value, position, 1)) BETWEEN 13312 AND 19903
                          OR unicode(substr(value, position, 1)) BETWEEN 19968 AND 40959
                          OR unicode(substr(value, position, 1)) BETWEEN 63744 AND 64255
                        THEN ' ' || substr(value, position, 1) || ' '
                        ELSE substr(value, position, 1)
                    END
                FROM characters
                WHERE position <= length(value)
            )
            INSERT INTO knowledge_documents_fts(rowid, title, summary, body_text)
            SELECT id,
                MAX(CASE WHEN field = 'title' THEN normalized END),
                MAX(CASE WHEN field = 'summary' THEN normalized END),
                MAX(CASE WHEN field = 'body_text' THEN normalized END)
            FROM characters
            WHERE position > length(value)
            GROUP BY id;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE knowledge_documents_fts;");
    }
}
