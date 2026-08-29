using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_knowledge_document_revisions_id_knowledge_document_id",
                table: "knowledge_document_revisions",
                columns: new[] { "id", "knowledge_document_id" });

            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    knowledge_document_id = table.Column<long>(type: "INTEGER", nullable: false),
                    original_file_name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    extension = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    content_type = table.Column<string>(type: "TEXT", maxLength: 127, nullable: false),
                    size_bytes = table.Column<long>(type: "INTEGER", nullable: false),
                    storage_key = table.Column<string>(type: "TEXT", maxLength: 96, nullable: false),
                    sha256 = table.Column<byte[]>(type: "BLOB", nullable: false),
                    storage_state = table.Column<string>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    created_by_display_name_snapshot = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attachments", x => x.id);
                    table.UniqueConstraint("AK_attachments_id_knowledge_document_id", x => new { x.id, x.knowledge_document_id });
                    table.CheckConstraint("ck_attachments_content_type", "length(trim(content_type)) BETWEEN 1 AND 127");
                    table.CheckConstraint("ck_attachments_creator_snapshot", "length(trim(created_by_display_name_snapshot)) > 0");
                    table.CheckConstraint("ck_attachments_extension", "length(extension) BETWEEN 2 AND 16 AND extension = lower(extension) AND substr(extension, 1, 1) = '.'");
                    table.CheckConstraint("ck_attachments_kind", "kind IN ('Image','File')");
                    table.CheckConstraint("ck_attachments_original_file_name", "length(trim(original_file_name)) BETWEEN 1 AND 255");
                    table.CheckConstraint("ck_attachments_sha256", "length(sha256) = 32");
                    table.CheckConstraint("ck_attachments_size", "size_bytes > 0");
                    table.CheckConstraint("ck_attachments_storage_key", "length(storage_key) BETWEEN 1 AND 96");
                    table.CheckConstraint("ck_attachments_storage_state", "storage_state IN ('Ready','DeletePending')");
                    table.CheckConstraint("ck_attachments_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_attachments_knowledge_documents_knowledge_document_id",
                        column: x => x.knowledge_document_id,
                        principalTable: "knowledge_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attachments_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attachment_references",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    knowledge_document_id = table.Column<long>(type: "INTEGER", nullable: false),
                    knowledge_document_revision_id = table.Column<long>(type: "INTEGER", nullable: false),
                    attachment_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attachment_references", x => x.id);
                    table.ForeignKey(
                        name: "FK_attachment_references_attachments_attachment_id_knowledge_document_id",
                        columns: x => new { x.attachment_id, x.knowledge_document_id },
                        principalTable: "attachments",
                        principalColumns: new[] { "id", "knowledge_document_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attachment_references_knowledge_document_revisions_knowledge_document_revision_id_knowledge_document_id",
                        columns: x => new { x.knowledge_document_revision_id, x.knowledge_document_id },
                        principalTable: "knowledge_document_revisions",
                        principalColumns: new[] { "id", "knowledge_document_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attachment_references_attachment_id_knowledge_document_id",
                table: "attachment_references",
                columns: new[] { "attachment_id", "knowledge_document_id" });

            migrationBuilder.CreateIndex(
                name: "IX_attachment_references_attachment_id_knowledge_document_revision_id",
                table: "attachment_references",
                columns: new[] { "attachment_id", "knowledge_document_revision_id" });

            migrationBuilder.CreateIndex(
                name: "IX_attachment_references_knowledge_document_id",
                table: "attachment_references",
                column: "knowledge_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_attachment_references_knowledge_document_revision_id_attachment_id",
                table: "attachment_references",
                columns: new[] { "knowledge_document_revision_id", "attachment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attachment_references_knowledge_document_revision_id_knowledge_document_id",
                table: "attachment_references",
                columns: new[] { "knowledge_document_revision_id", "knowledge_document_id" });

            migrationBuilder.CreateIndex(
                name: "IX_attachments_created_by_user_id",
                table: "attachments",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_attachments_knowledge_document_id_created_at_id",
                table: "attachments",
                columns: new[] { "knowledge_document_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_attachments_storage_key",
                table: "attachments",
                column: "storage_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachment_references");

            migrationBuilder.DropTable(
                name: "attachments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_knowledge_document_revisions_id_knowledge_document_id",
                table: "knowledge_document_revisions");
        }
    }
}
