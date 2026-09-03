using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.Portal.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.Portal.Persistence;

public sealed class PortalPageNodeConfiguration : IEntityTypeConfiguration<PortalPageNode>
{
    private const long JavaScriptMaxSafeInteger = 9_007_199_254_740_991;

    public void Configure(EntityTypeBuilder<PortalPageNode> builder)
    {
        builder.ToTable("portal_page_nodes", table =>
        {
            table.HasCheckConstraint("ck_portal_page_nodes_id", $"id BETWEEN 1 AND {JavaScriptMaxSafeInteger}");
            table.HasCheckConstraint("ck_portal_page_nodes_title", "length(trim(title)) BETWEEN 1 AND 200");
            table.HasCheckConstraint("ck_portal_page_nodes_kind", "node_kind IN ('Folder','Page')");
            table.HasCheckConstraint("ck_portal_page_nodes_shape", "(node_kind = 'Folder' AND portal_page_id IS NULL) OR (node_kind = 'Page' AND portal_page_id IS NOT NULL)");
            table.HasCheckConstraint("ck_portal_page_nodes_parent", "parent_id IS NULL OR parent_id <> id");
            table.HasCheckConstraint("ck_portal_page_nodes_sort", "sort_order >= 0");
            table.HasCheckConstraint("ck_portal_page_nodes_published", "is_published IN (0,1)");
            table.HasCheckConstraint("ck_portal_page_nodes_version", "version >= 1");
            table.HasCheckConstraint("ck_portal_page_nodes_publication_audit", "((published_at IS NULL AND published_by_user_id IS NULL AND published_by_display_name IS NULL) OR (published_at IS NOT NULL AND published_by_user_id IS NOT NULL AND published_by_display_name IS NOT NULL AND length(trim(published_by_display_name)) > 0)) AND ((unpublished_at IS NULL AND unpublished_by_user_id IS NULL AND unpublished_by_display_name IS NULL) OR (unpublished_at IS NOT NULL AND unpublished_by_user_id IS NOT NULL AND unpublished_by_display_name IS NOT NULL AND length(trim(unpublished_by_display_name)) > 0)) AND (is_published = 0 OR published_at IS NOT NULL)");
            table.HasCheckConstraint("ck_portal_page_nodes_deletion_audit", "is_deleted IN (0,1) AND ((is_deleted = 0 AND deleted_at IS NULL AND deleted_by_user_id IS NULL AND deleted_by_display_name IS NULL) OR (deleted_at IS NOT NULL AND deleted_by_user_id IS NOT NULL AND deleted_by_display_name IS NOT NULL AND length(trim(deleted_by_display_name)) > 0))");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(entity => entity.ParentId).HasColumnName("parent_id");
        builder.Property(entity => entity.Title).HasColumnName("title").UseCollation("NOCASE").HasMaxLength(200)
            .HasConversion(value => value.Trim(), value => value).IsRequired();
        builder.Property(entity => entity.NodeKind).HasColumnName("node_kind").HasConversion<string>().IsRequired();
        builder.Property(entity => entity.PortalPageId).HasColumnName("portal_page_id");
        builder.Property(entity => entity.SortOrder).HasColumnName("sort_order").IsRequired();
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

        builder.HasOne(entity => entity.Parent).WithMany(entity => entity.Children)
            .HasForeignKey(entity => entity.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.PortalPage).WithMany(entity => entity.Nodes)
            .HasForeignKey(entity => entity.PortalPageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.UnpublishedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.DeletedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => entity.SortOrder)
            .IsUnique()
            .HasFilter("parent_id IS NULL AND is_deleted = 0")
            .HasDatabaseName("IX_portal_page_nodes_active_root_sort_order");
        builder.HasIndex(entity => new { entity.ParentId, entity.SortOrder })
            .IsUnique()
            .HasFilter("parent_id IS NOT NULL AND is_deleted = 0")
            .HasDatabaseName("IX_portal_page_nodes_active_parent_sort_order");
        builder.HasIndex(entity => new { entity.IsPublished, entity.IsDeleted });
        builder.HasIndex(entity => entity.PortalPageId);
        builder.HasQueryFilter(entity => !entity.IsDeleted);
    }
}
