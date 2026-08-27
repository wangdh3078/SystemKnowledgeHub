namespace SystemKnowledgeHub.Api.Features.BusinessFunctions.Api.Contracts;

public sealed record DeleteBusinessFunctionRequest(string? ConcurrencyToken);

public sealed record BusinessFunctionActorRequest(string DisplayName, string? Role);

public sealed record CreateBusinessFunctionRequest(
    long SystemId,
    string Name,
    string? DisplayName,
    string FunctionType,
    string? Purpose,
    string RewriteStatus,
    BusinessFunctionActorRequest Actor);

public sealed record UpdateBusinessFunctionOverviewRequest(
    string Name,
    string? DisplayName,
    string FunctionType,
    string? Purpose,
    string? Caller,
    string? Input,
    string? Output,
    string RewriteStatus,
    BusinessFunctionActorRequest Actor,
    string ConcurrencyToken);

public sealed record BusinessProcessStepRequest(int Order, string Name, string? Description);

public sealed record ReplaceBusinessProcessStepsRequest(
    IReadOnlyList<BusinessProcessStepRequest> Steps,
    BusinessFunctionActorRequest Actor,
    string ConcurrencyToken);
