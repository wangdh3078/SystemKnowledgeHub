using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Persistence;

public sealed class KnowledgeDocumentRevisionConfiguration : IEntityTypeConfiguration<KnowledgeDocumentRevision>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocumentRevision> builder)
    {
        builder.ToTable("knowledge_document_revisions", table =>
        {
            table.HasCheckConstraint("ck_knowledge_document_revisions_revision_number", "revision_number > 0");
            table.HasCheckConstraint("ck_knowledge_document_revisions_title", "length(title) BETWEEN 1 AND 300");
            table.HasCheckConstraint("ck_knowledge_document_revisions_summary", "summary IS NULL OR length(summary) <= 2000");
            table.HasCheckConstraint("ck_knowledge_document_revisions_body", "length(body_markdown) <= 1000000");
            table.HasCheckConstraint("ck_knowledge_document_revisions_lifecycle", "lifecycle_context IN ('Draft','Published','Archived')");
            table.HasCheckConstraint("ck_knowledge_document_revisions_origin", "revision_origin IN ('Created','ContentSave','Restore','MigrationBaseline')");
            table.HasCheckConstraint("ck_knowledge_document_revisions_change_summary", "change_summary IS NULL OR length(change_summary) <= 500");
            table.HasCheckConstraint(
                "ck_knowledge_document_revisions_actor",
                "(revision_origin = 'MigrationBaseline' AND author_user_id IS NULL AND author_display_name_snapshot IS NULL) OR (revision_origin <> 'MigrationBaseline' AND author_user_id IS NOT NULL AND author_display_name_snapshot IS NOT NULL AND length(trim(author_display_name_snapshot)) > 0)");
            table.HasCheckConstraint(
                "ck_knowledge_document_revisions_restore",
                "(revision_origin = 'Restore' AND restore_reason IS NOT NULL AND length(trim(restore_reason)) BETWEEN 5 AND 500 AND restored_from_revision_number IS NOT NULL AND restored_from_revision_number > 0 AND restored_from_revision_number < revision_number) OR (revision_origin <> 'Restore' AND restore_reason IS NULL AND restored_from_revision_number IS NULL)");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(entity => entity.KnowledgeDocumentId).HasColumnName("knowledge_document_id").IsRequired();
        builder.Property(entity => entity.RevisionNumber).HasColumnName("revision_number").IsRequired();
        builder.Property(entity => entity.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.Summary).HasColumnName("summary").HasMaxLength(2_000);
        builder.Property(entity => entity.BodyMarkdown).HasColumnName("body_markdown").IsRequired();
        builder.Property(entity => entity.AuthorUserId).HasColumnName("author_user_id");
        builder.Property(entity => entity.AuthorDisplayNameSnapshot).HasColumnName("author_display_name_snapshot");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(entity => entity.LifecycleContext).HasColumnName("lifecycle_context").HasConversion<string>().IsRequired();
        builder.Property(entity => entity.ChangeSummary).HasColumnName("change_summary").HasMaxLength(500);
        builder.Property(entity => entity.RestoreReason).HasColumnName("restore_reason").HasMaxLength(500);
        builder.Property(entity => entity.RestoredFromRevisionNumber).HasColumnName("restored_from_revision_number");
        builder.Property(entity => entity.RevisionOrigin).HasColumnName("revision_origin").HasConversion<string>().IsRequired();

        builder.HasOne<KnowledgeDocument>()
            .WithMany()
            .HasForeignKey(entity => entity.KnowledgeDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.KnowledgeDocumentId, entity.RevisionNumber }).IsUnique();
    }
}
