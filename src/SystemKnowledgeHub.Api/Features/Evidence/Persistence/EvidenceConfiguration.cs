using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using EvidenceEntity = SystemKnowledgeHub.Api.Features.Evidence.Domain.Evidence;
using UserEntity = SystemKnowledgeHub.Api.Features.Users.Domain.User;

namespace SystemKnowledgeHub.Api.Features.Evidence.Persistence;

public sealed class EvidenceConfiguration : IEntityTypeConfiguration<EvidenceEntity>
{
    public void Configure(EntityTypeBuilder<EvidenceEntity> builder)
    {
        builder.ToTable("evidence", table =>
        {
            table.HasCheckConstraint("ck_evidence_type", "evidence_type IN ('CodeReference','Sql','DatabaseSample','DatabaseComment','Api','MqMessage','ExistingDocument','HumanConfirmation')");
            table.HasCheckConstraint("ck_evidence_subject_type", "subject_type IN ('System','DatabaseSource','BusinessFunction','DatabaseObject','DatabaseColumn','BusinessRule','Integration','KnowledgeDocument','KnowledgeRelation','UnknownItem','Finding','Resolution','KnowledgeUpdate')");
            table.HasCheckConstraint("ck_evidence_confidence", "confidence IS NULL OR confidence IN ('High','Medium','Low')");
            table.HasCheckConstraint("ck_evidence_source_locator", "source_reference IS NOT NULL OR source_locator_json IS NOT NULL");
            table.HasCheckConstraint("ck_evidence_source_locator_json", "source_locator_json IS NULL OR (json_valid(source_locator_json) AND json_type(source_locator_json) = 'object')");
            table.HasCheckConstraint("ck_evidence_version", "version >= 1");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(entity => entity.EvidenceType).HasColumnName("evidence_type").HasConversion<string>().IsRequired();
        builder.Property(entity => entity.SubjectType).HasColumnName("subject_type").HasConversion<string>().IsRequired();
        builder.Property(entity => entity.SubjectId).HasColumnName("subject_id").IsRequired();
        builder.Property(entity => entity.SubjectDetailKey).HasColumnName("subject_detail_key");
        builder.Property(entity => entity.SourceTitle).HasColumnName("source_title").IsRequired();
        builder.Property(entity => entity.SourceReference).HasColumnName("source_reference").UseCollation("NOCASE");
        builder.Property(entity => entity.SourceLocatorJson).HasColumnName("source_locator_json");
        builder.Property(entity => entity.Summary).HasColumnName("summary");
        builder.Property(entity => entity.SupportReason).HasColumnName("support_reason").IsRequired();
        builder.Property(entity => entity.Confidence).HasColumnName("confidence").HasConversion<string>();
        builder.Property(entity => entity.ProviderUserId).HasColumnName("provider_user_id");
        builder.Property(entity => entity.ProviderKnowledgeRoleId).HasColumnName("provider_knowledge_role_id");
        builder.Property(entity => entity.ProviderEmployeeNo).HasColumnName("provider_employee_no");
        builder.Property(entity => entity.ProviderName).HasColumnName("provider_name").IsRequired();
        builder.Property(entity => entity.ProviderRole).HasColumnName("provider_role").IsRequired();
        builder.Property(entity => entity.ProviderTeam).HasColumnName("provider_team");
        builder.Property(entity => entity.ProviderJobTitle).HasColumnName("provider_job_title");
        builder.Property(entity => entity.ProviderExternalKey).HasColumnName("provider_external_key");
        builder.Property(entity => entity.ProviderSource).HasColumnName("provider_source");
        builder.Property(entity => entity.ProviderNote).HasColumnName("provider_note");
        builder.Property(entity => entity.ProvidedAt).HasColumnName("provided_at").IsRequired();
        builder.Property(entity => entity.KnowledgeDocumentRevisionNumberSnapshot).HasColumnName("knowledge_document_revision_number_snapshot");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(entity => entity.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

        builder.HasIndex(entity => new { entity.SubjectType, entity.SubjectId, entity.SubjectDetailKey });
        builder.HasIndex(entity => new { entity.EvidenceType, entity.ProvidedAt }).IsDescending(false, true);
        builder.HasIndex(entity => entity.SourceReference);
        builder.HasIndex(entity => entity.ProviderUserId);
        builder.HasIndex(entity => entity.ProviderKnowledgeRoleId);

        builder.HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.ProviderUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgeRole>()
            .WithMany()
            .HasForeignKey(entity => entity.ProviderKnowledgeRoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
