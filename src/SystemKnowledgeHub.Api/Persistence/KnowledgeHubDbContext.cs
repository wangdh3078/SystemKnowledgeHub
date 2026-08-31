using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Domain;
using SystemKnowledgeHub.Api.Features.Attachments.Domain;
using SystemKnowledgeHub.Api.Features.BusinessRules.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Integrations.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using UserEntity = SystemKnowledgeHub.Api.Features.Users.Domain.User;

namespace SystemKnowledgeHub.Api.Persistence;

public sealed class KnowledgeHubDbContext(DbContextOptions<KnowledgeHubDbContext> options)
    : DbContext(options)
{
    public DbSet<KnowledgeSystem> Systems => Set<KnowledgeSystem>();
    public DbSet<BusinessFunction> BusinessFunctions => Set<BusinessFunction>();
    public DbSet<BusinessRule> BusinessRules => Set<BusinessRule>();
    public DbSet<BusinessProcessStep> BusinessProcessSteps => Set<BusinessProcessStep>();
    public DbSet<SystemTechnologyTag> SystemTechnologyTags => Set<SystemTechnologyTag>();
    public DbSet<DatabaseSource> DatabaseSources => Set<DatabaseSource>();
    public DbSet<DatabaseObject> DatabaseObjects => Set<DatabaseObject>();
    public DbSet<DatabaseColumn> DatabaseColumns => Set<DatabaseColumn>();
    public DbSet<ColumnKnownValue> ColumnKnownValues => Set<ColumnKnownValue>();
    public DbSet<DatabaseConnectionProfile> DatabaseConnectionProfiles => Set<DatabaseConnectionProfile>();
    public DbSet<DatabaseConnectionSecret> DatabaseConnectionSecrets => Set<DatabaseConnectionSecret>();
    public DbSet<DatabaseConnectionAuditEvent> DatabaseConnectionAuditEvents => Set<DatabaseConnectionAuditEvent>();
    public DbSet<DatabaseDiscoveryRun> DatabaseDiscoveryRuns => Set<DatabaseDiscoveryRun>();
    public DbSet<DatabaseDiscoveryScopeGeneration> DatabaseDiscoveryScopeGenerations => Set<DatabaseDiscoveryScopeGeneration>();
    public DbSet<DatabaseDiscoverySnapshot> DatabaseDiscoverySnapshots => Set<DatabaseDiscoverySnapshot>();
    public DbSet<DatabaseDiscoveryDifference> DatabaseDiscoveryDifferences => Set<DatabaseDiscoveryDifference>();
    public DbSet<DatabaseDiscoveryDifferenceEntry> DatabaseDiscoveryDifferenceEntries => Set<DatabaseDiscoveryDifferenceEntry>();
    public DbSet<DatabaseObjectDiscoveryBinding> DatabaseObjectDiscoveryBindings => Set<DatabaseObjectDiscoveryBinding>();
    public DbSet<DatabaseColumnDiscoveryBinding> DatabaseColumnDiscoveryBindings => Set<DatabaseColumnDiscoveryBinding>();
    public DbSet<DatabaseDiscoverySyncPlan> DatabaseDiscoverySyncPlans => Set<DatabaseDiscoverySyncPlan>();
    public DbSet<DatabaseDiscoverySyncApplyResult> DatabaseDiscoverySyncApplyResults => Set<DatabaseDiscoverySyncApplyResult>();
    public DbSet<DatabaseDiscoverySyncAuditEvent> DatabaseDiscoverySyncAuditEvents => Set<DatabaseDiscoverySyncAuditEvent>();
    public DbSet<Evidence> Evidence => Set<Evidence>();
    public DbSet<Integration> Integrations => Set<Integration>();
    public DbSet<IntegrationContractField> IntegrationContractFields => Set<IntegrationContractField>();
    public DbSet<KnowledgeRelation> KnowledgeRelations => Set<KnowledgeRelation>();
    public DbSet<UnknownItem> UnknownItems => Set<UnknownItem>();
    public DbSet<UnknownItemTarget> UnknownItemTargets => Set<UnknownItemTarget>();
    public DbSet<Finding> Findings => Set<Finding>();
    public DbSet<Resolution> Resolutions => Set<Resolution>();
    public DbSet<KnowledgeUpdate> KnowledgeUpdates => Set<KnowledgeUpdate>();
    public DbSet<UnknownItemActivity> UnknownItemActivities => Set<UnknownItemActivity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<KnowledgeRole> KnowledgeRoles => Set<KnowledgeRole>();
    public DbSet<UserKnowledgeRole> UserKnowledgeRoles => Set<UserKnowledgeRole>();
    public DbSet<LoginIdentity> LoginIdentities => Set<LoginIdentity>();
    public DbSet<LocalLoginCredential> LocalLoginCredentials => Set<LocalLoginCredential>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeDocumentRevision> KnowledgeDocumentRevisions => Set<KnowledgeDocumentRevision>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<AttachmentReference> AttachmentReferences => Set<AttachmentReference>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceImmutableAttachmentReferences();
        EnforceImmutableDiscoveryHistory();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnforceImmutableAttachmentReferences();
        EnforceImmutableDiscoveryHistory();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KnowledgeHubDbContext).Assembly);
    }

    private void EnforceImmutableAttachmentReferences()
    {
        if (ChangeTracker.Entries<AttachmentReference>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Attachment references are immutable revision snapshots.");
        }
    }

    private void EnforceImmutableDiscoveryHistory()
    {
        var immutableChanged = ChangeTracker.Entries()
            .Any(entry => entry.Entity is DatabaseDiscoveryScopeGeneration
                    or DatabaseDiscoverySnapshot
                    or DatabaseDiscoveryDifference
                    or DatabaseDiscoveryDifferenceEntry
                && entry.State is EntityState.Modified or EntityState.Deleted);
        if (immutableChanged)
        {
            throw new InvalidOperationException("Discovery scope, snapshot and difference history is immutable.");
        }
    }
}
