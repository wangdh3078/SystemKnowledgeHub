using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Relationships.Application.Models;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.Integrations.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
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
                    new[] { new SystemContextResponse(item.Id, item.Name) }, null))
                .SingleOrDefaultAsync(cancellationToken),
            KnowledgeTargetType.DatabaseSource => await dbContext.DatabaseSources.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new RelationshipEndpointContext(
                    item.System.Name + " · " + item.Name, "数据库来源", item.Description, "Unknown",
                    new[] { new SystemContextResponse(item.System.Id, item.System.Name) }, null))
                .SingleOrDefaultAsync(cancellationToken),
            KnowledgeTargetType.BusinessFunction => await dbContext.BusinessFunctions.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new RelationshipEndpointContext(
                    item.Name, "业务功能", item.Purpose, item.KnowledgeStatus.ToString(),
                    new[] { new SystemContextResponse(item.System.Id, item.System.Name) }, null))
                .SingleOrDefaultAsync(cancellationToken),
            KnowledgeTargetType.DatabaseObject => await dbContext.DatabaseObjects.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new RelationshipEndpointContext(
                    item.SchemaName + "." + item.ObjectName, "数据库对象", item.BusinessDescription, item.KnowledgeStatus.ToString(),
                    new[] { new SystemContextResponse(item.DatabaseSource.System.Id, item.DatabaseSource.System.Name) }, null))
                .SingleOrDefaultAsync(cancellationToken),
            KnowledgeTargetType.DatabaseColumn => await dbContext.DatabaseColumns.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new RelationshipEndpointContext(
                    item.DatabaseObject.SchemaName + "." + item.DatabaseObject.ObjectName + "." + item.ColumnName,
                    "字段", item.BusinessDescription, item.KnowledgeStatus.ToString(),
                    new[] { new SystemContextResponse(item.DatabaseObject.DatabaseSource.System.Id, item.DatabaseObject.DatabaseSource.System.Name) }, null))
                .SingleOrDefaultAsync(cancellationToken),
            KnowledgeTargetType.BusinessRule => await dbContext.BusinessRules.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new RelationshipEndpointContext(
                    item.Name, "业务规则", item.Description, item.KnowledgeStatus.ToString(),
                    new[] { new SystemContextResponse(item.System.Id, item.System.Name) }, null))
                .SingleOrDefaultAsync(cancellationToken),
            KnowledgeTargetType.Integration => await ResolveIntegration(id, cancellationToken),
            KnowledgeTargetType.KnowledgeDocument => await dbContext.KnowledgeDocuments.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new RelationshipEndpointContext(
                    item.Title, "知识文档", item.Summary, item.KnowledgeStatus.ToString(), Array.Empty<SystemContextResponse>(), item.DocumentType))
                .SingleOrDefaultAsync(cancellationToken),
            _ => null,
        };
    }

    public async Task<IReadOnlyList<TargetPreviewResponse>> Search(
        IReadOnlyList<KnowledgeTargetType> allowedTypes,
        long? systemId,
        string? query,
        IReadOnlyList<DocumentType>? allowedDocumentTypes,
        CancellationToken cancellationToken)
    {
        var normalized = query?.Trim();
        var pattern = string.IsNullOrWhiteSpace(normalized) ? null : $"%{normalized}%";
        var results = new List<TargetPreviewResponse>();

        if (allowedTypes.Contains(KnowledgeTargetType.System))
        {
            results.AddRange(await dbContext.Systems.AsNoTracking()
                .Where(item => (!systemId.HasValue || item.Id == systemId.Value) && (pattern == null || EF.Functions.Like(item.Name, pattern) || EF.Functions.Like(item.DisplayName, pattern)))
                .Select(item => new TargetPreviewResponse(
                    new TargetReferenceResponse("System", item.Id),
                    new[] { new SystemContextResponse(item.Id, item.Name) }, item.Name, "系统", item.Purpose, item.KnowledgeStatus.ToString()))
                .ToArrayAsync(cancellationToken));
        }
        if (allowedTypes.Contains(KnowledgeTargetType.DatabaseSource))
        {
            results.AddRange(await dbContext.DatabaseSources.AsNoTracking()
                .Where(item => (!systemId.HasValue || item.SystemId == systemId.Value) && (pattern == null || EF.Functions.Like(item.Name, pattern)))
                .Select(item => new TargetPreviewResponse(
                    new TargetReferenceResponse("DatabaseSource", item.Id),
                    new[] { new SystemContextResponse(item.System.Id, item.System.Name) },
                    item.System.Name + " · " + item.Name, "数据库来源", item.Description, "Unknown"))
                .ToArrayAsync(cancellationToken));
        }
        if (allowedTypes.Contains(KnowledgeTargetType.BusinessFunction))
        {
            results.AddRange(await dbContext.BusinessFunctions.AsNoTracking()
                .Where(item => (!systemId.HasValue || item.SystemId == systemId.Value) && (pattern == null || EF.Functions.Like(item.Name, pattern) || (item.Purpose != null && EF.Functions.Like(item.Purpose, pattern))))
                .Select(item => new TargetPreviewResponse(
                    new TargetReferenceResponse("BusinessFunction", item.Id),
                    new[] { new SystemContextResponse(item.System.Id, item.System.Name) }, item.Name, "业务功能", item.Purpose, item.KnowledgeStatus.ToString()))
                .ToArrayAsync(cancellationToken));
        }
        if (allowedTypes.Contains(KnowledgeTargetType.DatabaseObject))
        {
            results.AddRange(await dbContext.DatabaseObjects.AsNoTracking()
                .Where(item => (!systemId.HasValue || item.DatabaseSource.SystemId == systemId.Value)
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
                .Where(item => (!systemId.HasValue || item.DatabaseObject.DatabaseSource.SystemId == systemId.Value)
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
                .Where(item => (!systemId.HasValue || item.SystemId == systemId.Value)
                    && (pattern == null || EF.Functions.Like(item.Name, pattern) || EF.Functions.Like(item.Description, pattern)))
                .Select(item => new TargetPreviewResponse(
                    new TargetReferenceResponse("BusinessRule", item.Id),
                    new[] { new SystemContextResponse(item.System.Id, item.System.Name) },
                    item.Name, "业务规则", item.Description, item.KnowledgeStatus.ToString()))
                .ToArrayAsync(cancellationToken));
        }
        if (allowedTypes.Contains(KnowledgeTargetType.Integration))
        {
            var integrations = await dbContext.Integrations.AsNoTracking()
                .Include(item => item.SourceSystem).Include(item => item.TargetSystem)
                .Where(item => !systemId.HasValue || item.SourceSystemId == systemId.Value || item.TargetSystemId == systemId.Value)
                .Where(item => pattern == null || EF.Functions.Like(item.Name, pattern) || (item.Purpose != null && EF.Functions.Like(item.Purpose, pattern)))
                .ToArrayAsync(cancellationToken);
            results.AddRange(integrations.Select(item => new TargetPreviewResponse(
                new TargetReferenceResponse("Integration", item.Id), IntegrationSystems(item), item.Name, "集成关系", item.Purpose, item.KnowledgeStatus.ToString())));
        }
        if (allowedTypes.Contains(KnowledgeTargetType.KnowledgeDocument))
        {
            var documents = dbContext.KnowledgeDocuments.AsNoTracking()
                .Where(item => item.LifecycleStatus != DocumentLifecycleStatus.Archived);
            if (allowedDocumentTypes is not null)
            {
                documents = documents.Where(item => allowedDocumentTypes.Contains(item.DocumentType));
            }
            results.AddRange(await documents
                .Where(item => pattern == null || EF.Functions.Like(item.Title, pattern)
                    || (item.Summary != null && EF.Functions.Like(item.Summary, pattern)))
                .Select(item => new TargetPreviewResponse(
                    new TargetReferenceResponse("KnowledgeDocument", item.Id), Array.Empty<SystemContextResponse>(),
                    item.Title, "知识文档 · " + item.DocumentType, item.Summary, item.KnowledgeStatus.ToString()))
                .ToArrayAsync(cancellationToken));
        }

        return results.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private async Task<RelationshipEndpointContext?> ResolveIntegration(long id, CancellationToken cancellationToken)
    {
        var integration = await dbContext.Integrations.AsNoTracking().Include(item => item.SourceSystem).Include(item => item.TargetSystem)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return integration is null ? null : new RelationshipEndpointContext(integration.Name, "集成关系", integration.Purpose,
            integration.KnowledgeStatus.ToString(), IntegrationSystems(integration), null);
    }

    private static SystemContextResponse[] IntegrationSystems(Integration integration) =>
        new[] { integration.SourceSystem, integration.TargetSystem }.Where(item => item is not null)
            .Select(item => new SystemContextResponse(item!.Id, item.Name)).DistinctBy(item => item.Id).ToArray();
}
