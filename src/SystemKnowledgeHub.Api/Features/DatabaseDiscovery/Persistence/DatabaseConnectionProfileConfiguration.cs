using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Persistence;

public sealed class DatabaseConnectionProfileConfiguration : IEntityTypeConfiguration<DatabaseConnectionProfile>
{
    public void Configure(EntityTypeBuilder<DatabaseConnectionProfile> builder)
    {
        builder.ToTable("database_connection_profiles", table =>
        {
            table.HasCheckConstraint("ck_database_connection_profiles_port", "port BETWEEN 1 AND 65535");
            table.HasCheckConstraint("ck_database_connection_profiles_enabled", "is_enabled IN (0,1)");
            table.HasCheckConstraint("ck_database_connection_profiles_revision", "configuration_revision >= 1");
            table.HasCheckConstraint("ck_database_connection_profiles_version", "version >= 1");
            table.HasCheckConstraint("ck_database_connection_profiles_provider", "provider_type IN ('Oracle','PostgreSql','SqlServer')");
            table.HasCheckConstraint("ck_database_connection_profiles_auth", "authentication_mode = 'UsernamePassword'");
            table.HasCheckConstraint("ck_database_connection_profiles_status", "connection_status IN ('Unknown','Succeeded','Failed')");
            table.HasCheckConstraint("ck_database_connection_profiles_locator", "(provider_type = 'Oracle' AND service_name IS NOT NULL AND database_name IS NULL) OR (provider_type IN ('PostgreSql','SqlServer') AND service_name IS NULL AND database_name IS NOT NULL)");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.DatabaseSourceId).HasColumnName("database_source_id").IsRequired();
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(160).UseCollation("NOCASE").IsRequired();
        builder.Property(item => item.ProviderType).HasColumnName("provider_type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.Host).HasColumnName("host").HasMaxLength(253).IsRequired();
        builder.Property(item => item.Port).HasColumnName("port").IsRequired();
        builder.Property(item => item.DatabaseName).HasColumnName("database_name").HasMaxLength(128);
        builder.Property(item => item.ServiceName).HasColumnName("service_name").HasMaxLength(128);
        builder.Property(item => item.AuthenticationMode).HasColumnName("authentication_mode").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.Username).HasColumnName("username").HasMaxLength(128).IsRequired();
        builder.Property(item => item.ProviderSpecificOptionsJson).HasColumnName("provider_specific_options_json").HasMaxLength(2048).IsRequired();
        builder.Property(item => item.IncludedSchemasJson).HasColumnName("included_schemas_json").HasMaxLength(32768).IsRequired();
        builder.Property(item => item.IsEnabled).HasColumnName("is_enabled").IsRequired();
        builder.Property(item => item.ConnectionStatus).HasColumnName("connection_status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.LatestConnectionTestAttemptId).HasColumnName("latest_connection_test_attempt_id").HasMaxLength(32);
        builder.Property(item => item.LastConnectionTestStartedAt).HasColumnName("last_connection_test_started_at");
        builder.Property(item => item.LastConnectionTestAt).HasColumnName("last_connection_test_at");
        builder.Property(item => item.LastConnectionTestErrorCode).HasColumnName("last_connection_test_error_code").HasMaxLength(64);
        builder.Property(item => item.LastConnectionTestVendorCode).HasColumnName("last_connection_test_vendor_code").HasMaxLength(16);
        builder.Property(item => item.LastConnectionTestSummary).HasColumnName("last_connection_test_summary").HasMaxLength(500);
        builder.Property(item => item.LastDiscoveryAt).HasColumnName("last_discovery_at");
        builder.Property(item => item.LastSuccessfulDiscoveryAt).HasColumnName("last_successful_discovery_at");
        builder.Property(item => item.ConfigurationRevision).HasColumnName("configuration_revision").IsRequired();
        builder.Property(item => item.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(item => item.CreatedByDisplayName).HasColumnName("created_by_display_name").HasMaxLength(160).IsRequired();
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();

        builder.HasOne(item => item.DatabaseSource)
            .WithMany()
            .HasForeignKey(item => item.DatabaseSourceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.DatabaseSourceId).IsUnique();
        builder.HasIndex(item => item.Name).IsUnique();
        builder.HasIndex(item => new { item.IsEnabled, item.ProviderType });
    }
}
