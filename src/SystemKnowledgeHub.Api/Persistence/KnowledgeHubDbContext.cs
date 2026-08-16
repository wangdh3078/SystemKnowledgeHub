using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Domain;
using SystemKnowledgeHub.Api.Features.BusinessRules.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Integrations.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KnowledgeHubDbContext).Assembly);
    }
}
