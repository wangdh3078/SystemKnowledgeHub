using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.Systems.Application.Models;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Features.Systems.Application;

/// <summary>为 System Detail 提供无副作用、受限条数的统一知识只读投影。</summary>
public sealed class SystemKnowledgeViewQueries(KnowledgeHubDbContext dbContext)
{
    private const int SectionLimit = 5;

    public async Task<SystemKnowledgeViewResponse?> Get(long systemId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Systems.AsNoTracking().AnyAsync(system => system.Id == systemId, cancellationToken)) return null;

        var businessFunctions = dbContext.BusinessFunctions.AsNoTracking().Where(item => item.SystemId == systemId);
        var databaseObjects = dbContext.DatabaseObjects.AsNoTracking().Where(item => item.DatabaseSource.SystemId == systemId);
        var businessRules = dbContext.BusinessRules.AsNoTracking().Where(item => item.SystemId == systemId);
        var integrations = dbContext.Integrations.AsNoTracking().Where(item => item.SourceSystemId == systemId || item.TargetSystemId == systemId);
        var evidence = dbContext.Evidence.AsNoTracking().Where(item => item.SubjectType == EvidenceSubjectType.System && item.SubjectId == systemId);
        var openUnknownItems = dbContext.UnknownItems.AsNoTracking().Where(item => item.SystemId == systemId && item.Status != UnknownItemStatus.Closed);
        var systemRelations = dbContext.KnowledgeRelations.AsNoTracking().Where(item =>
            item.SourceType == KnowledgeTargetType.System && item.SourceId == systemId
            || item.TargetType == KnowledgeTargetType.System && item.TargetId == systemId);
        var documentRelations = systemRelations.Where(item =>
            item.SourceType == KnowledgeTargetType.KnowledgeDocument || item.TargetType == KnowledgeTargetType.KnowledgeDocument);
        var documentRelationRows = await documentRelations
            .Select(item => new { item.RelationType, DocumentId = item.SourceType == KnowledgeTargetType.KnowledgeDocument ? item.SourceId : item.TargetId })
            .ToArrayAsync(cancellationToken);
        var documentIds = documentRelationRows.Select(item => item.DocumentId).Distinct().ToArray();
        var documents = dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(item => documentIds.Contains(item.Id) && item.LifecycleStatus != DocumentLifecycleStatus.Archived);

        var businessFunctionCount = await businessFunctions.CountAsync(cancellationToken);
        var databaseObjectCount = await databaseObjects.CountAsync(cancellationToken);
        var businessRuleCount = await businessRules.CountAsync(cancellationToken);
        var integrationCount = await integrations.CountAsync(cancellationToken);
        var documentCount = await documents.CountAsync(cancellationToken);
        var evidenceCount = await evidence.CountAsync(cancellationToken);
        var openUnknownItemCount = await openUnknownItems.CountAsync(cancellationToken);

        var businessFunctionItems = await businessFunctions.OrderBy(item => item.Name).Take(SectionLimit)
            .Select(item => new SystemKnowledgeItemResponse(item.Id, item.Name, item.Purpose, item.KnowledgeStatus.ToString()))
            .ToArrayAsync(cancellationToken);
        var databaseObjectItems = await databaseObjects.OrderBy(item => item.SchemaName).ThenBy(item => item.ObjectName).Take(SectionLimit)
            .Select(item => new SystemKnowledgeItemResponse(item.Id, item.SchemaName + "." + item.ObjectName, item.BusinessDescription, item.KnowledgeStatus.ToString()))
            .ToArrayAsync(cancellationToken);
        var businessRuleItems = await businessRules.OrderBy(item => item.Name).Take(SectionLimit)
            .Select(item => new SystemKnowledgeItemResponse(item.Id, item.Name, item.Description, item.KnowledgeStatus.ToString()))
            .ToArrayAsync(cancellationToken);
        var integrationItems = await integrations.OrderBy(item => item.Name).Take(SectionLimit)
            .Select(item => new SystemKnowledgeIntegrationResponse(
                item.Id, item.Name, item.IntegrationType.ToString(), item.FlowDirection.ToString(),
                item.SourceSystemId == systemId ? item.TargetPartyName : item.SourcePartyName,
                item.KnowledgeStatus.ToString()))
            .ToArrayAsync(cancellationToken);
        var documentItems = await documents.OrderByDescending(item => item.Id).Take(SectionLimit)
            .Select(item => new { item.Id, item.DocumentType, item.Title, item.LifecycleStatus, item.KnowledgeStatus, item.UpdatedAt })
            .ToArrayAsync(cancellationToken);
        var relationshipItems = await systemRelations.OrderByDescending(item => item.Id).Take(SectionLimit)
            .Select(item => new SystemKnowledgeRelationshipResponse(
                item.Id,
                item.SourceType == KnowledgeTargetType.System && item.SourceId == systemId ? "Outgoing" : "Incoming",
                item.RelationType.ToString(),
                (item.SourceType == KnowledgeTargetType.System && item.SourceId == systemId ? item.TargetType : item.SourceType).ToString(),
                item.SourceType == KnowledgeTargetType.System && item.SourceId == systemId ? item.TargetId : item.SourceId,
                item.KnowledgeStatus.ToString()))
            .ToArrayAsync(cancellationToken);
        var evidenceItems = await evidence.OrderByDescending(item => item.Id).Take(SectionLimit)
            .Select(item => new SystemKnowledgeEvidenceResponse(item.Id, item.EvidenceType.ToString(), item.SourceTitle, item.Summary, item.ProvidedAt))
            .ToArrayAsync(cancellationToken);
        var unknownItems = await openUnknownItems.OrderBy(item => item.Status).ThenByDescending(item => item.Id).Take(SectionLimit)
            .Select(item => new SystemKnowledgeUnknownItemResponse(item.Id, item.ItemCode, item.Question, item.Priority.ToString(), item.Status.ToString(), item.UpdatedAt))
            .ToArrayAsync(cancellationToken);

        return new SystemKnowledgeViewResponse(
            systemId,
            new SystemKnowledgeOverviewResponse(businessFunctionCount, databaseObjectCount, businessRuleCount, integrationCount, documentCount, evidenceCount, openUnknownItemCount),
            businessFunctionItems,
            databaseObjectItems,
            businessRuleItems,
            integrationItems,
            documentItems.Select(item => new SystemKnowledgeDocumentResponse(
                item.Id, item.DocumentType.ToString(), item.Title, item.LifecycleStatus.ToString(), item.KnowledgeStatus.ToString(), item.UpdatedAt,
                documentRelationRows.Where(relation => relation.DocumentId == item.Id).Select(relation => relation.RelationType.ToString()).Distinct().Order().ToArray())).ToArray(),
            relationshipItems,
            evidenceItems,
            unknownItems);
    }
}
