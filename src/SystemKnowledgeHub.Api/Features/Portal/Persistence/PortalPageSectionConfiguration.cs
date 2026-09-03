using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.Portal.Domain;

namespace SystemKnowledgeHub.Api.Features.Portal.Persistence;

public sealed class PortalPageSectionConfiguration : IEntityTypeConfiguration<PortalPageSection>
{
    private const long JavaScriptMaxSafeInteger = 9_007_199_254_740_991;

    public void Configure(EntityTypeBuilder<PortalPageSection> builder)
    {
        builder.ToTable("portal_page_sections", table =>
        {
            table.HasCheckConstraint("ck_portal_page_sections_id", $"id BETWEEN 1 AND {JavaScriptMaxSafeInteger}");
            table.HasCheckConstraint("ck_portal_page_sections_heading", "length(trim(heading)) BETWEEN 1 AND 200");
            table.HasCheckConstraint("ck_portal_page_sections_source_kind", "source_kind IN ('PrimaryTarget','ExplicitReference','Derived')");
            table.HasCheckConstraint("ck_portal_page_sections_projection_kind", "projection_kind IN ('Summary','KnowledgeDocumentBody','StructuredOverview','DatabaseStructure','AttachmentList','TrustSummary','RelatedKnowledge','Traceability')");
            table.HasCheckConstraint("ck_portal_page_sections_reference", $"(source_kind IN ('PrimaryTarget','Derived') AND reference_target_type IS NULL AND reference_target_id IS NULL) OR (source_kind = 'ExplicitReference' AND reference_target_type IS NOT NULL AND reference_target_id IS NOT NULL AND reference_target_type IN ('System','BusinessFunction','DatabaseObject','KnowledgeDocument','Integration') AND reference_target_id BETWEEN 1 AND {JavaScriptMaxSafeInteger})");
            table.HasCheckConstraint("ck_portal_page_sections_sort", "sort_order >= 0");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(entity => entity.PortalPageId).HasColumnName("portal_page_id").IsRequired();
        builder.Property(entity => entity.Heading).HasColumnName("heading").HasMaxLength(200)
            .HasConversion(value => value.Trim(), value => value).IsRequired();
        builder.Property(entity => entity.SourceKind).HasColumnName("source_kind").HasConversion<string>().IsRequired();
        builder.Property(entity => entity.ReferenceTargetType).HasColumnName("reference_target_type").HasConversion<string>();
        builder.Property(entity => entity.ReferenceTargetId).HasColumnName("reference_target_id");
        builder.Property(entity => entity.ProjectionKind).HasColumnName("projection_kind").HasConversion<string>().IsRequired();
        builder.Property(entity => entity.SortOrder).HasColumnName("sort_order").IsRequired();

        builder.HasOne(entity => entity.PortalPage).WithMany(entity => entity.Sections)
            .HasForeignKey(entity => entity.PortalPageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.PortalPageId, entity.SortOrder }).IsUnique();
        builder.HasQueryFilter(entity => !entity.PortalPage.IsDeleted);
    }
}
