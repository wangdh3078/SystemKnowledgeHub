using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemsListCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "version",
                table: "systems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateTable(
                name: "system_technology_tags",
                columns: table => new
                {
                    system_id = table.Column<long>(type: "INTEGER", nullable: false),
                    technology = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_technology_tags", x => new { x.system_id, x.technology });
                    table.ForeignKey(
                        name: "FK_system_technology_tags_systems_system_id",
                        column: x => x.system_id,
                        principalTable: "systems",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_systems_version",
                table: "systems",
                sql: "version >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_system_technology_tags_technology_system_id",
                table: "system_technology_tags",
                columns: new[] { "technology", "system_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_technology_tags");

            migrationBuilder.DropCheckConstraint(
                name: "ck_systems_version",
                table: "systems");

            migrationBuilder.DropColumn(
                name: "version",
                table: "systems");
        }
    }
}
