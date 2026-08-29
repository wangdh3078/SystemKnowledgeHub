using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Persistence;

public sealed class DatabaseConnectionAuditEventConfiguration : IEntityTypeConfiguration<DatabaseConnectionAuditEvent>
{
    public void Configure(EntityTypeBuilder<DatabaseConnectionAuditEvent> builder)
    {
        builder.ToTable("database_connection_audit_events");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(item => item.Action).HasColumnName("action").HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(item => item.Outcome).HasColumnName("outcome").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.ErrorCode).HasColumnName("error_code").HasMaxLength(64);
        builder.Property(item => item.VendorCode).HasColumnName("vendor_code").HasMaxLength(16);
        builder.Property(item => item.ActorUserId).HasColumnName("actor_user_id").IsRequired();
        builder.Property(item => item.ActorDisplayName).HasColumnName("actor_display_name").HasMaxLength(160).IsRequired();
        builder.Property(item => item.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.HasOne(item => item.Profile).WithMany(item => item.AuditEvents).HasForeignKey(item => item.ProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.ProfileId, item.OccurredAt });
    }
}
