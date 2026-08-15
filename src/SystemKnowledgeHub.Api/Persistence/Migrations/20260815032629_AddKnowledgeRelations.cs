using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_relations",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    source_type = table.Column<string>(type: "TEXT", nullable: false),
                    source_id = table.Column<long>(type: "INTEGER", nullable: false),
                    target_type = table.Column<string>(type: "TEXT", nullable: false),
                    target_id = table.Column<long>(type: "INTEGER", nullable: false),
                    relation_type = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true),
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
                    table.PrimaryKey("PK_knowledge_relations", x => x.id);
                    table.CheckConstraint("ck_knowledge_relations_distinct_endpoints", "source_type <> target_type OR source_id <> target_id");
                    table.CheckConstraint("ck_knowledge_relations_relation_type", "relation_type IN ('Calls','Reads','Writes','UsesField','AppliesRule','PublishesVia','ConsumesVia','UsesIntegration','DependsOn')");
                    table.CheckConstraint("ck_knowledge_relations_source_type", "source_type IN ('System','DatabaseSource','BusinessFunction','DatabaseObject','DatabaseColumn','BusinessRule','Integration')");
                    table.CheckConstraint("ck_knowledge_relations_status", "knowledge_status IN ('Unknown','Inferred','Confirmed')");
                    table.CheckConstraint("ck_knowledge_relations_target_type", "target_type IN ('System','DatabaseSource','BusinessFunction','DatabaseObject','DatabaseColumn','BusinessRule','Integration')");
                    table.CheckConstraint("ck_knowledge_relations_version", "version >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_relations_relation_type_knowledge_status",
                table: "knowledge_relations",
                columns: new[] { "relation_type", "knowledge_status" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_relations_source_type_source_id_relation_type",
                table: "knowledge_relations",
                columns: new[] { "source_type", "source_id", "relation_type" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_relations_source_type_source_id_target_type_target_id_relation_type",
                table: "knowledge_relations",
                columns: new[] { "source_type", "source_id", "target_type", "target_id", "relation_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_relations_target_type_target_id_relation_type",
                table: "knowledge_relations",
                columns: new[] { "target_type", "target_id", "relation_type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_relations");
        }
    }
}
