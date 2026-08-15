using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Relationships.Application.Models;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Features.Relationships.Application;

public sealed class RelationshipTargetResolver(KnowledgeHubDbContext dbContext)
{
    public async Task<RelationshipEndpointContext?> Resolve(
        KnowledgeTargetType type,
        long id,
        CancellationToken cancellationToken)
    {
        return type switch
        {
            KnowledgeTargetType.System => await dbContext.Systems.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new RelationshipEndpointContext(
                    item.Name, "系统", item.Purpose, item.KnowledgeStatus.ToString(),
                    new[] { new SystemContextResponse(item.Id, item.Name) }))
                .SingleOrDefaultAsync(cancellationToken),
            KnowledgeTargetType.DatabaseSource => await dbContext.DatabaseSources.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new RelationshipEndpointContext(
                    item.System.Name + " · " + item.Name, "数据库来源", item.Description, "Unknown",
                    new[] { new SystemContextResponse(item.System.Id, item.System.Name) }))
                .SingleOrDefaultAsync(cancellationToken),
            KnowledgeTargetType.BusinessFunction => await dbContext.BusinessFunctions.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new RelationshipEndpointContext(
                    item.Name, "业务功能", item.Purpose, item.KnowledgeStatus.ToString(),
                    new[] { new SystemContextResponse(item.System.Id, item.System.Name) }))
                .SingleOrDefaultAsync(cancellationToken),
            KnowledgeTargetType.DatabaseObject => await dbContext.DatabaseObjects.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new RelationshipEndpointContext(
                    item.SchemaName + "." + item.ObjectName, "数据库对象", item.BusinessDescription, item.KnowledgeStatus.ToString(),
                    new[] { new SystemContextResponse(item.DatabaseSource.System.Id, item.DatabaseSource.System.Name) }))
                .SingleOrDefaultAsync(cancellationToken),
            KnowledgeTargetType.DatabaseColumn => await dbContext.DatabaseColumns.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new RelationshipEndpointContext(
                    item.DatabaseObject.SchemaName + "." + item.DatabaseObject.ObjectName + "." + item.ColumnName,
                    "字段", item.BusinessDescription, item.KnowledgeStatus.ToString(),
                    new[] { new SystemContextResponse(item.DatabaseObject.DatabaseSource.System.Id, item.DatabaseObject.DatabaseSource.System.Name) }))
                .SingleOrDefaultAsync(cancellationToken),
            KnowledgeTargetType.BusinessRule => await dbContext.BusinessRules.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new RelationshipEndpointContext(
                    item.Name, "业务规则", item.Description, item.KnowledgeStatus.ToString(),
                    new[] { new SystemContextResponse(item.System.Id, item.System.Name) }))
                .SingleOrDefaultAsync(cancellationToken),
            _ => null,
        };
    }

    public async Task<IReadOnlyList<TargetPreviewResponse>> Search(
        IReadOnlyList<KnowledgeTargetType> allowedTypes,
        long systemId,
        string? query,
        CancellationToken cancellationToken)
    {
        var normalized = query?.Trim();
        var pattern = string.IsNullOrWhiteSpace(normalized) ? null : $"%{normalized}%";
        var results = new List<TargetPreviewResponse>();

        if (allowedTypes.Contains(KnowledgeTargetType.System))
        {
            results.AddRange(await dbContext.Systems.AsNoTracking()
                .Where(item => item.Id == systemId && (pattern == null || EF.Functions.Like(item.Name, pattern) || EF.Functions.Like(item.DisplayName, pattern)))
                .Select(item => new TargetPreviewResponse(
                    new TargetReferenceResponse("System", item.Id),
                    new[] { new SystemContextResponse(item.Id, item.Name) }, item.Name, "系统", item.Purpose, item.KnowledgeStatus.ToString()))
                .ToArrayAsync(cancellationToken));
        }
        if (allowedTypes.Contains(KnowledgeTargetType.DatabaseSource))
        {
            results.AddRange(await dbContext.DatabaseSources.AsNoTracking()
                .Where(item => item.SystemId == systemId && (pattern == null || EF.Functions.Like(item.Name, pattern)))
                .Select(item => new TargetPreviewResponse(
                    new TargetReferenceResponse("DatabaseSource", item.Id),
                    new[] { new SystemContextResponse(item.System.Id, item.System.Name) },
                    item.System.Name + " · " + item.Name, "数据库来源", item.Description, "Unknown"))
                .ToArrayAsync(cancellationToken));
        }
        if (allowedTypes.Contains(KnowledgeTargetType.BusinessFunction))
        {
            results.AddRange(await dbContext.BusinessFunctions.AsNoTracking()
                .Where(item => item.SystemId == systemId && (pattern == null || EF.Functions.Like(item.Name, pattern) || (item.Purpose != null && EF.Functions.Like(item.Purpose, pattern))))
                .Select(item => new TargetPreviewResponse(
                    new TargetReferenceResponse("BusinessFunction", item.Id),
                    new[] { new SystemContextResponse(item.System.Id, item.System.Name) }, item.Name, "业务功能", item.Purpose, item.KnowledgeStatus.ToString()))
                .ToArrayAsync(cancellationToken));
        }
        if (allowedTypes.Contains(KnowledgeTargetType.DatabaseObject))
        {
            results.AddRange(await dbContext.DatabaseObjects.AsNoTracking()
                .Where(item => item.DatabaseSource.SystemId == systemId
                    && (pattern == null || EF.Functions.Like(item.ObjectName, pattern) || EF.Functions.Like(item.SchemaName + "." + item.ObjectName, pattern) || (item.BusinessDescription != null && EF.Functions.Like(item.BusinessDescription, pattern))))
                .Select(item => new TargetPreviewResponse(
                    new TargetReferenceResponse("DatabaseObject", item.Id),
                    new[] { new SystemContextResponse(item.DatabaseSource.System.Id, item.DatabaseSource.System.Name) },
                    item.SchemaName + "." + item.ObjectName, "数据库对象", item.BusinessDescription, item.KnowledgeStatus.ToString()))
                .ToArrayAsync(cancellationToken));
        }
        if (allowedTypes.Contains(KnowledgeTargetType.DatabaseColumn))
        {
            results.AddRange(await dbContext.DatabaseColumns.AsNoTracking()
                .Where(item => item.DatabaseObject.DatabaseSource.SystemId == systemId
                    && (pattern == null || EF.Functions.Like(item.ColumnName, pattern) || EF.Functions.Like(item.DatabaseObject.ObjectName + "." + item.ColumnName, pattern) || (item.BusinessDescription != null && EF.Functions.Like(item.BusinessDescription, pattern))))
                .Select(item => new TargetPreviewResponse(
                    new TargetReferenceResponse("DatabaseColumn", item.Id),
                    new[] { new SystemContextResponse(item.DatabaseObject.DatabaseSource.System.Id, item.DatabaseObject.DatabaseSource.System.Name) },
                    item.DatabaseObject.SchemaName + "." + item.DatabaseObject.ObjectName + "." + item.ColumnName,
                    "字段", item.BusinessDescription, item.KnowledgeStatus.ToString()))
                .ToArrayAsync(cancellationToken));
        }
        if (allowedTypes.Contains(KnowledgeTargetType.BusinessRule))
        {
            results.AddRange(await dbContext.BusinessRules.AsNoTracking()
                .Where(item => item.SystemId == systemId
                    && (pattern == null || EF.Functions.Like(item.Name, pattern) || EF.Functions.Like(item.Description, pattern)))
                .Select(item => new TargetPreviewResponse(
                    new TargetReferenceResponse("BusinessRule", item.Id),
                    new[] { new SystemContextResponse(item.System.Id, item.System.Name) },
                    item.Name, "业务规则", item.Description, item.KnowledgeStatus.ToString()))
                .ToArrayAsync(cancellationToken));
        }

        return results.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
