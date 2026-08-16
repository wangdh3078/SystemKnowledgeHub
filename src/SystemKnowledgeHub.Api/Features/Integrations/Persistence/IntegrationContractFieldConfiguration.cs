using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.Integrations.Domain;

namespace SystemKnowledgeHub.Api.Features.Integrations.Persistence;

public sealed class IntegrationContractFieldConfiguration : IEntityTypeConfiguration<IntegrationContractField>
{
    public void Configure(EntityTypeBuilder<IntegrationContractField> builder)
    {
        builder.ToTable("integration_contract_fields", table =>
        {
            table.HasCheckConstraint("ck_integration_contract_fields_ordinal", "ordinal > 0");
            table.HasCheckConstraint("ck_integration_contract_fields_required", "is_required IN (0,1)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.IntegrationId).HasColumnName("integration_id").IsRequired();
        builder.Property(item => item.Ordinal).HasColumnName("ordinal").IsRequired();
        builder.Property(item => item.FieldName).HasColumnName("field_name").UseCollation("NOCASE").IsRequired();
        builder.Property(item => item.DataType).HasColumnName("data_type");
        builder.Property(item => item.IsRequired).HasColumnName("is_required").IsRequired();
        builder.Property(item => item.Description).HasColumnName("description");
        builder.Property(item => item.SampleValue).HasColumnName("sample_value");
        builder.HasOne(item => item.Integration).WithMany(item => item.ContractFields).HasForeignKey(item => item.IntegrationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.IntegrationId, item.Ordinal }).IsUnique();
        builder.HasIndex(item => new { item.IntegrationId, item.FieldName }).IsUnique();
    }
}
