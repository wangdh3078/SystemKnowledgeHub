using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Application.Models;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.BusinessFunctions.Application;

public sealed class BusinessFunctionService(
    KnowledgeHubDbContext dbContext,
    ConcurrencyTokenCodec concurrencyTokenCodec)
{
    public async Task<CreateBusinessFunctionResult> CreateBusinessFunction(
        CreateBusinessFunctionCommand request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var functionType = request.FunctionType.Trim();
        var actorName = request.Actor.DisplayName.Trim();
        var errors = ValidateCommon(name, functionType, request.RewriteStatus, actorName, out var rewriteStatus);

        if (errors.Count > 0)
        {
            return new CreateBusinessFunctionResult(null, errors, CreateBusinessFunctionFailure.Validation);
        }

        var system = await dbContext.Systems
            .SingleOrDefaultAsync(item => item.Id == request.SystemId, cancellationToken);
        if (system is null)
        {
            return new CreateBusinessFunctionResult(null, null, CreateBusinessFunctionFailure.SystemNotFound);
        }

        if (await dbContext.BusinessFunctions.AnyAsync(
                item => item.SystemId == request.SystemId && item.Name == name,
                cancellationToken))
        {
            return new CreateBusinessFunctionResult(null, null, CreateBusinessFunctionFailure.DuplicateName);
        }

        var timestamp = DateTimeOffset.UtcNow;
        var actorRole = NormalizeOptional(request.Actor.Role);
        var function = new BusinessFunction
        {
            System = system,
            Name = name,
            DisplayName = NormalizeOptional(request.DisplayName),
            FunctionType = functionType,
            Purpose = NormalizeOptional(request.Purpose),
            RewriteStatus = rewriteStatus,
            CreatedAt = timestamp,
            CreatedByName = actorName,
            CreatedByRole = actorRole,
            UpdatedAt = timestamp,
            KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = timestamp,
            KnowledgeStatusChangedByName = actorName,
            KnowledgeStatusChangedByRole = actorRole ?? "创建人",
            Version = 1,
        };

        dbContext.BusinessFunctions.Add(function);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return new CreateBusinessFunctionResult(null, null, CreateBusinessFunctionFailure.DuplicateName);
        }

        return new CreateBusinessFunctionResult(
            new CreateBusinessFunctionResponse(
                function.Id,
                new KnowledgeSystemReferenceResponse(system.Id, system.Name),
                function.Name,
                function.RewriteStatus.ToString(),
                function.KnowledgeStatus.ToString(),
                concurrencyTokenCodec.Encode(function.Version)),
            null,
            CreateBusinessFunctionFailure.None);
    }

    public async Task<UpdateBusinessFunctionOverviewResult> UpdateBusinessFunctionOverview(
        UpdateBusinessFunctionOverviewCommand request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var functionType = request.FunctionType.Trim();
        var actorName = request.Actor.DisplayName.Trim();
        var errors = ValidateCommon(name, functionType, request.RewriteStatus, actorName, out var rewriteStatus);

        if (!concurrencyTokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion))
        {
            errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"]; 
        }

        if (errors.Count > 0)
        {
            return new UpdateBusinessFunctionOverviewResult(null, errors, UpdateBusinessFunctionFailure.Validation);
        }

        var function = await dbContext.BusinessFunctions
            .SingleOrDefaultAsync(item => item.Id == request.BusinessFunctionId, cancellationToken);
        if (function is null)
        {
            return new UpdateBusinessFunctionOverviewResult(null, null, UpdateBusinessFunctionFailure.NotFound);
        }

        if (function.Version != expectedVersion)
        {
            return new UpdateBusinessFunctionOverviewResult(null, null, UpdateBusinessFunctionFailure.Conflict);
        }

        if (await dbContext.BusinessFunctions.AnyAsync(
                item => item.SystemId == function.SystemId
                    && item.Id != function.Id
                    && item.Name == name,
                cancellationToken))
        {
            return new UpdateBusinessFunctionOverviewResult(null, null, UpdateBusinessFunctionFailure.DuplicateName);
        }

        function.Name = name;
        if (request.DisplayName is not null)
        {
            function.DisplayName = NormalizeOptional(request.DisplayName);
        }
        function.FunctionType = functionType;
        function.Purpose = NormalizeOptional(request.Purpose);
        function.CallerSummary = NormalizeOptional(request.Caller);
        function.InputDescription = NormalizeOptional(request.Input);
        function.OutputDescription = NormalizeOptional(request.Output);
        function.RewriteStatus = rewriteStatus;
        function.UpdatedAt = DateTimeOffset.UtcNow;
        function.Version = expectedVersion + 1;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new UpdateBusinessFunctionOverviewResult(null, null, UpdateBusinessFunctionFailure.Conflict);
        }
        catch (DbUpdateException)
        {
            return new UpdateBusinessFunctionOverviewResult(null, null, UpdateBusinessFunctionFailure.DuplicateName);
        }

        return new UpdateBusinessFunctionOverviewResult(
            new UpdateBusinessFunctionOverviewResponse(
                new UpdatedBusinessFunctionOverviewResponse(
                    function.Name,
                    function.DisplayName,
                    function.FunctionType,
                    function.Purpose,
                    function.CallerSummary,
                    function.InputDescription,
                    function.OutputDescription,
                    function.RewriteStatus.ToString()),
                concurrencyTokenCodec.Encode(function.Version)),
            null,
            UpdateBusinessFunctionFailure.None);
    }

    public async Task<ReplaceBusinessProcessStepsResult> ReplaceBusinessProcessSteps(
        ReplaceBusinessProcessStepsCommand request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateSteps(request);
        if (!concurrencyTokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion))
        {
            errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"]; 
        }

        if (errors.Count > 0)
        {
            return new ReplaceBusinessProcessStepsResult(null, errors, UpdateBusinessFunctionFailure.Validation);
        }

        var function = await dbContext.BusinessFunctions
            .Include(item => item.ProcessSteps)
            .SingleOrDefaultAsync(item => item.Id == request.BusinessFunctionId, cancellationToken);
        if (function is null)
        {
            return new ReplaceBusinessProcessStepsResult(null, null, UpdateBusinessFunctionFailure.NotFound);
        }

        if (function.Version != expectedVersion)
        {
            return new ReplaceBusinessProcessStepsResult(null, null, UpdateBusinessFunctionFailure.Conflict);
        }

        dbContext.BusinessProcessSteps.RemoveRange(function.ProcessSteps);
        function.ProcessSteps.Clear();
        foreach (var step in request.Steps!)
        {
            function.ProcessSteps.Add(new BusinessProcessStep
            {
                StepOrder = step.Order,
                Name = step.Name.Trim(),
                Description = NormalizeOptional(step.Description),
            });
        }

        function.UpdatedAt = DateTimeOffset.UtcNow;
        function.Version = expectedVersion + 1;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ReplaceBusinessProcessStepsResult(null, null, UpdateBusinessFunctionFailure.Conflict);
        }

        var responseSteps = function.ProcessSteps
            .OrderBy(step => step.StepOrder)
            .Select(step => new BusinessProcessStepResponse(step.StepOrder, step.Name, step.Description))
            .ToArray();

        return new ReplaceBusinessProcessStepsResult(
            new ReplaceBusinessProcessStepsResponse(
                responseSteps,
                concurrencyTokenCodec.Encode(function.Version)),
            null,
            UpdateBusinessFunctionFailure.None);
    }

    private static Dictionary<string, string[]> ValidateCommon(
        string name,
        string functionType,
        string rewriteStatusValue,
        string actorName,
        out RewriteStatus rewriteStatus)
    {
        var errors = new Dictionary<string, string[]>();
        rewriteStatus = default;

        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["业务功能名称不能为空。"]; 
        }

        if (string.IsNullOrWhiteSpace(functionType))
        {
            errors["functionType"] = ["功能类型不能为空。"]; 
        }

        if (!Enum.TryParse<RewriteStatus>(rewriteStatusValue, false, out var parsed)
            || parsed.ToString() != rewriteStatusValue)
        {
            errors["rewriteStatus"] = ["改写状态值无效。"]; 
        }
        else
        {
            rewriteStatus = parsed;
        }

        if (string.IsNullOrWhiteSpace(actorName))
        {
            errors["actor.displayName"] = ["操作人姓名不能为空。"]; 
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateSteps(ReplaceBusinessProcessStepsCommand request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Steps is null)
        {
            errors["steps"] = ["业务流程步骤必须作为完整集合提交。"]; 
        }
        else
        {
            var orders = request.Steps.Select(step => step.Order).ToArray();
            var expectedOrders = Enumerable.Range(1, request.Steps.Count).ToArray();
            if (!orders.SequenceEqual(expectedOrders))
            {
                errors["steps"] = ["步骤顺序必须从 1 开始且连续。"]; 
            }
            else if (request.Steps.Any(step => string.IsNullOrWhiteSpace(step.Name)))
            {
                errors["steps"] = ["步骤名称不能为空。"]; 
            }
        }

        if (string.IsNullOrWhiteSpace(request.Actor.DisplayName))
        {
            errors["actor.displayName"] = ["操作人姓名不能为空。"]; 
        }

        return errors;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
