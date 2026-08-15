namespace SystemKnowledgeHub.Api.Features.Relationships.Api.Contracts;

public sealed record RelationshipTargetRequest(string? Type, long Id);
public sealed record RelationshipActorRequest(string? DisplayName, string? Role);
public sealed record RelationshipStatusActorRequest(
    string? DisplayName, string? RoleOrIdentity, DateTimeOffset? OccurredAt,
    string? Team, string? ExternalUserKey, string? Source, string? Note);
public sealed record AddRelationshipRequest(
    RelationshipTargetRequest? Source, string? RelationType, RelationshipTargetRequest? Target,
    string? Description, RelationshipActorRequest? Actor);
public sealed record UpdateRelationshipDescriptionRequest(
    string? Description, RelationshipActorRequest? Actor, string? ConcurrencyToken);
public sealed record ChangeRelationshipStatusRequest(
    string? TargetStatus, string? Reason, RelationshipStatusActorRequest? Actor, string? ConcurrencyToken);
