using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHumanConfirmationCorrectionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "replaces_evidence_id",
                table: "evidence",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "withdrawal_reason",
                table: "evidence",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "withdrawn_at",
                table: "evidence",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "withdrawn_by_display_name",
                table: "evidence",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "withdrawn_by_user_id",
                table: "evidence",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_evidence_replaces_evidence_id",
                table: "evidence",
                column: "replaces_evidence_id",
                unique: true,
                filter: "replaces_evidence_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_withdrawn_by_user_id",
                table: "evidence",
                column: "withdrawn_by_user_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evidence_hc_lifecycle",
                table: "evidence",
                sql: "evidence_type = 'HumanConfirmation' OR (withdrawn_at IS NULL AND withdrawn_by_user_id IS NULL AND withdrawn_by_display_name IS NULL AND withdrawal_reason IS NULL AND replaces_evidence_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evidence_replacement_self",
                table: "evidence",
                sql: "replaces_evidence_id IS NULL OR replaces_evidence_id <> id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evidence_withdrawal_complete",
                table: "evidence",
                sql: "(withdrawn_at IS NULL AND withdrawn_by_user_id IS NULL AND withdrawn_by_display_name IS NULL AND withdrawal_reason IS NULL) OR (withdrawn_at IS NOT NULL AND withdrawn_by_user_id IS NOT NULL AND withdrawn_by_display_name IS NOT NULL AND withdrawal_reason IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evidence_withdrawal_name",
                table: "evidence",
                sql: "withdrawn_by_display_name IS NULL OR length(trim(withdrawn_by_display_name)) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evidence_withdrawal_reason",
                table: "evidence",
                sql: "withdrawal_reason IS NULL OR (length(trim(withdrawal_reason)) BETWEEN 1 AND 1000 AND withdrawal_reason = trim(withdrawal_reason))");

            migrationBuilder.AddForeignKey(
                name: "FK_evidence_evidence_replaces_evidence_id",
                table: "evidence",
                column: "replaces_evidence_id",
                principalTable: "evidence",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_evidence_users_withdrawn_by_user_id",
                table: "evidence",
                column: "withdrawn_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_evidence_evidence_replaces_evidence_id",
                table: "evidence");

            migrationBuilder.DropForeignKey(
                name: "FK_evidence_users_withdrawn_by_user_id",
                table: "evidence");

            migrationBuilder.DropIndex(
                name: "IX_evidence_replaces_evidence_id",
                table: "evidence");

            migrationBuilder.DropIndex(
                name: "IX_evidence_withdrawn_by_user_id",
                table: "evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_evidence_hc_lifecycle",
                table: "evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_evidence_replacement_self",
                table: "evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_evidence_withdrawal_complete",
                table: "evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_evidence_withdrawal_name",
                table: "evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_evidence_withdrawal_reason",
                table: "evidence");

            migrationBuilder.DropColumn(
                name: "replaces_evidence_id",
                table: "evidence");

            migrationBuilder.DropColumn(
                name: "withdrawal_reason",
                table: "evidence");

            migrationBuilder.DropColumn(
                name: "withdrawn_at",
                table: "evidence");

            migrationBuilder.DropColumn(
                name: "withdrawn_by_display_name",
                table: "evidence");

            migrationBuilder.DropColumn(
                name: "withdrawn_by_user_id",
                table: "evidence");
        }
    }
}
