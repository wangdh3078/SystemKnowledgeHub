using Microsoft.EntityFrameworkCore;
using System.Data;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.Search.Application.Models;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Features.Search.Application;

public sealed class SearchQueries(KnowledgeHubDbContext dbContext)
{
    private const int DefaultLimitPerGroup = 5;
    private const int MaximumLimitPerGroup = 20;

    public async Task<SearchKnowledgeQueryResult> SearchKnowledge(
        SearchKnowledgeQuery request,
        CancellationToken cancellationToken)
    {
        var query = request.QueryText?.Trim();
        var errors = Validate(request, query, out var objectTypes, out var limitPerGroup);
        if (errors.Count > 0)
        {
            return new SearchKnowledgeQueryResult(null, errors);
        }

        var pattern = $"%{query}%";
        var groups = new List<SearchResultGroup>();
        var total = 0;

        if (objectTypes.Contains("System"))
        {
            var rows = await dbContext.Systems
                .AsNoTracking()
                .Where(system =>
                    EF.Functions.Like(system.Name, pattern)
                    || EF.Functions.Like(system.DisplayName, pattern)
                    || (system.Purpose != null && EF.Functions.Like(system.Purpose, pattern))
                    || system.TechnologyTags.Any(tag => EF.Functions.Like(tag.Technology, pattern)))
                .Select(system => new SearchResultItem(
                    system.Id,
                    system.Name,
                    system.Name,
                    system.Purpose ?? system.DisplayName,
                    system.KnowledgeStatus.ToString(),
                    null,
                    new SearchNavigation("System", system.Id, null, null), null, null, null))
                .ToArrayAsync(cancellationToken);
            total += rows.Length;
            AddGroup(groups, "System", "系统", rows, query!, limitPerGroup);
        }

        if (objectTypes.Contains("BusinessFunction"))
        {
            var rows = await dbContext.BusinessFunctions
                .AsNoTracking()
                .Where(function =>
                    EF.Functions.Like(function.Name, pattern)
                    || (function.DisplayName != null && EF.Functions.Like(function.DisplayName, pattern))
                    || (function.Purpose != null && EF.Functions.Like(function.Purpose, pattern))
                    || (function.InputDescription != null && EF.Functions.Like(function.InputDescription, pattern))
                    || (function.OutputDescription != null && EF.Functions.Like(function.OutputDescription, pattern)))
                .Select(function => new SearchResultItem(
                    function.Id,
                    function.System.Name,
                    function.Name,
                    function.Purpose ?? function.DisplayName ?? function.FunctionType,
                    function.KnowledgeStatus.ToString(),
                    null,
                    new SearchNavigation("BusinessFunction", function.Id, null, null), null, null, null))
                .ToArrayAsync(cancellationToken);
            total += rows.Length;
            AddGroup(groups, "BusinessFunction", "业务功能", rows, query!, limitPerGroup);
        }

        if (objectTypes.Contains("DatabaseObject"))
        {
            var rows = await dbContext.DatabaseObjects
                .AsNoTracking()
                .Where(item =>
                    EF.Functions.Like(item.SchemaName, pattern)
                    || EF.Functions.Like(item.ObjectName, pattern)
                    || (item.BusinessDescription != null && EF.Functions.Like(item.BusinessDescription, pattern))
                    || EF.Functions.Like(item.DatabaseSource.Name, pattern))
                .Select(item => new SearchResultItem(
                    item.Id,
                    item.DatabaseSource.System.Name,
                    item.SchemaName + "." + item.ObjectName,
                    item.BusinessDescription ?? item.ObjectType.ToString(),
                    item.KnowledgeStatus.ToString(),
                    null,
                    new SearchNavigation("DatabaseObject", item.Id, null, null), null, null, null))
                .ToArrayAsync(cancellationToken);
            total += rows.Length;
            AddGroup(groups, "DatabaseObject", "数据库对象", rows, query!, limitPerGroup);
        }

        if (objectTypes.Contains("DatabaseColumn"))
        {
            var rows = await dbContext.DatabaseColumns
                .AsNoTracking()
                .Where(column =>
                    EF.Functions.Like(column.ColumnName, pattern)
                    || (column.BusinessDescription != null && EF.Functions.Like(column.BusinessDescription, pattern))
                    || (column.DatabaseComment != null && EF.Functions.Like(column.DatabaseComment, pattern))
                    || EF.Functions.Like(column.DatabaseObject.SchemaName, pattern)
                    || EF.Functions.Like(column.DatabaseObject.ObjectName, pattern)
                    || column.KnownValues.Any(value =>
                        EF.Functions.Like(value.ValueText, pattern)
                        || EF.Functions.Like(value.Meaning, pattern)))
                .Select(column => new SearchResultItem(
                    column.Id,
                    column.DatabaseObject.DatabaseSource.System.Name,
                    column.DatabaseObject.SchemaName + "." + column.DatabaseObject.ObjectName + "." + column.ColumnName,
                    column.BusinessDescription ?? column.DatabaseComment ?? column.DataType,
                    column.KnowledgeStatus.ToString(),
                    null,
                    new SearchNavigation("DatabaseObject", column.DatabaseObjectId, "DatabaseColumn", column.Id), null, null, null))
                .ToArrayAsync(cancellationToken);
            total += rows.Length;
            AddGroup(groups, "DatabaseColumn", "字段", rows, query!, limitPerGroup);
        }

        if (objectTypes.Contains("BusinessRule"))
        {
            var rows = await dbContext.BusinessRules
                .AsNoTracking()
                .Where(rule =>
                    EF.Functions.Like(rule.Name, pattern)
                    || EF.Functions.Like(rule.Description, pattern)
                    || (rule.ConditionText != null && EF.Functions.Like(rule.ConditionText, pattern))
                    || (rule.ResultText != null && EF.Functions.Like(rule.ResultText, pattern))
                    || (rule.InputDataJson != null && EF.Functions.Like(rule.InputDataJson, pattern)))
                .Select(rule => new SearchResultItem(
                    rule.Id,
                    rule.System.Name,
                    rule.Name,
                    rule.Description,
                    rule.KnowledgeStatus.ToString(),
                    null,
                    new SearchNavigation("BusinessRule", rule.Id, null, null), null, null, null))
                .ToArrayAsync(cancellationToken);
            total += rows.Length;
            AddGroup(groups, "BusinessRule", "业务规则", rows, query!, limitPerGroup);
        }

        if (objectTypes.Contains("Integration"))
        {
            var rows = await dbContext.Integrations
                .AsNoTracking()
                .Where(integration =>
                    EF.Functions.Like(integration.Name, pattern)
                    || EF.Functions.Like(integration.SourcePartyName, pattern)
                    || EF.Functions.Like(integration.TargetPartyName, pattern)
                    || (integration.Purpose != null && EF.Functions.Like(integration.Purpose, pattern))
                    || (integration.TopicOrQueue != null && EF.Functions.Like(integration.TopicOrQueue, pattern))
                    || (integration.EndpointDisplay != null && EF.Functions.Like(integration.EndpointDisplay, pattern))
                    || (integration.EndpointJson != null && EF.Functions.Like(integration.EndpointJson, pattern)))
                .Select(integration => new IntegrationSearchRow(
                    integration.Id,
                    integration.Name,
                    integration.SourcePartyName,
                    integration.TargetPartyName,
                    integration.SourceSystemId,
                    integration.TargetSystemId,
                    integration.SourceSystem == null ? null : integration.SourceSystem.Name,
                    integration.TargetSystem == null ? null : integration.TargetSystem.Name,
                    integration.Purpose,
                    integration.EndpointDisplay,
                    integration.TopicOrQueue,
                    integration.KnowledgeStatus.ToString()))
                .ToArrayAsync(cancellationToken);
            var items = rows.Select(row => new SearchResultItem(
                row.Id,
                BuildIntegrationSystemContext(row),
                row.Name,
                row.Purpose ?? row.EndpointDisplay ?? row.TopicOrQueue ?? $"{row.SourcePartyName} → {row.TargetPartyName}",
                row.KnowledgeStatus,
                null,
                new SearchNavigation("Integration", row.Id, null, null), null, null, null)).ToArray();
            total += items.Length;
            AddGroup(groups, "Integration", "集成关系", items, query!, limitPerGroup);
        }

        if (objectTypes.Contains("UnknownItem"))
        {
            var rows = await dbContext.UnknownItems
                .AsNoTracking()
                .Where(item =>
                    EF.Functions.Like(item.Question, pattern)
                    || (item.Context != null && EF.Functions.Like(item.Context, pattern))
                    || item.Targets.Any(target => EF.Functions.Like(target.DisplaySnapshot, pattern)))
                .Select(item => new SearchResultItem(
                    item.Id,
                    item.System.Name,
                    item.Question,
                    item.Context ?? item.Targets.Where(target => target.IsPrimary).Select(target => target.DisplaySnapshot).FirstOrDefault() ?? "待补充调查上下文",
                    null,
                    item.Status.ToString(),
                    new SearchNavigation("UnknownItem", item.Id, null, null), null, null, null))
                .ToArrayAsync(cancellationToken);
            total += rows.Length;
            AddGroup(groups, "UnknownItem", "待确认事项", rows, query!, limitPerGroup);
        }

        if (objectTypes.Contains("KnowledgeDocument"))
        {
            var rows = await SearchKnowledgeDocuments(query!, limitPerGroup, cancellationToken);
            total += await CountKnowledgeDocuments(query!, cancellationToken);
            AddGroup(groups, "KnowledgeDocument", "知识内容", rows, query!, limitPerGroup);
        }

        return new SearchKnowledgeQueryResult(
            new SearchKnowledgeResponse(query!, groups, total),
            null);
    }

    private static Dictionary<string, string[]> Validate(
        SearchKnowledgeQuery request,
        string? query,
        out HashSet<string> objectTypes,
        out int limitPerGroup)
    {
        var errors = new Dictionary<string, string[]>();
        objectTypes = new HashSet<string>(StringComparer.Ordinal);
        limitPerGroup = request.LimitPerGroup ?? DefaultLimitPerGroup;

        if (string.IsNullOrWhiteSpace(query) || query.Length is < 1 or > 100)
        {
            errors["q"] = ["搜索关键词长度必须在 1 到 100 个字符之间。"];
        }

        if (request.LimitPerGroup is < 1 or > MaximumLimitPerGroup)
        {
            errors["limitPerGroup"] = ["每个分组的结果数量必须在 1 到 20 之间。"];
        }

        var rawTypes = request.Types?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (rawTypes is null || rawTypes.Length == 0)
        {
            objectTypes.UnionWith(SupportedObjectTypes);
        }
        else if (rawTypes.Any(type => !SupportedObjectTypes.Contains(type, StringComparer.Ordinal)))
        {
            errors["types"] = ["搜索对象类型无效。"];
        }
        else
        {
            objectTypes.UnionWith(rawTypes);
        }

        return errors;
    }

    private static void AddGroup(
        ICollection<SearchResultGroup> groups,
        string objectType,
        string label,
        IEnumerable<SearchResultItem> rows,
        string query,
        int limitPerGroup)
    {
        var items = objectType == "KnowledgeDocument"
            ? rows.Take(limitPerGroup).ToArray()
            : rows
                .OrderBy(item => SearchRank(item, query))
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .Take(limitPerGroup)
                .ToArray();
        if (items.Length > 0)
        {
            groups.Add(new SearchResultGroup(objectType, label, items));
        }
    }

    private static int SearchRank(SearchResultItem item, string query)
    {
        if (item.Title.Equals(query, StringComparison.OrdinalIgnoreCase)) return 0;
        if (item.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 1;
        if (item.SystemContext.Equals(query, StringComparison.OrdinalIgnoreCase)) return 2;
        return 3;
    }

    private static string BuildIntegrationSystemContext(IntegrationSearchRow row)
    {
        if (row.SourceSystemName is not null && row.TargetSystemName is not null)
        {
            return $"{row.SourceSystemName} → {row.TargetSystemName}";
        }

        return row.SourceSystemName ?? row.TargetSystemName ?? $"{row.SourcePartyName} → {row.TargetPartyName}";
    }

    private static readonly string[] SupportedObjectTypes =
    [
        "System",
        "BusinessFunction",
        "DatabaseObject",
        "DatabaseColumn",
        "BusinessRule",
        "Integration",
        "UnknownItem",
        "KnowledgeDocument",
    ];

    private async Task<IReadOnlyList<SearchResultItem>> SearchKnowledgeDocuments(
        string query,
        int limitPerGroup,
        CancellationToken cancellationToken)
    {
        var ftsQuery = KnowledgeDocumentSearchText.BuildQuery(query);
        if (string.IsNullOrWhiteSpace(ftsQuery)) return [];

        var connection = dbContext.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT d.id, d.document_type, d.title, d.summary, d.body_markdown, d.lifecycle_status, d.knowledge_status, d.updated_at
                FROM knowledge_documents_fts
                INNER JOIN knowledge_documents AS d ON d.id = knowledge_documents_fts.rowid
                WHERE knowledge_documents_fts MATCH $query
                  AND d.lifecycle_status <> 'Archived'
                ORDER BY bm25(knowledge_documents_fts, 10.0, 4.0, 1.0), d.updated_at DESC
                LIMIT $limit;
                """;
            AddParameter(command, "$query", ftsQuery);
            AddParameter(command, "$limit", limitPerGroup);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var items = new List<SearchResultItem>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetInt64(0);
                var documentType = reader.GetString(1);
                var title = reader.GetString(2);
                var summary = reader.IsDBNull(3) ? null : reader.GetString(3);
                var bodyMarkdown = reader.GetString(4);
                var lifecycleStatus = reader.GetString(5);
                var knowledgeStatus = reader.GetString(6);
                var updatedAt = reader.GetFieldValue<DateTimeOffset>(7);
                items.Add(new SearchResultItem(
                    id,
                    "知识内容",
                    title,
                    KnowledgeDocumentSearchText.CreateSnippet(title, summary, bodyMarkdown, query),
                    knowledgeStatus,
                    null,
                    new SearchNavigation("KnowledgeDocument", id, null, null),
                    documentType,
                    lifecycleStatus,
                    updatedAt));
            }
            return items;
        }
        finally
        {
            if (closeConnection) await connection.CloseAsync();
        }
    }

    private async Task<int> CountKnowledgeDocuments(string query, CancellationToken cancellationToken)
    {
        var ftsQuery = KnowledgeDocumentSearchText.BuildQuery(query);
        if (string.IsNullOrWhiteSpace(ftsQuery)) return 0;

        var connection = dbContext.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT count(*)
                FROM knowledge_documents_fts
                INNER JOIN knowledge_documents AS d ON d.id = knowledge_documents_fts.rowid
                WHERE knowledge_documents_fts MATCH $query
                  AND d.lifecycle_status <> 'Archived';
                """;
            AddParameter(command, "$query", ftsQuery);
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }
        finally
        {
            if (closeConnection) await connection.CloseAsync();
        }
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record IntegrationSearchRow(
        long Id,
        string Name,
        string SourcePartyName,
        string TargetPartyName,
        long? SourceSystemId,
        long? TargetSystemId,
        string? SourceSystemName,
        string? TargetSystemName,
        string? Purpose,
        string? EndpointDisplay,
        string? TopicOrQueue,
        string KnowledgeStatus);
}
