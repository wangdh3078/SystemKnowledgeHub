using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualDiscoverySyncFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "database_comment",
                table: "database_objects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "technical_identity",
                table: "database_objects",
                type: "TEXT",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "technical_identity_algorithm_version",
                table: "database_objects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "technical_identity",
                table: "database_columns",
                type: "TEXT",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "technical_identity_algorithm_version",
                table: "database_columns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("""
                UPDATE database_objects
                SET technical_identity = 'legacy:object:v1:' || id
                WHERE technical_identity = '';
                """);
            migrationBuilder.Sql("""
                UPDATE database_columns
                SET technical_identity = 'legacy:column:v1:' || id
                WHERE technical_identity = '';
                """);

            migrationBuilder.CreateTable(
                name: "database_column_discovery_bindings",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    profile_id = table.Column<long>(type: "INTEGER", nullable: false),
                    scope_generation_id = table.Column<long>(type: "INTEGER", nullable: false),
                    identity_algorithm_version = table.Column<int>(type: "INTEGER", nullable: false),
                    schema_logical_identity = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    parent_object_logical_identity = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    logical_identity = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    database_column_id = table.Column<long>(type: "INTEGER", nullable: false),
                    first_applied_snapshot_id = table.Column<long>(type: "INTEGER", nullable: false),
                    last_applied_snapshot_id = table.Column<long>(type: "INTEGER", nullable: false),
                    source_missing_since_snapshot_id = table.Column<long>(type: "INTEGER", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_column_discovery_bindings", x => x.id);
                    table.CheckConstraint("ck_database_column_discovery_bindings_versions", "identity_algorithm_version >= 1 AND version >= 1");
                    table.ForeignKey(
                        name: "FK_database_column_discovery_bindings_database_columns_database_column_id",
                        column: x => x.database_column_id,
                        principalTable: "database_columns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_column_discovery_bindings_database_connection_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "database_connection_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_column_discovery_bindings_database_discovery_scope_generations_scope_generation_id",
                        column: x => x.scope_generation_id,
                        principalTable: "database_discovery_scope_generations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_column_discovery_bindings_database_discovery_snapshots_first_applied_snapshot_id",
                        column: x => x.first_applied_snapshot_id,
                        principalTable: "database_discovery_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_column_discovery_bindings_database_discovery_snapshots_last_applied_snapshot_id",
                        column: x => x.last_applied_snapshot_id,
                        principalTable: "database_discovery_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_column_discovery_bindings_database_discovery_snapshots_source_missing_since_snapshot_id",
                        column: x => x.source_missing_since_snapshot_id,
                        principalTable: "database_discovery_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "database_discovery_sync_plans",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    profile_id = table.Column<long>(type: "INTEGER", nullable: false),
                    database_source_id = table.Column<long>(type: "INTEGER", nullable: false),
                    profile_configuration_revision = table.Column<long>(type: "INTEGER", nullable: false),
                    base_snapshot_id = table.Column<long>(type: "INTEGER", nullable: true),
                    target_snapshot_id = table.Column<long>(type: "INTEGER", nullable: false),
                    target_difference_id = table.Column<long>(type: "INTEGER", nullable: true),
                    scope_generation_id = table.Column<long>(type: "INTEGER", nullable: false),
                    identity_algorithm_version = table.Column<int>(type: "INTEGER", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    selection_format_version = table.Column<int>(type: "INTEGER", nullable: false),
                    selection_json = table.Column<string>(type: "TEXT", nullable: false),
                    preview_format_version = table.Column<int>(type: "INTEGER", nullable: true),
                    preview_payload_json = table.Column<string>(type: "TEXT", nullable: true),
                    preview_hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    confirmed_preview_hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    created_by_user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    created_by_display_name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    confirmed_by_user_id = table.Column<long>(type: "INTEGER", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    applied_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_discovery_sync_plans", x => x.id);
                    table.CheckConstraint("ck_database_discovery_sync_plans_hashes", "(preview_hash IS NULL OR length(preview_hash) = 64) AND (confirmed_preview_hash IS NULL OR length(confirmed_preview_hash) = 64)");
                    table.CheckConstraint("ck_database_discovery_sync_plans_selection", "json_valid(selection_json) AND json_type(selection_json) = 'array'");
                    table.CheckConstraint("ck_database_discovery_sync_plans_status", "status IN ('Draft','Ready','Applied','Superseded')");
                    table.CheckConstraint("ck_database_discovery_sync_plans_versions", "profile_configuration_revision >= 1 AND selection_format_version >= 1 AND identity_algorithm_version >= 1 AND version >= 1");
                    table.ForeignKey(
                        name: "FK_database_discovery_sync_plans_database_connection_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "database_connection_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_discovery_sync_plans_database_discovery_differences_target_difference_id",
                        column: x => x.target_difference_id,
                        principalTable: "database_discovery_differences",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_discovery_sync_plans_database_discovery_scope_generations_scope_generation_id",
                        column: x => x.scope_generation_id,
                        principalTable: "database_discovery_scope_generations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_discovery_sync_plans_database_discovery_snapshots_base_snapshot_id",
                        column: x => x.base_snapshot_id,
                        principalTable: "database_discovery_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_discovery_sync_plans_database_discovery_snapshots_target_snapshot_id",
                        column: x => x.target_snapshot_id,
                        principalTable: "database_discovery_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_discovery_sync_plans_database_sources_database_source_id",
                        column: x => x.database_source_id,
                        principalTable: "database_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_discovery_sync_plans_users_confirmed_by_user_id",
                        column: x => x.confirmed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_discovery_sync_plans_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "database_object_discovery_bindings",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    profile_id = table.Column<long>(type: "INTEGER", nullable: false),
                    scope_generation_id = table.Column<long>(type: "INTEGER", nullable: false),
                    identity_algorithm_version = table.Column<int>(type: "INTEGER", nullable: false),
                    schema_logical_identity = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    logical_identity = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    database_object_id = table.Column<long>(type: "INTEGER", nullable: false),
                    first_applied_snapshot_id = table.Column<long>(type: "INTEGER", nullable: false),
                    last_applied_snapshot_id = table.Column<long>(type: "INTEGER", nullable: false),
                    source_missing_since_snapshot_id = table.Column<long>(type: "INTEGER", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_object_discovery_bindings", x => x.id);
                    table.CheckConstraint("ck_database_object_discovery_bindings_versions", "identity_algorithm_version >= 1 AND version >= 1");
                    table.ForeignKey(
                        name: "FK_database_object_discovery_bindings_database_connection_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "database_connection_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_object_discovery_bindings_database_discovery_scope_generations_scope_generation_id",
                        column: x => x.scope_generation_id,
                        principalTable: "database_discovery_scope_generations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_object_discovery_bindings_database_discovery_snapshots_first_applied_snapshot_id",
                        column: x => x.first_applied_snapshot_id,
                        principalTable: "database_discovery_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_object_discovery_bindings_database_discovery_snapshots_last_applied_snapshot_id",
                        column: x => x.last_applied_snapshot_id,
                        principalTable: "database_discovery_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_object_discovery_bindings_database_discovery_snapshots_source_missing_since_snapshot_id",
                        column: x => x.source_missing_since_snapshot_id,
                        principalTable: "database_discovery_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_object_discovery_bindings_database_objects_database_object_id",
                        column: x => x.database_object_id,
                        principalTable: "database_objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "database_discovery_sync_apply_results",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    plan_id = table.Column<long>(type: "INTEGER", nullable: false),
                    created_objects = table.Column<int>(type: "INTEGER", nullable: false),
                    linked_objects = table.Column<int>(type: "INTEGER", nullable: false),
                    created_columns = table.Column<int>(type: "INTEGER", nullable: false),
                    linked_columns = table.Column<int>(type: "INTEGER", nullable: false),
                    updated_objects = table.Column<int>(type: "INTEGER", nullable: false),
                    updated_columns = table.Column<int>(type: "INTEGER", nullable: false),
                    marked_missing = table.Column<int>(type: "INTEGER", nullable: false),
                    cleared_missing = table.Column<int>(type: "INTEGER", nullable: false),
                    applied_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    applied_by_user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    applied_by_display_name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_discovery_sync_apply_results", x => x.id);
                    table.CheckConstraint("ck_database_discovery_sync_apply_results_counts", "created_objects >= 0 AND linked_objects >= 0 AND created_columns >= 0 AND linked_columns >= 0 AND updated_objects >= 0 AND updated_columns >= 0 AND marked_missing >= 0 AND cleared_missing >= 0");
                    table.ForeignKey(
                        name: "FK_database_discovery_sync_apply_results_database_discovery_sync_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "database_discovery_sync_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_discovery_sync_apply_results_users_applied_by_user_id",
                        column: x => x.applied_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "database_discovery_sync_audit_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    profile_id = table.Column<long>(type: "INTEGER", nullable: false),
                    plan_id = table.Column<long>(type: "INTEGER", nullable: true),
                    action = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    outcome = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    reason_code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    safe_metadata_json = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    actor_user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    actor_display_name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_discovery_sync_audit_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_database_discovery_sync_audit_events_database_connection_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "database_connection_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_discovery_sync_audit_events_database_discovery_sync_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "database_discovery_sync_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_discovery_sync_audit_events_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_database_objects_database_source_id_technical_identity_algorithm_version_technical_identity",
                table: "database_objects",
                columns: new[] { "database_source_id", "technical_identity_algorithm_version", "technical_identity" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_database_objects_technical_identity_version",
                table: "database_objects",
                sql: "technical_identity_algorithm_version >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_database_columns_database_object_id_technical_identity_algorithm_version_technical_identity",
                table: "database_columns",
                columns: new[] { "database_object_id", "technical_identity_algorithm_version", "technical_identity" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_database_columns_technical_identity_version",
                table: "database_columns",
                sql: "technical_identity_algorithm_version >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_database_column_discovery_bindings_database_column_id",
                table: "database_column_discovery_bindings",
                column: "database_column_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_column_discovery_bindings_first_applied_snapshot_id",
                table: "database_column_discovery_bindings",
                column: "first_applied_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_column_discovery_bindings_last_applied_snapshot_id",
                table: "database_column_discovery_bindings",
                column: "last_applied_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_column_discovery_bindings_profile_id_scope_generation_id_identity_algorithm_version_logical_identity",
                table: "database_column_discovery_bindings",
                columns: new[] { "profile_id", "scope_generation_id", "identity_algorithm_version", "logical_identity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_column_discovery_bindings_scope_generation_id",
                table: "database_column_discovery_bindings",
                column: "scope_generation_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_column_discovery_bindings_source_missing_since_snapshot_id",
                table: "database_column_discovery_bindings",
                column: "source_missing_since_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_sync_apply_results_applied_by_user_id",
                table: "database_discovery_sync_apply_results",
                column: "applied_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_sync_apply_results_plan_id",
                table: "database_discovery_sync_apply_results",
                column: "plan_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_sync_audit_events_actor_user_id",
                table: "database_discovery_sync_audit_events",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_sync_audit_events_plan_id_occurred_at",
                table: "database_discovery_sync_audit_events",
                columns: new[] { "plan_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_sync_audit_events_profile_id_occurred_at",
                table: "database_discovery_sync_audit_events",
                columns: new[] { "profile_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_sync_plans_base_snapshot_id",
                table: "database_discovery_sync_plans",
                column: "base_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_sync_plans_confirmed_by_user_id",
                table: "database_discovery_sync_plans",
                column: "confirmed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_sync_plans_created_by_user_id",
                table: "database_discovery_sync_plans",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_sync_plans_database_source_id",
                table: "database_discovery_sync_plans",
                column: "database_source_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_sync_plans_profile_id_status_created_at",
                table: "database_discovery_sync_plans",
                columns: new[] { "profile_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_sync_plans_scope_generation_id",
                table: "database_discovery_sync_plans",
                column: "scope_generation_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_sync_plans_target_difference_id",
                table: "database_discovery_sync_plans",
                column: "target_difference_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_sync_plans_target_snapshot_id",
                table: "database_discovery_sync_plans",
                column: "target_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_object_discovery_bindings_database_object_id",
                table: "database_object_discovery_bindings",
                column: "database_object_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_object_discovery_bindings_first_applied_snapshot_id",
                table: "database_object_discovery_bindings",
                column: "first_applied_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_object_discovery_bindings_last_applied_snapshot_id",
                table: "database_object_discovery_bindings",
                column: "last_applied_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_object_discovery_bindings_profile_id_scope_generation_id_identity_algorithm_version_logical_identity",
                table: "database_object_discovery_bindings",
                columns: new[] { "profile_id", "scope_generation_id", "identity_algorithm_version", "logical_identity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_object_discovery_bindings_scope_generation_id",
                table: "database_object_discovery_bindings",
                column: "scope_generation_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_object_discovery_bindings_source_missing_since_snapshot_id",
                table: "database_object_discovery_bindings",
                column: "source_missing_since_snapshot_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "database_column_discovery_bindings");

            migrationBuilder.DropTable(
                name: "database_discovery_sync_apply_results");

            migrationBuilder.DropTable(
                name: "database_discovery_sync_audit_events");

            migrationBuilder.DropTable(
                name: "database_object_discovery_bindings");

            migrationBuilder.DropTable(
                name: "database_discovery_sync_plans");

            migrationBuilder.DropIndex(
                name: "IX_database_objects_database_source_id_technical_identity_algorithm_version_technical_identity",
                table: "database_objects");

            migrationBuilder.DropCheckConstraint(
                name: "ck_database_objects_technical_identity_version",
                table: "database_objects");

            migrationBuilder.DropIndex(
                name: "IX_database_columns_database_object_id_technical_identity_algorithm_version_technical_identity",
                table: "database_columns");

            migrationBuilder.DropCheckConstraint(
                name: "ck_database_columns_technical_identity_version",
                table: "database_columns");

            migrationBuilder.DropColumn(
                name: "database_comment",
                table: "database_objects");

            migrationBuilder.DropColumn(
                name: "technical_identity",
                table: "database_objects");

            migrationBuilder.DropColumn(
                name: "technical_identity_algorithm_version",
                table: "database_objects");

            migrationBuilder.DropColumn(
                name: "technical_identity",
                table: "database_columns");

            migrationBuilder.DropColumn(
                name: "technical_identity_algorithm_version",
                table: "database_columns");
        }
    }
}
