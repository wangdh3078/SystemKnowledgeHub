using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHumanConfirmationCurrentUserSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "provider_employee_no",
                table: "evidence",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_job_title",
                table: "evidence",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "provider_knowledge_role_id",
                table: "evidence",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "provider_user_id",
                table: "evidence",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_evidence_provider_knowledge_role_id",
                table: "evidence",
                column: "provider_knowledge_role_id");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_provider_user_id",
                table: "evidence",
                column: "provider_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_evidence_knowledge_roles_provider_knowledge_role_id",
                table: "evidence",
                column: "provider_knowledge_role_id",
                principalTable: "knowledge_roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_evidence_users_provider_user_id",
                table: "evidence",
                column: "provider_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_evidence_knowledge_roles_provider_knowledge_role_id",
                table: "evidence");

            migrationBuilder.DropForeignKey(
                name: "FK_evidence_users_provider_user_id",
                table: "evidence");

            migrationBuilder.DropIndex(
                name: "IX_evidence_provider_knowledge_role_id",
                table: "evidence");

            migrationBuilder.DropIndex(
                name: "IX_evidence_provider_user_id",
                table: "evidence");

            migrationBuilder.DropColumn(
                name: "provider_employee_no",
                table: "evidence");

            migrationBuilder.DropColumn(
                name: "provider_job_title",
                table: "evidence");

            migrationBuilder.DropColumn(
                name: "provider_knowledge_role_id",
                table: "evidence");

            migrationBuilder.DropColumn(
                name: "provider_user_id",
                table: "evidence");
        }
    }
}
