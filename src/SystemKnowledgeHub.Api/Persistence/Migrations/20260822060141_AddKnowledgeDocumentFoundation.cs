using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeDocumentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    document_type = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false, collation: "NOCASE"),
                    summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    body_markdown = table.Column<string>(type: "TEXT", nullable: false),
                    lifecycle_status = table.Column<string>(type: "TEXT", nullable: false),
                    knowledge_status = table.Column<string>(type: "TEXT", nullable: false),
                    knowledge_status_reason = table.Column<string>(type: "TEXT", nullable: true),
                    knowledge_status_changed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    knowledge_status_changed_by_name = table.Column<string>(type: "TEXT", nullable: false),
                    knowledge_status_changed_by_role = table.Column<string>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    created_by_display_name = table.Column<string>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_by_display_name = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    archived_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    version = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_documents", x => x.id);
                    table.CheckConstraint("ck_knowledge_documents_document_type", "document_type IN ('Requirement','Specification','TestCase','Sop','Troubleshooting','KnowledgeArticle','DesignNote')");
                    table.CheckConstraint("ck_knowledge_documents_knowledge_status", "knowledge_status IN ('Unknown','Inferred','Confirmed')");
                    table.CheckConstraint("ck_knowledge_documents_lifecycle_status", "lifecycle_status IN ('Draft','Published','Archived')");
                    table.CheckConstraint("ck_knowledge_documents_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_knowledge_documents_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_documents_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_created_by_user_id",
                table: "knowledge_documents",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_document_type_lifecycle_status_updated_at",
                table: "knowledge_documents",
                columns: new[] { "document_type", "lifecycle_status", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_updated_by_user_id",
                table: "knowledge_documents",
                column: "updated_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_documents");
        }
    }
}
