using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.Attachments.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;

namespace SystemKnowledgeHub.Api.Features.Attachments.Persistence;

public sealed class AttachmentReferenceConfiguration : IEntityTypeConfiguration<AttachmentReference>
{
    public void Configure(EntityTypeBuilder<AttachmentReference> builder)
    {
        builder.ToTable("attachment_references");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(entity => entity.KnowledgeDocumentId).HasColumnName("knowledge_document_id").IsRequired();
        builder.Property(entity => entity.KnowledgeDocumentRevisionId).HasColumnName("knowledge_document_revision_id").IsRequired();
        builder.Property(entity => entity.AttachmentId).HasColumnName("attachment_id").IsRequired();

        builder.HasOne<Attachment>()
            .WithMany()
            .HasForeignKey(entity => new { entity.AttachmentId, entity.KnowledgeDocumentId })
            .HasPrincipalKey(entity => new { entity.Id, entity.KnowledgeDocumentId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgeDocumentRevision>()
            .WithMany()
            .HasForeignKey(entity => new { entity.KnowledgeDocumentRevisionId, entity.KnowledgeDocumentId })
            .HasPrincipalKey(entity => new { entity.Id, entity.KnowledgeDocumentId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.KnowledgeDocumentRevisionId, entity.AttachmentId }).IsUnique();
        builder.HasIndex(entity => new { entity.AttachmentId, entity.KnowledgeDocumentRevisionId });
        builder.HasIndex(entity => entity.KnowledgeDocumentId);
    }
}
