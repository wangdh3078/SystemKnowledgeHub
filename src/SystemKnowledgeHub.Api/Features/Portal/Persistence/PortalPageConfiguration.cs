using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.Portal.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.Portal.Persistence;

public sealed class PortalPageConfiguration : IEntityTypeConfiguration<PortalPage>
{
    private const long JavaScriptMaxSafeInteger = 9_007_199_254_740_991;

    public void Configure(EntityTypeBuilder<PortalPage> builder)
    {
        builder.ToTable("portal_pages", table =>
        {
            table.HasCheckConstraint("ck_portal_pages_id", $"id BETWEEN 1 AND {JavaScriptMaxSafeInteger}");
            table.HasCheckConstraint("ck_portal_pages_title", "length(trim(title)) BETWEEN 1 AND 200");
            table.HasCheckConstraint("ck_portal_pages_target_type", "primary_target_type IN ('System','BusinessFunction','DatabaseObject','KnowledgeDocument','Integration')");
            table.HasCheckConstraint("ck_portal_pages_target_id", $"primary_target_id BETWEEN 1 AND {JavaScriptMaxSafeInteger}");
            table.HasCheckConstraint("ck_portal_pages_published", "is_published IN (0,1)");
            table.HasCheckConstraint("ck_portal_pages_version", "version >= 1");
            table.HasCheckConstraint("ck_portal_pages_publication_audit", "((published_at IS NULL AND published_by_user_id IS NULL AND published_by_display_name IS NULL) OR (published_at IS NOT NULL AND published_by_user_id IS NOT NULL AND published_by_display_name IS NOT NULL AND length(trim(published_by_display_name)) > 0)) AND ((unpublished_at IS NULL AND unpublished_by_user_id IS NULL AND unpublished_by_display_name IS NULL) OR (unpublished_at IS NOT NULL AND unpublished_by_user_id IS NOT NULL AND unpublished_by_display_name IS NOT NULL AND length(trim(unpublished_by_display_name)) > 0)) AND (is_published = 0 OR published_at IS NOT NULL)");
            table.HasCheckConstraint("ck_portal_pages_deletion_audit", "is_deleted IN (0,1) AND ((is_deleted = 0 AND deleted_at IS NULL AND deleted_by_user_id IS NULL AND deleted_by_display_name IS NULL) OR (deleted_at IS NOT NULL AND deleted_by_user_id IS NOT NULL AND deleted_by_display_name IS NOT NULL AND length(trim(deleted_by_display_name)) > 0))");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(entity => entity.Title).HasColumnName("title").UseCollation("NOCASE").HasMaxLength(200)
            .HasConversion(value => value.Trim(), value => value).IsRequired();
        builder.Property(entity => entity.PrimaryTargetType).HasColumnName("primary_target_type").HasConversion<string>().IsRequired();
        builder.Property(entity => entity.PrimaryTargetId).HasColumnName("primary_target_id").IsRequired();
        builder.Property(entity => entity.IsPublished).HasColumnName("is_published").HasDefaultValue(false).IsRequired();
        builder.Property(entity => entity.PublishedAt).HasColumnName("published_at");
        builder.Property(entity => entity.PublishedByUserId).HasColumnName("published_by_user_id");
        builder.Property(entity => entity.PublishedByDisplayName).HasColumnName("published_by_display_name").HasMaxLength(200);
        builder.Property(entity => entity.UnpublishedAt).HasColumnName("unpublished_at");
        builder.Property(entity => entity.UnpublishedByUserId).HasColumnName("unpublished_by_user_id");
        builder.Property(entity => entity.UnpublishedByDisplayName).HasColumnName("unpublished_by_display_name").HasMaxLength(200);
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(entity => entity.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(entity => entity.CreatedByDisplayName).HasColumnName("created_by_display_name").HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(entity => entity.UpdatedByUserId).HasColumnName("updated_by_user_id").IsRequired();
        builder.Property(entity => entity.UpdatedByDisplayName).HasColumnName("updated_by_display_name").HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();
        builder.Property(entity => entity.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(entity => entity.DeletedAt).HasColumnName("deleted_at");
        builder.Property(entity => entity.DeletedByUserId).HasColumnName("deleted_by_user_id");
        builder.Property(entity => entity.DeletedByDisplayName).HasColumnName("deleted_by_display_name").HasMaxLength(200);

        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.UnpublishedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.DeletedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.IsPublished, entity.IsDeleted });
        builder.HasIndex(entity => new { entity.PrimaryTargetType, entity.PrimaryTargetId });
        builder.HasQueryFilter(entity => !entity.IsDeleted);
    }
}
