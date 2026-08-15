using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.Systems.Domain;

namespace SystemKnowledgeHub.Api.Features.Systems.Persistence;

public sealed class SystemTechnologyTagConfiguration : IEntityTypeConfiguration<SystemTechnologyTag>
{
    public void Configure(EntityTypeBuilder<SystemTechnologyTag> builder)
    {
        builder.ToTable("system_technology_tags");
        builder.HasKey(entity => new { entity.SystemId, entity.Technology });
        builder.Property(entity => entity.SystemId).HasColumnName("system_id");
        builder.Property(entity => entity.Technology).HasColumnName("technology").UseCollation("NOCASE").IsRequired();

        builder.HasOne(entity => entity.System)
            .WithMany(system => system.TechnologyTags)
            .HasForeignKey(entity => entity.SystemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.Technology, entity.SystemId });
    }
}
