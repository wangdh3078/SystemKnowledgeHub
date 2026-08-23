namespace SystemKnowledgeHub.Api.Features.Relationships.Api.Contracts;

public sealed record RelationshipTargetRequest(string? Type, long Id);
public sealed record AddRelationshipRequest(
    RelationshipTargetRequest? Source, string? RelationType, RelationshipTargetRequest? Target,
    string? Description);
public sealed record UpdateRelationshipDescriptionRequest(
    string? Description, string? ConcurrencyToken);
public sealed record ChangeRelationshipStatusRequest(
    string? TargetStatus, string? Reason, string? ConcurrencyToken);
