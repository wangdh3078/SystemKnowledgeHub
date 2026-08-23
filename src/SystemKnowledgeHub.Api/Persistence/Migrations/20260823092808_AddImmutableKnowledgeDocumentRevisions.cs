using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImmutableKnowledgeDocumentRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TEMP TABLE __rev_b01_preflight_guard (
                    violation INTEGER NOT NULL CHECK (violation = 0)
                );
                INSERT INTO __rev_b01_preflight_guard (violation)
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM knowledge_documents AS document
                    LEFT JOIN users AS created_user ON created_user.id = document.created_by_user_id
                    LEFT JOIN users AS updated_user ON updated_user.id = document.updated_by_user_id
                    WHERE document.id <= 0
                       OR document.version <= 0
                       OR length(document.title) NOT BETWEEN 1 AND 300
                       OR (document.summary IS NOT NULL AND length(document.summary) > 2000)
                       OR length(document.body_markdown) > 1000000
                       OR created_user.id IS NULL
                       OR updated_user.id IS NULL
                ) THEN 1 ELSE 0 END;
                DROP TABLE __rev_b01_preflight_guard;
                """);

            migrationBuilder.AddColumn<long>(
                name: "current_revision_number",
                table: "knowledge_documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "latest_published_revision_number",
                table: "knowledge_documents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "knowledge_document_revision_number_snapshot",
                table: "evidence",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "knowledge_document_revisions",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    knowledge_document_id = table.Column<long>(type: "INTEGER", nullable: false),
                    revision_number = table.Column<long>(type: "INTEGER", nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    body_markdown = table.Column<string>(type: "TEXT", nullable: false),
                    author_user_id = table.Column<long>(type: "INTEGER", nullable: true),
                    author_display_name_snapshot = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    lifecycle_context = table.Column<string>(type: "TEXT", nullable: false),
                    change_summary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    restore_reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    restored_from_revision_number = table.Column<long>(type: "INTEGER", nullable: true),
                    revision_origin = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_document_revisions", x => x.id);
                    table.CheckConstraint("ck_knowledge_document_revisions_actor", "(revision_origin = 'MigrationBaseline' AND author_user_id IS NULL AND author_display_name_snapshot IS NULL) OR (revision_origin <> 'MigrationBaseline' AND author_user_id IS NOT NULL AND author_display_name_snapshot IS NOT NULL AND length(trim(author_display_name_snapshot)) > 0)");
                    table.CheckConstraint("ck_knowledge_document_revisions_body", "length(body_markdown) <= 1000000");
                    table.CheckConstraint("ck_knowledge_document_revisions_change_summary", "change_summary IS NULL OR length(change_summary) <= 500");
                    table.CheckConstraint("ck_knowledge_document_revisions_lifecycle", "lifecycle_context IN ('Draft','Published','Archived')");
                    table.CheckConstraint("ck_knowledge_document_revisions_origin", "revision_origin IN ('Created','ContentSave','Restore','MigrationBaseline')");
                    table.CheckConstraint("ck_knowledge_document_revisions_restore", "(revision_origin = 'Restore' AND restore_reason IS NOT NULL AND length(trim(restore_reason)) BETWEEN 5 AND 500 AND restored_from_revision_number IS NOT NULL AND restored_from_revision_number > 0 AND restored_from_revision_number < revision_number) OR (revision_origin <> 'Restore' AND restore_reason IS NULL AND restored_from_revision_number IS NULL)");
                    table.CheckConstraint("ck_knowledge_document_revisions_revision_number", "revision_number > 0");
                    table.CheckConstraint("ck_knowledge_document_revisions_summary", "summary IS NULL OR length(summary) <= 2000");
                    table.CheckConstraint("ck_knowledge_document_revisions_title", "length(title) BETWEEN 1 AND 300");
                    table.ForeignKey(
                        name: "FK_knowledge_document_revisions_knowledge_documents_knowledge_document_id",
                        column: x => x.knowledge_document_id,
                        principalTable: "knowledge_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_document_revisions_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_document_revisions_author_user_id",
                table: "knowledge_document_revisions",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_document_revisions_knowledge_document_id_revision_number",
                table: "knowledge_document_revisions",
                columns: new[] { "knowledge_document_id", "revision_number" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO knowledge_document_revisions (
                    knowledge_document_id,
                    revision_number,
                    title,
                    summary,
                    body_markdown,
                    author_user_id,
                    author_display_name_snapshot,
                    created_at,
                    lifecycle_context,
                    change_summary,
                    restore_reason,
                    restored_from_revision_number,
                    revision_origin)
                SELECT
                    id,
                    1,
                    title,
                    summary,
                    body_markdown,
                    NULL,
                    NULL,
                    strftime('%Y-%m-%dT%H:%M:%f+00:00', 'now'),
                    lifecycle_status,
                    NULL,
                    NULL,
                    NULL,
                    'MigrationBaseline'
                FROM knowledge_documents;

                UPDATE knowledge_documents
                SET current_revision_number = 1,
                    latest_published_revision_number = CASE
                        WHEN lifecycle_status = 'Published' THEN 1
                        ELSE NULL
                    END;

                CREATE TEMP TABLE __rev_b01_postflight_guard (
                    violation INTEGER NOT NULL CHECK (violation = 0)
                );
                INSERT INTO __rev_b01_postflight_guard (violation)
                SELECT CASE WHEN
                    (SELECT count(*) FROM knowledge_document_revisions)
                        <> (SELECT count(*) FROM knowledge_documents)
                    OR EXISTS (
                        SELECT 1
                        FROM knowledge_documents AS document
                        LEFT JOIN knowledge_document_revisions AS revision
                          ON revision.knowledge_document_id = document.id
                         AND revision.revision_number = 1
                        WHERE revision.id IS NULL
                           OR revision.title <> document.title
                           OR NOT (revision.summary IS document.summary)
                           OR revision.body_markdown <> document.body_markdown
                           OR revision.lifecycle_context <> document.lifecycle_status
                           OR revision.revision_origin <> 'MigrationBaseline'
                           OR revision.author_user_id IS NOT NULL
                           OR revision.author_display_name_snapshot IS NOT NULL
                           OR revision.change_summary IS NOT NULL
                           OR revision.restore_reason IS NOT NULL
                           OR revision.restored_from_revision_number IS NOT NULL
                           OR document.current_revision_number <> 1
                           OR (document.lifecycle_status = 'Published' AND document.latest_published_revision_number <> 1)
                           OR (document.lifecycle_status <> 'Published' AND document.latest_published_revision_number IS NOT NULL)
                    )
                    OR EXISTS (
                        SELECT 1
                        FROM evidence
                        WHERE knowledge_document_revision_number_snapshot IS NOT NULL
                    )
                    THEN 1 ELSE 0 END;
                DROP TABLE __rev_b01_postflight_guard;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TEMP TABLE __rev_b01_down_guard (
                    violation INTEGER NOT NULL CHECK (violation = 0)
                );
                INSERT INTO __rev_b01_down_guard (violation)
                SELECT CASE WHEN
                    EXISTS (
                        SELECT 1
                        FROM knowledge_document_revisions AS revision
                        LEFT JOIN knowledge_documents AS document
                          ON document.id = revision.knowledge_document_id
                        WHERE revision.revision_number <> 1
                           OR revision.revision_origin <> 'MigrationBaseline'
                           OR revision.author_user_id IS NOT NULL
                           OR revision.author_display_name_snapshot IS NOT NULL
                           OR revision.title <> document.title
                           OR NOT (revision.summary IS document.summary)
                           OR revision.body_markdown <> document.body_markdown
                           OR revision.lifecycle_context <> document.lifecycle_status
                           OR document.current_revision_number <> 1
                           OR (document.lifecycle_status = 'Published' AND document.latest_published_revision_number <> 1)
                           OR (document.lifecycle_status <> 'Published' AND document.latest_published_revision_number IS NOT NULL)
                    )
                    OR (SELECT count(*) FROM knowledge_document_revisions)
                        <> (SELECT count(*) FROM knowledge_documents)
                    OR EXISTS (
                        SELECT 1
                        FROM evidence
                        WHERE knowledge_document_revision_number_snapshot IS NOT NULL
                    )
                    THEN 1 ELSE 0 END;
                DROP TABLE __rev_b01_down_guard;
                """);

            migrationBuilder.DropTable(
                name: "knowledge_document_revisions");

            migrationBuilder.DropColumn(
                name: "current_revision_number",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "latest_published_revision_number",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "knowledge_document_revision_number_snapshot",
                table: "evidence");
        }
    }
}
