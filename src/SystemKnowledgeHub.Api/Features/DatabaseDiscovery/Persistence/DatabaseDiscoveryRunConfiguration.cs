using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Persistence;

public sealed class DatabaseDiscoveryRunConfiguration : IEntityTypeConfiguration<DatabaseDiscoveryRun>
{
    public void Configure(EntityTypeBuilder<DatabaseDiscoveryRun> builder)
    {
        builder.ToTable("database_discovery_runs", table =>
        {
            table.HasCheckConstraint("ck_database_discovery_runs_status", "status IN ('Queued','Running','Succeeded','Failed','Cancelled')");
            table.HasCheckConstraint("ck_database_discovery_runs_provider", "provider_type IN ('Oracle','PostgreSql','SqlServer')");
            table.HasCheckConstraint("ck_database_discovery_runs_revisions", "profile_configuration_revision >= 1 AND secret_version >= 1 AND version >= 1");
            table.HasCheckConstraint("ck_database_discovery_runs_terminal", "(status IN ('Succeeded','Failed','Cancelled') AND completed_at IS NOT NULL) OR (status IN ('Queued','Running') AND completed_at IS NULL)");
            table.HasCheckConstraint("ck_database_discovery_runs_lease", "(status = 'Running' AND lease_owner_id IS NOT NULL AND lease_token IS NOT NULL AND lease_expires_at IS NOT NULL) OR status <> 'Running'");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(item => item.ProfileConfigurationRevision).HasColumnName("profile_configuration_revision").IsRequired();
        builder.Property(item => item.SecretVersion).HasColumnName("secret_version").IsRequired();
        builder.Property(item => item.BaseSnapshotId).HasColumnName("base_snapshot_id");
        builder.Property(item => item.ScopeGenerationId).HasColumnName("scope_generation_id");
        builder.Property(item => item.QueuedAt).HasColumnName("queued_at").IsRequired();
        builder.Property(item => item.StartedAt).HasColumnName("started_at");
        builder.Property(item => item.CompletedAt).HasColumnName("completed_at");
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.LeaseOwnerId).HasColumnName("lease_owner_id").HasMaxLength(96);
        builder.Property(item => item.LeaseToken).HasColumnName("lease_token").HasMaxLength(64);
        builder.Property(item => item.LeaseHeartbeatAt).HasColumnName("lease_heartbeat_at");
        builder.Property(item => item.LeaseExpiresAt).HasColumnName("lease_expires_at");
        builder.Property(item => item.CancellationRequestedAt).HasColumnName("cancellation_requested_at");
        builder.Property(item => item.CancellationRequestedByUserId).HasColumnName("cancellation_requested_by_user_id");
        builder.Property(item => item.ProviderType).HasColumnName("provider_type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.ProviderVersion).HasColumnName("provider_version").HasMaxLength(128);
        builder.Property(item => item.RequestedIncludedSchemasJson).HasColumnName("requested_included_schemas_json").HasMaxLength(32768).IsRequired();
        builder.Property(item => item.RequestedProviderSpecificOptionsJson).HasColumnName("requested_provider_specific_options_json").HasMaxLength(2048).IsRequired();
        builder.Property(item => item.ScopeFingerprint).HasColumnName("scope_fingerprint").HasMaxLength(64);
        builder.Property(item => item.CapabilitySnapshotJson).HasColumnName("capability_snapshot_json").HasMaxLength(32768);
        builder.Property(item => item.ErrorCode).HasColumnName("error_code").HasMaxLength(64);
        builder.Property(item => item.ErrorSummary).HasColumnName("error_summary").HasMaxLength(500);
        builder.Property(item => item.SafeErrorMetadataJson).HasColumnName("safe_error_metadata_json").HasMaxLength(2048);
        builder.Property(item => item.ObjectCountsJson).HasColumnName("object_counts_json").HasMaxLength(2048);
        builder.Property(item => item.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
        builder.Property(item => item.RequestedByDisplayName).HasColumnName("requested_by_display_name").HasMaxLength(160).IsRequired();
        builder.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();

        builder.HasOne(item => item.Profile).WithMany().HasForeignKey(item => item.ProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ScopeGeneration).WithMany().HasForeignKey(item => item.ScopeGenerationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DatabaseDiscoverySnapshot>().WithMany().HasForeignKey(item => item.BaseSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.CancellationRequestedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.ProfileId)
            .IsUnique()
            .HasFilter("status IN ('Queued','Running')")
            .HasDatabaseName("ux_database_discovery_runs_one_active_profile");
        builder.HasIndex(item => new { item.Status, item.QueuedAt });
        builder.HasIndex(item => new { item.Status, item.LeaseExpiresAt });
        builder.HasIndex(item => new { item.ProfileId, item.CompletedAt });
    }
}

public sealed class DatabaseDiscoveryScopeGenerationConfiguration : IEntityTypeConfiguration<DatabaseDiscoveryScopeGeneration>
{
    public void Configure(EntityTypeBuilder<DatabaseDiscoveryScopeGeneration> builder)
    {
        builder.ToTable("database_discovery_scope_generations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(item => item.ScopeFingerprint).HasColumnName("scope_fingerprint").HasMaxLength(64).IsRequired();
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasOne(item => item.Profile).WithMany().HasForeignKey(item => item.ProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.ProfileId, item.ScopeFingerprint }).IsUnique();
    }
}
