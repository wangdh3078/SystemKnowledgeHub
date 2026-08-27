using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.BusinessRules.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.BusinessRules.Persistence;

public sealed class BusinessRuleConfiguration : IEntityTypeConfiguration<BusinessRule>
{
    public void Configure(EntityTypeBuilder<BusinessRule> builder)
    {
        builder.ToTable("business_rules", table =>
        {
            table.HasCheckConstraint("ck_business_rules_input_data", "input_data_json IS NULL OR (json_valid(input_data_json) AND json_type(input_data_json) = 'array')");
            table.HasCheckConstraint("ck_business_rules_knowledge_status", "knowledge_status IN ('Unknown','Inferred','Confirmed')");
            table.HasCheckConstraint("ck_business_rules_version", "version >= 1");
            table.HasCheckConstraint("ck_business_rules_deletion_audit", "is_deleted IN (0,1) AND ((is_deleted = 0 AND deleted_at IS NULL AND deleted_by_user_id IS NULL AND deleted_by_display_name IS NULL) OR (deleted_at IS NOT NULL AND deleted_by_user_id IS NOT NULL AND deleted_by_display_name IS NOT NULL AND length(trim(deleted_by_display_name)) > 0))");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.SystemId).HasColumnName("system_id").IsRequired();
        builder.Property(item => item.Name).HasColumnName("name").UseCollation("NOCASE").IsRequired();
        builder.Property(item => item.Description).HasColumnName("description").IsRequired();
        builder.Property(item => item.ConditionText).HasColumnName("condition_text");
        builder.Property(item => item.ResultText).HasColumnName("result_text");
        builder.Property(item => item.InputDataJson).HasColumnName("input_data_json");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(item => item.CreatedByName).HasColumnName("created_by_name").IsRequired();
        builder.Property(item => item.CreatedByRole).HasColumnName("created_by_role");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(item => item.KnowledgeStatus).HasColumnName("knowledge_status").HasConversion<string>().IsRequired();
        builder.Property(item => item.KnowledgeStatusReason).HasColumnName("knowledge_status_reason");
        builder.Property(item => item.KnowledgeStatusChangedAt).HasColumnName("knowledge_status_changed_at").IsRequired();
        builder.Property(item => item.KnowledgeStatusChangedByName).HasColumnName("knowledge_status_changed_by_name").IsRequired();
        builder.Property(item => item.KnowledgeStatusChangedByRole).HasColumnName("knowledge_status_changed_by_role").IsRequired();
        builder.Property(item => item.Version).HasColumnName("version").HasDefaultValue(1L).IsRequired().IsConcurrencyToken();
        builder.Property(item => item.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(item => item.DeletedAt).HasColumnName("deleted_at");
        builder.Property(item => item.DeletedByUserId).HasColumnName("deleted_by_user_id");
        builder.Property(item => item.DeletedByDisplayName).HasColumnName("deleted_by_display_name");

        builder.HasOne(item => item.System).WithMany().HasForeignKey(item => item.SystemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.DeletedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.SystemId, item.Name }).IsUnique().HasFilter("is_deleted = 0");
        builder.HasIndex(item => new { item.SystemId, item.KnowledgeStatus, item.UpdatedAt });
        builder.HasIndex(item => item.KnowledgeStatus);
        builder.HasQueryFilter(item => !item.IsDeleted);
    }
}
