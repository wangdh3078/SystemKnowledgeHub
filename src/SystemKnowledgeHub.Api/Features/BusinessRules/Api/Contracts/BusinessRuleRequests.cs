namespace SystemKnowledgeHub.Api.Features.BusinessRules.Api.Contracts;

public sealed record DeleteBusinessRuleRequest(string? ConcurrencyToken);

public sealed record BusinessRuleInputDataRequest(string? Name, string? Description);
public sealed record BusinessRuleActorRequest(string? DisplayName, string? Role);
public sealed record CreateBusinessRuleRequest(long SystemId, string? Name, string? Description, string? Condition,
    string? Result, IReadOnlyList<BusinessRuleInputDataRequest>? InputData, BusinessRuleActorRequest? Actor);
public sealed record UpdateBusinessRuleRequest(string? Name, string? Description, string? Condition,
    string? Result, IReadOnlyList<BusinessRuleInputDataRequest>? InputData, BusinessRuleActorRequest? Actor,
    string? ConcurrencyToken);
