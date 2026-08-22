using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.Users.Persistence;

public sealed class LoginIdentityConfiguration : IEntityTypeConfiguration<LoginIdentity>
{
    public void Configure(EntityTypeBuilder<LoginIdentity> builder)
    {
        builder.ToTable("login_identities", table =>
        {
            table.HasCheckConstraint("ck_login_identities_is_active", "is_active IN (0,1)");
            table.HasCheckConstraint("ck_login_identities_version", "version >= 1");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(entity => entity.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(entity => entity.Provider).HasColumnName("provider").IsRequired();
        builder.Property(entity => entity.Subject).HasColumnName("subject").IsRequired();
        builder.Property(entity => entity.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(entity => entity.Version)
            .HasColumnName("version")
            .HasDefaultValue(1L)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.Provider, entity.Subject }).IsUnique();
        builder.HasIndex(entity => entity.UserId);
    }
}
