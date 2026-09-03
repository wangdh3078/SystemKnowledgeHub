using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPortalCompositionFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "portal_pages",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "NOCASE"),
                    primary_target_type = table.Column<string>(type: "TEXT", nullable: false),
                    primary_target_id = table.Column<long>(type: "INTEGER", nullable: false),
                    is_published = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    published_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    published_by_user_id = table.Column<long>(type: "INTEGER", nullable: true),
                    published_by_display_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    unpublished_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    unpublished_by_user_id = table.Column<long>(type: "INTEGER", nullable: true),
                    unpublished_by_display_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    created_by_display_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_by_display_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 1L),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    deleted_by_user_id = table.Column<long>(type: "INTEGER", nullable: true),
                    deleted_by_display_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portal_pages", x => x.id);
                    table.CheckConstraint("ck_portal_pages_deletion_audit", "is_deleted IN (0,1) AND ((is_deleted = 0 AND deleted_at IS NULL AND deleted_by_user_id IS NULL AND deleted_by_display_name IS NULL) OR (deleted_at IS NOT NULL AND deleted_by_user_id IS NOT NULL AND deleted_by_display_name IS NOT NULL AND length(trim(deleted_by_display_name)) > 0))");
                    table.CheckConstraint("ck_portal_pages_id", "id BETWEEN 1 AND 9007199254740991");
                    table.CheckConstraint("ck_portal_pages_publication_audit", "((published_at IS NULL AND published_by_user_id IS NULL AND published_by_display_name IS NULL) OR (published_at IS NOT NULL AND published_by_user_id IS NOT NULL AND published_by_display_name IS NOT NULL AND length(trim(published_by_display_name)) > 0)) AND ((unpublished_at IS NULL AND unpublished_by_user_id IS NULL AND unpublished_by_display_name IS NULL) OR (unpublished_at IS NOT NULL AND unpublished_by_user_id IS NOT NULL AND unpublished_by_display_name IS NOT NULL AND length(trim(unpublished_by_display_name)) > 0)) AND (is_published = 0 OR published_at IS NOT NULL)");
                    table.CheckConstraint("ck_portal_pages_published", "is_published IN (0,1)");
                    table.CheckConstraint("ck_portal_pages_target_id", "primary_target_id BETWEEN 1 AND 9007199254740991");
                    table.CheckConstraint("ck_portal_pages_target_type", "primary_target_type IN ('System','BusinessFunction','DatabaseObject','KnowledgeDocument','Integration')");
                    table.CheckConstraint("ck_portal_pages_title", "length(trim(title)) BETWEEN 1 AND 200");
                    table.CheckConstraint("ck_portal_pages_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_portal_pages_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_portal_pages_users_deleted_by_user_id",
                        column: x => x.deleted_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_portal_pages_users_published_by_user_id",
                        column: x => x.published_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_portal_pages_users_unpublished_by_user_id",
                        column: x => x.unpublished_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_portal_pages_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "portal_page_nodes",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    parent_id = table.Column<long>(type: "INTEGER", nullable: true),
                    title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "NOCASE"),
                    node_kind = table.Column<string>(type: "TEXT", nullable: false),
                    portal_page_id = table.Column<long>(type: "INTEGER", nullable: true),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    is_published = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    published_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    published_by_user_id = table.Column<long>(type: "INTEGER", nullable: true),
                    published_by_display_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    unpublished_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    unpublished_by_user_id = table.Column<long>(type: "INTEGER", nullable: true),
                    unpublished_by_display_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    created_by_display_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_by_display_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 1L),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    deleted_by_user_id = table.Column<long>(type: "INTEGER", nullable: true),
                    deleted_by_display_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portal_page_nodes", x => x.id);
                    table.CheckConstraint("ck_portal_page_nodes_deletion_audit", "is_deleted IN (0,1) AND ((is_deleted = 0 AND deleted_at IS NULL AND deleted_by_user_id IS NULL AND deleted_by_display_name IS NULL) OR (deleted_at IS NOT NULL AND deleted_by_user_id IS NOT NULL AND deleted_by_display_name IS NOT NULL AND length(trim(deleted_by_display_name)) > 0))");
                    table.CheckConstraint("ck_portal_page_nodes_id", "id BETWEEN 1 AND 9007199254740991");
                    table.CheckConstraint("ck_portal_page_nodes_kind", "node_kind IN ('Folder','Page')");
                    table.CheckConstraint("ck_portal_page_nodes_parent", "parent_id IS NULL OR parent_id <> id");
                    table.CheckConstraint("ck_portal_page_nodes_publication_audit", "((published_at IS NULL AND published_by_user_id IS NULL AND published_by_display_name IS NULL) OR (published_at IS NOT NULL AND published_by_user_id IS NOT NULL AND published_by_display_name IS NOT NULL AND length(trim(published_by_display_name)) > 0)) AND ((unpublished_at IS NULL AND unpublished_by_user_id IS NULL AND unpublished_by_display_name IS NULL) OR (unpublished_at IS NOT NULL AND unpublished_by_user_id IS NOT NULL AND unpublished_by_display_name IS NOT NULL AND length(trim(unpublished_by_display_name)) > 0)) AND (is_published = 0 OR published_at IS NOT NULL)");
                    table.CheckConstraint("ck_portal_page_nodes_published", "is_published IN (0,1)");
                    table.CheckConstraint("ck_portal_page_nodes_shape", "(node_kind = 'Folder' AND portal_page_id IS NULL) OR (node_kind = 'Page' AND portal_page_id IS NOT NULL)");
                    table.CheckConstraint("ck_portal_page_nodes_sort", "sort_order >= 0");
                    table.CheckConstraint("ck_portal_page_nodes_title", "length(trim(title)) BETWEEN 1 AND 200");
                    table.CheckConstraint("ck_portal_page_nodes_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_portal_page_nodes_portal_page_nodes_parent_id",
                        column: x => x.parent_id,
                        principalTable: "portal_page_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_portal_page_nodes_portal_pages_portal_page_id",
                        column: x => x.portal_page_id,
                        principalTable: "portal_pages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_portal_page_nodes_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_portal_page_nodes_users_deleted_by_user_id",
                        column: x => x.deleted_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_portal_page_nodes_users_published_by_user_id",
                        column: x => x.published_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_portal_page_nodes_users_unpublished_by_user_id",
                        column: x => x.unpublished_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_portal_page_nodes_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "portal_page_sections",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    portal_page_id = table.Column<long>(type: "INTEGER", nullable: false),
                    heading = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    source_kind = table.Column<string>(type: "TEXT", nullable: false),
                    reference_target_type = table.Column<string>(type: "TEXT", nullable: true),
                    reference_target_id = table.Column<long>(type: "INTEGER", nullable: true),
                    projection_kind = table.Column<string>(type: "TEXT", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portal_page_sections", x => x.id);
                    table.CheckConstraint("ck_portal_page_sections_heading", "length(trim(heading)) BETWEEN 1 AND 200");
                    table.CheckConstraint("ck_portal_page_sections_id", "id BETWEEN 1 AND 9007199254740991");
                    table.CheckConstraint("ck_portal_page_sections_projection_kind", "projection_kind IN ('Summary','KnowledgeDocumentBody','StructuredOverview','DatabaseStructure','AttachmentList','TrustSummary','RelatedKnowledge','Traceability')");
                    table.CheckConstraint("ck_portal_page_sections_reference", "(source_kind IN ('PrimaryTarget','Derived') AND reference_target_type IS NULL AND reference_target_id IS NULL) OR (source_kind = 'ExplicitReference' AND reference_target_type IS NOT NULL AND reference_target_id IS NOT NULL AND reference_target_type IN ('System','BusinessFunction','DatabaseObject','KnowledgeDocument','Integration') AND reference_target_id BETWEEN 1 AND 9007199254740991)");
                    table.CheckConstraint("ck_portal_page_sections_sort", "sort_order >= 0");
                    table.CheckConstraint("ck_portal_page_sections_source_kind", "source_kind IN ('PrimaryTarget','ExplicitReference','Derived')");
                    table.ForeignKey(
                        name: "FK_portal_page_sections_portal_pages_portal_page_id",
                        column: x => x.portal_page_id,
                        principalTable: "portal_pages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_portal_page_nodes_active_parent_sort_order",
                table: "portal_page_nodes",
                columns: new[] { "parent_id", "sort_order" },
                unique: true,
                filter: "parent_id IS NOT NULL AND is_deleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_portal_page_nodes_active_root_sort_order",
                table: "portal_page_nodes",
                column: "sort_order",
                unique: true,
                filter: "parent_id IS NULL AND is_deleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_portal_page_nodes_created_by_user_id",
                table: "portal_page_nodes",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_portal_page_nodes_deleted_by_user_id",
                table: "portal_page_nodes",
                column: "deleted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_portal_page_nodes_is_published_is_deleted",
                table: "portal_page_nodes",
                columns: new[] { "is_published", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "IX_portal_page_nodes_portal_page_id",
                table: "portal_page_nodes",
                column: "portal_page_id");

            migrationBuilder.CreateIndex(
                name: "IX_portal_page_nodes_published_by_user_id",
                table: "portal_page_nodes",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_portal_page_nodes_unpublished_by_user_id",
                table: "portal_page_nodes",
                column: "unpublished_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_portal_page_nodes_updated_by_user_id",
                table: "portal_page_nodes",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_portal_page_sections_portal_page_id_sort_order",
                table: "portal_page_sections",
                columns: new[] { "portal_page_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_portal_pages_created_by_user_id",
                table: "portal_pages",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_portal_pages_deleted_by_user_id",
                table: "portal_pages",
                column: "deleted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_portal_pages_is_published_is_deleted",
                table: "portal_pages",
                columns: new[] { "is_published", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "IX_portal_pages_primary_target_type_primary_target_id",
                table: "portal_pages",
                columns: new[] { "primary_target_type", "primary_target_id" });

            migrationBuilder.CreateIndex(
                name: "IX_portal_pages_published_by_user_id",
                table: "portal_pages",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_portal_pages_unpublished_by_user_id",
                table: "portal_pages",
                column: "unpublished_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_portal_pages_updated_by_user_id",
                table: "portal_pages",
                column: "updated_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "portal_page_nodes");

            migrationBuilder.DropTable(
                name: "portal_page_sections");

            migrationBuilder.DropTable(
                name: "portal_pages");
        }
    }
}
