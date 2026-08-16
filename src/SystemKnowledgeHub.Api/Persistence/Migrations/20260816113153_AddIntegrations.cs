using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integrations",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    integration_type = table.Column<string>(type: "TEXT", nullable: false),
                    source_system_id = table.Column<long>(type: "INTEGER", nullable: true),
                    source_party_name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    target_system_id = table.Column<long>(type: "INTEGER", nullable: true),
                    target_party_name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    flow_direction = table.Column<string>(type: "TEXT", nullable: false),
                    purpose = table.Column<string>(type: "TEXT", nullable: true),
                    topic_or_queue = table.Column<string>(type: "TEXT", nullable: true),
                    endpoint_display = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    endpoint_json = table.Column<string>(type: "TEXT", nullable: true),
                    database_source_id = table.Column<long>(type: "INTEGER", nullable: true),
                    database_object_id = table.Column<long>(type: "INTEGER", nullable: true),
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
                    table.PrimaryKey("PK_integrations", x => x.id);
                    table.CheckConstraint("ck_integrations_direction", "flow_direction IN ('OneWay','Bidirectional')");
                    table.CheckConstraint("ck_integrations_endpoint_json", "endpoint_json IS NULL OR json_valid(endpoint_json)");
                    table.CheckConstraint("ck_integrations_party_system", "source_system_id IS NOT NULL OR target_system_id IS NOT NULL");
                    table.CheckConstraint("ck_integrations_status", "knowledge_status IN ('Unknown','Inferred','Confirmed')");
                    table.CheckConstraint("ck_integrations_type", "integration_type IN ('HttpApi','RabbitMq','FileExchange','DatabaseDependency')");
                    table.CheckConstraint("ck_integrations_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_integrations_database_objects_database_object_id",
                        column: x => x.database_object_id,
                        principalTable: "database_objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_integrations_database_sources_database_source_id",
                        column: x => x.database_source_id,
                        principalTable: "database_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_integrations_systems_source_system_id",
                        column: x => x.source_system_id,
                        principalTable: "systems",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_integrations_systems_target_system_id",
                        column: x => x.target_system_id,
                        principalTable: "systems",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "integration_contract_fields",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    integration_id = table.Column<long>(type: "INTEGER", nullable: false),
                    ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    field_name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    data_type = table.Column<string>(type: "TEXT", nullable: true),
                    is_required = table.Column<bool>(type: "INTEGER", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    sample_value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_contract_fields", x => x.id);
                    table.CheckConstraint("ck_integration_contract_fields_ordinal", "ordinal > 0");
                    table.CheckConstraint("ck_integration_contract_fields_required", "is_required IN (0,1)");
                    table.ForeignKey(
                        name: "FK_integration_contract_fields_integrations_integration_id",
                        column: x => x.integration_id,
                        principalTable: "integrations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_integration_contract_fields_integration_id_field_name",
                table: "integration_contract_fields",
                columns: new[] { "integration_id", "field_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_contract_fields_integration_id_ordinal",
                table: "integration_contract_fields",
                columns: new[] { "integration_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integrations_database_object_id",
                table: "integrations",
                column: "database_object_id");

            migrationBuilder.CreateIndex(
                name: "IX_integrations_database_source_id",
                table: "integrations",
                column: "database_source_id");

            migrationBuilder.CreateIndex(
                name: "IX_integrations_endpoint_display",
                table: "integrations",
                column: "endpoint_display");

            migrationBuilder.CreateIndex(
                name: "IX_integrations_integration_type_knowledge_status",
                table: "integrations",
                columns: new[] { "integration_type", "knowledge_status" });

            migrationBuilder.CreateIndex(
                name: "IX_integrations_integration_type_name_source_party_name_target_party_name",
                table: "integrations",
                columns: new[] { "integration_type", "name", "source_party_name", "target_party_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integrations_source_system_id_integration_type",
                table: "integrations",
                columns: new[] { "source_system_id", "integration_type" });

            migrationBuilder.CreateIndex(
                name: "IX_integrations_target_system_id_integration_type",
                table: "integrations",
                columns: new[] { "target_system_id", "integration_type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_contract_fields");

            migrationBuilder.DropTable(
                name: "integrations");
        }
    }
}
