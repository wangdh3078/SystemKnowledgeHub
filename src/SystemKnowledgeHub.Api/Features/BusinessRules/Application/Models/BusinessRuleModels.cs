namespace SystemKnowledgeHub.Api.Features.BusinessRules.Application.Models;

using SystemKnowledgeHub.Api.Features.Users.Application.Models;

public sealed record BusinessRuleInputData(string Name, string? Description);
public sealed record BusinessRuleActor(string DisplayName, string? Role);
public sealed record CreateBusinessRuleCommand(long SystemId, string Name, string Description, string? Condition,
    string? Result, IReadOnlyList<BusinessRuleInputData>? InputData, BusinessRuleActor Actor, CanonicalCreator Creator);
public sealed record UpdateBusinessRuleCommand(long BusinessRuleId, string Name, string Description, string? Condition,
    string? Result, IReadOnlyList<BusinessRuleInputData>? InputData, BusinessRuleActor Actor, string ConcurrencyToken);

public sealed record BusinessRuleSystemResponse(long Id, string Name);
public sealed record BusinessRuleWriteResponse(long Id, BusinessRuleSystemResponse System, string Name, string Description,
    string? Condition, string? Result, IReadOnlyList<BusinessRuleInputData> InputData, string KnowledgeStatus,
    string ConcurrencyToken);
public sealed record BusinessRuleHeaderResponse(string Name, string KnowledgeStatus);
public sealed record BusinessRuleRelationshipResponse(long RelationshipId, long Id, string Name, string RelationType);
public sealed record BusinessRuleEvidenceResponse(long Id, string EvidenceType, string SourceTitle);
public sealed record BusinessRuleUnknownItemResponse(long Id, string Question, string Status);
public sealed record BusinessRuleContextRailResponse(int RelationshipCount, int OpenUnknownCount);
public sealed record BusinessRuleDetailResponse(long Id, BusinessRuleSystemResponse System, string ConcurrencyToken,
    BusinessRuleHeaderResponse Header, string Description, string? Condition, string? Result,
    IReadOnlyList<BusinessRuleInputData> InputData,
    IReadOnlyList<BusinessRuleRelationshipResponse> RelatedFunctions,
    IReadOnlyList<BusinessRuleRelationshipResponse> RelatedFields,
    IReadOnlyList<BusinessRuleRelationshipResponse> Integrations,
    IReadOnlyList<BusinessRuleEvidenceResponse> Evidence,
    IReadOnlyList<BusinessRuleUnknownItemResponse> UnknownItems,
    BusinessRuleContextRailResponse ContextRail,
    bool CanDelete,
    IReadOnlyList<string> AvailableActions);

public enum BusinessRuleFailure { None, Validation, SystemNotFound, NotFound, DuplicateName, Conflict }
public sealed record BusinessRuleCommandResult(BusinessRuleWriteResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors, BusinessRuleFailure Failure);
