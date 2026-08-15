namespace SystemKnowledgeHub.Api.Features.StatusProgression.Api.Contracts;

public sealed record KnowledgeStatusTargetRequest(string? Type, long Id);

public sealed record KnowledgeStatusActorRequest(
    string? DisplayName,
    string? RoleOrIdentity,
    DateTimeOffset? OccurredAt,
    string? Team,
    string? ExternalUserKey,
    string? Source,
    string? Note);

public sealed record ChangeKnowledgeStatusRequest(
    KnowledgeStatusTargetRequest? Target,
    string? TargetStatus,
    string? Reason,
    KnowledgeStatusActorRequest? Actor,
    string? ConcurrencyToken);
