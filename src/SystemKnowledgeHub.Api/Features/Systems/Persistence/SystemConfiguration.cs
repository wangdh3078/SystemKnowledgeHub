using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.Systems.Persistence;

public sealed class SystemConfiguration : IEntityTypeConfiguration<KnowledgeSystem>
{
    public void Configure(EntityTypeBuilder<KnowledgeSystem> builder)
    {
        builder.ToTable("systems", table =>
        {
            table.HasCheckConstraint("ck_systems_lifecycle", "lifecycle IN ('Planned','InDevelopment','Running','Maintaining','Legacy','Retired')");
            table.HasCheckConstraint("ck_systems_knowledge_status", "knowledge_status IN ('Unknown','Inferred','Confirmed')");
            table.HasCheckConstraint("ck_systems_main_users_json", "main_users_json IS NULL OR (json_valid(main_users_json) AND json_type(main_users_json) = 'array')");
            table.HasCheckConstraint("ck_systems_deployment_json", "deployment_json IS NULL OR (json_valid(deployment_json) AND json_type(deployment_json) = 'array')");
            table.HasCheckConstraint("ck_systems_main_projects_json", "main_projects_json IS NULL OR (json_valid(main_projects_json) AND json_type(main_projects_json) = 'array')");
            table.HasCheckConstraint("ck_systems_entry_points_json", "main_entry_points_json IS NULL OR (json_valid(main_entry_points_json) AND json_type(main_entry_points_json) = 'array')");
            table.HasCheckConstraint("ck_systems_version", "version >= 1");
            table.HasCheckConstraint("ck_systems_deletion_audit", "is_deleted IN (0,1) AND ((is_deleted = 0 AND deleted_at IS NULL AND deleted_by_user_id IS NULL AND deleted_by_display_name IS NULL) OR (deleted_at IS NOT NULL AND deleted_by_user_id IS NOT NULL AND deleted_by_display_name IS NOT NULL AND length(trim(deleted_by_display_name)) > 0))");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(entity => entity.Name).HasColumnName("name").UseCollation("NOCASE").IsRequired();
        builder.Property(entity => entity.DisplayName).HasColumnName("display_name").IsRequired();
        builder.Property(entity => entity.SystemType).HasColumnName("system_type").IsRequired();
        builder.Property(entity => entity.Lifecycle).HasColumnName("lifecycle").HasConversion<string>().IsRequired();
        builder.Property(entity => entity.Purpose).HasColumnName("purpose");
        builder.Property(entity => entity.MainUsersJson).HasColumnName("main_users_json");
        builder.Property(entity => entity.RepositoryName).HasColumnName("repository_name");
        builder.Property(entity => entity.RepositoryUrl).HasColumnName("repository_url");
        builder.Property(entity => entity.DeploymentJson).HasColumnName("deployment_json");
        builder.Property(entity => entity.MainProjectsJson).HasColumnName("main_projects_json");
        builder.Property(entity => entity.MainEntryPointsJson).HasColumnName("main_entry_points_json");
        builder.Property(entity => entity.Notes).HasColumnName("notes");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(entity => entity.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(entity => entity.CreatedByName).HasColumnName("created_by_name").IsRequired();
        builder.Property(entity => entity.CreatedByRole).HasColumnName("created_by_role");
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(entity => entity.KnowledgeStatus).HasColumnName("knowledge_status").HasConversion<string>().IsRequired();
        builder.Property(entity => entity.KnowledgeStatusReason).HasColumnName("knowledge_status_reason");
        builder.Property(entity => entity.KnowledgeStatusChangedAt).HasColumnName("knowledge_status_changed_at").IsRequired();
        builder.Property(entity => entity.KnowledgeStatusChangedByName).HasColumnName("knowledge_status_changed_by_name").IsRequired();
        builder.Property(entity => entity.KnowledgeStatusChangedByRole).HasColumnName("knowledge_status_changed_by_role").IsRequired();
        builder.Property(entity => entity.Version)
            .HasColumnName("version")
            .HasDefaultValue(1L)
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(entity => entity.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(entity => entity.DeletedAt).HasColumnName("deleted_at");
        builder.Property(entity => entity.DeletedByUserId).HasColumnName("deleted_by_user_id");
        builder.Property(entity => entity.DeletedByDisplayName).HasColumnName("deleted_by_display_name");

        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.DeletedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => entity.Name).IsUnique().HasFilter("is_deleted = 0");
        builder.HasIndex(entity => new { entity.Lifecycle, entity.KnowledgeStatus, entity.UpdatedAt });
        builder.HasIndex(entity => entity.KnowledgeStatus);
        builder.HasQueryFilter(entity => !entity.IsDeleted);
    }
}
