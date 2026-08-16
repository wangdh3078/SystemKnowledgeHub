using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.Integrations.Domain;

namespace SystemKnowledgeHub.Api.Features.Integrations.Persistence;

public sealed class IntegrationConfiguration : IEntityTypeConfiguration<Integration>
{
    public void Configure(EntityTypeBuilder<Integration> builder)
    {
        builder.ToTable("integrations", table =>
        {
            table.HasCheckConstraint("ck_integrations_type", "integration_type IN ('HttpApi','RabbitMq','FileExchange','DatabaseDependency')");
            table.HasCheckConstraint("ck_integrations_direction", "flow_direction IN ('OneWay','Bidirectional')");
            table.HasCheckConstraint("ck_integrations_party_system", "source_system_id IS NOT NULL OR target_system_id IS NOT NULL");
            table.HasCheckConstraint("ck_integrations_status", "knowledge_status IN ('Unknown','Inferred','Confirmed')");
            table.HasCheckConstraint("ck_integrations_endpoint_json", "endpoint_json IS NULL OR json_valid(endpoint_json)");
            table.HasCheckConstraint("ck_integrations_version", "version >= 1");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.Name).HasColumnName("name").UseCollation("NOCASE").IsRequired();
        builder.Property(item => item.IntegrationType).HasColumnName("integration_type").HasConversion<string>().IsRequired();
        builder.Property(item => item.SourceSystemId).HasColumnName("source_system_id");
        builder.Property(item => item.SourcePartyName).HasColumnName("source_party_name").UseCollation("NOCASE").IsRequired();
        builder.Property(item => item.TargetSystemId).HasColumnName("target_system_id");
        builder.Property(item => item.TargetPartyName).HasColumnName("target_party_name").UseCollation("NOCASE").IsRequired();
        builder.Property(item => item.FlowDirection).HasColumnName("flow_direction").HasConversion<string>().IsRequired();
        builder.Property(item => item.Purpose).HasColumnName("purpose");
        builder.Property(item => item.TopicOrQueue).HasColumnName("topic_or_queue");
        builder.Property(item => item.EndpointDisplay).HasColumnName("endpoint_display").UseCollation("NOCASE");
        builder.Property(item => item.EndpointJson).HasColumnName("endpoint_json");
        builder.Property(item => item.DatabaseSourceId).HasColumnName("database_source_id");
        builder.Property(item => item.DatabaseObjectId).HasColumnName("database_object_id");
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
        builder.HasOne(item => item.SourceSystem).WithMany().HasForeignKey(item => item.SourceSystemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.TargetSystem).WithMany().HasForeignKey(item => item.TargetSystemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.DatabaseSource).WithMany().HasForeignKey(item => item.DatabaseSourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.DatabaseObject).WithMany().HasForeignKey(item => item.DatabaseObjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.IntegrationType, item.Name, item.SourcePartyName, item.TargetPartyName }).IsUnique();
        builder.HasIndex(item => new { item.SourceSystemId, item.IntegrationType });
        builder.HasIndex(item => new { item.TargetSystemId, item.IntegrationType });
        builder.HasIndex(item => item.DatabaseSourceId);
        builder.HasIndex(item => item.DatabaseObjectId);
        builder.HasIndex(item => new { item.IntegrationType, item.KnowledgeStatus });
        builder.HasIndex(item => item.EndpointDisplay);
    }
}
