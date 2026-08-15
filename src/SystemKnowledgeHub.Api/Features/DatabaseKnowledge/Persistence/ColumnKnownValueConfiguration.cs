using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Persistence;

public sealed class ColumnKnownValueConfiguration : IEntityTypeConfiguration<ColumnKnownValue>
{
    public void Configure(EntityTypeBuilder<ColumnKnownValue> builder)
    {
        builder.ToTable("column_known_values");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(entity => entity.DatabaseColumnId).HasColumnName("database_column_id").IsRequired();
        builder.Property(entity => entity.ValueText).HasColumnName("value_text").IsRequired();
        builder.Property(entity => entity.Meaning).HasColumnName("meaning").IsRequired();
        builder.Property(entity => entity.SortOrder).HasColumnName("sort_order").HasDefaultValue(0).IsRequired();
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne(entity => entity.DatabaseColumn)
            .WithMany(column => column.KnownValues)
            .HasForeignKey(entity => entity.DatabaseColumnId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.DatabaseColumnId, entity.ValueText }).IsUnique();
        builder.HasIndex(entity => new { entity.DatabaseColumnId, entity.SortOrder, entity.ValueText });
    }
}
