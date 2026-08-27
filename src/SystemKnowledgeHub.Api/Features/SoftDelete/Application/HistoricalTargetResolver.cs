using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Features.SoftDelete.Application;

/// <summary>
/// Resolves the deliberately small identity projection used by immutable historical reads.
/// This is not a general-purpose unfiltered entity resolver.
/// </summary>
public sealed class HistoricalTargetResolver(KnowledgeHubDbContext dbContext)
{
    public Task<HistoricalTargetIdentity?> Resolve(
        KnowledgeTargetType type,
        long id,
        CancellationToken cancellationToken) => type switch
    {
        KnowledgeTargetType.System => dbContext.Systems.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new HistoricalTargetIdentity(item.Id, type.ToString(), item.Name, item.IsDeleted, !item.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken),
        KnowledgeTargetType.DatabaseSource => dbContext.DatabaseSources.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new HistoricalTargetIdentity(item.Id, type.ToString(), item.Name, item.IsDeleted, !item.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken),
        KnowledgeTargetType.BusinessFunction => dbContext.BusinessFunctions.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new HistoricalTargetIdentity(item.Id, type.ToString(), item.Name, item.IsDeleted, !item.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken),
        KnowledgeTargetType.DatabaseObject => dbContext.DatabaseObjects.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new HistoricalTargetIdentity(item.Id, type.ToString(), item.SchemaName + "." + item.ObjectName, item.IsDeleted, !item.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken),
        KnowledgeTargetType.DatabaseColumn => dbContext.DatabaseColumns.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new HistoricalTargetIdentity(item.Id, type.ToString(), item.ColumnName, item.IsDeleted, !item.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken),
        KnowledgeTargetType.BusinessRule => dbContext.BusinessRules.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new HistoricalTargetIdentity(item.Id, type.ToString(), item.Name, item.IsDeleted, !item.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken),
        KnowledgeTargetType.Integration => dbContext.Integrations.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new HistoricalTargetIdentity(item.Id, type.ToString(), item.Name, item.IsDeleted, !item.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken),
        KnowledgeTargetType.KnowledgeDocument => dbContext.KnowledgeDocuments.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new HistoricalTargetIdentity(item.Id, type.ToString(), item.Title, item.IsDeleted, !item.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken),
        _ => Task.FromResult<HistoricalTargetIdentity?>(null),
    };

    public Task<HistoricalTargetIdentity?> Resolve(
        EvidenceSubjectType type,
        long id,
        CancellationToken cancellationToken) => TryMap(type, out var targetType)
            ? Resolve(targetType, id, cancellationToken)
            : Task.FromResult<HistoricalTargetIdentity?>(null);

    private static bool TryMap(EvidenceSubjectType type, out KnowledgeTargetType targetType)
    {
        targetType = type switch
        {
            EvidenceSubjectType.System => KnowledgeTargetType.System,
            EvidenceSubjectType.DatabaseSource => KnowledgeTargetType.DatabaseSource,
            EvidenceSubjectType.BusinessFunction => KnowledgeTargetType.BusinessFunction,
            EvidenceSubjectType.DatabaseObject => KnowledgeTargetType.DatabaseObject,
            EvidenceSubjectType.DatabaseColumn => KnowledgeTargetType.DatabaseColumn,
            EvidenceSubjectType.BusinessRule => KnowledgeTargetType.BusinessRule,
            EvidenceSubjectType.Integration => KnowledgeTargetType.Integration,
            EvidenceSubjectType.KnowledgeDocument => KnowledgeTargetType.KnowledgeDocument,
            _ => default,
        };
        return type is EvidenceSubjectType.System
            or EvidenceSubjectType.DatabaseSource
            or EvidenceSubjectType.BusinessFunction
            or EvidenceSubjectType.DatabaseObject
            or EvidenceSubjectType.DatabaseColumn
            or EvidenceSubjectType.BusinessRule
            or EvidenceSubjectType.Integration
            or EvidenceSubjectType.KnowledgeDocument;
    }
}

public sealed record HistoricalTargetIdentity(
    long Id,
    string TargetType,
    string DisplayName,
    bool IsDeleted,
    bool IsNavigable);
