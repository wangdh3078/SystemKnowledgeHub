using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.Users.Persistence;

public sealed class LocalLoginCredentialConfiguration : IEntityTypeConfiguration<LocalLoginCredential>
{
    public void Configure(EntityTypeBuilder<LocalLoginCredential> builder)
    {
        builder.ToTable("local_login_credentials", table =>
        {
            table.HasCheckConstraint("ck_local_login_credentials_is_active", "is_active IN (0,1)");
            table.HasCheckConstraint("ck_local_login_credentials_failed_login_attempts", "failed_login_attempts >= 0");
            table.HasCheckConstraint("ck_local_login_credentials_session_version", "session_version >= 1");
            table.HasCheckConstraint("ck_local_login_credentials_version", "version >= 1");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(entity => entity.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(entity => entity.Username).HasColumnName("username").IsRequired();
        builder.Property(entity => entity.NormalizedUsername).HasColumnName("normalized_username").IsRequired();
        builder.Property(entity => entity.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(entity => entity.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(entity => entity.FailedLoginAttempts).HasColumnName("failed_login_attempts").HasDefaultValue(0).IsRequired();
        builder.Property(entity => entity.FailedLoginWindowStartedAt).HasColumnName("failed_login_window_started_at");
        builder.Property(entity => entity.LockedUntil).HasColumnName("locked_until");
        builder.Property(entity => entity.SessionVersion).HasColumnName("session_version").HasDefaultValue(1L).IsRequired();
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(entity => entity.LastPasswordChangedAt).HasColumnName("last_password_changed_at").IsRequired();
        builder.Property(entity => entity.Version)
            .HasColumnName("version")
            .HasDefaultValue(1L)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasOne(entity => entity.User)
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => entity.UserId).IsUnique();
        builder.HasIndex(entity => entity.NormalizedUsername).IsUnique();
    }
}
