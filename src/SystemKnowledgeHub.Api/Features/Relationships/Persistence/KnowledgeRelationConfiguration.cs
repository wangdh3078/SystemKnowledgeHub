using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;

namespace SystemKnowledgeHub.Api.Features.Relationships.Persistence;

public sealed class KnowledgeRelationConfiguration : IEntityTypeConfiguration<KnowledgeRelation>
{
    public void Configure(EntityTypeBuilder<KnowledgeRelation> builder)
    {
        builder.ToTable("knowledge_relations", table =>
        {
            table.HasCheckConstraint("ck_knowledge_relations_source_type", "source_type IN ('System','DatabaseSource','BusinessFunction','DatabaseObject','DatabaseColumn','BusinessRule','Integration')");
            table.HasCheckConstraint("ck_knowledge_relations_target_type", "target_type IN ('System','DatabaseSource','BusinessFunction','DatabaseObject','DatabaseColumn','BusinessRule','Integration')");
            table.HasCheckConstraint("ck_knowledge_relations_relation_type", "relation_type IN ('Calls','Reads','Writes','UsesField','AppliesRule','PublishesVia','ConsumesVia','UsesIntegration','DependsOn')");
            table.HasCheckConstraint("ck_knowledge_relations_status", "knowledge_status IN ('Unknown','Inferred','Confirmed')");
            table.HasCheckConstraint("ck_knowledge_relations_distinct_endpoints", "source_type <> target_type OR source_id <> target_id");
            table.HasCheckConstraint("ck_knowledge_relations_version", "version >= 1");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.SourceType).HasColumnName("source_type").HasConversion<string>().IsRequired();
        builder.Property(item => item.SourceId).HasColumnName("source_id").IsRequired();
        builder.Property(item => item.TargetType).HasColumnName("target_type").HasConversion<string>().IsRequired();
        builder.Property(item => item.TargetId).HasColumnName("target_id").IsRequired();
        builder.Property(item => item.RelationType).HasColumnName("relation_type").HasConversion<string>().IsRequired();
        builder.Property(item => item.Description).HasColumnName("description");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.CreatedByName).HasColumnName("created_by_name").IsRequired();
        builder.Property(item => item.CreatedByRole).HasColumnName("created_by_role");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(item => item.KnowledgeStatus).HasColumnName("knowledge_status").HasConversion<string>().IsRequired();
        builder.Property(item => item.KnowledgeStatusReason).HasColumnName("knowledge_status_reason");
        builder.Property(item => item.KnowledgeStatusChangedAt).HasColumnName("knowledge_status_changed_at").IsRequired();
        builder.Property(item => item.KnowledgeStatusChangedByName).HasColumnName("knowledge_status_changed_by_name").IsRequired();
        builder.Property(item => item.KnowledgeStatusChangedByRole).HasColumnName("knowledge_status_changed_by_role").IsRequired();
        builder.Property(item => item.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

        builder.HasIndex(item => new { item.SourceType, item.SourceId, item.TargetType, item.TargetId, item.RelationType }).IsUnique();
        builder.HasIndex(item => new { item.SourceType, item.SourceId, item.RelationType });
        builder.HasIndex(item => new { item.TargetType, item.TargetId, item.RelationType });
        builder.HasIndex(item => new { item.RelationType, item.KnowledgeStatus });
    }
}
