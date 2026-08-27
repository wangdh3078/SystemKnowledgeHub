using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemKnowledgeHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteOwnershipFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_systems_name",
                table: "systems");

            migrationBuilder.DropIndex(
                name: "IX_integrations_integration_type_name_source_party_name_target_party_name",
                table: "integrations");

            migrationBuilder.DropIndex(
                name: "IX_database_sources_system_id",
                table: "database_sources");

            migrationBuilder.DropIndex(
                name: "IX_database_sources_system_id_name",
                table: "database_sources");

            migrationBuilder.DropIndex(
                name: "IX_database_objects_database_source_id_schema_name_object_name",
                table: "database_objects");

            migrationBuilder.DropIndex(
                name: "IX_database_columns_database_object_id_column_name",
                table: "database_columns");

            migrationBuilder.DropIndex(
                name: "IX_database_columns_database_object_id_ordinal_position",
                table: "database_columns");

            migrationBuilder.DropIndex(
                name: "IX_business_rules_system_id_name",
                table: "business_rules");

            migrationBuilder.DropIndex(
                name: "IX_business_functions_system_id_name",
                table: "business_functions");

            migrationBuilder.AddColumn<long>(
                name: "created_by_user_id",
                table: "systems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "systems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by_display_name",
                table: "systems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "deleted_by_user_id",
                table: "systems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "systems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "knowledge_documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by_display_name",
                table: "knowledge_documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "deleted_by_user_id",
                table: "knowledge_documents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "knowledge_documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "created_by_user_id",
                table: "integrations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "integrations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by_display_name",
                table: "integrations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "deleted_by_user_id",
                table: "integrations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "integrations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "created_by_user_id",
                table: "database_sources",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "database_sources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by_display_name",
                table: "database_sources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "deleted_by_user_id",
                table: "database_sources",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "database_sources",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "version",
                table: "database_sources",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "created_by_user_id",
                table: "database_objects",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "database_objects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by_display_name",
                table: "database_objects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "deleted_by_user_id",
                table: "database_objects",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "database_objects",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "created_by_display_name",
                table: "database_columns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "created_by_user_id",
                table: "database_columns",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "database_columns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by_display_name",
                table: "database_columns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "deleted_by_user_id",
                table: "database_columns",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "database_columns",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "created_by_user_id",
                table: "business_rules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "business_rules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by_display_name",
                table: "business_rules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "deleted_by_user_id",
                table: "business_rules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "business_rules",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "created_by_user_id",
                table: "business_functions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "business_functions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by_display_name",
                table: "business_functions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "deleted_by_user_id",
                table: "business_functions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "business_functions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_systems_created_by_user_id",
                table: "systems",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_systems_deleted_by_user_id",
                table: "systems",
                column: "deleted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_systems_name",
                table: "systems",
                column: "name",
                unique: true,
                filter: "is_deleted = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_systems_deletion_audit",
                table: "systems",
                sql: "is_deleted IN (0,1) AND ((is_deleted = 0 AND deleted_at IS NULL AND deleted_by_user_id IS NULL AND deleted_by_display_name IS NULL) OR (deleted_at IS NOT NULL AND deleted_by_user_id IS NOT NULL AND deleted_by_display_name IS NOT NULL AND length(trim(deleted_by_display_name)) > 0))");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_deleted_by_user_id",
                table: "knowledge_documents",
                column: "deleted_by_user_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_knowledge_documents_deletion_audit",
                table: "knowledge_documents",
                sql: "is_deleted IN (0,1) AND ((is_deleted = 0 AND deleted_at IS NULL AND deleted_by_user_id IS NULL AND deleted_by_display_name IS NULL) OR (deleted_at IS NOT NULL AND deleted_by_user_id IS NOT NULL AND deleted_by_display_name IS NOT NULL AND length(trim(deleted_by_display_name)) > 0))");

            migrationBuilder.CreateIndex(
                name: "IX_integrations_created_by_user_id",
                table: "integrations",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_integrations_deleted_by_user_id",
                table: "integrations",
                column: "deleted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_integrations_integration_type_name_source_party_name_target_party_name",
                table: "integrations",
                columns: new[] { "integration_type", "name", "source_party_name", "target_party_name" },
                unique: true,
                filter: "is_deleted = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_integrations_deletion_audit",
                table: "integrations",
                sql: "is_deleted IN (0,1) AND ((is_deleted = 0 AND deleted_at IS NULL AND deleted_by_user_id IS NULL AND deleted_by_display_name IS NULL) OR (deleted_at IS NOT NULL AND deleted_by_user_id IS NOT NULL AND deleted_by_display_name IS NOT NULL AND length(trim(deleted_by_display_name)) > 0))");

            migrationBuilder.CreateIndex(
                name: "IX_database_sources_created_by_user_id",
                table: "database_sources",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_sources_deleted_by_user_id",
                table: "database_sources",
                column: "deleted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_sources_system_id",
                table: "database_sources",
                column: "system_id",
                unique: true,
                filter: "is_primary = 1 AND is_deleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_database_sources_system_id_name",
                table: "database_sources",
                columns: new[] { "system_id", "name" },
                unique: true,
                filter: "is_deleted = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_database_sources_deletion_audit",
                table: "database_sources",
                sql: "is_deleted IN (0,1) AND ((is_deleted = 0 AND deleted_at IS NULL AND deleted_by_user_id IS NULL AND deleted_by_display_name IS NULL) OR (deleted_at IS NOT NULL AND deleted_by_user_id IS NOT NULL AND deleted_by_display_name IS NOT NULL AND length(trim(deleted_by_display_name)) > 0))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_database_sources_version",
                table: "database_sources",
                sql: "version >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_database_objects_created_by_user_id",
                table: "database_objects",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_objects_database_source_id_schema_name_object_name",
                table: "database_objects",
                columns: new[] { "database_source_id", "schema_name", "object_name" },
                unique: true,
                filter: "is_deleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_database_objects_deleted_by_user_id",
                table: "database_objects",
                column: "deleted_by_user_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_database_objects_deletion_audit",
                table: "database_objects",
                sql: "is_deleted IN (0,1) AND ((is_deleted = 0 AND deleted_at IS NULL AND deleted_by_user_id IS NULL AND deleted_by_display_name IS NULL) OR (deleted_at IS NOT NULL AND deleted_by_user_id IS NOT NULL AND deleted_by_display_name IS NOT NULL AND length(trim(deleted_by_display_name)) > 0))");

            migrationBuilder.CreateIndex(
                name: "IX_database_columns_created_by_user_id",
                table: "database_columns",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_database_columns_database_object_id_column_name",
                table: "database_columns",
                columns: new[] { "database_object_id", "column_name" },
                unique: true,
                filter: "is_deleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_database_columns_database_object_id_ordinal_position",
                table: "database_columns",
                columns: new[] { "database_object_id", "ordinal_position" },
                unique: true,
                filter: "is_deleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_database_columns_deleted_by_user_id",
                table: "database_columns",
                column: "deleted_by_user_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_database_columns_deletion_audit",
                table: "database_columns",
                sql: "is_deleted IN (0,1) AND ((is_deleted = 0 AND deleted_at IS NULL AND deleted_by_user_id IS NULL AND deleted_by_display_name IS NULL) OR (deleted_at IS NOT NULL AND deleted_by_user_id IS NOT NULL AND deleted_by_display_name IS NOT NULL AND length(trim(deleted_by_display_name)) > 0))");

            migrationBuilder.CreateIndex(
                name: "IX_business_rules_created_by_user_id",
                table: "business_rules",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_business_rules_deleted_by_user_id",
                table: "business_rules",
                column: "deleted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_business_rules_system_id_name",
                table: "business_rules",
                columns: new[] { "system_id", "name" },
                unique: true,
                filter: "is_deleted = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_business_rules_deletion_audit",
                table: "business_rules",
                sql: "is_deleted IN (0,1) AND ((is_deleted = 0 AND deleted_at IS NULL AND deleted_by_user_id IS NULL AND deleted_by_display_name IS NULL) OR (deleted_at IS NOT NULL AND deleted_by_user_id IS NOT NULL AND deleted_by_display_name IS NOT NULL AND length(trim(deleted_by_display_name)) > 0))");

            migrationBuilder.CreateIndex(
                name: "IX_business_functions_created_by_user_id",
                table: "business_functions",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_business_functions_deleted_by_user_id",
                table: "business_functions",
                column: "deleted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_business_functions_system_id_name",
                table: "business_functions",
                columns: new[] { "system_id", "name" },
                unique: true,
                filter: "is_deleted = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_business_functions_deletion_audit",
                table: "business_functions",
                sql: "is_deleted IN (0,1) AND ((is_deleted = 0 AND deleted_at IS NULL AND deleted_by_user_id IS NULL AND deleted_by_display_name IS NULL) OR (deleted_at IS NOT NULL AND deleted_by_user_id IS NOT NULL AND deleted_by_display_name IS NOT NULL AND length(trim(deleted_by_display_name)) > 0))");

            migrationBuilder.AddForeignKey(
                name: "FK_business_functions_users_created_by_user_id",
                table: "business_functions",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_business_functions_users_deleted_by_user_id",
                table: "business_functions",
                column: "deleted_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_business_rules_users_created_by_user_id",
                table: "business_rules",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_business_rules_users_deleted_by_user_id",
                table: "business_rules",
                column: "deleted_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_database_columns_users_created_by_user_id",
                table: "database_columns",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_database_columns_users_deleted_by_user_id",
                table: "database_columns",
                column: "deleted_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_database_objects_users_created_by_user_id",
                table: "database_objects",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_database_objects_users_deleted_by_user_id",
                table: "database_objects",
                column: "deleted_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_database_sources_users_created_by_user_id",
                table: "database_sources",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_database_sources_users_deleted_by_user_id",
                table: "database_sources",
                column: "deleted_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_integrations_users_created_by_user_id",
                table: "integrations",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_integrations_users_deleted_by_user_id",
                table: "integrations",
                column: "deleted_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_knowledge_documents_users_deleted_by_user_id",
                table: "knowledge_documents",
                column: "deleted_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_systems_users_created_by_user_id",
                table: "systems",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_systems_users_deleted_by_user_id",
                table: "systems",
                column: "deleted_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_business_functions_users_created_by_user_id",
                table: "business_functions");

            migrationBuilder.DropForeignKey(
                name: "FK_business_functions_users_deleted_by_user_id",
                table: "business_functions");

            migrationBuilder.DropForeignKey(
                name: "FK_business_rules_users_created_by_user_id",
                table: "business_rules");

            migrationBuilder.DropForeignKey(
                name: "FK_business_rules_users_deleted_by_user_id",
                table: "business_rules");

            migrationBuilder.DropForeignKey(
                name: "FK_database_columns_users_created_by_user_id",
                table: "database_columns");

            migrationBuilder.DropForeignKey(
                name: "FK_database_columns_users_deleted_by_user_id",
                table: "database_columns");

            migrationBuilder.DropForeignKey(
                name: "FK_database_objects_users_created_by_user_id",
                table: "database_objects");

            migrationBuilder.DropForeignKey(
                name: "FK_database_objects_users_deleted_by_user_id",
                table: "database_objects");

            migrationBuilder.DropForeignKey(
                name: "FK_database_sources_users_created_by_user_id",
                table: "database_sources");

            migrationBuilder.DropForeignKey(
                name: "FK_database_sources_users_deleted_by_user_id",
                table: "database_sources");

            migrationBuilder.DropForeignKey(
                name: "FK_integrations_users_created_by_user_id",
                table: "integrations");

            migrationBuilder.DropForeignKey(
                name: "FK_integrations_users_deleted_by_user_id",
                table: "integrations");

            migrationBuilder.DropForeignKey(
                name: "FK_knowledge_documents_users_deleted_by_user_id",
                table: "knowledge_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_systems_users_created_by_user_id",
                table: "systems");

            migrationBuilder.DropForeignKey(
                name: "FK_systems_users_deleted_by_user_id",
                table: "systems");

            migrationBuilder.DropIndex(
                name: "IX_systems_created_by_user_id",
                table: "systems");

            migrationBuilder.DropIndex(
                name: "IX_systems_deleted_by_user_id",
                table: "systems");

            migrationBuilder.DropIndex(
                name: "IX_systems_name",
                table: "systems");

            migrationBuilder.DropCheckConstraint(
                name: "ck_systems_deletion_audit",
                table: "systems");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_documents_deleted_by_user_id",
                table: "knowledge_documents");

            migrationBuilder.DropCheckConstraint(
                name: "ck_knowledge_documents_deletion_audit",
                table: "knowledge_documents");

            migrationBuilder.DropIndex(
                name: "IX_integrations_created_by_user_id",
                table: "integrations");

            migrationBuilder.DropIndex(
                name: "IX_integrations_deleted_by_user_id",
                table: "integrations");

            migrationBuilder.DropIndex(
                name: "IX_integrations_integration_type_name_source_party_name_target_party_name",
                table: "integrations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_integrations_deletion_audit",
                table: "integrations");

            migrationBuilder.DropIndex(
                name: "IX_database_sources_created_by_user_id",
                table: "database_sources");

            migrationBuilder.DropIndex(
                name: "IX_database_sources_deleted_by_user_id",
                table: "database_sources");

            migrationBuilder.DropIndex(
                name: "IX_database_sources_system_id",
                table: "database_sources");

            migrationBuilder.DropIndex(
                name: "IX_database_sources_system_id_name",
                table: "database_sources");

            migrationBuilder.DropCheckConstraint(
                name: "ck_database_sources_deletion_audit",
                table: "database_sources");

            migrationBuilder.DropCheckConstraint(
                name: "ck_database_sources_version",
                table: "database_sources");

            migrationBuilder.DropIndex(
                name: "IX_database_objects_created_by_user_id",
                table: "database_objects");

            migrationBuilder.DropIndex(
                name: "IX_database_objects_database_source_id_schema_name_object_name",
                table: "database_objects");

            migrationBuilder.DropIndex(
                name: "IX_database_objects_deleted_by_user_id",
                table: "database_objects");

            migrationBuilder.DropCheckConstraint(
                name: "ck_database_objects_deletion_audit",
                table: "database_objects");

            migrationBuilder.DropIndex(
                name: "IX_database_columns_created_by_user_id",
                table: "database_columns");

            migrationBuilder.DropIndex(
                name: "IX_database_columns_database_object_id_column_name",
                table: "database_columns");

            migrationBuilder.DropIndex(
                name: "IX_database_columns_database_object_id_ordinal_position",
                table: "database_columns");

            migrationBuilder.DropIndex(
                name: "IX_database_columns_deleted_by_user_id",
                table: "database_columns");

            migrationBuilder.DropCheckConstraint(
                name: "ck_database_columns_deletion_audit",
                table: "database_columns");

            migrationBuilder.DropIndex(
                name: "IX_business_rules_created_by_user_id",
                table: "business_rules");

            migrationBuilder.DropIndex(
                name: "IX_business_rules_deleted_by_user_id",
                table: "business_rules");

            migrationBuilder.DropIndex(
                name: "IX_business_rules_system_id_name",
                table: "business_rules");

            migrationBuilder.DropCheckConstraint(
                name: "ck_business_rules_deletion_audit",
                table: "business_rules");

            migrationBuilder.DropIndex(
                name: "IX_business_functions_created_by_user_id",
                table: "business_functions");

            migrationBuilder.DropIndex(
                name: "IX_business_functions_deleted_by_user_id",
                table: "business_functions");

            migrationBuilder.DropIndex(
                name: "IX_business_functions_system_id_name",
                table: "business_functions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_business_functions_deletion_audit",
                table: "business_functions");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "systems");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "systems");

            migrationBuilder.DropColumn(
                name: "deleted_by_display_name",
                table: "systems");

            migrationBuilder.DropColumn(
                name: "deleted_by_user_id",
                table: "systems");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "systems");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "deleted_by_display_name",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "deleted_by_user_id",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "integrations");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "integrations");

            migrationBuilder.DropColumn(
                name: "deleted_by_display_name",
                table: "integrations");

            migrationBuilder.DropColumn(
                name: "deleted_by_user_id",
                table: "integrations");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "integrations");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "database_sources");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "database_sources");

            migrationBuilder.DropColumn(
                name: "deleted_by_display_name",
                table: "database_sources");

            migrationBuilder.DropColumn(
                name: "deleted_by_user_id",
                table: "database_sources");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "database_sources");

            migrationBuilder.DropColumn(
                name: "version",
                table: "database_sources");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "database_objects");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "database_objects");

            migrationBuilder.DropColumn(
                name: "deleted_by_display_name",
                table: "database_objects");

            migrationBuilder.DropColumn(
                name: "deleted_by_user_id",
                table: "database_objects");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "database_objects");

            migrationBuilder.DropColumn(
                name: "created_by_display_name",
                table: "database_columns");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "database_columns");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "database_columns");

            migrationBuilder.DropColumn(
                name: "deleted_by_display_name",
                table: "database_columns");

            migrationBuilder.DropColumn(
                name: "deleted_by_user_id",
                table: "database_columns");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "database_columns");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "business_rules");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "business_rules");

            migrationBuilder.DropColumn(
                name: "deleted_by_display_name",
                table: "business_rules");

            migrationBuilder.DropColumn(
                name: "deleted_by_user_id",
                table: "business_rules");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "business_rules");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "business_functions");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "business_functions");

            migrationBuilder.DropColumn(
                name: "deleted_by_display_name",
                table: "business_functions");

            migrationBuilder.DropColumn(
                name: "deleted_by_user_id",
                table: "business_functions");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "business_functions");

            migrationBuilder.CreateIndex(
                name: "IX_systems_name",
                table: "systems",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integrations_integration_type_name_source_party_name_target_party_name",
                table: "integrations",
                columns: new[] { "integration_type", "name", "source_party_name", "target_party_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_sources_system_id",
                table: "database_sources",
                column: "system_id",
                unique: true,
                filter: "is_primary = 1");

            migrationBuilder.CreateIndex(
                name: "IX_database_sources_system_id_name",
                table: "database_sources",
                columns: new[] { "system_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_objects_database_source_id_schema_name_object_name",
                table: "database_objects",
                columns: new[] { "database_source_id", "schema_name", "object_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_columns_database_object_id_column_name",
                table: "database_columns",
                columns: new[] { "database_object_id", "column_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_columns_database_object_id_ordinal_position",
                table: "database_columns",
                columns: new[] { "database_object_id", "ordinal_position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_business_rules_system_id_name",
                table: "business_rules",
                columns: new[] { "system_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_business_functions_system_id_name",
                table: "business_functions",
                columns: new[] { "system_id", "name" },
                unique: true);
        }
    }
}
