using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.Attachments.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.Attachments.Persistence;

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments", table =>
        {
            table.HasCheckConstraint("ck_attachments_original_file_name", "length(trim(original_file_name)) BETWEEN 1 AND 255");
            table.HasCheckConstraint("ck_attachments_extension", "length(extension) BETWEEN 2 AND 16 AND extension = lower(extension) AND substr(extension, 1, 1) = '.'");
            table.HasCheckConstraint("ck_attachments_kind", "kind IN ('Image','File')");
            table.HasCheckConstraint("ck_attachments_content_type", "length(trim(content_type)) BETWEEN 1 AND 127");
            table.HasCheckConstraint("ck_attachments_size", "size_bytes > 0");
            table.HasCheckConstraint("ck_attachments_storage_key", "length(storage_key) BETWEEN 1 AND 96");
            table.HasCheckConstraint("ck_attachments_sha256", "length(sha256) = 32");
            table.HasCheckConstraint("ck_attachments_storage_state", "storage_state IN ('Ready','DeletePending')");
            table.HasCheckConstraint("ck_attachments_creator_snapshot", "length(trim(created_by_display_name_snapshot)) > 0");
            table.HasCheckConstraint("ck_attachments_version", "version > 0");
        });

        builder.HasKey(entity => entity.Id);
        builder.HasAlternateKey(entity => new { entity.Id, entity.KnowledgeDocumentId });
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(entity => entity.KnowledgeDocumentId).HasColumnName("knowledge_document_id").IsRequired();
        builder.Property(entity => entity.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(255).IsRequired();
        builder.Property(entity => entity.Extension).HasColumnName("extension").HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.Kind).HasColumnName("kind").HasConversion<string>().IsRequired();
        builder.Property(entity => entity.ContentType).HasColumnName("content_type").HasMaxLength(127).IsRequired();
        builder.Property(entity => entity.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(entity => entity.StorageKey).HasColumnName("storage_key").HasMaxLength(96).IsRequired();
        builder.Property(entity => entity.Sha256).HasColumnName("sha256").HasColumnType("BLOB").IsRequired();
        builder.Property(entity => entity.StorageState).HasColumnName("storage_state").HasConversion<string>().IsRequired();
        builder.Property(entity => entity.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(entity => entity.CreatedByDisplayNameSnapshot).HasColumnName("created_by_display_name_snapshot").IsRequired();
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(entity => entity.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();

        builder.HasOne<KnowledgeDocument>()
            .WithMany()
            .HasForeignKey(entity => entity.KnowledgeDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => entity.StorageKey).IsUnique();
        builder.HasIndex(entity => new { entity.KnowledgeDocumentId, entity.CreatedAt, entity.Id });
    }
}
