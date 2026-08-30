using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalPasswordLifecycleSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "must_change_password",
                table: "local_login_credentials",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_local_login_credentials_must_change_password",
                table: "local_login_credentials",
                sql: "must_change_password IN (0,1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_local_login_credentials_must_change_password",
                table: "local_login_credentials");

            migrationBuilder.DropColumn(
                name: "must_change_password",
                table: "local_login_credentials");
        }
    }
}
