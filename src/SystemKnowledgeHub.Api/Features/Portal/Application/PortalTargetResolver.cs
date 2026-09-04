using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Portal.Application.Models;
using SystemKnowledgeHub.Api.Features.Portal.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.Portal.Application;

public sealed class PortalTargetResolver(KnowledgeHubDbContext dbContext)
{
    public async Task<IReadOnlyDictionary<PortalTargetKey, PortalTargetIdentity>> ResolveAdminIdentitiesAsync(
        IEnumerable<PortalTargetKey> targetKeys,
        CancellationToken cancellationToken)
    {
        var keys = targetKeys.Where(key => ApiIdParser.IsSafePositive(key.Id)).Distinct().ToArray();
        var resolved = new Dictionary<PortalTargetKey, PortalTargetIdentity>();

        var systemIds = Ids(keys, PortalTargetType.System);
        var systems = await dbContext.Systems.AsNoTracking()
            .Where(item => systemIds.Contains(item.Id))
            .Select(item => new { item.Id, item.Name, item.DisplayName })
            .ToListAsync(cancellationToken);
        foreach (var item in systems)
            Add(resolved, PortalTargetType.System, item.Id, Display(item.DisplayName, item.Name));

        var businessFunctionIds = Ids(keys, PortalTargetType.BusinessFunction);
        var functions = await dbContext.BusinessFunctions.AsNoTracking()
            .Where(item => businessFunctionIds.Contains(item.Id)
                && dbContext.Systems.Any(system => system.Id == item.SystemId))
            .Select(item => new { item.Id, item.Name, item.DisplayName })
            .ToListAsync(cancellationToken);
        foreach (var item in functions)
            Add(resolved, PortalTargetType.BusinessFunction, item.Id, Display(item.DisplayName, item.Name));

        var databaseObjectIds = Ids(keys, PortalTargetType.DatabaseObject);
        var objects = await dbContext.DatabaseObjects.AsNoTracking()
            .Where(item => databaseObjectIds.Contains(item.Id)
                && dbContext.DatabaseSources.Any(source => source.Id == item.DatabaseSourceId
                    && dbContext.Systems.Any(system => system.Id == source.SystemId)))
            .Select(item => new { item.Id, item.SchemaName, item.ObjectName })
            .ToListAsync(cancellationToken);
        foreach (var item in objects)
            Add(resolved, PortalTargetType.DatabaseObject, item.Id, $"{item.SchemaName}.{item.ObjectName}");

        var knowledgeDocumentIds = Ids(keys, PortalTargetType.KnowledgeDocument);
        var documents = await dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(item => knowledgeDocumentIds.Contains(item.Id))
            .Select(item => new { item.Id, item.Title, item.DocumentType, item.LifecycleStatus })
            .ToListAsync(cancellationToken);
        foreach (var item in documents)
            resolved[new(PortalTargetType.KnowledgeDocument, item.Id)] = new(
                PortalTargetType.KnowledgeDocument,
                item.Id,
                item.Title,
                item.DocumentType.ToString(),
                item.LifecycleStatus.ToString());

        var integrationIds = Ids(keys, PortalTargetType.Integration);
        var integrations = await dbContext.Integrations.AsNoTracking()
            .Where(item => integrationIds.Contains(item.Id)
                && (item.SourceSystemId == null || dbContext.Systems.Any(system => system.Id == item.SourceSystemId))
                && (item.TargetSystemId == null || dbContext.Systems.Any(system => system.Id == item.TargetSystemId))
                && (item.DatabaseSourceId == null || dbContext.DatabaseSources.Any(source => source.Id == item.DatabaseSourceId
                    && dbContext.Systems.Any(system => system.Id == source.SystemId)))
                && (item.DatabaseObjectId == null || dbContext.DatabaseObjects.Any(databaseObject => databaseObject.Id == item.DatabaseObjectId
                    && dbContext.DatabaseSources.Any(source => source.Id == databaseObject.DatabaseSourceId
                        && dbContext.Systems.Any(system => system.Id == source.SystemId)))))
            .Select(item => new { item.Id, item.Name })
            .ToListAsync(cancellationToken);
        foreach (var item in integrations)
            Add(resolved, PortalTargetType.Integration, item.Id, item.Name);

        return resolved;
    }

    public async Task<IReadOnlyDictionary<PortalTargetKey, PortalTargetIdentity>> ResolveEligibleIdentitiesAsync(
        IEnumerable<PortalTargetKey> targetKeys,
        CancellationToken cancellationToken)
    {
        var keys = targetKeys.Where(key => ApiIdParser.IsSafePositive(key.Id)).Distinct().ToArray();
        var resolved = new Dictionary<PortalTargetKey, PortalTargetIdentity>();

        var systemIds = Ids(keys, PortalTargetType.System);
        var systems = await dbContext.Systems.AsNoTracking()
            .Where(item => systemIds.Contains(item.Id))
            .Select(item => new { item.Id, item.Name, item.DisplayName })
            .ToListAsync(cancellationToken);
        foreach (var item in systems)
            Add(resolved, PortalTargetType.System, item.Id, Display(item.DisplayName, item.Name));

        var businessFunctionIds = Ids(keys, PortalTargetType.BusinessFunction);
        var functions = await dbContext.BusinessFunctions.AsNoTracking()
            .Where(item => businessFunctionIds.Contains(item.Id)
                && dbContext.Systems.Any(system => system.Id == item.SystemId))
            .Select(item => new { item.Id, item.Name, item.DisplayName })
            .ToListAsync(cancellationToken);
        foreach (var item in functions)
            Add(resolved, PortalTargetType.BusinessFunction, item.Id, Display(item.DisplayName, item.Name));

        var databaseObjectIds = Ids(keys, PortalTargetType.DatabaseObject);
        var objects = await dbContext.DatabaseObjects.AsNoTracking()
            .Where(item => databaseObjectIds.Contains(item.Id)
                && dbContext.DatabaseSources.Any(source => source.Id == item.DatabaseSourceId
                    && dbContext.Systems.Any(system => system.Id == source.SystemId)))
            .Select(item => new { item.Id, item.SchemaName, item.ObjectName })
            .ToListAsync(cancellationToken);
        foreach (var item in objects)
            Add(resolved, PortalTargetType.DatabaseObject, item.Id, $"{item.SchemaName}.{item.ObjectName}");

        var knowledgeDocumentIds = Ids(keys, PortalTargetType.KnowledgeDocument);
        var documents = await dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(item => knowledgeDocumentIds.Contains(item.Id)
                && item.LifecycleStatus == DocumentLifecycleStatus.Published)
            .Select(item => new { item.Id, item.Title, item.DocumentType, item.LifecycleStatus })
            .ToListAsync(cancellationToken);
        foreach (var item in documents)
            resolved[new(PortalTargetType.KnowledgeDocument, item.Id)] = new(
                PortalTargetType.KnowledgeDocument,
                item.Id,
                item.Title,
                item.DocumentType.ToString(),
                item.LifecycleStatus.ToString());

        var integrationIds = Ids(keys, PortalTargetType.Integration);
        var integrations = await dbContext.Integrations.AsNoTracking()
            .Where(item => integrationIds.Contains(item.Id)
                && (item.SourceSystemId == null || dbContext.Systems.Any(system => system.Id == item.SourceSystemId))
                && (item.TargetSystemId == null || dbContext.Systems.Any(system => system.Id == item.TargetSystemId))
                && (item.DatabaseSourceId == null || dbContext.DatabaseSources.Any(source => source.Id == item.DatabaseSourceId
                    && dbContext.Systems.Any(system => system.Id == source.SystemId)))
                && (item.DatabaseObjectId == null || dbContext.DatabaseObjects.Any(databaseObject => databaseObject.Id == item.DatabaseObjectId
                    && dbContext.DatabaseSources.Any(source => source.Id == databaseObject.DatabaseSourceId
                        && dbContext.Systems.Any(system => system.Id == source.SystemId)))))
            .Select(item => new { item.Id, item.Name })
            .ToListAsync(cancellationToken);
        foreach (var item in integrations)
            Add(resolved, PortalTargetType.Integration, item.Id, item.Name);

        return resolved;
    }

    public async Task<IReadOnlyDictionary<PortalTargetKey, PortalResolvedTarget>> ResolveEligibleTargetsAsync(
        IEnumerable<PortalTargetKey> targetKeys,
        IEnumerable<long> databaseStructureObjectIds,
        CancellationToken cancellationToken)
    {
        var keys = targetKeys.Where(key => ApiIdParser.IsSafePositive(key.Id)).Distinct().ToArray();
        var resolved = new Dictionary<PortalTargetKey, PortalResolvedTarget>();

        var systemIds = Ids(keys, PortalTargetType.System);
        var systems = await dbContext.Systems.AsNoTracking()
            .Where(item => systemIds.Contains(item.Id))
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.DisplayName,
                item.SystemType,
                item.Lifecycle,
                item.Purpose,
            })
            .ToListAsync(cancellationToken);
        foreach (var item in systems)
            resolved[new(PortalTargetType.System, item.Id)] = new PortalResolvedSystem(
                item.Id,
                Display(item.DisplayName, item.Name),
                item.Purpose,
                item.Name,
                item.DisplayName,
                item.SystemType,
                item.Lifecycle.ToString());

        var businessFunctionIds = Ids(keys, PortalTargetType.BusinessFunction);
        var functions = await dbContext.BusinessFunctions.AsNoTracking()
            .Where(item => businessFunctionIds.Contains(item.Id)
                && dbContext.Systems.Any(system => system.Id == item.SystemId))
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.DisplayName,
                item.FunctionType,
                item.Purpose,
                item.CallerSummary,
                item.InputDescription,
                item.OutputDescription,
                SystemName = item.System.DisplayName,
                SystemFallbackName = item.System.Name,
            })
            .ToListAsync(cancellationToken);
        foreach (var item in functions)
            resolved[new(PortalTargetType.BusinessFunction, item.Id)] = new PortalResolvedBusinessFunction(
                item.Id,
                Display(item.DisplayName, item.Name),
                item.Purpose,
                item.Name,
                item.DisplayName,
                item.FunctionType,
                Display(item.SystemName, item.SystemFallbackName),
                item.CallerSummary,
                item.InputDescription,
                item.OutputDescription);

        var requestedStructureIds = databaseStructureObjectIds.Distinct().ToArray();
        var databaseObjectIds = Ids(keys, PortalTargetType.DatabaseObject);
        var columnCounts = await dbContext.DatabaseColumns.AsNoTracking()
            .Where(item => requestedStructureIds.Contains(item.DatabaseObjectId))
            .GroupBy(item => item.DatabaseObjectId)
            .Select(group => new { DatabaseObjectId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.DatabaseObjectId, item => item.Count, cancellationToken);
        var boundedStructureIds = requestedStructureIds
            .Where(id => !columnCounts.TryGetValue(id, out var count)
                || count <= PortalLimits.MaximumDatabaseColumnsPerObject)
            .ToArray();
        var columns = await dbContext.DatabaseColumns.AsNoTracking()
            .Where(item => boundedStructureIds.Contains(item.DatabaseObjectId))
            .OrderBy(item => item.DatabaseObjectId)
            .ThenBy(item => item.OrdinalPosition)
            .ThenBy(item => item.Id)
            .Select(item => new
            {
                item.DatabaseObjectId,
                Column = new PortalResolvedDatabaseColumn(
                    item.OrdinalPosition,
                    item.ColumnName,
                    item.DataType,
                    item.IsNullable,
                    item.DatabaseComment),
            })
            .ToListAsync(cancellationToken);
        var columnsByObject = columns.ToLookup(item => item.DatabaseObjectId, item => item.Column);
        var objects = await dbContext.DatabaseObjects.AsNoTracking()
            .Where(item => databaseObjectIds.Contains(item.Id)
                && dbContext.DatabaseSources.Any(source => source.Id == item.DatabaseSourceId
                    && dbContext.Systems.Any(system => system.Id == source.SystemId)))
            .Select(item => new
            {
                item.Id,
                item.SchemaName,
                item.ObjectName,
                item.ObjectType,
                item.BusinessDescription,
                item.DatabaseComment,
                item.EstimatedRows,
                item.AccessMode,
                item.BusinessKeyColumnsJson,
            })
            .ToListAsync(cancellationToken);
        foreach (var item in objects)
        {
            if (requestedStructureIds.Contains(item.Id) && !boundedStructureIds.Contains(item.Id)) continue;
            resolved[new(PortalTargetType.DatabaseObject, item.Id)] = new PortalResolvedDatabaseObject(
                item.Id,
                $"{item.SchemaName}.{item.ObjectName}",
                item.BusinessDescription,
                item.DatabaseComment,
                item.SchemaName,
                item.ObjectName,
                item.ObjectType.ToString(),
                item.EstimatedRows,
                item.AccessMode.ToString(),
                ParseStringArray(item.BusinessKeyColumnsJson),
                columnsByObject[item.Id].ToArray());
        }

        var documentIds = Ids(keys, PortalTargetType.KnowledgeDocument);
        var documents = await dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(item => documentIds.Contains(item.Id)
                && item.LifecycleStatus == DocumentLifecycleStatus.Published)
            .Select(item => new
            {
                item.Id,
                item.Title,
                item.Summary,
                item.DocumentType,
                item.BodyMarkdown,
            })
            .ToListAsync(cancellationToken);
        foreach (var item in documents)
            resolved[new(PortalTargetType.KnowledgeDocument, item.Id)] = new PortalResolvedKnowledgeDocument(
                item.Id,
                item.Title,
                item.Summary,
                item.DocumentType.ToString(),
                item.BodyMarkdown);

        var integrationIds = Ids(keys, PortalTargetType.Integration);
        var integrations = await dbContext.Integrations.AsNoTracking()
            .Where(item => integrationIds.Contains(item.Id)
                && (item.SourceSystemId == null || dbContext.Systems.Any(system => system.Id == item.SourceSystemId))
                && (item.TargetSystemId == null || dbContext.Systems.Any(system => system.Id == item.TargetSystemId))
                && (item.DatabaseSourceId == null || dbContext.DatabaseSources.Any(source => source.Id == item.DatabaseSourceId
                    && dbContext.Systems.Any(system => system.Id == source.SystemId)))
                && (item.DatabaseObjectId == null || dbContext.DatabaseObjects.Any(databaseObject => databaseObject.Id == item.DatabaseObjectId
                    && dbContext.DatabaseSources.Any(source => source.Id == databaseObject.DatabaseSourceId
                        && dbContext.Systems.Any(system => system.Id == source.SystemId)))))
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.IntegrationType,
                item.SourcePartyName,
                item.TargetPartyName,
                item.FlowDirection,
                item.Purpose,
            })
            .ToListAsync(cancellationToken);
        foreach (var item in integrations)
            resolved[new(PortalTargetType.Integration, item.Id)] = new PortalResolvedIntegration(
                item.Id,
                item.Name,
                item.Purpose,
                item.IntegrationType.ToString(),
                item.SourcePartyName,
                item.TargetPartyName,
                item.FlowDirection.ToString());

        return resolved;
    }

    private static long[] Ids(IEnumerable<PortalTargetKey> keys, PortalTargetType type) =>
        keys.Where(key => key.Type == type).Select(key => key.Id).Distinct().ToArray();

    private static void Add(
        IDictionary<PortalTargetKey, PortalTargetIdentity> targets,
        PortalTargetType type,
        long id,
        string title) => targets[new(type, id)] = new(type, id, title);

    private static string Display(string? preferred, string fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;

    private static IReadOnlyList<string> ParseStringArray(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        try
        {
            return JsonSerializer.Deserialize<string[]>(value)
                ?.Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
