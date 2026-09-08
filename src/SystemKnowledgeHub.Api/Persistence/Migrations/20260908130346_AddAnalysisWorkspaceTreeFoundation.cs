using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisWorkspaceTreeFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analysis_nodes",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    parent_id = table.Column<long>(type: "INTEGER", nullable: true),
                    node_type = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    knowledge_document_id = table.Column<long>(type: "INTEGER", nullable: true),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_nodes", x => x.id);
                    table.CheckConstraint("ck_analysis_nodes_id", "id BETWEEN 1 AND 9007199254740991");
                    table.CheckConstraint("ck_analysis_nodes_order", "sort_order BETWEEN 0 AND 2147483647");
                    table.CheckConstraint("ck_analysis_nodes_parent", "parent_id IS NULL OR parent_id <> id");
                    table.CheckConstraint("ck_analysis_nodes_shape", "(node_type = 'Folder' AND title IS NOT NULL AND length(trim(title)) BETWEEN 1 AND 200 AND title = trim(title) AND knowledge_document_id IS NULL) OR (node_type = 'Document' AND title IS NULL AND knowledge_document_id IS NOT NULL)");
                    table.CheckConstraint("ck_analysis_nodes_type", "node_type IN ('Folder','Document')");
                    table.CheckConstraint("ck_analysis_nodes_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_analysis_nodes_analysis_nodes_parent_id",
                        column: x => x.parent_id,
                        principalTable: "analysis_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_analysis_nodes_knowledge_documents_knowledge_document_id",
                        column: x => x.knowledge_document_id,
                        principalTable: "knowledge_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analysis_nodes_document",
                table: "analysis_nodes",
                column: "knowledge_document_id",
                unique: true,
                filter: "knowledge_document_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_nodes_parent_order",
                table: "analysis_nodes",
                columns: new[] { "parent_id", "sort_order" },
                unique: true,
                filter: "parent_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_nodes_root_order",
                table: "analysis_nodes",
                column: "sort_order",
                unique: true,
                filter: "parent_id IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analysis_nodes");
        }
    }
}
