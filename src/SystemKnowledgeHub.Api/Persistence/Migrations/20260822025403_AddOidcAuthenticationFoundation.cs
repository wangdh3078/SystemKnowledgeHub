using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOidcAuthenticationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "access_level",
                table: "users",
                type: "TEXT",
                nullable: false,
                defaultValue: "Viewer");

            migrationBuilder.CreateTable(
                name: "login_identities",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    provider = table.Column<string>(type: "TEXT", nullable: false),
                    subject = table.Column<string>(type: "TEXT", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_identities", x => x.id);
                    table.CheckConstraint("ck_login_identities_is_active", "is_active IN (0,1)");
                    table.CheckConstraint("ck_login_identities_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_login_identities_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_access_level",
                table: "users",
                sql: "access_level IN ('Viewer','Editor','Administrator')");

            migrationBuilder.CreateIndex(
                name: "IX_login_identities_provider_subject",
                table: "login_identities",
                columns: new[] { "provider", "subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_login_identities_user_id",
                table: "login_identities",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "login_identities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_access_level",
                table: "users");

            migrationBuilder.DropColumn(
                name: "access_level",
                table: "users");
        }
    }
}
