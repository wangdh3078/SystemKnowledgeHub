using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseDiscoveryConnectionFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "database_connection_profiles",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    database_source_id = table.Column<long>(type: "INTEGER", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false, collation: "NOCASE"),
                    provider_type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    host = table.Column<string>(type: "TEXT", maxLength: 253, nullable: false),
                    port = table.Column<int>(type: "INTEGER", nullable: false),
                    database_name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    service_name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    authentication_mode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    username = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    provider_specific_options_json = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    included_schemas_json = table.Column<string>(type: "TEXT", maxLength: 32768, nullable: false),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    connection_status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    latest_connection_test_attempt_id = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    last_connection_test_started_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    last_connection_test_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    last_connection_test_error_code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    last_connection_test_vendor_code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    last_connection_test_summary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    last_discovery_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    last_successful_discovery_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    configuration_revision = table.Column<long>(type: "INTEGER", nullable: false),
                    created_by_user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    created_by_display_name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_connection_profiles", x => x.id);
                    table.CheckConstraint("ck_database_connection_profiles_auth", "authentication_mode = 'UsernamePassword'");
                    table.CheckConstraint("ck_database_connection_profiles_enabled", "is_enabled IN (0,1)");
                    table.CheckConstraint("ck_database_connection_profiles_locator", "(provider_type = 'Oracle' AND service_name IS NOT NULL AND database_name IS NULL) OR (provider_type IN ('PostgreSql','SqlServer') AND service_name IS NULL AND database_name IS NOT NULL)");
                    table.CheckConstraint("ck_database_connection_profiles_port", "port BETWEEN 1 AND 65535");
                    table.CheckConstraint("ck_database_connection_profiles_provider", "provider_type IN ('Oracle','PostgreSql','SqlServer')");
                    table.CheckConstraint("ck_database_connection_profiles_revision", "configuration_revision >= 1");
                    table.CheckConstraint("ck_database_connection_profiles_status", "connection_status IN ('Unknown','Succeeded','Failed')");
                    table.CheckConstraint("ck_database_connection_profiles_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_database_connection_profiles_database_sources_database_source_id",
                        column: x => x.database_source_id,
                        principalTable: "database_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_connection_profiles_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "database_connection_audit_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    profile_id = table.Column<long>(type: "INTEGER", nullable: false),
                    action = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    outcome = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    error_code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    vendor_code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    actor_user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    actor_display_name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_connection_audit_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_database_connection_audit_events_database_connection_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "database_connection_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_connection_audit_events_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "database_connection_secrets",
                columns: table => new
                {
                    profile_id = table.Column<long>(type: "INTEGER", nullable: false),
                    protected_payload = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: true),
                    payload_format_version = table.Column<int>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_connection_secrets", x => x.profile_id);
                    table.CheckConstraint("ck_database_connection_secrets_format", "payload_format_version = 1");
                    table.CheckConstraint("ck_database_connection_secrets_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_database_connection_secrets_database_connection_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "database_connection_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_database_connection_audit_events_actor_user_id",
                table: "database_connection_audit_events",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_connection_audit_events_profile_id_occurred_at",
                table: "database_connection_audit_events",
                columns: new[] { "profile_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_database_connection_profiles_created_by_user_id",
                table: "database_connection_profiles",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_connection_profiles_database_source_id",
                table: "database_connection_profiles",
                column: "database_source_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_connection_profiles_is_enabled_provider_type",
                table: "database_connection_profiles",
                columns: new[] { "is_enabled", "provider_type" });

            migrationBuilder.CreateIndex(
                name: "IX_database_connection_profiles_name",
                table: "database_connection_profiles",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "database_connection_audit_events");

            migrationBuilder.DropTable(
                name: "database_connection_secrets");

            migrationBuilder.DropTable(
                name: "database_connection_profiles");
        }
    }
}
