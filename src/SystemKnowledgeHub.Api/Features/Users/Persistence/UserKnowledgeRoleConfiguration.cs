using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.Users.Persistence;

public sealed class UserKnowledgeRoleConfiguration : IEntityTypeConfiguration<UserKnowledgeRole>
{
    public void Configure(EntityTypeBuilder<UserKnowledgeRole> builder)
    {
        builder.ToTable("user_knowledge_roles");
        builder.HasKey(entity => new { entity.UserId, entity.KnowledgeRoleId });
        builder.Property(entity => entity.UserId).HasColumnName("user_id");
        builder.Property(entity => entity.KnowledgeRoleId).HasColumnName("knowledge_role_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgeRole>()
            .WithMany()
            .HasForeignKey(entity => entity.KnowledgeRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => entity.KnowledgeRoleId);
    }
}
