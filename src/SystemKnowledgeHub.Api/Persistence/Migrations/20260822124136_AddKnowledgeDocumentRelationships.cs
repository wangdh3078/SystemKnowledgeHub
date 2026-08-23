using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeDocumentRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_knowledge_relations_relation_type",
                table: "knowledge_relations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_knowledge_relations_source_type",
                table: "knowledge_relations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_knowledge_relations_target_type",
                table: "knowledge_relations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_knowledge_relations_relation_type",
                table: "knowledge_relations",
                sql: "relation_type IN ('Calls','Reads','Writes','UsesField','AppliesRule','PublishesVia','ConsumesVia','UsesIntegration','DependsOn','Documents','References','AppliesTo','Implements','SpecifiedBy','VerifiedBy','Resolves','RelatedTo','Supersedes')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_knowledge_relations_source_type",
                table: "knowledge_relations",
                sql: "source_type IN ('System','DatabaseSource','BusinessFunction','DatabaseObject','DatabaseColumn','BusinessRule','Integration','KnowledgeDocument')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_knowledge_relations_target_type",
                table: "knowledge_relations",
                sql: "target_type IN ('System','DatabaseSource','BusinessFunction','DatabaseObject','DatabaseColumn','BusinessRule','Integration','KnowledgeDocument')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_knowledge_relations_relation_type",
                table: "knowledge_relations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_knowledge_relations_source_type",
                table: "knowledge_relations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_knowledge_relations_target_type",
                table: "knowledge_relations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_knowledge_relations_relation_type",
                table: "knowledge_relations",
                sql: "relation_type IN ('Calls','Reads','Writes','UsesField','AppliesRule','PublishesVia','ConsumesVia','UsesIntegration','DependsOn')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_knowledge_relations_source_type",
                table: "knowledge_relations",
                sql: "source_type IN ('System','DatabaseSource','BusinessFunction','DatabaseObject','DatabaseColumn','BusinessRule','Integration')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_knowledge_relations_target_type",
                table: "knowledge_relations",
                sql: "target_type IN ('System','DatabaseSource','BusinessFunction','DatabaseObject','DatabaseColumn','BusinessRule','Integration')");
        }
    }
}
