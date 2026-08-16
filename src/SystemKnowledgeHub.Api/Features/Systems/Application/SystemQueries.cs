using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SystemKnowledgeHub.Api.Features.Systems.Application.Models;
using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.Systems.Application;

public sealed class SystemQueries(
    KnowledgeHubDbContext dbContext,
    ConcurrencyTokenCodec concurrencyTokenCodec)
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SystemsListQueryResult> GetSystemsList(
        SystemsListQuery request,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request, out var lifecycle, out var knowledgeStatus, out var sort);
        if (errors.Count > 0)
        {
            return new SystemsListQueryResult(null, errors);
        }

        var page = request.Page ?? 1;
        var pageSize = request.PageSize ?? DefaultPageSize;
        var search = NormalizeOptional(request.Search);
        var technology = NormalizeOptional(request.Technology);

        var query = dbContext.Systems.AsNoTracking();

        if (search is not null)
        {
            var pattern = $"%{search}%";
            query = query.Where(system =>
                EF.Functions.Like(system.Name, pattern)
                || EF.Functions.Like(system.DisplayName, pattern)
                || (system.Purpose != null && EF.Functions.Like(system.Purpose, pattern)));
        }

        if (lifecycle.HasValue)
        {
            query = query.Where(system => system.Lifecycle == lifecycle.Value);
        }

        if (knowledgeStatus.HasValue)
        {
            query = query.Where(system => system.KnowledgeStatus == knowledgeStatus.Value);
        }

        if (technology is not null)
        {
            query = query.Where(system =>
                system.TechnologyTags.Any(tag => tag.Technology == technology));
        }

        var rows = await query
            .Select(system => new SystemListRow(
                system.Id,
                system.Name,
                system.DisplayName,
                system.SystemType,
                system.Purpose,
                system.TechnologyTags
                    .OrderBy(tag => tag.Technology)
                    .Select(tag => tag.Technology)
                    .ToArray(),
                dbContext.BusinessFunctions.Count(function => function.SystemId == system.Id),
                dbContext.DatabaseObjects.Count(databaseObject =>
                    databaseObject.DatabaseSource.SystemId == system.Id),
                system.Lifecycle,
                system.KnowledgeStatus,
                system.UpdatedAt))
            .ToListAsync(cancellationToken);

        var ordered = ApplySort(rows, sort);
        var total = rows.Count;
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new SystemSummaryResponse(
                row.Id,
                row.Name,
                row.DisplayName,
                row.SystemType,
                row.Purpose,
                row.Technologies,
                row.FunctionCount,
                row.DatabaseObjectCount,
                OpenUnknownCount: 0,
                row.Lifecycle.ToString(),
                row.KnowledgeStatus.ToString(),
                row.UpdatedAt))
            .ToArray();

        return new SystemsListQueryResult(
            new SystemsListResponse(items, page, pageSize, total),
            null);
    }

    public async Task<SystemDetailResponse?> GetSystemDetail(
        long systemId,
        CancellationToken cancellationToken)
    {
        var system = await dbContext.Systems
            .AsNoTracking()
            .Where(item => item.Id == systemId)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.DisplayName,
                item.SystemType,
                item.Lifecycle,
                item.Purpose,
                item.MainUsersJson,
                item.RepositoryName,
                item.RepositoryUrl,
                item.DeploymentJson,
                item.Notes,
                item.KnowledgeStatus,
                item.Version,
                Technologies = item.TechnologyTags
                    .OrderBy(tag => tag.Technology)
                    .Select(tag => tag.Technology)
                    .ToArray(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (system is null)
        {
            return null;
        }

        var businessFunctions = await dbContext.BusinessFunctions
            .AsNoTracking()
            .Where(function => function.SystemId == systemId)
            .OrderBy(function => function.Name)
            .Select(function => new SystemBusinessFunctionSummaryResponse(
                function.Id,
                function.Name,
                function.Purpose,
                function.KnowledgeStatus.ToString(),
                0))
            .ToArrayAsync(cancellationToken);

        var databaseObjects = await dbContext.DatabaseObjects
            .AsNoTracking()
            .Where(databaseObject => databaseObject.DatabaseSource.SystemId == systemId)
            .OrderBy(databaseObject => databaseObject.SchemaName)
            .ThenBy(databaseObject => databaseObject.ObjectName)
            .Select(databaseObject => new SystemDatabaseObjectSummaryResponse(
                databaseObject.Id,
                databaseObject.SchemaName + "." + databaseObject.ObjectName,
                databaseObject.ObjectType.ToString(),
                databaseObject.KnowledgeStatus.ToString(),
                0))
            .ToArrayAsync(cancellationToken);

        var integrations = await dbContext.Integrations
            .AsNoTracking()
            .Include(item => item.SourceSystem)
            .Include(item => item.TargetSystem)
            .Where(item => item.SourceSystemId == systemId || item.TargetSystemId == systemId)
            .OrderBy(item => item.Name)
            .ToArrayAsync(cancellationToken);
        var integrationRows = integrations.Select(item => new SystemIntegrationSummaryResponse(
            item.Id, item.Name, item.IntegrationType.ToString(),
            item.SourceSystemId == systemId ? item.TargetPartyName : item.SourcePartyName,
            item.KnowledgeStatus.ToString())).ToArray();
        var relatedSystems = integrations.SelectMany(item => new[]
            {
                item.SourceSystemId == systemId ? item.TargetSystem : item.SourceSystem,
            }).Where(item => item is not null).Select(item => new RelatedSystemSummaryResponse(item!.Id, item.Name))
            .DistinctBy(item => item.Id).ToArray();

        var databaseObjectStatuses = await dbContext.DatabaseObjects
            .AsNoTracking()
            .Where(databaseObject => databaseObject.DatabaseSource.SystemId == systemId)
            .Select(databaseObject => databaseObject.KnowledgeStatus)
            .ToArrayAsync(cancellationToken);
        var businessFunctionStatuses = await dbContext.BusinessFunctions
            .AsNoTracking()
            .Where(function => function.SystemId == systemId)
            .Select(function => function.KnowledgeStatus)
            .ToArrayAsync(cancellationToken);
        var databaseColumnStatuses = await dbContext.DatabaseColumns
            .AsNoTracking()
            .Where(column => column.DatabaseObject.DatabaseSource.SystemId == systemId)
            .Select(column => column.KnowledgeStatus)
            .ToArrayAsync(cancellationToken);
        var missingColumnDescriptionCount = await dbContext.DatabaseColumns
            .AsNoTracking()
            .CountAsync(
                column => column.DatabaseObject.DatabaseSource.SystemId == systemId
                    && (column.BusinessDescription == null || column.BusinessDescription == string.Empty),
                cancellationToken);
        var mainDatabase = await dbContext.DatabaseSources
            .AsNoTracking()
            .Where(source => source.SystemId == systemId)
            .OrderByDescending(source => source.IsPrimary)
            .ThenBy(source => source.Name)
            .Select(source => new MainDatabaseSummaryResponse(source.Id, source.Name))
            .FirstOrDefaultAsync(cancellationToken);

        var statuses = databaseObjectStatuses
            .Concat(databaseColumnStatuses)
            .Concat(businessFunctionStatuses)
            .Concat(integrations.Select(item => item.KnowledgeStatus))
            .Append(system.KnowledgeStatus)
            .ToArray();
        var knowledgeGaps = missingColumnDescriptionCount == 0
            ? Array.Empty<string>()
            : [$"{missingColumnDescriptionCount} 个字段缺少业务说明"];

        return new SystemDetailResponse(
            system.Id,
            concurrencyTokenCodec.Encode(system.Version),
            new SystemOverviewResponse(
                system.Name,
                system.DisplayName,
                system.SystemType,
                system.Lifecycle.ToString(),
                system.Purpose,
                DeserializeStringArray(system.MainUsersJson),
                system.Technologies,
                new SystemRepositoryResponse(system.RepositoryName, system.RepositoryUrl),
                DeserializeDeployment(system.DeploymentJson),
                system.Notes,
                system.KnowledgeStatus.ToString()),
            new SystemKnowledgeSummaryResponse(
                statuses.Count(status => status == KnowledgeStatus.Confirmed),
                statuses.Count(status => status == KnowledgeStatus.Inferred),
                statuses.Count(status => status == KnowledgeStatus.Unknown),
                0),
            businessFunctions,
            databaseObjects,
            integrationRows,
            Array.Empty<SystemUnknownItemSummaryResponse>(),
            new SystemContextRailResponse(
                relatedSystems,
                integrations.Length,
                mainDatabase,
                0,
                knowledgeGaps),
            ["UpdateSystemOverview", "UpdateSystemTechnology", "UpdateSystemLifecycle"]);
    }

    private static Dictionary<string, string[]> Validate(
        SystemsListQuery request,
        out SystemLifecycle? lifecycle,
        out KnowledgeStatus? knowledgeStatus,
        out SystemSort sort)
    {
        var errors = new Dictionary<string, string[]>();
        lifecycle = null;
        knowledgeStatus = null;
        sort = SystemSort.UpdatedAtDescending;

        if (request.Page is < 1)
        {
            errors["page"] = ["页码必须从 1 开始。"]; 
        }

        if (request.PageSize is < 1 or > MaximumPageSize)
        {
            errors["pageSize"] = ["每页数量必须在 1 到 100 之间。"]; 
        }

        if (request.Lifecycle is not null)
        {
            if (!Enum.TryParse<SystemLifecycle>(request.Lifecycle, false, out var parsedLifecycle)
                || parsedLifecycle.ToString() != request.Lifecycle)
            {
                errors["lifecycle"] = ["生命周期筛选值无效。"]; 
            }
            else
            {
                lifecycle = parsedLifecycle;
            }
        }

        if (request.KnowledgeStatus is not null)
        {
            if (!Enum.TryParse<KnowledgeStatus>(request.KnowledgeStatus, false, out var parsedStatus)
                || parsedStatus.ToString() != request.KnowledgeStatus)
            {
                errors["knowledgeStatus"] = ["知识状态筛选值无效。"]; 
            }
            else
            {
                knowledgeStatus = parsedStatus;
            }
        }

        if (request.Sort is not null && !TryParseSort(request.Sort, out sort))
        {
            errors["sort"] = ["排序值无效。"]; 
        }

        return errors;
    }

    private static bool TryParseSort(string value, out SystemSort sort)
    {
        sort = value switch
        {
            "name:asc" => SystemSort.NameAscending,
            "name:desc" => SystemSort.NameDescending,
            "updatedAt:asc" => SystemSort.UpdatedAtAscending,
            "updatedAt:desc" => SystemSort.UpdatedAtDescending,
            "knowledgeStatus:asc" => SystemSort.KnowledgeStatusAscending,
            "knowledgeStatus:desc" => SystemSort.KnowledgeStatusDescending,
            _ => SystemSort.Invalid,
        };
        return sort != SystemSort.Invalid;
    }

    private static IEnumerable<SystemListRow> ApplySort(
        IReadOnlyList<SystemListRow> rows,
        SystemSort sort)
    {
        return sort switch
        {
            SystemSort.NameAscending => rows.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
            SystemSort.NameDescending => rows.OrderByDescending(row => row.Name, StringComparer.OrdinalIgnoreCase),
            SystemSort.UpdatedAtAscending => rows.OrderBy(row => row.UpdatedAt).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
            SystemSort.KnowledgeStatusAscending => rows.OrderBy(row => row.KnowledgeStatus).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
            SystemSort.KnowledgeStatusDescending => rows.OrderByDescending(row => row.KnowledgeStatus).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
            _ => rows.OrderByDescending(row => row.UpdatedAt).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static string[] DeserializeStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];
    }

    private static SystemDeploymentResponse[] DeserializeDeployment(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<SystemDeploymentResponse[]>(json, JsonOptions) ?? [];
    }

    private sealed record SystemListRow(
        long Id,
        string Name,
        string DisplayName,
        string SystemType,
        string? Purpose,
        string[] Technologies,
        int FunctionCount,
        int DatabaseObjectCount,
        SystemLifecycle Lifecycle,
        KnowledgeStatus KnowledgeStatus,
        DateTimeOffset UpdatedAt);

    private enum SystemSort
    {
        Invalid,
        NameAscending,
        NameDescending,
        UpdatedAtAscending,
        UpdatedAtDescending,
        KnowledgeStatusAscending,
        KnowledgeStatusDescending,
    }
}
