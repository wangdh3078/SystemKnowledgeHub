using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_roles",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_roles", x => x.id);
                    table.CheckConstraint("ck_knowledge_roles_is_active", "is_active IN (0,1)");
                    table.CheckConstraint("ck_knowledge_roles_version", "version >= 1");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    employee_no = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    display_name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    email = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    department_or_team = table.Column<string>(type: "TEXT", nullable: true),
                    job_title = table.Column<string>(type: "TEXT", nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("ck_users_is_active", "is_active IN (0,1)");
                    table.CheckConstraint("ck_users_version", "version >= 1");
                });

            migrationBuilder.CreateTable(
                name: "user_knowledge_roles",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    knowledge_role_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_knowledge_roles", x => new { x.user_id, x.knowledge_role_id });
                    table.ForeignKey(
                        name: "FK_user_knowledge_roles_knowledge_roles_knowledge_role_id",
                        column: x => x.knowledge_role_id,
                        principalTable: "knowledge_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_knowledge_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_roles_is_active_name",
                table: "knowledge_roles",
                columns: new[] { "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_roles_name",
                table: "knowledge_roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_knowledge_roles_knowledge_role_id",
                table: "user_knowledge_roles",
                column: "knowledge_role_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true,
                filter: "email IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_users_employee_no",
                table: "users",
                column: "employee_no",
                unique: true,
                filter: "employee_no IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_users_is_active_display_name",
                table: "users",
                columns: new[] { "is_active", "display_name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_knowledge_roles");

            migrationBuilder.DropTable(
                name: "knowledge_roles");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
