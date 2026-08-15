using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessFunctions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_functions",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    system_id = table.Column<long>(type: "INTEGER", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    display_name = table.Column<string>(type: "TEXT", nullable: true),
                    function_type = table.Column<string>(type: "TEXT", nullable: false),
                    purpose = table.Column<string>(type: "TEXT", nullable: true),
                    caller_summary = table.Column<string>(type: "TEXT", nullable: true),
                    input_description = table.Column<string>(type: "TEXT", nullable: true),
                    output_description = table.Column<string>(type: "TEXT", nullable: true),
                    rewrite_status = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Unknown"),
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
                    table.PrimaryKey("PK_business_functions", x => x.id);
                    table.CheckConstraint("ck_business_functions_knowledge_status", "knowledge_status IN ('Unknown','Inferred','Confirmed')");
                    table.CheckConstraint("ck_business_functions_rewrite_status", "rewrite_status IN ('Keep','Change','Remove','Unknown')");
                    table.CheckConstraint("ck_business_functions_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_business_functions_systems_system_id",
                        column: x => x.system_id,
                        principalTable: "systems",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "business_process_steps",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    business_function_id = table.Column<long>(type: "INTEGER", nullable: false),
                    step_order = table.Column<int>(type: "INTEGER", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_process_steps", x => x.id);
                    table.CheckConstraint("ck_business_process_steps_order", "step_order > 0");
                    table.ForeignKey(
                        name: "FK_business_process_steps_business_functions_business_function_id",
                        column: x => x.business_function_id,
                        principalTable: "business_functions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_functions_knowledge_status",
                table: "business_functions",
                column: "knowledge_status");

            migrationBuilder.CreateIndex(
                name: "IX_business_functions_system_id_function_type_rewrite_status_knowledge_status_updated_at",
                table: "business_functions",
                columns: new[] { "system_id", "function_type", "rewrite_status", "knowledge_status", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_business_functions_system_id_name",
                table: "business_functions",
                columns: new[] { "system_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_business_process_steps_business_function_id_step_order",
                table: "business_process_steps",
                columns: new[] { "business_function_id", "step_order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_process_steps");

            migrationBuilder.DropTable(
                name: "business_functions");
        }
    }
}
