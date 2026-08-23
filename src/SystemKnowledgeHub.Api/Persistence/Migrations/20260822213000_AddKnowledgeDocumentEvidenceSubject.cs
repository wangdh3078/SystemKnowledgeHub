using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations;

[DbContext(typeof(KnowledgeHubDbContext))]
[Migration("20260822213000_AddKnowledgeDocumentEvidenceSubject")]
public partial class AddKnowledgeDocumentEvidenceSubject : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        RebuildEvidenceTable(migrationBuilder, includeKnowledgeDocument: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        RebuildEvidenceTable(migrationBuilder, includeKnowledgeDocument: false);
    }

    private static void RebuildEvidenceTable(MigrationBuilder migrationBuilder, bool includeKnowledgeDocument)
    {
        var subjectTypes = includeKnowledgeDocument
            ? "'System','DatabaseSource','BusinessFunction','DatabaseObject','DatabaseColumn','BusinessRule','Integration','KnowledgeDocument','KnowledgeRelation','UnknownItem','Finding','Resolution','KnowledgeUpdate'"
            : "'System','DatabaseSource','BusinessFunction','DatabaseObject','DatabaseColumn','BusinessRule','Integration','KnowledgeRelation','UnknownItem','Finding','Resolution','KnowledgeUpdate'";

        migrationBuilder.CreateTable(
            name: "__ef_temp_evidence",
            columns: table => new
            {
                id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                evidence_type = table.Column<string>(type: "TEXT", nullable: false),
                subject_type = table.Column<string>(type: "TEXT", nullable: false),
                subject_id = table.Column<long>(type: "INTEGER", nullable: false),
                subject_detail_key = table.Column<string>(type: "TEXT", nullable: true),
                source_title = table.Column<string>(type: "TEXT", nullable: false),
                source_reference = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                source_locator_json = table.Column<string>(type: "TEXT", nullable: true),
                summary = table.Column<string>(type: "TEXT", nullable: true),
                support_reason = table.Column<string>(type: "TEXT", nullable: false),
                confidence = table.Column<string>(type: "TEXT", nullable: true),
                provider_user_id = table.Column<long>(type: "INTEGER", nullable: true),
                provider_knowledge_role_id = table.Column<long>(type: "INTEGER", nullable: true),
                provider_employee_no = table.Column<string>(type: "TEXT", nullable: true),
                provider_name = table.Column<string>(type: "TEXT", nullable: false),
                provider_role = table.Column<string>(type: "TEXT", nullable: false),
                provider_team = table.Column<string>(type: "TEXT", nullable: true),
                provider_job_title = table.Column<string>(type: "TEXT", nullable: true),
                provider_external_key = table.Column<string>(type: "TEXT", nullable: true),
                provider_source = table.Column<string>(type: "TEXT", nullable: true),
                provider_note = table.Column<string>(type: "TEXT", nullable: true),
                provided_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                version = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 1L),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_evidence", x => x.id);
                table.CheckConstraint("ck_evidence_confidence", "confidence IS NULL OR confidence IN ('High','Medium','Low')");
                table.CheckConstraint("ck_evidence_source_locator", "source_reference IS NOT NULL OR source_locator_json IS NOT NULL");
                table.CheckConstraint("ck_evidence_source_locator_json", "source_locator_json IS NULL OR (json_valid(source_locator_json) AND json_type(source_locator_json) = 'object')");
                table.CheckConstraint("ck_evidence_subject_type", $"subject_type IN ({subjectTypes})");
                table.CheckConstraint("ck_evidence_type", "evidence_type IN ('CodeReference','Sql','DatabaseSample','DatabaseComment','Api','MqMessage','ExistingDocument','HumanConfirmation')");
                table.CheckConstraint("ck_evidence_version", "version >= 1");
                table.ForeignKey("FK_evidence_knowledge_roles_provider_knowledge_role_id", x => x.provider_knowledge_role_id, "knowledge_roles", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_evidence_users_provider_user_id", x => x.provider_user_id, "users", "id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.Sql("INSERT INTO __ef_temp_evidence (id,evidence_type,subject_type,subject_id,subject_detail_key,source_title,source_reference,source_locator_json,summary,support_reason,confidence,provider_user_id,provider_knowledge_role_id,provider_employee_no,provider_name,provider_role,provider_team,provider_job_title,provider_external_key,provider_source,provider_note,provided_at,created_at,updated_at,version) SELECT id,evidence_type,subject_type,subject_id,subject_detail_key,source_title,source_reference,source_locator_json,summary,support_reason,confidence,provider_user_id,provider_knowledge_role_id,provider_employee_no,provider_name,provider_role,provider_team,provider_job_title,provider_external_key,provider_source,provider_note,provided_at,created_at,updated_at,version FROM evidence;");
        migrationBuilder.DropTable(name: "evidence");
        migrationBuilder.RenameTable(name: "__ef_temp_evidence", newName: "evidence");
        migrationBuilder.CreateIndex(name: "IX_evidence_evidence_type_provided_at", table: "evidence", columns: new[] { "evidence_type", "provided_at" }, descending: new[] { false, true });
        migrationBuilder.CreateIndex(name: "IX_evidence_provider_knowledge_role_id", table: "evidence", column: "provider_knowledge_role_id");
        migrationBuilder.CreateIndex(name: "IX_evidence_provider_user_id", table: "evidence", column: "provider_user_id");
        migrationBuilder.CreateIndex(name: "IX_evidence_source_reference", table: "evidence", column: "source_reference");
        migrationBuilder.CreateIndex(name: "IX_evidence_subject_type_subject_id_subject_detail_key", table: "evidence", columns: new[] { "subject_type", "subject_id", "subject_detail_key" });
    }
}
