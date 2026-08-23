using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Persistence;

public sealed class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocument> builder)
    {
        builder.ToTable("knowledge_documents", table =>
        {
            table.HasCheckConstraint("ck_knowledge_documents_document_type", "document_type IN ('Requirement','Specification','TestCase','Sop','Troubleshooting','KnowledgeArticle','DesignNote')");
            table.HasCheckConstraint("ck_knowledge_documents_lifecycle_status", "lifecycle_status IN ('Draft','Published','Archived')");
            table.HasCheckConstraint("ck_knowledge_documents_knowledge_status", "knowledge_status IN ('Unknown','Inferred','Confirmed')");
            table.HasCheckConstraint("ck_knowledge_documents_version", "version >= 1");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(entity => entity.DocumentType).HasColumnName("document_type").HasConversion<string>().IsRequired();
        builder.Property(entity => entity.Title).HasColumnName("title").UseCollation("NOCASE").HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.Summary).HasColumnName("summary").HasMaxLength(2_000);
        builder.Property(entity => entity.BodyMarkdown).HasColumnName("body_markdown").IsRequired();
        builder.Property(entity => entity.LifecycleStatus).HasColumnName("lifecycle_status").HasConversion<string>().IsRequired();
        builder.Property(entity => entity.KnowledgeStatus).HasColumnName("knowledge_status").HasConversion<string>().IsRequired();
        builder.Property(entity => entity.KnowledgeStatusReason).HasColumnName("knowledge_status_reason");
        builder.Property(entity => entity.KnowledgeStatusChangedAt).HasColumnName("knowledge_status_changed_at").IsRequired();
        builder.Property(entity => entity.KnowledgeStatusChangedByName).HasColumnName("knowledge_status_changed_by_name").IsRequired();
        builder.Property(entity => entity.KnowledgeStatusChangedByRole).HasColumnName("knowledge_status_changed_by_role").IsRequired();
        builder.Property(entity => entity.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(entity => entity.CreatedByDisplayName).HasColumnName("created_by_display_name").IsRequired();
        builder.Property(entity => entity.UpdatedByUserId).HasColumnName("updated_by_user_id").IsRequired();
        builder.Property(entity => entity.UpdatedByDisplayName).HasColumnName("updated_by_display_name").IsRequired();
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(entity => entity.PublishedAt).HasColumnName("published_at");
        builder.Property(entity => entity.ArchivedAt).HasColumnName("archived_at");
        builder.Property(entity => entity.CurrentRevisionNumber).HasColumnName("current_revision_number").HasDefaultValue(1L).IsRequired();
        builder.Property(entity => entity.LatestPublishedRevisionNumber).HasColumnName("latest_published_revision_number");
        builder.Property(entity => entity.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.DocumentType, entity.LifecycleStatus, entity.UpdatedAt });
    }
}
