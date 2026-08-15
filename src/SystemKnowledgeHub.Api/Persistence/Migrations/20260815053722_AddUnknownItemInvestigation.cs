using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnknownItemInvestigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "unknown_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    item_code = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    system_id = table.Column<long>(type: "INTEGER", nullable: false),
                    question = table.Column<string>(type: "TEXT", nullable: false),
                    context = table.Column<string>(type: "TEXT", nullable: true),
                    priority = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    investigation_started_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    conclusion_confirmed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    created_by_name = table.Column<string>(type: "TEXT", nullable: false),
                    created_by_role = table.Column<string>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unknown_items", x => x.id);
                    table.CheckConstraint("ck_unknown_items_priority", "priority IN ('High','Medium','Low')");
                    table.CheckConstraint("ck_unknown_items_status", "status IN ('Open','Investigating','ConclusionConfirmed','Closed')");
                    table.CheckConstraint("ck_unknown_items_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_unknown_items_systems_system_id",
                        column: x => x.system_id,
                        principalTable: "systems",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "findings",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    unknown_item_id = table.Column<long>(type: "INTEGER", nullable: false),
                    content = table.Column<string>(type: "TEXT", nullable: false),
                    recorded_by_name = table.Column<string>(type: "TEXT", nullable: false),
                    recorded_by_role = table.Column<string>(type: "TEXT", nullable: false),
                    recorded_by_team = table.Column<string>(type: "TEXT", nullable: true),
                    recorded_by_external_key = table.Column<string>(type: "TEXT", nullable: true),
                    recorded_by_source = table.Column<string>(type: "TEXT", nullable: true),
                    recorded_by_note = table.Column<string>(type: "TEXT", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_findings", x => x.id);
                    table.ForeignKey(
                        name: "FK_findings_unknown_items_unknown_item_id",
                        column: x => x.unknown_item_id,
                        principalTable: "unknown_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_updates",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    unknown_item_id = table.Column<long>(type: "INTEGER", nullable: false),
                    target_type = table.Column<string>(type: "TEXT", nullable: false),
                    target_id = table.Column<long>(type: "INTEGER", nullable: false),
                    subject_detail_key = table.Column<string>(type: "TEXT", nullable: true),
                    change_summary = table.Column<string>(type: "TEXT", nullable: false),
                    before_json = table.Column<string>(type: "TEXT", nullable: false),
                    after_json = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    knowledge_status_before = table.Column<string>(type: "TEXT", nullable: true),
                    knowledge_status_after = table.Column<string>(type: "TEXT", nullable: true),
                    applied_by_name = table.Column<string>(type: "TEXT", nullable: true),
                    applied_by_role = table.Column<string>(type: "TEXT", nullable: true),
                    applied_by_team = table.Column<string>(type: "TEXT", nullable: true),
                    applied_by_external_key = table.Column<string>(type: "TEXT", nullable: true),
                    applied_by_source = table.Column<string>(type: "TEXT", nullable: true),
                    applied_by_note = table.Column<string>(type: "TEXT", nullable: true),
                    applied_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_updates", x => x.id);
                    table.CheckConstraint("ck_knowledge_updates_after_json", "json_valid(after_json)");
                    table.CheckConstraint("ck_knowledge_updates_applied_snapshot", "status = 'Proposed' OR (applied_by_name IS NOT NULL AND applied_by_role IS NOT NULL AND applied_at IS NOT NULL)");
                    table.CheckConstraint("ck_knowledge_updates_before_json", "json_valid(before_json)");
                    table.CheckConstraint("ck_knowledge_updates_status", "status IN ('Proposed','Applied')");
                    table.CheckConstraint("ck_knowledge_updates_status_pair", "(knowledge_status_before IS NULL AND knowledge_status_after IS NULL) OR (knowledge_status_before IS NOT NULL AND knowledge_status_after IS NOT NULL)");
                    table.CheckConstraint("ck_knowledge_updates_target_type", "target_type IN ('System','DatabaseSource','BusinessFunction','DatabaseObject','DatabaseColumn','BusinessRule','Integration')");
                    table.ForeignKey(
                        name: "FK_knowledge_updates_unknown_items_unknown_item_id",
                        column: x => x.unknown_item_id,
                        principalTable: "unknown_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resolutions",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    unknown_item_id = table.Column<long>(type: "INTEGER", nullable: false),
                    conclusion = table.Column<string>(type: "TEXT", nullable: false),
                    confirmed_by_name = table.Column<string>(type: "TEXT", nullable: true),
                    confirmed_by_role = table.Column<string>(type: "TEXT", nullable: true),
                    confirmed_by_team = table.Column<string>(type: "TEXT", nullable: true),
                    confirmed_by_external_key = table.Column<string>(type: "TEXT", nullable: true),
                    confirmed_by_source = table.Column<string>(type: "TEXT", nullable: true),
                    confirmed_by_note = table.Column<string>(type: "TEXT", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resolutions", x => x.id);
                    table.ForeignKey(
                        name: "FK_resolutions_unknown_items_unknown_item_id",
                        column: x => x.unknown_item_id,
                        principalTable: "unknown_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "unknown_item_activities",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    unknown_item_id = table.Column<long>(type: "INTEGER", nullable: false),
                    activity_type = table.Column<string>(type: "TEXT", nullable: false),
                    actor_name = table.Column<string>(type: "TEXT", nullable: false),
                    actor_role = table.Column<string>(type: "TEXT", nullable: false),
                    actor_team = table.Column<string>(type: "TEXT", nullable: true),
                    actor_external_key = table.Column<string>(type: "TEXT", nullable: true),
                    actor_source = table.Column<string>(type: "TEXT", nullable: true),
                    actor_note = table.Column<string>(type: "TEXT", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    note = table.Column<string>(type: "TEXT", nullable: true),
                    related_type = table.Column<string>(type: "TEXT", nullable: true),
                    related_id = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unknown_item_activities", x => x.id);
                    table.CheckConstraint("ck_unknown_item_activities_related_pair", "(related_type IS NULL AND related_id IS NULL) OR (related_type IS NOT NULL AND related_id IS NOT NULL)");
                    table.CheckConstraint("ck_unknown_item_activities_related_type", "related_type IS NULL OR related_type IN ('Finding','Evidence','Resolution','KnowledgeUpdate')");
                    table.CheckConstraint("ck_unknown_item_activities_type", "activity_type IN ('Created','StatusChanged','FindingAdded','EvidenceAdded','ResolutionRecorded','KnowledgeUpdateApplied','Closed','Reopened')");
                    table.ForeignKey(
                        name: "FK_unknown_item_activities_unknown_items_unknown_item_id",
                        column: x => x.unknown_item_id,
                        principalTable: "unknown_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "unknown_item_targets",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    unknown_item_id = table.Column<long>(type: "INTEGER", nullable: false),
                    target_type = table.Column<string>(type: "TEXT", nullable: false),
                    target_id = table.Column<long>(type: "INTEGER", nullable: false),
                    is_primary = table.Column<bool>(type: "INTEGER", nullable: false),
                    display_snapshot = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unknown_item_targets", x => x.id);
                    table.CheckConstraint("ck_unknown_item_targets_primary", "is_primary IN (0,1)");
                    table.CheckConstraint("ck_unknown_item_targets_type", "target_type IN ('System','DatabaseSource','BusinessFunction','DatabaseObject','DatabaseColumn','BusinessRule','Integration')");
                    table.ForeignKey(
                        name: "FK_unknown_item_targets_unknown_items_unknown_item_id",
                        column: x => x.unknown_item_id,
                        principalTable: "unknown_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_findings_unknown_item_id_recorded_at",
                table: "findings",
                columns: new[] { "unknown_item_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_updates_status_applied_at",
                table: "knowledge_updates",
                columns: new[] { "status", "applied_at" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_updates_target_type_target_id",
                table: "knowledge_updates",
                columns: new[] { "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_updates_unknown_item_id_status",
                table: "knowledge_updates",
                columns: new[] { "unknown_item_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_resolutions_unknown_item_id",
                table: "resolutions",
                column: "unknown_item_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unknown_item_activities_related_type_related_id",
                table: "unknown_item_activities",
                columns: new[] { "related_type", "related_id" });

            migrationBuilder.CreateIndex(
                name: "IX_unknown_item_activities_unknown_item_id_occurred_at_id",
                table: "unknown_item_activities",
                columns: new[] { "unknown_item_id", "occurred_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_unknown_item_targets_target_type_target_id_unknown_item_id",
                table: "unknown_item_targets",
                columns: new[] { "target_type", "target_id", "unknown_item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_unknown_item_targets_unknown_item_id",
                table: "unknown_item_targets",
                column: "unknown_item_id",
                unique: true,
                filter: "is_primary = 1");

            migrationBuilder.CreateIndex(
                name: "IX_unknown_item_targets_unknown_item_id_is_primary",
                table: "unknown_item_targets",
                columns: new[] { "unknown_item_id", "is_primary" });

            migrationBuilder.CreateIndex(
                name: "IX_unknown_item_targets_unknown_item_id_target_type_target_id",
                table: "unknown_item_targets",
                columns: new[] { "unknown_item_id", "target_type", "target_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unknown_items_item_code",
                table: "unknown_items",
                column: "item_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unknown_items_priority_status",
                table: "unknown_items",
                columns: new[] { "priority", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_unknown_items_status_updated_at",
                table: "unknown_items",
                columns: new[] { "status", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_unknown_items_system_id_status_priority_updated_at",
                table: "unknown_items",
                columns: new[] { "system_id", "status", "priority", "updated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "findings");

            migrationBuilder.DropTable(
                name: "knowledge_updates");

            migrationBuilder.DropTable(
                name: "resolutions");

            migrationBuilder.DropTable(
                name: "unknown_item_activities");

            migrationBuilder.DropTable(
                name: "unknown_item_targets");

            migrationBuilder.DropTable(
                name: "unknown_items");
        }
    }
}
