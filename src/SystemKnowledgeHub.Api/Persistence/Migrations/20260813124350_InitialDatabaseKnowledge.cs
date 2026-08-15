using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDatabaseKnowledge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "systems",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    display_name = table.Column<string>(type: "TEXT", nullable: false),
                    system_type = table.Column<string>(type: "TEXT", nullable: false),
                    lifecycle = table.Column<string>(type: "TEXT", nullable: false),
                    purpose = table.Column<string>(type: "TEXT", nullable: true),
                    main_users_json = table.Column<string>(type: "TEXT", nullable: true),
                    repository_name = table.Column<string>(type: "TEXT", nullable: true),
                    repository_url = table.Column<string>(type: "TEXT", nullable: true),
                    deployment_json = table.Column<string>(type: "TEXT", nullable: true),
                    main_projects_json = table.Column<string>(type: "TEXT", nullable: true),
                    main_entry_points_json = table.Column<string>(type: "TEXT", nullable: true),
                    notes = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    created_by_name = table.Column<string>(type: "TEXT", nullable: false),
                    created_by_role = table.Column<string>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    knowledge_status = table.Column<string>(type: "TEXT", nullable: false),
                    knowledge_status_reason = table.Column<string>(type: "TEXT", nullable: true),
                    knowledge_status_changed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    knowledge_status_changed_by_name = table.Column<string>(type: "TEXT", nullable: false),
                    knowledge_status_changed_by_role = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_systems", x => x.id);
                    table.CheckConstraint("ck_systems_deployment_json", "deployment_json IS NULL OR (json_valid(deployment_json) AND json_type(deployment_json) = 'array')");
                    table.CheckConstraint("ck_systems_entry_points_json", "main_entry_points_json IS NULL OR (json_valid(main_entry_points_json) AND json_type(main_entry_points_json) = 'array')");
                    table.CheckConstraint("ck_systems_knowledge_status", "knowledge_status IN ('Unknown','Inferred','Confirmed')");
                    table.CheckConstraint("ck_systems_lifecycle", "lifecycle IN ('Planned','InDevelopment','Running','Maintaining','Legacy','Retired')");
                    table.CheckConstraint("ck_systems_main_projects_json", "main_projects_json IS NULL OR (json_valid(main_projects_json) AND json_type(main_projects_json) = 'array')");
                    table.CheckConstraint("ck_systems_main_users_json", "main_users_json IS NULL OR (json_valid(main_users_json) AND json_type(main_users_json) = 'array')");
                });

            migrationBuilder.CreateTable(
                name: "database_sources",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    system_id = table.Column<long>(type: "INTEGER", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    engine = table.Column<string>(type: "TEXT", nullable: false),
                    environment = table.Column<string>(type: "TEXT", nullable: true),
                    instance_name = table.Column<string>(type: "TEXT", nullable: true),
                    service_name = table.Column<string>(type: "TEXT", nullable: true),
                    database_name = table.Column<string>(type: "TEXT", nullable: true),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    is_primary = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    created_by_name = table.Column<string>(type: "TEXT", nullable: false),
                    created_by_role = table.Column<string>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_sources", x => x.id);
                    table.CheckConstraint("ck_database_sources_is_primary", "is_primary IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_database_sources_systems_system_id",
                        column: x => x.system_id,
                        principalTable: "systems",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "database_objects",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    database_source_id = table.Column<long>(type: "INTEGER", nullable: false),
                    schema_name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    object_name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    object_type = table.Column<string>(type: "TEXT", nullable: false),
                    business_description = table.Column<string>(type: "TEXT", nullable: true),
                    estimated_rows = table.Column<long>(type: "INTEGER", nullable: true),
                    access_mode = table.Column<string>(type: "TEXT", nullable: false),
                    primary_key_columns_json = table.Column<string>(type: "TEXT", nullable: true),
                    business_key_columns_json = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    created_by_name = table.Column<string>(type: "TEXT", nullable: false),
                    created_by_role = table.Column<string>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    knowledge_status = table.Column<string>(type: "TEXT", nullable: false),
                    knowledge_status_reason = table.Column<string>(type: "TEXT", nullable: true),
                    knowledge_status_changed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    knowledge_status_changed_by_name = table.Column<string>(type: "TEXT", nullable: false),
                    knowledge_status_changed_by_role = table.Column<string>(type: "TEXT", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_objects", x => x.id);
                    table.CheckConstraint("ck_database_objects_access_mode", "access_mode IN ('Read','Write','ReadWrite','Unknown')");
                    table.CheckConstraint("ck_database_objects_business_keys", "business_key_columns_json IS NULL OR (json_valid(business_key_columns_json) AND json_type(business_key_columns_json) = 'array')");
                    table.CheckConstraint("ck_database_objects_knowledge_status", "knowledge_status IN ('Unknown','Inferred','Confirmed')");
                    table.CheckConstraint("ck_database_objects_primary_keys", "primary_key_columns_json IS NULL OR (json_valid(primary_key_columns_json) AND json_type(primary_key_columns_json) = 'array')");
                    table.CheckConstraint("ck_database_objects_rows", "estimated_rows IS NULL OR estimated_rows >= 0");
                    table.CheckConstraint("ck_database_objects_type", "object_type IN ('Table','View')");
                    table.CheckConstraint("ck_database_objects_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_database_objects_database_sources_database_source_id",
                        column: x => x.database_source_id,
                        principalTable: "database_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "database_columns",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    database_object_id = table.Column<long>(type: "INTEGER", nullable: false),
                    ordinal_position = table.Column<int>(type: "INTEGER", nullable: false),
                    column_name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    data_type = table.Column<string>(type: "TEXT", nullable: false),
                    is_nullable = table.Column<bool>(type: "INTEGER", nullable: false),
                    default_value = table.Column<string>(type: "TEXT", nullable: true),
                    business_description = table.Column<string>(type: "TEXT", nullable: true),
                    database_comment = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    knowledge_status = table.Column<string>(type: "TEXT", nullable: false),
                    knowledge_status_reason = table.Column<string>(type: "TEXT", nullable: true),
                    knowledge_status_changed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    knowledge_status_changed_by_name = table.Column<string>(type: "TEXT", nullable: false),
                    knowledge_status_changed_by_role = table.Column<string>(type: "TEXT", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_columns", x => x.id);
                    table.CheckConstraint("ck_database_columns_knowledge_status", "knowledge_status IN ('Unknown','Inferred','Confirmed')");
                    table.CheckConstraint("ck_database_columns_nullable", "is_nullable IN (0, 1)");
                    table.CheckConstraint("ck_database_columns_ordinal", "ordinal_position > 0");
                    table.CheckConstraint("ck_database_columns_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_database_columns_database_objects_database_object_id",
                        column: x => x.database_object_id,
                        principalTable: "database_objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "column_known_values",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    database_column_id = table.Column<long>(type: "INTEGER", nullable: false),
                    value_text = table.Column<string>(type: "TEXT", nullable: false),
                    meaning = table.Column<string>(type: "TEXT", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_column_known_values", x => x.id);
                    table.ForeignKey(
                        name: "FK_column_known_values_database_columns_database_column_id",
                        column: x => x.database_column_id,
                        principalTable: "database_columns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_column_known_values_database_column_id_sort_order_value_text",
                table: "column_known_values",
                columns: new[] { "database_column_id", "sort_order", "value_text" });

            migrationBuilder.CreateIndex(
                name: "IX_column_known_values_database_column_id_value_text",
                table: "column_known_values",
                columns: new[] { "database_column_id", "value_text" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_columns_column_name",
                table: "database_columns",
                column: "column_name");

            migrationBuilder.CreateIndex(
                name: "IX_database_columns_database_object_id_column_name",
                table: "database_columns",
                columns: new[] { "database_object_id", "column_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_columns_database_object_id_ordinal_position",
                table: "database_columns",
                columns: new[] { "database_object_id", "ordinal_position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_columns_knowledge_status",
                table: "database_columns",
                column: "knowledge_status");

            migrationBuilder.CreateIndex(
                name: "IX_database_objects_database_source_id_schema_name_object_name",
                table: "database_objects",
                columns: new[] { "database_source_id", "schema_name", "object_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_objects_database_source_id_schema_name_object_type_knowledge_status",
                table: "database_objects",
                columns: new[] { "database_source_id", "schema_name", "object_type", "knowledge_status" });

            migrationBuilder.CreateIndex(
                name: "IX_database_objects_knowledge_status",
                table: "database_objects",
                column: "knowledge_status");

            migrationBuilder.CreateIndex(
                name: "IX_database_objects_object_name",
                table: "database_objects",
                column: "object_name");

            migrationBuilder.CreateIndex(
                name: "IX_database_sources_engine",
                table: "database_sources",
                column: "engine");

            migrationBuilder.CreateIndex(
                name: "IX_database_sources_system_id",
                table: "database_sources",
                column: "system_id",
                unique: true,
                filter: "is_primary = 1");

            migrationBuilder.CreateIndex(
                name: "IX_database_sources_system_id_is_primary_name",
                table: "database_sources",
                columns: new[] { "system_id", "is_primary", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_database_sources_system_id_name",
                table: "database_sources",
                columns: new[] { "system_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_systems_knowledge_status",
                table: "systems",
                column: "knowledge_status");

            migrationBuilder.CreateIndex(
                name: "IX_systems_lifecycle_knowledge_status_updated_at",
                table: "systems",
                columns: new[] { "lifecycle", "knowledge_status", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_systems_name",
                table: "systems",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "column_known_values");

            migrationBuilder.DropTable(
                name: "database_columns");

            migrationBuilder.DropTable(
                name: "database_objects");

            migrationBuilder.DropTable(
                name: "database_sources");

            migrationBuilder.DropTable(
                name: "systems");
        }
    }
}
