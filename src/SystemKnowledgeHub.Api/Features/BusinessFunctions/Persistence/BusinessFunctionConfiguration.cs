using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.BusinessFunctions.Persistence;

public sealed class BusinessFunctionConfiguration : IEntityTypeConfiguration<BusinessFunction>
{
    public void Configure(EntityTypeBuilder<BusinessFunction> builder)
    {
        builder.ToTable("business_functions", table =>
        {
            table.HasCheckConstraint("ck_business_functions_rewrite_status", "rewrite_status IN ('Keep','Change','Remove','Unknown')");
            table.HasCheckConstraint("ck_business_functions_knowledge_status", "knowledge_status IN ('Unknown','Inferred','Confirmed')");
            table.HasCheckConstraint("ck_business_functions_version", "version >= 1");
            table.HasCheckConstraint("ck_business_functions_deletion_audit", "is_deleted IN (0,1) AND ((is_deleted = 0 AND deleted_at IS NULL AND deleted_by_user_id IS NULL AND deleted_by_display_name IS NULL) OR (deleted_at IS NOT NULL AND deleted_by_user_id IS NOT NULL AND deleted_by_display_name IS NOT NULL AND length(trim(deleted_by_display_name)) > 0))");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(entity => entity.SystemId).HasColumnName("system_id").IsRequired();
        builder.Property(entity => entity.Name).HasColumnName("name").UseCollation("NOCASE").IsRequired();
        builder.Property(entity => entity.DisplayName).HasColumnName("display_name");
        builder.Property(entity => entity.FunctionType).HasColumnName("function_type").IsRequired();
        builder.Property(entity => entity.Purpose).HasColumnName("purpose");
        builder.Property(entity => entity.CallerSummary).HasColumnName("caller_summary");
        builder.Property(entity => entity.InputDescription).HasColumnName("input_description");
        builder.Property(entity => entity.OutputDescription).HasColumnName("output_description");
        builder.Property(entity => entity.RewriteStatus).HasColumnName("rewrite_status").HasConversion<string>().HasDefaultValue(RewriteStatus.Unknown).IsRequired();
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(entity => entity.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(entity => entity.CreatedByName).HasColumnName("created_by_name").IsRequired();
        builder.Property(entity => entity.CreatedByRole).HasColumnName("created_by_role");
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(entity => entity.KnowledgeStatus).HasColumnName("knowledge_status").HasConversion<string>().IsRequired();
        builder.Property(entity => entity.KnowledgeStatusReason).HasColumnName("knowledge_status_reason");
        builder.Property(entity => entity.KnowledgeStatusChangedAt).HasColumnName("knowledge_status_changed_at").IsRequired();
        builder.Property(entity => entity.KnowledgeStatusChangedByName).HasColumnName("knowledge_status_changed_by_name").IsRequired();
        builder.Property(entity => entity.KnowledgeStatusChangedByRole).HasColumnName("knowledge_status_changed_by_role").IsRequired();
        builder.Property(entity => entity.Version).HasColumnName("version").HasDefaultValue(1L).IsRequired().IsConcurrencyToken();
        builder.Property(entity => entity.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(entity => entity.DeletedAt).HasColumnName("deleted_at");
        builder.Property(entity => entity.DeletedByUserId).HasColumnName("deleted_by_user_id");
        builder.Property(entity => entity.DeletedByDisplayName).HasColumnName("deleted_by_display_name");

        builder.HasOne(entity => entity.System)
            .WithMany()
            .HasForeignKey(entity => entity.SystemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.DeletedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.SystemId, entity.Name }).IsUnique().HasFilter("is_deleted = 0");
        builder.HasIndex(entity => new { entity.SystemId, entity.FunctionType, entity.RewriteStatus, entity.KnowledgeStatus, entity.UpdatedAt });
        builder.HasIndex(entity => entity.KnowledgeStatus);
        builder.HasQueryFilter(entity => !entity.IsDeleted);
    }
}
