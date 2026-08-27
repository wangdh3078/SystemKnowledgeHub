using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Persistence;

public sealed class DatabaseObjectConfiguration : IEntityTypeConfiguration<DatabaseObject>
{
    public void Configure(EntityTypeBuilder<DatabaseObject> builder)
    {
        builder.ToTable("database_objects", table =>
        {
            table.HasCheckConstraint("ck_database_objects_type", "object_type IN ('Table','View')");
            table.HasCheckConstraint("ck_database_objects_access_mode", "access_mode IN ('Read','Write','ReadWrite','Unknown')");
            table.HasCheckConstraint("ck_database_objects_rows", "estimated_rows IS NULL OR estimated_rows >= 0");
            table.HasCheckConstraint("ck_database_objects_primary_keys", "primary_key_columns_json IS NULL OR (json_valid(primary_key_columns_json) AND json_type(primary_key_columns_json) = 'array')");
            table.HasCheckConstraint("ck_database_objects_business_keys", "business_key_columns_json IS NULL OR (json_valid(business_key_columns_json) AND json_type(business_key_columns_json) = 'array')");
            table.HasCheckConstraint("ck_database_objects_knowledge_status", "knowledge_status IN ('Unknown','Inferred','Confirmed')");
            table.HasCheckConstraint("ck_database_objects_version", "version >= 1");
            table.HasCheckConstraint("ck_database_objects_deletion_audit", "is_deleted IN (0,1) AND ((is_deleted = 0 AND deleted_at IS NULL AND deleted_by_user_id IS NULL AND deleted_by_display_name IS NULL) OR (deleted_at IS NOT NULL AND deleted_by_user_id IS NOT NULL AND deleted_by_display_name IS NOT NULL AND length(trim(deleted_by_display_name)) > 0))");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(entity => entity.DatabaseSourceId).HasColumnName("database_source_id").IsRequired();
        builder.Property(entity => entity.SchemaName).HasColumnName("schema_name").UseCollation("NOCASE").IsRequired();
        builder.Property(entity => entity.ObjectName).HasColumnName("object_name").UseCollation("NOCASE").IsRequired();
        builder.Property(entity => entity.ObjectType).HasColumnName("object_type").HasConversion<string>().IsRequired();
        builder.Property(entity => entity.BusinessDescription).HasColumnName("business_description");
        builder.Property(entity => entity.EstimatedRows).HasColumnName("estimated_rows");
        builder.Property(entity => entity.AccessMode).HasColumnName("access_mode").HasConversion<string>().IsRequired();
        builder.Property(entity => entity.PrimaryKeyColumnsJson).HasColumnName("primary_key_columns_json");
        builder.Property(entity => entity.BusinessKeyColumnsJson).HasColumnName("business_key_columns_json");
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

        builder.HasOne(entity => entity.DatabaseSource)
            .WithMany(source => source.DatabaseObjects)
            .HasForeignKey(entity => entity.DatabaseSourceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.DeletedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.DatabaseSourceId, entity.SchemaName, entity.ObjectName }).IsUnique().HasFilter("is_deleted = 0");
        builder.HasIndex(entity => new { entity.DatabaseSourceId, entity.SchemaName, entity.ObjectType, entity.KnowledgeStatus });
        builder.HasIndex(entity => entity.ObjectName);
        builder.HasIndex(entity => entity.KnowledgeStatus);
        builder.HasQueryFilter(entity => !entity.IsDeleted);
    }
}
