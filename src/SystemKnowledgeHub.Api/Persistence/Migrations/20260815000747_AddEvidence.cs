using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evidence",
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
                    provider_name = table.Column<string>(type: "TEXT", nullable: false),
                    provider_role = table.Column<string>(type: "TEXT", nullable: false),
                    provider_team = table.Column<string>(type: "TEXT", nullable: true),
                    provider_external_key = table.Column<string>(type: "TEXT", nullable: true),
                    provider_source = table.Column<string>(type: "TEXT", nullable: true),
                    provider_note = table.Column<string>(type: "TEXT", nullable: true),
                    provided_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence", x => x.id);
                    table.CheckConstraint("ck_evidence_confidence", "confidence IS NULL OR confidence IN ('High','Medium','Low')");
                    table.CheckConstraint("ck_evidence_source_locator", "source_reference IS NOT NULL OR source_locator_json IS NOT NULL");
                    table.CheckConstraint("ck_evidence_source_locator_json", "source_locator_json IS NULL OR (json_valid(source_locator_json) AND json_type(source_locator_json) = 'object')");
                    table.CheckConstraint("ck_evidence_subject_type", "subject_type IN ('System','DatabaseSource','BusinessFunction','DatabaseObject','DatabaseColumn','BusinessRule','Integration','KnowledgeRelation','UnknownItem','Finding','Resolution','KnowledgeUpdate')");
                    table.CheckConstraint("ck_evidence_type", "evidence_type IN ('CodeReference','Sql','DatabaseSample','DatabaseComment','Api','MqMessage','ExistingDocument','HumanConfirmation')");
                    table.CheckConstraint("ck_evidence_version", "version >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_evidence_type_provided_at",
                table: "evidence",
                columns: new[] { "evidence_type", "provided_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_source_reference",
                table: "evidence",
                column: "source_reference");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_subject_type_subject_id_subject_detail_key",
                table: "evidence",
                columns: new[] { "subject_type", "subject_id", "subject_detail_key" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evidence");
        }
    }
}
