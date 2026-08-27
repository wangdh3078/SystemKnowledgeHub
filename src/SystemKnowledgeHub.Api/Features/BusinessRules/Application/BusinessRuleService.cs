using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.BusinessRules.Application.Models;
using SystemKnowledgeHub.Api.Features.BusinessRules.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.BusinessRules.Application;

public sealed class BusinessRuleService(KnowledgeHubDbContext dbContext, ConcurrencyTokenCodec tokenCodec)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task<BusinessRuleCommandResult> Create(CreateBusinessRuleCommand request, CancellationToken cancellationToken)
    {
        var errors = Validate(request.Name, request.Description, request.InputData, request.Actor.DisplayName);
        if (errors.Count > 0) return Failure(BusinessRuleFailure.Validation, errors);
        var system = await dbContext.Systems.SingleOrDefaultAsync(item => item.Id == request.SystemId, cancellationToken);
        if (system is null) return Failure(BusinessRuleFailure.SystemNotFound);
        var name = request.Name.Trim();
        if (await dbContext.BusinessRules.AnyAsync(item => item.SystemId == request.SystemId && item.Name == name, cancellationToken))
            return Failure(BusinessRuleFailure.DuplicateName);

        var now = DateTimeOffset.UtcNow;
        var actorRole = Normalize(request.Actor.Role);
        var rule = new BusinessRule
        {
            System = system,
            Name = name,
            Description = request.Description.Trim(),
            ConditionText = Normalize(request.Condition),
            ResultText = Normalize(request.Result),
            InputDataJson = SerializeInputData(request.InputData),
            CreatedAt = now,
            CreatedByUserId = request.Creator.UserId,
            CreatedByName = request.Creator.DisplayName,
            CreatedByRole = actorRole,
            UpdatedAt = now,
            KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = request.Creator.DisplayName,
            KnowledgeStatusChangedByRole = actorRole ?? "创建人",
            Version = 1,
        };
        dbContext.BusinessRules.Add(rule);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return Failure(BusinessRuleFailure.DuplicateName); }
        return Success(rule);
    }

    public async Task<BusinessRuleCommandResult> Update(UpdateBusinessRuleCommand request, CancellationToken cancellationToken)
    {
        var errors = Validate(request.Name, request.Description, request.InputData, request.Actor.DisplayName);
        if (!tokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion)) errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"]; 
        if (errors.Count > 0) return Failure(BusinessRuleFailure.Validation, errors);
        var rule = await dbContext.BusinessRules.Include(item => item.System)
            .SingleOrDefaultAsync(item => item.Id == request.BusinessRuleId, cancellationToken);
        if (rule is null) return Failure(BusinessRuleFailure.NotFound);
        if (rule.Version != expectedVersion) return Failure(BusinessRuleFailure.Conflict);
        var name = request.Name.Trim();
        if (await dbContext.BusinessRules.AnyAsync(item => item.SystemId == rule.SystemId && item.Id != rule.Id && item.Name == name, cancellationToken))
            return Failure(BusinessRuleFailure.DuplicateName);

        rule.Name = name;
        rule.Description = request.Description.Trim();
        rule.ConditionText = Normalize(request.Condition);
        rule.ResultText = Normalize(request.Result);
        rule.InputDataJson = SerializeInputData(request.InputData);
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        rule.Version = expectedVersion + 1;
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Failure(BusinessRuleFailure.Conflict); }
        catch (DbUpdateException) { return Failure(BusinessRuleFailure.DuplicateName); }
        return Success(rule);
    }

    private BusinessRuleCommandResult Success(BusinessRule rule) => new(new(rule.Id,
        new(rule.System.Id, rule.System.Name), rule.Name, rule.Description, rule.ConditionText, rule.ResultText,
        DeserializeInputData(rule.InputDataJson), rule.KnowledgeStatus.ToString(), tokenCodec.Encode(rule.Version)), null, BusinessRuleFailure.None);

    private static Dictionary<string, string[]> Validate(string name, string description,
        IReadOnlyList<BusinessRuleInputData>? inputData, string actorName)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name)) errors["name"] = ["规则名称不能为空。"]; 
        if (string.IsNullOrWhiteSpace(description)) errors["description"] = ["规则描述不能为空。"]; 
        if (string.IsNullOrWhiteSpace(actorName)) errors["actor.displayName"] = ["操作人姓名不能为空。"]; 
        if (inputData?.Any(item => string.IsNullOrWhiteSpace(item.Name)) == true) errors["inputData"] = ["输入数据名称不能为空。"]; 
        return errors;
    }

    public static IReadOnlyList<BusinessRuleInputData> DeserializeInputData(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<BusinessRuleInputData[]>(json, JsonOptions) ?? [];
    public static string? SerializeInputData(IReadOnlyList<BusinessRuleInputData>? values) => values is null
        ? null
        : JsonSerializer.Serialize(values.Select(item => new BusinessRuleInputData(item.Name.Trim(), Normalize(item.Description))), JsonOptions);
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static BusinessRuleCommandResult Failure(BusinessRuleFailure failure, IReadOnlyDictionary<string, string[]>? errors = null) => new(null, errors, failure);
}
