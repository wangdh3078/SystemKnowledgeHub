using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Persistence;

public sealed class DatabaseSourceConfiguration : IEntityTypeConfiguration<DatabaseSource>
{
    public void Configure(EntityTypeBuilder<DatabaseSource> builder)
    {
        builder.ToTable("database_sources", table =>
        {
            table.HasCheckConstraint("ck_database_sources_is_primary", "is_primary IN (0, 1)");
            table.HasCheckConstraint("ck_database_sources_version", "version >= 1");
            table.HasCheckConstraint("ck_database_sources_deletion_audit", "is_deleted IN (0,1) AND ((is_deleted = 0 AND deleted_at IS NULL AND deleted_by_user_id IS NULL AND deleted_by_display_name IS NULL) OR (deleted_at IS NOT NULL AND deleted_by_user_id IS NOT NULL AND deleted_by_display_name IS NOT NULL AND length(trim(deleted_by_display_name)) > 0))");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(entity => entity.SystemId).HasColumnName("system_id").IsRequired();
        builder.Property(entity => entity.Name).HasColumnName("name").UseCollation("NOCASE").IsRequired();
        builder.Property(entity => entity.Engine).HasColumnName("engine").IsRequired();
        builder.Property(entity => entity.Environment).HasColumnName("environment");
        builder.Property(entity => entity.InstanceName).HasColumnName("instance_name");
        builder.Property(entity => entity.ServiceName).HasColumnName("service_name");
        builder.Property(entity => entity.DatabaseName).HasColumnName("database_name");
        builder.Property(entity => entity.Description).HasColumnName("description");
        builder.Property(entity => entity.IsPrimary).HasColumnName("is_primary").IsRequired();
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(entity => entity.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(entity => entity.CreatedByName).HasColumnName("created_by_name").IsRequired();
        builder.Property(entity => entity.CreatedByRole).HasColumnName("created_by_role");
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(entity => entity.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();
        builder.Property(entity => entity.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(entity => entity.DeletedAt).HasColumnName("deleted_at");
        builder.Property(entity => entity.DeletedByUserId).HasColumnName("deleted_by_user_id");
        builder.Property(entity => entity.DeletedByDisplayName).HasColumnName("deleted_by_display_name");

        builder.HasOne(entity => entity.System)
            .WithMany()
            .HasForeignKey(entity => entity.SystemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.DeletedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.SystemId, entity.Name }).IsUnique().HasFilter("is_deleted = 0");
        builder.HasIndex(entity => entity.SystemId).HasFilter("is_primary = 1 AND is_deleted = 0").IsUnique();
        builder.HasIndex(entity => new { entity.SystemId, entity.IsPrimary, entity.Name });
        builder.HasIndex(entity => entity.Engine);
        builder.HasQueryFilter(entity => !entity.IsDeleted);
    }
}
