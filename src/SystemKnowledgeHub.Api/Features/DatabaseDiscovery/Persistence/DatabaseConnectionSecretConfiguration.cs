using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Persistence;

public sealed class DatabaseConnectionSecretConfiguration : IEntityTypeConfiguration<DatabaseConnectionSecret>
{
    public void Configure(EntityTypeBuilder<DatabaseConnectionSecret> builder)
    {
        builder.ToTable("database_connection_secrets", table =>
        {
            table.HasCheckConstraint("ck_database_connection_secrets_format", "payload_format_version = 1");
            table.HasCheckConstraint("ck_database_connection_secrets_version", "version >= 1");
        });
        builder.HasKey(item => item.ProfileId);
        builder.Property(item => item.ProfileId).HasColumnName("profile_id").ValueGeneratedNever();
        builder.Property(item => item.ProtectedPayload).HasColumnName("protected_payload").HasMaxLength(8192);
        builder.Property(item => item.PayloadFormatVersion).HasColumnName("payload_format_version").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.HasOne(item => item.Profile)
            .WithOne(item => item.Secret)
            .HasForeignKey<DatabaseConnectionSecret>(item => item.ProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
