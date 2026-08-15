using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;

namespace SystemKnowledgeHub.Api.Features.UnknownItems.Persistence;

public sealed class UnknownItemConfiguration : IEntityTypeConfiguration<UnknownItem>
{
    public void Configure(EntityTypeBuilder<UnknownItem> builder)
    {
        builder.ToTable("unknown_items", table =>
        {
            table.HasCheckConstraint("ck_unknown_items_priority", "priority IN ('High','Medium','Low')");
            table.HasCheckConstraint("ck_unknown_items_status", "status IN ('Open','Investigating','ConclusionConfirmed','Closed')");
            table.HasCheckConstraint("ck_unknown_items_version", "version >= 1");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.ItemCode).HasColumnName("item_code").UseCollation("NOCASE").IsRequired();
        builder.Property(item => item.SystemId).HasColumnName("system_id").IsRequired();
        builder.Property(item => item.Question).HasColumnName("question").IsRequired();
        builder.Property(item => item.Context).HasColumnName("context");
        builder.Property(item => item.Priority).HasColumnName("priority").HasConversion<string>().IsRequired();
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(item => item.InvestigationStartedAt).HasColumnName("investigation_started_at");
        builder.Property(item => item.ConclusionConfirmedAt).HasColumnName("conclusion_confirmed_at");
        builder.Property(item => item.ClosedAt).HasColumnName("closed_at");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.CreatedByName).HasColumnName("created_by_name").IsRequired();
        builder.Property(item => item.CreatedByRole).HasColumnName("created_by_role");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(item => item.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();
        builder.HasOne(item => item.System).WithMany().HasForeignKey(item => item.SystemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.ItemCode).IsUnique();
        builder.HasIndex(item => new { item.SystemId, item.Status, item.Priority, item.UpdatedAt });
        builder.HasIndex(item => new { item.Status, item.UpdatedAt });
        builder.HasIndex(item => new { item.Priority, item.Status });
    }
}

public sealed class UnknownItemTargetConfiguration : IEntityTypeConfiguration<UnknownItemTarget>
{
    public void Configure(EntityTypeBuilder<UnknownItemTarget> builder)
    {
        builder.ToTable("unknown_item_targets", table =>
        {
            table.HasCheckConstraint("ck_unknown_item_targets_type", "target_type IN ('System','DatabaseSource','BusinessFunction','DatabaseObject','DatabaseColumn','BusinessRule','Integration')");
            table.HasCheckConstraint("ck_unknown_item_targets_primary", "is_primary IN (0,1)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.UnknownItemId).HasColumnName("unknown_item_id").IsRequired();
        builder.Property(item => item.TargetType).HasColumnName("target_type").HasConversion<string>().IsRequired();
        builder.Property(item => item.TargetId).HasColumnName("target_id").IsRequired();
        builder.Property(item => item.IsPrimary).HasColumnName("is_primary").IsRequired();
        builder.Property(item => item.DisplaySnapshot).HasColumnName("display_snapshot").IsRequired();
        builder.HasOne(item => item.UnknownItem).WithMany(item => item.Targets).HasForeignKey(item => item.UnknownItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.UnknownItemId, item.TargetType, item.TargetId }).IsUnique();
        builder.HasIndex(item => new { item.TargetType, item.TargetId, item.UnknownItemId });
        builder.HasIndex(item => new { item.UnknownItemId, item.IsPrimary });
        builder.HasIndex(item => item.UnknownItemId).HasFilter("is_primary = 1").IsUnique();
    }
}

public sealed class FindingConfiguration : IEntityTypeConfiguration<Finding>
{
    public void Configure(EntityTypeBuilder<Finding> builder)
    {
        builder.ToTable("findings");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.UnknownItemId).HasColumnName("unknown_item_id").IsRequired();
        builder.Property(item => item.Content).HasColumnName("content").IsRequired();
        builder.Property(item => item.RecordedByName).HasColumnName("recorded_by_name").IsRequired();
        builder.Property(item => item.RecordedByRole).HasColumnName("recorded_by_role").IsRequired();
        builder.Property(item => item.RecordedByTeam).HasColumnName("recorded_by_team");
        builder.Property(item => item.RecordedByExternalKey).HasColumnName("recorded_by_external_key");
        builder.Property(item => item.RecordedBySource).HasColumnName("recorded_by_source");
        builder.Property(item => item.RecordedByNote).HasColumnName("recorded_by_note");
        builder.Property(item => item.RecordedAt).HasColumnName("recorded_at").IsRequired();
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasOne(item => item.UnknownItem).WithMany(item => item.Findings).HasForeignKey(item => item.UnknownItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.UnknownItemId, item.RecordedAt });
    }
}

public sealed class ResolutionConfiguration : IEntityTypeConfiguration<Resolution>
{
    public void Configure(EntityTypeBuilder<Resolution> builder)
    {
        builder.ToTable("resolutions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.UnknownItemId).HasColumnName("unknown_item_id").IsRequired();
        builder.Property(item => item.Conclusion).HasColumnName("conclusion").IsRequired();
        builder.Property(item => item.ConfirmedByName).HasColumnName("confirmed_by_name");
        builder.Property(item => item.ConfirmedByRole).HasColumnName("confirmed_by_role");
        builder.Property(item => item.ConfirmedByTeam).HasColumnName("confirmed_by_team");
        builder.Property(item => item.ConfirmedByExternalKey).HasColumnName("confirmed_by_external_key");
        builder.Property(item => item.ConfirmedBySource).HasColumnName("confirmed_by_source");
        builder.Property(item => item.ConfirmedByNote).HasColumnName("confirmed_by_note");
        builder.Property(item => item.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasOne(item => item.UnknownItem).WithOne(item => item.Resolution).HasForeignKey<Resolution>(item => item.UnknownItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.UnknownItemId).IsUnique();
    }
}

public sealed class KnowledgeUpdateConfiguration : IEntityTypeConfiguration<KnowledgeUpdate>
{
    public void Configure(EntityTypeBuilder<KnowledgeUpdate> builder)
    {
        builder.ToTable("knowledge_updates", table =>
        {
            table.HasCheckConstraint("ck_knowledge_updates_target_type", "target_type IN ('System','DatabaseSource','BusinessFunction','DatabaseObject','DatabaseColumn','BusinessRule','Integration')");
            table.HasCheckConstraint("ck_knowledge_updates_status", "status IN ('Proposed','Applied')");
            table.HasCheckConstraint("ck_knowledge_updates_applied_snapshot", "status = 'Proposed' OR (applied_by_name IS NOT NULL AND applied_by_role IS NOT NULL AND applied_at IS NOT NULL)");
            table.HasCheckConstraint("ck_knowledge_updates_status_pair", "(knowledge_status_before IS NULL AND knowledge_status_after IS NULL) OR (knowledge_status_before IS NOT NULL AND knowledge_status_after IS NOT NULL)");
            table.HasCheckConstraint("ck_knowledge_updates_before_json", "json_valid(before_json)");
            table.HasCheckConstraint("ck_knowledge_updates_after_json", "json_valid(after_json)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.UnknownItemId).HasColumnName("unknown_item_id").IsRequired();
        builder.Property(item => item.TargetType).HasColumnName("target_type").HasConversion<string>().IsRequired();
        builder.Property(item => item.TargetId).HasColumnName("target_id").IsRequired();
        builder.Property(item => item.SubjectDetailKey).HasColumnName("subject_detail_key");
        builder.Property(item => item.ChangeSummary).HasColumnName("change_summary").IsRequired();
        builder.Property(item => item.BeforeJson).HasColumnName("before_json").IsRequired();
        builder.Property(item => item.AfterJson).HasColumnName("after_json").IsRequired();
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(item => item.KnowledgeStatusBefore).HasColumnName("knowledge_status_before").HasConversion<string>();
        builder.Property(item => item.KnowledgeStatusAfter).HasColumnName("knowledge_status_after").HasConversion<string>();
        builder.Property(item => item.AppliedByName).HasColumnName("applied_by_name");
        builder.Property(item => item.AppliedByRole).HasColumnName("applied_by_role");
        builder.Property(item => item.AppliedByTeam).HasColumnName("applied_by_team");
        builder.Property(item => item.AppliedByExternalKey).HasColumnName("applied_by_external_key");
        builder.Property(item => item.AppliedBySource).HasColumnName("applied_by_source");
        builder.Property(item => item.AppliedByNote).HasColumnName("applied_by_note");
        builder.Property(item => item.AppliedAt).HasColumnName("applied_at");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasOne(item => item.UnknownItem).WithMany(item => item.KnowledgeUpdates).HasForeignKey(item => item.UnknownItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.UnknownItemId, item.Status });
        builder.HasIndex(item => new { item.TargetType, item.TargetId });
        builder.HasIndex(item => new { item.Status, item.AppliedAt });
    }
}

public sealed class UnknownItemActivityConfiguration : IEntityTypeConfiguration<UnknownItemActivity>
{
    public void Configure(EntityTypeBuilder<UnknownItemActivity> builder)
    {
        builder.ToTable("unknown_item_activities", table =>
        {
            table.HasCheckConstraint("ck_unknown_item_activities_type", "activity_type IN ('Created','StatusChanged','FindingAdded','EvidenceAdded','ResolutionRecorded','KnowledgeUpdateApplied','Closed','Reopened')");
            table.HasCheckConstraint("ck_unknown_item_activities_related_type", "related_type IS NULL OR related_type IN ('Finding','Evidence','Resolution','KnowledgeUpdate')");
            table.HasCheckConstraint("ck_unknown_item_activities_related_pair", "(related_type IS NULL AND related_id IS NULL) OR (related_type IS NOT NULL AND related_id IS NOT NULL)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.UnknownItemId).HasColumnName("unknown_item_id").IsRequired();
        builder.Property(item => item.ActivityType).HasColumnName("activity_type").HasConversion<string>().IsRequired();
        builder.Property(item => item.ActorName).HasColumnName("actor_name").IsRequired();
        builder.Property(item => item.ActorRole).HasColumnName("actor_role").IsRequired();
        builder.Property(item => item.ActorTeam).HasColumnName("actor_team");
        builder.Property(item => item.ActorExternalKey).HasColumnName("actor_external_key");
        builder.Property(item => item.ActorSource).HasColumnName("actor_source");
        builder.Property(item => item.ActorNote).HasColumnName("actor_note");
        builder.Property(item => item.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(item => item.Note).HasColumnName("note");
        builder.Property(item => item.RelatedType).HasColumnName("related_type");
        builder.Property(item => item.RelatedId).HasColumnName("related_id");
        builder.HasOne(item => item.UnknownItem).WithMany(item => item.Activities).HasForeignKey(item => item.UnknownItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.UnknownItemId, item.OccurredAt, item.Id });
        builder.HasIndex(item => new { item.RelatedType, item.RelatedId });
    }
}
