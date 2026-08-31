using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Persistence;

public sealed class DatabaseObjectDiscoveryBindingConfiguration : IEntityTypeConfiguration<DatabaseObjectDiscoveryBinding>
{
    public void Configure(EntityTypeBuilder<DatabaseObjectDiscoveryBinding> builder)
    {
        builder.ToTable("database_object_discovery_bindings", table =>
        {
            table.HasCheckConstraint("ck_database_object_discovery_bindings_versions", "identity_algorithm_version >= 1 AND version >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(x => x.ScopeGenerationId).HasColumnName("scope_generation_id").IsRequired();
        builder.Property(x => x.IdentityAlgorithmVersion).HasColumnName("identity_algorithm_version").IsRequired();
        builder.Property(x => x.SchemaLogicalIdentity).HasColumnName("schema_logical_identity").HasMaxLength(2048).IsRequired();
        builder.Property(x => x.LogicalIdentity).HasColumnName("logical_identity").HasMaxLength(2048).IsRequired();
        builder.Property(x => x.DatabaseObjectId).HasColumnName("database_object_id").IsRequired();
        builder.Property(x => x.FirstAppliedSnapshotId).HasColumnName("first_applied_snapshot_id").IsRequired();
        builder.Property(x => x.LastAppliedSnapshotId).HasColumnName("last_applied_snapshot_id").IsRequired();
        builder.Property(x => x.SourceMissingSinceSnapshotId).HasColumnName("source_missing_since_snapshot_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.HasOne(x => x.Profile).WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ScopeGeneration).WithMany().HasForeignKey(x => x.ScopeGenerationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DatabaseObject).WithMany().HasForeignKey(x => x.DatabaseObjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DatabaseDiscoverySnapshot>().WithMany().HasForeignKey(x => x.FirstAppliedSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DatabaseDiscoverySnapshot>().WithMany().HasForeignKey(x => x.LastAppliedSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DatabaseDiscoverySnapshot>().WithMany().HasForeignKey(x => x.SourceMissingSinceSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.DatabaseObjectId).IsUnique();
        builder.HasIndex(x => new { x.ProfileId, x.ScopeGenerationId, x.IdentityAlgorithmVersion, x.LogicalIdentity }).IsUnique();
    }
}

public sealed class DatabaseColumnDiscoveryBindingConfiguration : IEntityTypeConfiguration<DatabaseColumnDiscoveryBinding>
{
    public void Configure(EntityTypeBuilder<DatabaseColumnDiscoveryBinding> builder)
    {
        builder.ToTable("database_column_discovery_bindings", table =>
        {
            table.HasCheckConstraint("ck_database_column_discovery_bindings_versions", "identity_algorithm_version >= 1 AND version >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(x => x.ScopeGenerationId).HasColumnName("scope_generation_id").IsRequired();
        builder.Property(x => x.IdentityAlgorithmVersion).HasColumnName("identity_algorithm_version").IsRequired();
        builder.Property(x => x.SchemaLogicalIdentity).HasColumnName("schema_logical_identity").HasMaxLength(2048).IsRequired();
        builder.Property(x => x.ParentObjectLogicalIdentity).HasColumnName("parent_object_logical_identity").HasMaxLength(2048).IsRequired();
        builder.Property(x => x.LogicalIdentity).HasColumnName("logical_identity").HasMaxLength(2048).IsRequired();
        builder.Property(x => x.DatabaseColumnId).HasColumnName("database_column_id").IsRequired();
        builder.Property(x => x.FirstAppliedSnapshotId).HasColumnName("first_applied_snapshot_id").IsRequired();
        builder.Property(x => x.LastAppliedSnapshotId).HasColumnName("last_applied_snapshot_id").IsRequired();
        builder.Property(x => x.SourceMissingSinceSnapshotId).HasColumnName("source_missing_since_snapshot_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.HasOne(x => x.Profile).WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ScopeGeneration).WithMany().HasForeignKey(x => x.ScopeGenerationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DatabaseColumn).WithMany().HasForeignKey(x => x.DatabaseColumnId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DatabaseDiscoverySnapshot>().WithMany().HasForeignKey(x => x.FirstAppliedSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DatabaseDiscoverySnapshot>().WithMany().HasForeignKey(x => x.LastAppliedSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DatabaseDiscoverySnapshot>().WithMany().HasForeignKey(x => x.SourceMissingSinceSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.DatabaseColumnId).IsUnique();
        builder.HasIndex(x => new { x.ProfileId, x.ScopeGenerationId, x.IdentityAlgorithmVersion, x.LogicalIdentity }).IsUnique();
    }
}

public sealed class DatabaseDiscoverySyncPlanConfiguration : IEntityTypeConfiguration<DatabaseDiscoverySyncPlan>
{
    public void Configure(EntityTypeBuilder<DatabaseDiscoverySyncPlan> builder)
    {
        builder.ToTable("database_discovery_sync_plans", table =>
        {
            table.HasCheckConstraint("ck_database_discovery_sync_plans_status", "status IN ('Draft','Ready','Applied','Superseded')");
            table.HasCheckConstraint("ck_database_discovery_sync_plans_versions", "profile_configuration_revision >= 1 AND selection_format_version >= 1 AND identity_algorithm_version >= 1 AND version >= 1");
            table.HasCheckConstraint("ck_database_discovery_sync_plans_selection", "json_valid(selection_json) AND json_type(selection_json) = 'array'");
            table.HasCheckConstraint("ck_database_discovery_sync_plans_hashes", "(preview_hash IS NULL OR length(preview_hash) = 64) AND (confirmed_preview_hash IS NULL OR length(confirmed_preview_hash) = 64)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(x => x.DatabaseSourceId).HasColumnName("database_source_id").IsRequired();
        builder.Property(x => x.ProfileConfigurationRevision).HasColumnName("profile_configuration_revision").IsRequired();
        builder.Property(x => x.BaseSnapshotId).HasColumnName("base_snapshot_id");
        builder.Property(x => x.TargetSnapshotId).HasColumnName("target_snapshot_id").IsRequired();
        builder.Property(x => x.TargetDifferenceId).HasColumnName("target_difference_id");
        builder.Property(x => x.ScopeGenerationId).HasColumnName("scope_generation_id").IsRequired();
        builder.Property(x => x.IdentityAlgorithmVersion).HasColumnName("identity_algorithm_version").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.SelectionFormatVersion).HasColumnName("selection_format_version").IsRequired();
        builder.Property(x => x.SelectionJson).HasColumnName("selection_json").IsRequired();
        builder.Property(x => x.PreviewFormatVersion).HasColumnName("preview_format_version");
        builder.Property(x => x.PreviewPayloadJson).HasColumnName("preview_payload_json");
        builder.Property(x => x.PreviewHash).HasColumnName("preview_hash").HasMaxLength(64);
        builder.Property(x => x.ConfirmedPreviewHash).HasColumnName("confirmed_preview_hash").HasMaxLength(64);
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(x => x.CreatedByDisplayName).HasColumnName("created_by_display_name").HasMaxLength(160).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(x => x.ConfirmedByUserId).HasColumnName("confirmed_by_user_id");
        builder.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(x => x.AppliedAt).HasColumnName("applied_at");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.HasOne(x => x.Profile).WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DatabaseSource).WithMany().HasForeignKey(x => x.DatabaseSourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BaseSnapshot).WithMany().HasForeignKey(x => x.BaseSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TargetSnapshot).WithMany().HasForeignKey(x => x.TargetSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TargetDifference).WithMany().HasForeignKey(x => x.TargetDifferenceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ScopeGeneration).WithMany().HasForeignKey(x => x.ScopeGenerationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ProfileId, x.Status, x.CreatedAt });
        builder.HasIndex(x => x.TargetSnapshotId);
    }
}

public sealed class DatabaseDiscoverySyncApplyResultConfiguration : IEntityTypeConfiguration<DatabaseDiscoverySyncApplyResult>
{
    public void Configure(EntityTypeBuilder<DatabaseDiscoverySyncApplyResult> builder)
    {
        builder.ToTable("database_discovery_sync_apply_results", table =>
        {
            table.HasCheckConstraint("ck_database_discovery_sync_apply_results_counts", "created_objects >= 0 AND linked_objects >= 0 AND created_columns >= 0 AND linked_columns >= 0 AND updated_objects >= 0 AND updated_columns >= 0 AND marked_missing >= 0 AND cleared_missing >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.PlanId).HasColumnName("plan_id").IsRequired();
        builder.Property(x => x.CreatedObjects).HasColumnName("created_objects").IsRequired();
        builder.Property(x => x.LinkedObjects).HasColumnName("linked_objects").IsRequired();
        builder.Property(x => x.CreatedColumns).HasColumnName("created_columns").IsRequired();
        builder.Property(x => x.LinkedColumns).HasColumnName("linked_columns").IsRequired();
        builder.Property(x => x.UpdatedObjects).HasColumnName("updated_objects").IsRequired();
        builder.Property(x => x.UpdatedColumns).HasColumnName("updated_columns").IsRequired();
        builder.Property(x => x.MarkedMissing).HasColumnName("marked_missing").IsRequired();
        builder.Property(x => x.ClearedMissing).HasColumnName("cleared_missing").IsRequired();
        builder.Property(x => x.AppliedAt).HasColumnName("applied_at").IsRequired();
        builder.Property(x => x.AppliedByUserId).HasColumnName("applied_by_user_id").IsRequired();
        builder.Property(x => x.AppliedByDisplayName).HasColumnName("applied_by_display_name").HasMaxLength(160).IsRequired();
        builder.HasOne(x => x.Plan).WithOne(x => x.ApplyResult).HasForeignKey<DatabaseDiscoverySyncApplyResult>(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.AppliedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.PlanId).IsUnique();
    }
}

public sealed class DatabaseDiscoverySyncAuditEventConfiguration : IEntityTypeConfiguration<DatabaseDiscoverySyncAuditEvent>
{
    public void Configure(EntityTypeBuilder<DatabaseDiscoverySyncAuditEvent> builder)
    {
        builder.ToTable("database_discovery_sync_audit_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(x => x.PlanId).HasColumnName("plan_id");
        builder.Property(x => x.Action).HasColumnName("action").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Outcome).HasColumnName("outcome").HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.ReasonCode).HasColumnName("reason_code").HasMaxLength(64);
        builder.Property(x => x.SafeMetadataJson).HasColumnName("safe_metadata_json").HasMaxLength(2048);
        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id").IsRequired();
        builder.Property(x => x.ActorDisplayName).HasColumnName("actor_display_name").HasMaxLength(160).IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.HasOne(x => x.Profile).WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ProfileId, x.OccurredAt });
        builder.HasIndex(x => new { x.PlanId, x.OccurredAt });
    }
}
