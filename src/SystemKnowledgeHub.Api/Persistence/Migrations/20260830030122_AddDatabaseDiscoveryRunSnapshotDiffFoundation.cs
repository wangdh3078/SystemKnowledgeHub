using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseDiscoveryRunSnapshotDiffFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "database_discovery_scope_generations",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    profile_id = table.Column<long>(type: "INTEGER", nullable: false),
                    scope_fingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_discovery_scope_generations", x => x.id);
                    table.ForeignKey(
                        name: "FK_database_discovery_scope_generations_database_connection_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "database_connection_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "database_discovery_difference_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    difference_id = table.Column<long>(type: "INTEGER", nullable: false),
                    entity_kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    logical_identity = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    parent_logical_identity = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    display_name = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    state = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    before_json = table.Column<string>(type: "TEXT", maxLength: 65536, nullable: true),
                    after_json = table.Column<string>(type: "TEXT", maxLength: 65536, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_discovery_difference_entries", x => x.id);
                    table.CheckConstraint("ck_database_discovery_difference_entries_kind", "entity_kind IN ('Schema','DatabaseObject','Column','PrimaryKey','ForeignKey','UniqueConstraint','Index','Sequence')");
                    table.CheckConstraint("ck_database_discovery_difference_entries_state", "state IN ('Added','Changed','MissingFromSource')");
                });

            migrationBuilder.CreateTable(
                name: "database_discovery_differences",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    profile_id = table.Column<long>(type: "INTEGER", nullable: false),
                    base_snapshot_id = table.Column<long>(type: "INTEGER", nullable: true),
                    target_snapshot_id = table.Column<long>(type: "INTEGER", nullable: false),
                    scope_generation_id = table.Column<long>(type: "INTEGER", nullable: false),
                    algorithm_version = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    summary_counts_json = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    content_sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_discovery_differences", x => x.id);
                    table.CheckConstraint("ck_database_discovery_differences_algorithm", "algorithm_version >= 1");
                    table.CheckConstraint("ck_database_discovery_differences_sha256", "length(content_sha256) = 64");
                    table.ForeignKey(
                        name: "FK_database_discovery_differences_database_connection_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "database_connection_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_discovery_differences_database_discovery_scope_generations_scope_generation_id",
                        column: x => x.scope_generation_id,
                        principalTable: "database_discovery_scope_generations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "database_discovery_runs",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    profile_id = table.Column<long>(type: "INTEGER", nullable: false),
                    profile_configuration_revision = table.Column<long>(type: "INTEGER", nullable: false),
                    secret_version = table.Column<long>(type: "INTEGER", nullable: false),
                    base_snapshot_id = table.Column<long>(type: "INTEGER", nullable: true),
                    scope_generation_id = table.Column<long>(type: "INTEGER", nullable: true),
                    queued_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    lease_owner_id = table.Column<string>(type: "TEXT", maxLength: 96, nullable: true),
                    lease_token = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    lease_heartbeat_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    lease_expires_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    cancellation_requested_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    cancellation_requested_by_user_id = table.Column<long>(type: "INTEGER", nullable: true),
                    provider_type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    provider_version = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    requested_included_schemas_json = table.Column<string>(type: "TEXT", maxLength: 32768, nullable: false),
                    requested_provider_specific_options_json = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    scope_fingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    capability_snapshot_json = table.Column<string>(type: "TEXT", maxLength: 32768, nullable: true),
                    error_code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    error_summary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    safe_error_metadata_json = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    object_counts_json = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    requested_by_user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    requested_by_display_name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_discovery_runs", x => x.id);
                    table.CheckConstraint("ck_database_discovery_runs_lease", "(status = 'Running' AND lease_owner_id IS NOT NULL AND lease_token IS NOT NULL AND lease_expires_at IS NOT NULL) OR status <> 'Running'");
                    table.CheckConstraint("ck_database_discovery_runs_provider", "provider_type IN ('Oracle','PostgreSql','SqlServer')");
                    table.CheckConstraint("ck_database_discovery_runs_revisions", "profile_configuration_revision >= 1 AND secret_version >= 1 AND version >= 1");
                    table.CheckConstraint("ck_database_discovery_runs_status", "status IN ('Queued','Running','Succeeded','Failed','Cancelled')");
                    table.CheckConstraint("ck_database_discovery_runs_terminal", "(status IN ('Succeeded','Failed','Cancelled') AND completed_at IS NOT NULL) OR (status IN ('Queued','Running') AND completed_at IS NULL)");
                    table.ForeignKey(
                        name: "FK_database_discovery_runs_database_connection_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "database_connection_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_discovery_runs_database_discovery_scope_generations_scope_generation_id",
                        column: x => x.scope_generation_id,
                        principalTable: "database_discovery_scope_generations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_discovery_runs_users_cancellation_requested_by_user_id",
                        column: x => x.cancellation_requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_discovery_runs_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "database_discovery_snapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    run_id = table.Column<long>(type: "INTEGER", nullable: false),
                    profile_id = table.Column<long>(type: "INTEGER", nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    format_version = table.Column<int>(type: "INTEGER", nullable: false),
                    identity_algorithm_version = table.Column<int>(type: "INTEGER", nullable: false),
                    scope_generation_id = table.Column<long>(type: "INTEGER", nullable: false),
                    scope_fingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    completeness = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    canonical_content_json = table.Column<string>(type: "TEXT", nullable: false),
                    content_sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    counts_json = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_discovery_snapshots", x => x.id);
                    table.CheckConstraint("ck_database_discovery_snapshots_completeness", "completeness = 'Complete'");
                    table.CheckConstraint("ck_database_discovery_snapshots_sha256", "length(content_sha256) = 64");
                    table.CheckConstraint("ck_database_discovery_snapshots_versions", "format_version >= 1 AND identity_algorithm_version >= 1");
                    table.ForeignKey(
                        name: "FK_database_discovery_snapshots_database_connection_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "database_connection_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_discovery_snapshots_database_discovery_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "database_discovery_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_discovery_snapshots_database_discovery_scope_generations_scope_generation_id",
                        column: x => x.scope_generation_id,
                        principalTable: "database_discovery_scope_generations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_difference_entries_difference_id_entity_kind_logical_identity",
                table: "database_discovery_difference_entries",
                columns: new[] { "difference_id", "entity_kind", "logical_identity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_difference_entries_difference_id_state_entity_kind_id",
                table: "database_discovery_difference_entries",
                columns: new[] { "difference_id", "state", "entity_kind", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_differences_base_snapshot_id",
                table: "database_discovery_differences",
                column: "base_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_differences_profile_id_created_at",
                table: "database_discovery_differences",
                columns: new[] { "profile_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_differences_scope_generation_id",
                table: "database_discovery_differences",
                column: "scope_generation_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_differences_target_snapshot_id",
                table: "database_discovery_differences",
                column: "target_snapshot_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_runs_base_snapshot_id",
                table: "database_discovery_runs",
                column: "base_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_runs_cancellation_requested_by_user_id",
                table: "database_discovery_runs",
                column: "cancellation_requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_runs_profile_id_completed_at",
                table: "database_discovery_runs",
                columns: new[] { "profile_id", "completed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_runs_requested_by_user_id",
                table: "database_discovery_runs",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_runs_scope_generation_id",
                table: "database_discovery_runs",
                column: "scope_generation_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_runs_status_lease_expires_at",
                table: "database_discovery_runs",
                columns: new[] { "status", "lease_expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_runs_status_queued_at",
                table: "database_discovery_runs",
                columns: new[] { "status", "queued_at" });

            migrationBuilder.CreateIndex(
                name: "ux_database_discovery_runs_one_active_profile",
                table: "database_discovery_runs",
                column: "profile_id",
                unique: true,
                filter: "status IN ('Queued','Running')");

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_scope_generations_profile_id_scope_fingerprint",
                table: "database_discovery_scope_generations",
                columns: new[] { "profile_id", "scope_fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_snapshots_profile_id_scope_fingerprint_id",
                table: "database_discovery_snapshots",
                columns: new[] { "profile_id", "scope_fingerprint", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_snapshots_profile_id_scope_generation_id_captured_at",
                table: "database_discovery_snapshots",
                columns: new[] { "profile_id", "scope_generation_id", "captured_at" });

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_snapshots_run_id",
                table: "database_discovery_snapshots",
                column: "run_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_discovery_snapshots_scope_generation_id",
                table: "database_discovery_snapshots",
                column: "scope_generation_id");

            migrationBuilder.AddForeignKey(
                name: "FK_database_discovery_difference_entries_database_discovery_differences_difference_id",
                table: "database_discovery_difference_entries",
                column: "difference_id",
                principalTable: "database_discovery_differences",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_database_discovery_differences_database_discovery_snapshots_base_snapshot_id",
                table: "database_discovery_differences",
                column: "base_snapshot_id",
                principalTable: "database_discovery_snapshots",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_database_discovery_differences_database_discovery_snapshots_target_snapshot_id",
                table: "database_discovery_differences",
                column: "target_snapshot_id",
                principalTable: "database_discovery_snapshots",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_database_discovery_runs_database_discovery_snapshots_base_snapshot_id",
                table: "database_discovery_runs",
                column: "base_snapshot_id",
                principalTable: "database_discovery_snapshots",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQLite cannot generate DROP FOREIGN KEY for the intentional Run/Snapshot/Scope
            // cycle. The B02 tables contain only B02-owned history, so remove the complete
            // foundation atomically from the migration runner's perspective with FK checks
            // disabled outside its transaction, then restore enforcement immediately.
            migrationBuilder.Sql(
                """
                PRAGMA foreign_keys = OFF;
                DROP TABLE database_discovery_difference_entries;
                DROP TABLE database_discovery_differences;
                DROP TABLE database_discovery_snapshots;
                DROP TABLE database_discovery_runs;
                DROP TABLE database_discovery_scope_generations;
                PRAGMA foreign_keys = ON;
                """,
                suppressTransaction: true);
        }
    }
}
