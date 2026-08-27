using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Domain;
using SystemKnowledgeHub.Api.Features.BusinessRules.Domain;
using SystemKnowledgeHub.Api.Features.Dashboard.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Integrations.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.Dashboard.Application;

public sealed class DashboardQueries(KnowledgeHubDbContext dbContext)
{
    private const int RecentActivityLimit = 4;

    public async Task<DashboardQueryResult> GetDashboard(
        DashboardQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await ResolveScope(request.SystemId, cancellationToken);
        if (scope is null)
        {
            return new DashboardQueryResult(null, DashboardQueryFailure.SystemNotFound);
        }

        var systemStatuses = await SystemsInScope(request.SystemId)
            .Select(system => system.KnowledgeStatus)
            .ToArrayAsync(cancellationToken);
        var functionStatuses = await BusinessFunctionsInScope(request.SystemId)
            .Select(function => function.KnowledgeStatus)
            .ToArrayAsync(cancellationToken);
        var databaseObjectStatuses = await DatabaseObjectsInScope(request.SystemId)
            .Select(databaseObject => databaseObject.KnowledgeStatus)
            .ToArrayAsync(cancellationToken);
        var columnStatuses = await DatabaseColumnsInScope(request.SystemId)
            .Select(column => column.KnowledgeStatus)
            .ToArrayAsync(cancellationToken);
        var businessRuleStatuses = await BusinessRulesInScope(request.SystemId)
            .Select(rule => rule.KnowledgeStatus)
            .ToArrayAsync(cancellationToken);
        var integrationStatuses = await IntegrationsInScope(request.SystemId)
            .Select(integration => integration.KnowledgeStatus)
            .ToArrayAsync(cancellationToken);
        var unknownItems = await UnknownItemsInScope(request.SystemId)
            .Select(item => new DashboardUnknownItemStatusRow(
                item.Status,
                item.Priority))
            .ToArrayAsync(cancellationToken);

        var allStatuses = systemStatuses
            .Concat(functionStatuses)
            .Concat(databaseObjectStatuses)
            .Concat(columnStatuses)
            .Concat(businessRuleStatuses)
            .Concat(integrationStatuses)
            .ToArray();
        var openUnknownItems = unknownItems.Count(item => item.Status != UnknownItemStatus.Closed);

        var needsAttention = await GetNeedsAttention(
            request.SystemId,
            columnStatuses,
            allStatuses,
            unknownItems,
            cancellationToken);
        var recentActivity = await GetRecentActivity(request.SystemId, cancellationToken);

        return new DashboardQueryResult(
            new DashboardResponse(
                scope,
                new DashboardKnowledgeOverviewResponse(
                    systemStatuses.Length,
                    functionStatuses.Length,
                    databaseObjectStatuses.Length,
                    columnStatuses.Length,
                    integrationStatuses.Length,
                    businessRuleStatuses.Length,
                    unknownItems.Length),
                new DashboardKnowledgeProgressResponse(
                    allStatuses.Count(status => status == KnowledgeStatus.Confirmed),
                    allStatuses.Count(status => status == KnowledgeStatus.Inferred),
                    allStatuses.Count(status => status == KnowledgeStatus.Unknown),
                    openUnknownItems),
                needsAttention,
                recentActivity),
            DashboardQueryFailure.None);
    }

    private async Task<DashboardScopeResponse?> ResolveScope(
        long? systemId,
        CancellationToken cancellationToken)
    {
        if (!systemId.HasValue)
        {
            return new DashboardScopeResponse(null, null);
        }

        return await dbContext.Systems
            .AsNoTracking()
            .Where(system => system.Id == systemId.Value)
            .Select(system => new DashboardScopeResponse(system.Id, system.Name))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<DashboardNeedsAttentionResponse>> GetNeedsAttention(
        long? systemId,
        IReadOnlyCollection<KnowledgeStatus> columnStatuses,
        IReadOnlyCollection<KnowledgeStatus> allStatuses,
        IReadOnlyCollection<DashboardUnknownItemStatusRow> unknownItems,
        CancellationToken cancellationToken)
    {
        var missingBusinessDescriptionCount = await DatabaseObjectsInScope(systemId)
            .CountAsync(
                databaseObject => databaseObject.ObjectType == DatabaseObjectType.Table
                    && (databaseObject.BusinessDescription == null || databaseObject.BusinessDescription == string.Empty),
                cancellationToken);
        var functionsWithoutRelatedDataCount = await GetFunctionsWithoutRelatedDataCount(systemId, cancellationToken);
        var systemsWithUnknownKnowledgeCount = await GetSystemsWithUnknownKnowledgeCount(systemId, cancellationToken);
        var attention = new[]
        {
            new DashboardNeedsAttentionResponse(
                "HighPriorityUnknownItem",
                unknownItems.Count(item => item.Status != UnknownItemStatus.Closed
                    && item.Priority == UnknownItemPriority.High),
                "高优先级待确认事项"),
            new DashboardNeedsAttentionResponse(
                "SystemsWithUnknownKnowledge",
                systemsWithUnknownKnowledgeCount,
                "存在未知知识的系统"),
            new DashboardNeedsAttentionResponse(
                "DatabaseObjectsWithoutBusinessDescription",
                missingBusinessDescriptionCount,
                "缺少业务说明的表"),
            new DashboardNeedsAttentionResponse(
                "DatabaseColumnsStillUnknown",
                columnStatuses.Count(status => status == KnowledgeStatus.Unknown),
                "仍为未知的字段"),
            new DashboardNeedsAttentionResponse(
                "InferredKnowledgeWaitingConfirmation",
                allStatuses.Count(status => status == KnowledgeStatus.Inferred),
                "等待确认的推断知识"),
            new DashboardNeedsAttentionResponse(
                "BusinessFunctionsWithoutRelatedData",
                functionsWithoutRelatedDataCount,
                "未关联数据的业务功能"),
        };

        return attention.Where(item => item.Count > 0).ToArray();
    }

    private async Task<int> GetFunctionsWithoutRelatedDataCount(
        long? systemId,
        CancellationToken cancellationToken)
    {
        var functionIds = await BusinessFunctionsInScope(systemId)
            .Select(function => function.Id)
            .ToArrayAsync(cancellationToken);
        if (functionIds.Length == 0)
        {
            return 0;
        }

        var relatedFunctionIds = await dbContext.KnowledgeRelations
            .AsNoTracking()
            .Where(relation =>
                relation.SourceType == KnowledgeTargetType.BusinessFunction
                    && functionIds.Contains(relation.SourceId)
                    && (relation.TargetType == KnowledgeTargetType.DatabaseObject
                        && dbContext.DatabaseObjects.Any(item => item.Id == relation.TargetId)
                        || relation.TargetType == KnowledgeTargetType.DatabaseColumn
                        && dbContext.DatabaseColumns.Any(item => item.Id == relation.TargetId))
                || relation.TargetType == KnowledgeTargetType.BusinessFunction
                    && functionIds.Contains(relation.TargetId)
                    && (relation.SourceType == KnowledgeTargetType.DatabaseObject
                        && dbContext.DatabaseObjects.Any(item => item.Id == relation.SourceId)
                        || relation.SourceType == KnowledgeTargetType.DatabaseColumn
                        && dbContext.DatabaseColumns.Any(item => item.Id == relation.SourceId)))
            .Select(relation => relation.SourceType == KnowledgeTargetType.BusinessFunction
                ? relation.SourceId
                : relation.TargetId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        return functionIds.Except(relatedFunctionIds).Count();
    }

    private async Task<int> GetSystemsWithUnknownKnowledgeCount(
        long? systemId,
        CancellationToken cancellationToken)
    {
        var systemIds = await SystemsInScope(systemId)
            .Select(system => system.Id)
            .ToArrayAsync(cancellationToken);
        if (systemIds.Length == 0)
        {
            return 0;
        }

        var unknownSystemIds = new HashSet<long>();
        var systemStatusIds = await SystemsInScope(systemId)
            .Where(system => system.KnowledgeStatus == KnowledgeStatus.Unknown)
            .Select(system => system.Id)
            .ToArrayAsync(cancellationToken);
        var functionSystemIds = await BusinessFunctionsInScope(systemId)
            .Where(function => function.KnowledgeStatus == KnowledgeStatus.Unknown)
            .Select(function => function.SystemId)
            .ToArrayAsync(cancellationToken);
        var databaseObjectSystemIds = await DatabaseObjectsInScope(systemId)
            .Where(databaseObject => databaseObject.KnowledgeStatus == KnowledgeStatus.Unknown)
            .Select(databaseObject => databaseObject.DatabaseSource.SystemId)
            .ToArrayAsync(cancellationToken);
        var columnSystemIds = await DatabaseColumnsInScope(systemId)
            .Where(column => column.KnowledgeStatus == KnowledgeStatus.Unknown)
            .Select(column => column.DatabaseObject.DatabaseSource.SystemId)
            .ToArrayAsync(cancellationToken);
        var businessRuleSystemIds = await BusinessRulesInScope(systemId)
            .Where(rule => rule.KnowledgeStatus == KnowledgeStatus.Unknown)
            .Select(rule => rule.SystemId)
            .ToArrayAsync(cancellationToken);
        var integrationSystemIds = await IntegrationsInScope(systemId)
            .Where(integration => integration.KnowledgeStatus == KnowledgeStatus.Unknown)
            .Select(integration => new { integration.SourceSystemId, integration.TargetSystemId })
            .ToArrayAsync(cancellationToken);

        unknownSystemIds.UnionWith(systemStatusIds);
        unknownSystemIds.UnionWith(functionSystemIds);
        unknownSystemIds.UnionWith(databaseObjectSystemIds);
        unknownSystemIds.UnionWith(columnSystemIds);
        unknownSystemIds.UnionWith(businessRuleSystemIds);
        foreach (var integration in integrationSystemIds)
        {
            if (integration.SourceSystemId.HasValue) unknownSystemIds.Add(integration.SourceSystemId.Value);
            if (integration.TargetSystemId.HasValue) unknownSystemIds.Add(integration.TargetSystemId.Value);
        }

        return unknownSystemIds.Count(systemIds.Contains);
    }

    private async Task<IReadOnlyList<DashboardRecentActivityResponse>> GetRecentActivity(
        long? systemId,
        CancellationToken cancellationToken)
    {
        // Microsoft.EntityFrameworkCore.Sqlite cannot translate DateTimeOffset ORDER BY.
        // Each query projects only Dashboard's four display fields; ordering remains server-side
        // application logic rather than a schema or persistence workaround.
        var systems = await SystemsInScope(systemId)
            .Select(system => new DashboardRecentActivityResponse(
                "System", system.Id, system.Name, system.UpdatedAt))
            .ToArrayAsync(cancellationToken);
        var functions = await BusinessFunctionsInScope(systemId)
            .Select(function => new DashboardRecentActivityResponse(
                "BusinessFunction", function.Id, function.Name, function.UpdatedAt))
            .ToArrayAsync(cancellationToken);
        var databaseObjects = await DatabaseObjectsInScope(systemId)
            .Select(databaseObject => new DashboardRecentActivityResponse(
                "DatabaseObject", databaseObject.Id, databaseObject.SchemaName + "." + databaseObject.ObjectName, databaseObject.UpdatedAt))
            .ToArrayAsync(cancellationToken);
        var columns = await DatabaseColumnsInScope(systemId)
            .Select(column => new DashboardRecentActivityResponse(
                "DatabaseColumn", column.Id,
                column.DatabaseObject.SchemaName + "." + column.DatabaseObject.ObjectName + "." + column.ColumnName,
                column.UpdatedAt))
            .ToArrayAsync(cancellationToken);
        var rules = await BusinessRulesInScope(systemId)
            .Select(rule => new DashboardRecentActivityResponse(
                "BusinessRule", rule.Id, rule.Name, rule.UpdatedAt))
            .ToArrayAsync(cancellationToken);
        var integrations = await IntegrationsInScope(systemId)
            .Select(integration => new DashboardRecentActivityResponse(
                "Integration", integration.Id, integration.Name, integration.UpdatedAt))
            .ToArrayAsync(cancellationToken);
        var unknownItems = await UnknownItemsInScope(systemId)
            .Select(item => new DashboardRecentActivityResponse(
                "UnknownItem", item.Id, item.Question, item.UpdatedAt))
            .ToArrayAsync(cancellationToken);

        return systems
            .Concat(functions)
            .Concat(databaseObjects)
            .Concat(columns)
            .Concat(rules)
            .Concat(integrations)
            .Concat(unknownItems)
            .OrderByDescending(item => item.UpdatedAt)
            .ThenByDescending(item => item.ObjectId)
            .Take(RecentActivityLimit)
            .ToArray();
    }

    private IQueryable<KnowledgeSystem> SystemsInScope(long? systemId)
    {
        var query = dbContext.Systems.AsNoTracking();
        return systemId.HasValue ? query.Where(system => system.Id == systemId.Value) : query;
    }

    private IQueryable<BusinessFunction> BusinessFunctionsInScope(long? systemId)
    {
        var query = dbContext.BusinessFunctions.AsNoTracking();
        return systemId.HasValue ? query.Where(function => function.SystemId == systemId.Value) : query;
    }

    private IQueryable<DatabaseObject> DatabaseObjectsInScope(long? systemId)
    {
        var query = dbContext.DatabaseObjects.AsNoTracking();
        return systemId.HasValue
            ? query.Where(databaseObject => databaseObject.DatabaseSource.SystemId == systemId.Value)
            : query;
    }

    private IQueryable<DatabaseColumn> DatabaseColumnsInScope(long? systemId)
    {
        var query = dbContext.DatabaseColumns.AsNoTracking();
        return systemId.HasValue
            ? query.Where(column => column.DatabaseObject.DatabaseSource.SystemId == systemId.Value)
            : query;
    }

    private IQueryable<BusinessRule> BusinessRulesInScope(long? systemId)
    {
        var query = dbContext.BusinessRules.AsNoTracking();
        return systemId.HasValue ? query.Where(rule => rule.SystemId == systemId.Value) : query;
    }

    private IQueryable<Integration> IntegrationsInScope(long? systemId)
    {
        var query = dbContext.Integrations.AsNoTracking();
        return systemId.HasValue
            ? query.Where(integration => integration.SourceSystemId == systemId.Value || integration.TargetSystemId == systemId.Value)
            : query;
    }

    private IQueryable<UnknownItem> UnknownItemsInScope(long? systemId)
    {
        var query = dbContext.UnknownItems.AsNoTracking()
            .Where(item => dbContext.Systems.Any(system => system.Id == item.SystemId));
        return systemId.HasValue ? query.Where(item => item.SystemId == systemId.Value) : query;
    }
}
