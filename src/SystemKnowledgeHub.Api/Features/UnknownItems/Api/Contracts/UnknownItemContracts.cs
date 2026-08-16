using System.Text.Json;

namespace SystemKnowledgeHub.Api.Features.UnknownItems.Api.Contracts;

public sealed record UnknownTargetRequest(string? Type, long Id);
public sealed record UnknownPersonSnapshotRequest(
    string? DisplayName,
    string? RoleOrIdentity,
    DateTimeOffset? OccurredAt,
    string? Team,
    string? ExternalUserKey,
    string? Source,
    string? Note);
public sealed record UnknownActorRequest(string? DisplayName, string? Role);

public sealed record CreateUnknownItemRequest(
    long SystemId,
    string? Question,
    string? Context,
    string? Priority,
    UnknownTargetRequest? PrimaryTarget,
    IReadOnlyList<UnknownTargetRequest>? RelatedTargets,
    UnknownPersonSnapshotRequest? Creator);

public sealed record UpdateUnknownItemRelatedTargetsRequest(
    IReadOnlyList<UnknownTargetRequest>? RelatedTargets,
    UnknownActorRequest? Actor,
    string? ConcurrencyToken);

public sealed record StartInvestigationRequest(UnknownPersonSnapshotRequest? Actor, string? ConcurrencyToken);
public sealed record AddFindingRequest(string? Content, UnknownPersonSnapshotRequest? Recorder, string? ConcurrencyToken);

public sealed record AddInvestigationEvidenceRequest(
    string? EvidenceType,
    UnknownTargetRequest? Subject,
    string? SubjectDetailKey,
    string? SourceTitle,
    string? SourceReference,
    JsonElement? SourceLocator,
    string? Summary,
    string? SupportReason,
    string? Confidence,
    UnknownPersonSnapshotRequest? Provider,
    string? ConcurrencyToken);

public sealed record KnowledgeUpdateDraftRequest(
    long? Id,
    UnknownTargetRequest? Target,
    string? SubjectDetailKey,
    string? ApplyAction,
    string? ChangeSummary,
    JsonElement? Before,
    JsonElement? After,
    string? KnowledgeStatusBefore,
    string? KnowledgeStatusAfter);

public sealed record SaveResolutionDraftRequest(
    string? Conclusion,
    IReadOnlyList<KnowledgeUpdateDraftRequest>? KnowledgeUpdates,
    UnknownPersonSnapshotRequest? Actor,
    string? ConcurrencyToken);

public sealed record KnowledgeStatusChangeRequest(string? TargetStatus, string? Reason);
public sealed record ApplyColumnKnownValueRequest(
    long ColumnId, string? Value, string? Meaning, int SortOrder, KnowledgeStatusChangeRequest? KnowledgeStatusChange,
    UnknownPersonSnapshotRequest? Applier, string? ConcurrencyToken, string? TargetConcurrencyToken);
public sealed record ApplyDatabaseColumnKnowledgeRequest(
    long ColumnId, string? BusinessDescription, KnowledgeStatusChangeRequest? KnowledgeStatusChange,
    UnknownPersonSnapshotRequest? Applier, string? ConcurrencyToken, string? TargetConcurrencyToken);
public sealed record BusinessFunctionOverviewRequest(
    string? Name, string? DisplayName, string? FunctionType, string? Purpose, string? Caller,
    string? Input, string? Output, string? RewriteStatus);
public sealed record ApplyBusinessFunctionRequest(
    long BusinessFunctionId, BusinessFunctionOverviewRequest? Overview, KnowledgeStatusChangeRequest? KnowledgeStatusChange,
    UnknownPersonSnapshotRequest? Applier, string? ConcurrencyToken, string? TargetConcurrencyToken);
public sealed record BusinessRuleInputDataUpdateRequest(string? Name, string? Description);
public sealed record BusinessRuleUpdateRequest(
    string? Name, string? Description, string? Condition, string? Result,
    IReadOnlyList<BusinessRuleInputDataUpdateRequest>? InputData);
public sealed record ApplyBusinessRuleRequest(
    long BusinessRuleId, BusinessRuleUpdateRequest? Rule, KnowledgeStatusChangeRequest? KnowledgeStatusChange,
    UnknownPersonSnapshotRequest? Applier, string? ConcurrencyToken, string? TargetConcurrencyToken);
public sealed record IntegrationPartyUpdateRequest(long? SystemId, string? DisplayName);
public sealed record IntegrationOverviewUpdateRequest(
    string? Name, string? IntegrationType, IntegrationPartyUpdateRequest? SourceParty, IntegrationPartyUpdateRequest? TargetParty,
    string? FlowDirection, string? Purpose, JsonElement? Endpoint, long? DatabaseSourceId, long? DatabaseObjectId);
public sealed record ApplyIntegrationRequest(
    long IntegrationId, IntegrationOverviewUpdateRequest? Integration, KnowledgeStatusChangeRequest? KnowledgeStatusChange,
    UnknownPersonSnapshotRequest? Applier, string? ConcurrencyToken, string? TargetConcurrencyToken);
public sealed record ConfirmConclusionRequest(UnknownPersonSnapshotRequest? Confirmer, string? ConcurrencyToken);
public sealed record CloseUnknownItemRequest(string? CloseNote, UnknownPersonSnapshotRequest? Actor, string? ConcurrencyToken);
public sealed record ReopenUnknownItemRequest(string? Reason, UnknownPersonSnapshotRequest? Actor, string? ConcurrencyToken);
