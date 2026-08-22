using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalLoginCredentialFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "local_login_credentials",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    username = table.Column<string>(type: "TEXT", nullable: false),
                    normalized_username = table.Column<string>(type: "TEXT", nullable: false),
                    password_hash = table.Column<string>(type: "TEXT", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    failed_login_attempts = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    failed_login_window_started_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    locked_until = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    session_version = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    last_password_changed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_local_login_credentials", x => x.id);
                    table.CheckConstraint("ck_local_login_credentials_failed_login_attempts", "failed_login_attempts >= 0");
                    table.CheckConstraint("ck_local_login_credentials_is_active", "is_active IN (0,1)");
                    table.CheckConstraint("ck_local_login_credentials_session_version", "session_version >= 1");
                    table.CheckConstraint("ck_local_login_credentials_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_local_login_credentials_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_local_login_credentials_normalized_username",
                table: "local_login_credentials",
                column: "normalized_username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_local_login_credentials_user_id",
                table: "local_login_credentials",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "local_login_credentials");
        }
    }
}
