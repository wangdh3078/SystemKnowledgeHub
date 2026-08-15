using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_rules",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    system_id = table.Column<long>(type: "INTEGER", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    condition_text = table.Column<string>(type: "TEXT", nullable: true),
                    result_text = table.Column<string>(type: "TEXT", nullable: true),
                    input_data_json = table.Column<string>(type: "TEXT", nullable: true),
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
                    table.PrimaryKey("PK_business_rules", x => x.id);
                    table.CheckConstraint("ck_business_rules_input_data", "input_data_json IS NULL OR (json_valid(input_data_json) AND json_type(input_data_json) = 'array')");
                    table.CheckConstraint("ck_business_rules_knowledge_status", "knowledge_status IN ('Unknown','Inferred','Confirmed')");
                    table.CheckConstraint("ck_business_rules_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_business_rules_systems_system_id",
                        column: x => x.system_id,
                        principalTable: "systems",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_rules_knowledge_status",
                table: "business_rules",
                column: "knowledge_status");

            migrationBuilder.CreateIndex(
                name: "IX_business_rules_system_id_knowledge_status_updated_at",
                table: "business_rules",
                columns: new[] { "system_id", "knowledge_status", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_business_rules_system_id_name",
                table: "business_rules",
                columns: new[] { "system_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_rules");
        }
    }
}
