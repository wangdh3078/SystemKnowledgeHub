namespace SystemKnowledgeHub.Api.Features.Relationships.Application.Models;

public sealed record RelationshipTargetCommand(string Type, long Id);
public sealed record RelationshipActorCommand(string DisplayName, string? Role);
public sealed record RelationshipStatusActorCommand(string DisplayName, string RoleOrIdentity, DateTimeOffset OccurredAt);

public sealed record AddRelationshipCommand(
    RelationshipTargetCommand? Source,
    string RelationType,
    RelationshipTargetCommand? Target,
    string? Description,
    RelationshipActorCommand? Actor);

public sealed record UpdateRelationshipDescriptionCommand(
    long RelationshipId,
    string? Description,
    RelationshipActorCommand? Actor,
    string ConcurrencyToken);

public sealed record ChangeRelationshipStatusCommand(
    long RelationshipId,
    string TargetStatus,
    string? Reason,
    RelationshipStatusActorCommand? Actor,
    string ConcurrencyToken);

public sealed record TargetReferenceResponse(string Type, long Id);
public sealed record SystemContextResponse(long Id, string Name);
public sealed record TargetPreviewResponse(
    TargetReferenceResponse Target,
    IReadOnlyList<SystemContextResponse> SystemContext,
    string Title,
    string ObjectTypeLabel,
    string? ShortDescription,
    string KnowledgeStatus);

public sealed record KnowledgeTargetsResponse(
    IReadOnlyList<TargetPreviewResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record SearchKnowledgeTargetsQuery(
    string? Purpose,
    string? Query,
    long? SystemId,
    string? SourceType,
    long? SourceId,
    string? RelationType,
    int? Page,
    int? PageSize);

public sealed record RelationshipEndpointResponse(
    TargetReferenceResponse Target,
    string Title,
    string SystemContext);

public sealed record RelationshipEvidenceResponse(long Id, string EvidenceType, string SourceTitle);
public sealed record RelationshipPersonContextResponse(string DisplayName, string? RoleOrIdentity, DateTimeOffset OccurredAt);

public sealed record RelationshipDetailResponse(
    long Id,
    string ConcurrencyToken,
    RelationshipEndpointResponse Source,
    RelationshipEndpointResponse Target,
    string RelationType,
    string? Description,
    string KnowledgeStatus,
    IReadOnlyList<RelationshipEvidenceResponse> Evidence,
    IReadOnlyList<object> UnknownItems,
    RelationshipPersonContextResponse Created,
    RelationshipPersonContextResponse StatusChanged,
    IReadOnlyList<string> AvailableActions);

public sealed record AddRelationshipResponse(
    long Id,
    TargetReferenceResponse Source,
    string RelationType,
    TargetReferenceResponse Target,
    string KnowledgeStatus,
    string ConcurrencyToken);

public sealed record UpdateRelationshipDescriptionResponse(
    long Id,
    string? Description,
    string KnowledgeStatus,
    string ConcurrencyToken);

public sealed record ChangeRelationshipStatusResponse(
    long RelationshipId,
    string PreviousStatus,
    string KnowledgeStatus,
    string? Reason,
    DateTimeOffset ChangedAt,
    string ConcurrencyToken);

public sealed record RelationshipEndpointContext(
    string Title,
    string ObjectTypeLabel,
    string? ShortDescription,
    string KnowledgeStatus,
    IReadOnlyList<SystemContextResponse> Systems);

public enum RelationshipFailure
{
    None,
    Validation,
    NotFound,
    ReferenceInvalid,
    Duplicate,
    Conflict,
    BusinessRuleViolation,
}

public sealed record RelationshipCommandResult(
    object? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    RelationshipFailure Failure,
    string? Message = null,
    object? Details = null);

public sealed record RelationshipDetailQueryResult(
    RelationshipDetailResponse? Response,
    RelationshipFailure Failure,
    string? Message = null);

public sealed record KnowledgeTargetsQueryResult(
    KnowledgeTargetsResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    RelationshipFailure Failure,
    string? Message = null);
