using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.Users.Persistence;

public sealed class KnowledgeRoleConfiguration : IEntityTypeConfiguration<KnowledgeRole>
{
    public void Configure(EntityTypeBuilder<KnowledgeRole> builder)
    {
        builder.ToTable("knowledge_roles", table =>
        {
            table.HasCheckConstraint("ck_knowledge_roles_is_active", "is_active IN (0,1)");
            table.HasCheckConstraint("ck_knowledge_roles_version", "version >= 1");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(entity => entity.Name).HasColumnName("name").UseCollation("NOCASE").IsRequired();
        builder.Property(entity => entity.Description).HasColumnName("description");
        builder.Property(entity => entity.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(entity => entity.Version)
            .HasColumnName("version")
            .HasDefaultValue(1L)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(entity => entity.Name).IsUnique();
        builder.HasIndex(entity => new { entity.IsActive, entity.Name });
    }
}
