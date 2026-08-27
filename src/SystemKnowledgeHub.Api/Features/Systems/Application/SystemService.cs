using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SystemKnowledgeHub.Api.Features.Systems.Application.Models;
using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.Systems.Application;

public sealed class SystemService(
    KnowledgeHubDbContext dbContext,
    ConcurrencyTokenCodec concurrencyTokenCodec)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CreateSystemResult> CreateSystem(
        CreateSystemCommand request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var displayName = request.DisplayName.Trim();
        var systemType = request.SystemType.Trim();
        var actorName = request.Actor.DisplayName.Trim();
        var actorRole = NormalizeOptional(request.Actor.Role);
        var purpose = NormalizeOptional(request.Purpose);
        var errors = Validate(
            name,
            displayName,
            systemType,
            request.Lifecycle,
            actorName,
            out var lifecycle);

        if (errors.Count > 0)
        {
            return new CreateSystemResult(null, errors, false);
        }

        if (await dbContext.Systems.AnyAsync(system => system.Name == name, cancellationToken))
        {
            return new CreateSystemResult(null, null, true);
        }

        var timestamp = DateTimeOffset.UtcNow;
        var system = new KnowledgeSystem
        {
            Name = name,
            DisplayName = displayName,
            SystemType = systemType,
            Lifecycle = lifecycle,
            Purpose = purpose,
            CreatedAt = timestamp,
            CreatedByUserId = request.Creator.UserId,
            CreatedByName = request.Creator.DisplayName,
            CreatedByRole = actorRole,
            UpdatedAt = timestamp,
            KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = timestamp,
            KnowledgeStatusChangedByName = request.Creator.DisplayName,
            KnowledgeStatusChangedByRole = actorRole ?? "创建人",
            Version = 1,
        };

        dbContext.Systems.Add(system);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return new CreateSystemResult(null, null, true);
        }

        return new CreateSystemResult(
            new CreateSystemResponse(
                system.Id,
                system.Name,
                system.DisplayName,
                system.Lifecycle.ToString(),
                system.KnowledgeStatus.ToString(),
                concurrencyTokenCodec.Encode(system.Version)),
            null,
            false);
    }

    public async Task<UpdateSystemOverviewResult> UpdateSystemOverview(
        UpdateSystemOverviewCommand request,
        CancellationToken cancellationToken)
    {
        var displayName = request.DisplayName.Trim();
        var systemType = request.SystemType.Trim();
        var actorName = request.Actor.DisplayName.Trim();
        var errors = ValidateOverview(request, displayName, systemType, actorName);

        if (!concurrencyTokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion))
        {
            errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"]; 
        }

        if (errors.Count > 0)
        {
            return new UpdateSystemOverviewResult(
                null,
                errors,
                UpdateSystemOverviewFailure.Validation);
        }

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);

        var system = await dbContext.Systems
            .SingleOrDefaultAsync(item => item.Id == request.SystemId, cancellationToken);
        if (system is null)
        {
            return new UpdateSystemOverviewResult(
                null,
                null,
                UpdateSystemOverviewFailure.NotFound);
        }

        if (system.Version != expectedVersion)
        {
            return new UpdateSystemOverviewResult(
                null,
                null,
                UpdateSystemOverviewFailure.Conflict);
        }

        system.DisplayName = displayName;
        system.SystemType = systemType;
        system.Purpose = NormalizeOptional(request.Purpose);
        system.MainUsersJson = JsonSerializer.Serialize(
            NormalizeList(request.MainUsers!),
            JsonOptions);
        system.RepositoryName = NormalizeOptional(request.Repository!.Name);
        system.RepositoryUrl = NormalizeOptional(request.Repository.Url);
        system.DeploymentJson = JsonSerializer.Serialize(
            request.Deployment!
                .Select(item => new UpdateSystemDeployment(
                    item.Environment.Trim(),
                    item.Description.Trim()))
                .ToArray(),
            JsonOptions);
        system.MainProjectsJson = JsonSerializer.Serialize(
            NormalizeList(request.MainProjects!),
            JsonOptions);
        system.MainEntryPointsJson = JsonSerializer.Serialize(
            NormalizeList(request.MainEntryPoints!),
            JsonOptions);
        system.Notes = NormalizeOptional(request.Notes);
        system.UpdatedAt = DateTimeOffset.UtcNow;
        system.Version = expectedVersion + 1;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new UpdateSystemOverviewResult(
                null,
                null,
                UpdateSystemOverviewFailure.Conflict);
        }

        await transaction.CommitAsync(cancellationToken);

        return new UpdateSystemOverviewResult(
            new UpdateSystemOverviewResponse(
                system.Id,
                new UpdatedSystemOverviewResponse(
                    system.DisplayName,
                    system.Purpose,
                    system.Notes),
                concurrencyTokenCodec.Encode(system.Version)),
            null,
            UpdateSystemOverviewFailure.None);
    }

    public async Task<UpdateSystemTechnologyResult> UpdateSystemTechnology(
        UpdateSystemTechnologyCommand request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateTechnology(request, out var technologies, out var expectedVersion);
        if (errors.Count > 0)
        {
            return new UpdateSystemTechnologyResult(
                null,
                errors,
                UpdateSystemTechnologyFailure.Validation);
        }

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);

        var system = await dbContext.Systems
            .Include(item => item.TechnologyTags)
            .SingleOrDefaultAsync(item => item.Id == request.SystemId, cancellationToken);
        if (system is null)
        {
            return new UpdateSystemTechnologyResult(
                null,
                null,
                UpdateSystemTechnologyFailure.NotFound);
        }

        if (system.Version != expectedVersion)
        {
            return new UpdateSystemTechnologyResult(
                null,
                null,
                UpdateSystemTechnologyFailure.Conflict);
        }

        var existingTechnologies = system.TechnologyTags
            .Select(item => item.Technology)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hasChanged = !existingTechnologies.SequenceEqual(
            technologies,
            StringComparer.OrdinalIgnoreCase);

        if (hasChanged)
        {
            system.TechnologyTags.Clear();
            foreach (var technology in technologies)
            {
                system.TechnologyTags.Add(new SystemTechnologyTag
                {
                    SystemId = system.Id,
                    Technology = technology,
                });
            }

            system.UpdatedAt = DateTimeOffset.UtcNow;
            system.Version = expectedVersion + 1;

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return new UpdateSystemTechnologyResult(
                    null,
                    null,
                    UpdateSystemTechnologyFailure.Conflict);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return new UpdateSystemTechnologyResult(
            new UpdateSystemTechnologyResponse(
                system.Id,
                technologies,
                concurrencyTokenCodec.Encode(system.Version)),
            null,
            UpdateSystemTechnologyFailure.None);
    }

    public async Task<UpdateSystemLifecycleResult> UpdateSystemLifecycle(
        UpdateSystemLifecycleCommand request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateLifecycle(request, out var lifecycle, out var expectedVersion);
        if (errors.Count > 0)
        {
            return new UpdateSystemLifecycleResult(
                null,
                errors,
                UpdateSystemLifecycleFailure.Validation);
        }

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);

        var system = await dbContext.Systems
            .SingleOrDefaultAsync(item => item.Id == request.SystemId, cancellationToken);
        if (system is null)
        {
            return new UpdateSystemLifecycleResult(
                null,
                null,
                UpdateSystemLifecycleFailure.NotFound);
        }

        if (system.Version != expectedVersion)
        {
            return new UpdateSystemLifecycleResult(
                null,
                null,
                UpdateSystemLifecycleFailure.Conflict);
        }

        if (system.Lifecycle == lifecycle)
        {
            return new UpdateSystemLifecycleResult(
                null,
                null,
                UpdateSystemLifecycleFailure.NoChange);
        }

        system.Lifecycle = lifecycle;
        system.UpdatedAt = DateTimeOffset.UtcNow;
        system.Version = expectedVersion + 1;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new UpdateSystemLifecycleResult(
                null,
                null,
                UpdateSystemLifecycleFailure.Conflict);
        }

        await transaction.CommitAsync(cancellationToken);

        return new UpdateSystemLifecycleResult(
            new UpdateSystemLifecycleResponse(
                system.Id,
                system.Lifecycle.ToString(),
                system.KnowledgeStatus.ToString(),
                concurrencyTokenCodec.Encode(system.Version)),
            null,
            UpdateSystemLifecycleFailure.None);
    }

    private static Dictionary<string, string[]> Validate(
        string name,
        string displayName,
        string systemType,
        string lifecycleValue,
        string actorName,
        out SystemLifecycle lifecycle)
    {
        var errors = new Dictionary<string, string[]>();
        lifecycle = default;

        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["系统名称不能为空。"]; 
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            errors["displayName"] = ["显示名称不能为空。"]; 
        }

        if (string.IsNullOrWhiteSpace(systemType))
        {
            errors["systemType"] = ["系统类型不能为空。"]; 
        }

        if (!Enum.TryParse<SystemLifecycle>(lifecycleValue, false, out var parsedLifecycle)
            || parsedLifecycle.ToString() != lifecycleValue)
        {
            errors["lifecycle"] = ["生命周期值无效。"]; 
        }
        else
        {
            lifecycle = parsedLifecycle;
        }

        if (string.IsNullOrWhiteSpace(actorName))
        {
            errors["actor.displayName"] = ["创建人姓名不能为空。"]; 
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateOverview(
        UpdateSystemOverviewCommand request,
        string displayName,
        string systemType,
        string actorName)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(displayName))
        {
            errors["displayName"] = ["显示名称不能为空。"]; 
        }

        if (string.IsNullOrWhiteSpace(systemType))
        {
            errors["systemType"] = ["系统类型不能为空。"]; 
        }

        if (request.MainUsers is null)
        {
            errors["mainUsers"] = ["主要用户必须作为完整集合提交。"]; 
        }

        if (request.Repository is null)
        {
            errors["repository"] = ["代码仓库必须作为完整对象提交。"]; 
        }
        else
        {
            var repositoryUrl = NormalizeOptional(request.Repository.Url);
            if (repositoryUrl is not null
                && (!Uri.TryCreate(repositoryUrl, UriKind.Absolute, out var parsedUri)
                    || (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps)))
            {
                errors["repository.url"] = ["仓库地址必须是有效的 HTTP 或 HTTPS 地址。"]; 
            }
        }

        if (request.Deployment is null)
        {
            errors["deployment"] = ["部署信息必须作为完整集合提交。"]; 
        }
        else if (request.Deployment.Any(item =>
                     string.IsNullOrWhiteSpace(item.Environment)
                     || string.IsNullOrWhiteSpace(item.Description)))
        {
            errors["deployment"] = ["部署环境和说明均不能为空。"]; 
        }

        if (request.MainProjects is null)
        {
            errors["mainProjects"] = ["主要项目必须作为完整集合提交。"]; 
        }

        if (request.MainEntryPoints is null)
        {
            errors["mainEntryPoints"] = ["主要入口必须作为完整集合提交。"]; 
        }

        if (string.IsNullOrWhiteSpace(actorName))
        {
            errors["actor.displayName"] = ["编辑人姓名不能为空。"]; 
        }

        return errors;
    }

    private Dictionary<string, string[]> ValidateTechnology(
        UpdateSystemTechnologyCommand request,
        out string[] technologies,
        out long expectedVersion)
    {
        var errors = new Dictionary<string, string[]>();
        technologies = [];
        expectedVersion = 0;

        if (request.Technologies is null)
        {
            errors["technologies"] = ["技术标签必须作为完整集合提交。"];
        }
        else
        {
            var normalized = request.Technologies.Select(item => item?.Trim() ?? string.Empty).ToArray();
            if (normalized.Any(string.IsNullOrWhiteSpace))
            {
                errors["technologies"] = ["技术标签不能为空。"];
            }
            else if (normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Length)
            {
                errors["technologies"] = ["技术标签不能重复。"];
            }
            else
            {
                technologies = normalized
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }

        ValidateActorAndToken(request.Actor, request.ConcurrencyToken, errors, out expectedVersion);
        return errors;
    }

    private Dictionary<string, string[]> ValidateLifecycle(
        UpdateSystemLifecycleCommand request,
        out SystemLifecycle lifecycle,
        out long expectedVersion)
    {
        var errors = new Dictionary<string, string[]>();
        lifecycle = default;

        if (!Enum.TryParse<SystemLifecycle>(request.TargetLifecycle, false, out var parsedLifecycle)
            || parsedLifecycle.ToString() != request.TargetLifecycle)
        {
            errors["targetLifecycle"] = ["生命周期值无效。"];
        }
        else
        {
            lifecycle = parsedLifecycle;
        }

        ValidateActorAndToken(request.Actor, request.ConcurrencyToken, errors, out expectedVersion);
        return errors;
    }

    private void ValidateActorAndToken(
        ActorContext actor,
        string concurrencyToken,
        IDictionary<string, string[]> errors,
        out long expectedVersion)
    {
        expectedVersion = 0;
        if (string.IsNullOrWhiteSpace(actor.DisplayName))
        {
            errors["actor.displayName"] = ["编辑人姓名不能为空。"];
        }

        if (!concurrencyTokenCodec.TryDecode(concurrencyToken, out expectedVersion))
        {
            errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"];
        }
    }

    private static string[] NormalizeList(IReadOnlyList<string> values)
    {
        return values
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
