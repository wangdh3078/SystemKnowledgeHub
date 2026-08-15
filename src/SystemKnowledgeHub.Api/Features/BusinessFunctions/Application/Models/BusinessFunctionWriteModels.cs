namespace SystemKnowledgeHub.Api.Features.BusinessFunctions.Application.Models;

public sealed record BusinessFunctionActorContext(string DisplayName, string? Role);

public sealed record CreateBusinessFunctionCommand(
    long SystemId,
    string Name,
    string? DisplayName,
    string FunctionType,
    string? Purpose,
    string RewriteStatus,
    BusinessFunctionActorContext Actor);

public sealed record CreateBusinessFunctionResponse(
    long Id,
    KnowledgeSystemReferenceResponse System,
    string Name,
    string RewriteStatus,
    string KnowledgeStatus,
    string ConcurrencyToken);

public enum CreateBusinessFunctionFailure
{
    None,
    Validation,
    SystemNotFound,
    DuplicateName,
}

public sealed record CreateBusinessFunctionResult(
    CreateBusinessFunctionResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    CreateBusinessFunctionFailure Failure);

public sealed record UpdateBusinessFunctionOverviewCommand(
    long BusinessFunctionId,
    string Name,
    string? DisplayName,
    string FunctionType,
    string? Purpose,
    string? Caller,
    string? Input,
    string? Output,
    string RewriteStatus,
    BusinessFunctionActorContext Actor,
    string ConcurrencyToken);

public sealed record UpdatedBusinessFunctionOverviewResponse(
    string Name,
    string? DisplayName,
    string FunctionType,
    string? Purpose,
    string? Caller,
    string? Input,
    string? Output,
    string RewriteStatus);

public sealed record UpdateBusinessFunctionOverviewResponse(
    UpdatedBusinessFunctionOverviewResponse Overview,
    string ConcurrencyToken);

public enum UpdateBusinessFunctionFailure
{
    None,
    Validation,
    NotFound,
    DuplicateName,
    Conflict,
}

public sealed record UpdateBusinessFunctionOverviewResult(
    UpdateBusinessFunctionOverviewResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    UpdateBusinessFunctionFailure Failure);

public sealed record BusinessProcessStepCommand(int Order, string Name, string? Description);

public sealed record ReplaceBusinessProcessStepsCommand(
    long BusinessFunctionId,
    IReadOnlyList<BusinessProcessStepCommand>? Steps,
    BusinessFunctionActorContext Actor,
    string ConcurrencyToken);

public sealed record ReplaceBusinessProcessStepsResponse(
    IReadOnlyList<BusinessProcessStepResponse> Steps,
    string ConcurrencyToken);

public sealed record ReplaceBusinessProcessStepsResult(
    ReplaceBusinessProcessStepsResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    UpdateBusinessFunctionFailure Failure);
