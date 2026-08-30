using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Persistence;

public sealed class DatabaseDiscoverySnapshotConfiguration : IEntityTypeConfiguration<DatabaseDiscoverySnapshot>
{
    public void Configure(EntityTypeBuilder<DatabaseDiscoverySnapshot> builder)
    {
        builder.ToTable("database_discovery_snapshots", table =>
        {
            table.HasCheckConstraint("ck_database_discovery_snapshots_versions", "format_version >= 1 AND identity_algorithm_version >= 1");
            table.HasCheckConstraint("ck_database_discovery_snapshots_completeness", "completeness = 'Complete'");
            table.HasCheckConstraint("ck_database_discovery_snapshots_sha256", "length(content_sha256) = 64");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.RunId).HasColumnName("run_id").IsRequired();
        builder.Property(item => item.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(item => item.CapturedAt).HasColumnName("captured_at").IsRequired();
        builder.Property(item => item.FormatVersion).HasColumnName("format_version").IsRequired();
        builder.Property(item => item.IdentityAlgorithmVersion).HasColumnName("identity_algorithm_version").IsRequired();
        builder.Property(item => item.ScopeGenerationId).HasColumnName("scope_generation_id").IsRequired();
        builder.Property(item => item.ScopeFingerprint).HasColumnName("scope_fingerprint").HasMaxLength(64).IsRequired();
        builder.Property(item => item.Completeness).HasColumnName("completeness").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(item => item.CanonicalContentJson).HasColumnName("canonical_content_json").IsRequired();
        builder.Property(item => item.ContentSha256).HasColumnName("content_sha256").HasMaxLength(64).IsRequired();
        builder.Property(item => item.CountsJson).HasColumnName("counts_json").HasMaxLength(2048).IsRequired();
        builder.HasOne(item => item.Run).WithOne(item => item.Snapshot).HasForeignKey<DatabaseDiscoverySnapshot>(item => item.RunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Profile).WithMany().HasForeignKey(item => item.ProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ScopeGeneration).WithMany().HasForeignKey(item => item.ScopeGenerationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.RunId).IsUnique();
        builder.HasIndex(item => new { item.ProfileId, item.ScopeGenerationId, item.CapturedAt });
        builder.HasIndex(item => new { item.ProfileId, item.ScopeFingerprint, item.Id });
    }
}

public sealed class DatabaseDiscoveryDifferenceConfiguration : IEntityTypeConfiguration<DatabaseDiscoveryDifference>
{
    public void Configure(EntityTypeBuilder<DatabaseDiscoveryDifference> builder)
    {
        builder.ToTable("database_discovery_differences", table =>
        {
            table.HasCheckConstraint("ck_database_discovery_differences_algorithm", "algorithm_version >= 1");
            table.HasCheckConstraint("ck_database_discovery_differences_sha256", "length(content_sha256) = 64");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(item => item.BaseSnapshotId).HasColumnName("base_snapshot_id");
        builder.Property(item => item.TargetSnapshotId).HasColumnName("target_snapshot_id").IsRequired();
        builder.Property(item => item.ScopeGenerationId).HasColumnName("scope_generation_id").IsRequired();
        builder.Property(item => item.AlgorithmVersion).HasColumnName("algorithm_version").IsRequired();
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.SummaryCountsJson).HasColumnName("summary_counts_json").HasMaxLength(2048).IsRequired();
        builder.Property(item => item.ContentSha256).HasColumnName("content_sha256").HasMaxLength(64).IsRequired();
        builder.HasOne<DatabaseConnectionProfile>().WithMany().HasForeignKey(item => item.ProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DatabaseDiscoverySnapshot>().WithMany().HasForeignKey(item => item.BaseSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DatabaseDiscoverySnapshot>().WithMany().HasForeignKey(item => item.TargetSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DatabaseDiscoveryScopeGeneration>().WithMany().HasForeignKey(item => item.ScopeGenerationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.TargetSnapshotId).IsUnique();
        builder.HasIndex(item => new { item.ProfileId, item.CreatedAt });
    }
}

public sealed class DatabaseDiscoveryDifferenceEntryConfiguration : IEntityTypeConfiguration<DatabaseDiscoveryDifferenceEntry>
{
    public void Configure(EntityTypeBuilder<DatabaseDiscoveryDifferenceEntry> builder)
    {
        builder.ToTable("database_discovery_difference_entries", table =>
        {
            table.HasCheckConstraint("ck_database_discovery_difference_entries_kind", "entity_kind IN ('Schema','DatabaseObject','Column','PrimaryKey','ForeignKey','UniqueConstraint','Index','Sequence')");
            table.HasCheckConstraint("ck_database_discovery_difference_entries_state", "state IN ('Added','Changed','MissingFromSource')");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.DifferenceId).HasColumnName("difference_id").IsRequired();
        builder.Property(item => item.EntityKind).HasColumnName("entity_kind").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.LogicalIdentity).HasColumnName("logical_identity").HasMaxLength(2048).IsRequired();
        builder.Property(item => item.ParentLogicalIdentity).HasColumnName("parent_logical_identity").HasMaxLength(2048);
        builder.Property(item => item.DisplayName).HasColumnName("display_name").HasMaxLength(512).IsRequired();
        builder.Property(item => item.State).HasColumnName("state").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.BeforeJson).HasColumnName("before_json").HasMaxLength(65536);
        builder.Property(item => item.AfterJson).HasColumnName("after_json").HasMaxLength(65536);
        builder.HasOne(item => item.Difference).WithMany(item => item.Entries).HasForeignKey(item => item.DifferenceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.DifferenceId, item.State, item.EntityKind, item.Id });
        builder.HasIndex(item => new { item.DifferenceId, item.EntityKind, item.LogicalIdentity }).IsUnique();
    }
}
