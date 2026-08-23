using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TightenRelationshipVocabulary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            RebuildKnowledgeRelations(migrationBuilder,
                "relation_type IN ('Calls','Reads','Writes','UsesField','AppliesRule','PublishesVia','ConsumesVia','UsesIntegration','DependsOn','Documents','References','AppliesTo','SpecifiedBy','VerifiedBy','Supersedes')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RebuildKnowledgeRelations(migrationBuilder,
                "relation_type IN ('Calls','Reads','Writes','UsesField','AppliesRule','PublishesVia','ConsumesVia','UsesIntegration','DependsOn','Documents','References','AppliesTo','Implements','SpecifiedBy','VerifiedBy','Resolves','RelatedTo','Supersedes')");
        }

        private static void RebuildKnowledgeRelations(MigrationBuilder migrationBuilder, string relationTypeConstraint)
        {
            migrationBuilder.Sql($"""
                CREATE TABLE knowledge_relations_new (
                    id INTEGER NOT NULL CONSTRAINT PK_knowledge_relations PRIMARY KEY AUTOINCREMENT,
                    source_type TEXT NOT NULL,
                    source_id INTEGER NOT NULL,
                    target_type TEXT NOT NULL,
                    target_id INTEGER NOT NULL,
                    relation_type TEXT NOT NULL,
                    description TEXT NULL,
                    created_at TEXT NOT NULL,
                    created_by_name TEXT NOT NULL,
                    created_by_role TEXT NULL,
                    updated_at TEXT NOT NULL,
                    knowledge_status TEXT NOT NULL,
                    knowledge_status_reason TEXT NULL,
                    knowledge_status_changed_at TEXT NOT NULL,
                    knowledge_status_changed_by_name TEXT NOT NULL,
                    knowledge_status_changed_by_role TEXT NOT NULL,
                    version INTEGER NOT NULL DEFAULT 1,
                    CONSTRAINT ck_knowledge_relations_source_type CHECK (source_type IN ('System','DatabaseSource','BusinessFunction','DatabaseObject','DatabaseColumn','BusinessRule','Integration','KnowledgeDocument')),
                    CONSTRAINT ck_knowledge_relations_target_type CHECK (target_type IN ('System','DatabaseSource','BusinessFunction','DatabaseObject','DatabaseColumn','BusinessRule','Integration','KnowledgeDocument')),
                    CONSTRAINT ck_knowledge_relations_relation_type CHECK ({relationTypeConstraint}),
                    CONSTRAINT ck_knowledge_relations_status CHECK (knowledge_status IN ('Unknown','Inferred','Confirmed')),
                    CONSTRAINT ck_knowledge_relations_distinct_endpoints CHECK (source_type <> target_type OR source_id <> target_id),
                    CONSTRAINT ck_knowledge_relations_version CHECK (version >= 1)
                );

                INSERT INTO knowledge_relations_new (
                    id, source_type, source_id, target_type, target_id, relation_type, description,
                    created_at, created_by_name, created_by_role, updated_at, knowledge_status,
                    knowledge_status_reason, knowledge_status_changed_at, knowledge_status_changed_by_name,
                    knowledge_status_changed_by_role, version)
                SELECT
                    id, source_type, source_id, target_type, target_id, relation_type, description,
                    created_at, created_by_name, created_by_role, updated_at, knowledge_status,
                    knowledge_status_reason, knowledge_status_changed_at, knowledge_status_changed_by_name,
                    knowledge_status_changed_by_role, version
                FROM knowledge_relations;

                DROP TABLE knowledge_relations;
                ALTER TABLE knowledge_relations_new RENAME TO knowledge_relations;

                CREATE UNIQUE INDEX IX_knowledge_relations_source_type_source_id_target_type_target_id_relation_type
                    ON knowledge_relations (source_type, source_id, target_type, target_id, relation_type);
                CREATE INDEX IX_knowledge_relations_source_type_source_id_relation_type
                    ON knowledge_relations (source_type, source_id, relation_type);
                CREATE INDEX IX_knowledge_relations_target_type_target_id_relation_type
                    ON knowledge_relations (target_type, target_id, relation_type);
                CREATE INDEX IX_knowledge_relations_relation_type_knowledge_status
                    ON knowledge_relations (relation_type, knowledge_status);
                """);
        }
    }
}
