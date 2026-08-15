using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Persistence;

public sealed class DatabaseColumnConfiguration : IEntityTypeConfiguration<DatabaseColumn>
{
    public void Configure(EntityTypeBuilder<DatabaseColumn> builder)
    {
        builder.ToTable("database_columns", table =>
        {
            table.HasCheckConstraint("ck_database_columns_ordinal", "ordinal_position > 0");
            table.HasCheckConstraint("ck_database_columns_nullable", "is_nullable IN (0, 1)");
            table.HasCheckConstraint("ck_database_columns_knowledge_status", "knowledge_status IN ('Unknown','Inferred','Confirmed')");
            table.HasCheckConstraint("ck_database_columns_version", "version >= 1");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(entity => entity.DatabaseObjectId).HasColumnName("database_object_id").IsRequired();
        builder.Property(entity => entity.OrdinalPosition).HasColumnName("ordinal_position").IsRequired();
        builder.Property(entity => entity.ColumnName).HasColumnName("column_name").UseCollation("NOCASE").IsRequired();
        builder.Property(entity => entity.DataType).HasColumnName("data_type").IsRequired();
        builder.Property(entity => entity.IsNullable).HasColumnName("is_nullable").IsRequired();
        builder.Property(entity => entity.DefaultValue).HasColumnName("default_value");
        builder.Property(entity => entity.BusinessDescription).HasColumnName("business_description");
        builder.Property(entity => entity.DatabaseComment).HasColumnName("database_comment");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(entity => entity.KnowledgeStatus).HasColumnName("knowledge_status").HasConversion<string>().IsRequired();
        builder.Property(entity => entity.KnowledgeStatusReason).HasColumnName("knowledge_status_reason");
        builder.Property(entity => entity.KnowledgeStatusChangedAt).HasColumnName("knowledge_status_changed_at").IsRequired();
        builder.Property(entity => entity.KnowledgeStatusChangedByName).HasColumnName("knowledge_status_changed_by_name").IsRequired();
        builder.Property(entity => entity.KnowledgeStatusChangedByRole).HasColumnName("knowledge_status_changed_by_role").IsRequired();
        builder.Property(entity => entity.Version).HasColumnName("version").HasDefaultValue(1L).IsRequired().IsConcurrencyToken();

        builder.HasOne(entity => entity.DatabaseObject)
            .WithMany(databaseObject => databaseObject.Columns)
            .HasForeignKey(entity => entity.DatabaseObjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.DatabaseObjectId, entity.ColumnName }).IsUnique();
        builder.HasIndex(entity => new { entity.DatabaseObjectId, entity.OrdinalPosition }).IsUnique();
        builder.HasIndex(entity => new { entity.DatabaseObjectId, entity.OrdinalPosition });
        builder.HasIndex(entity => entity.ColumnName);
        builder.HasIndex(entity => entity.KnowledgeStatus);
    }
}
