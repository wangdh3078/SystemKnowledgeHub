using System.Text.Json;
using SystemKnowledgeHub.Api.Features.Evidence.Application.Models;

namespace SystemKnowledgeHub.Api.Features.UnknownItems.Application.Models;

public sealed record UnknownTargetCommand(string Type, long Id);

public sealed record UnknownItemsListQuery(
    string? Keyword,
    long? SystemId,
    string? RelatedObjectType,
    string? Priority,
    string? Status,
    DateTimeOffset? UpdatedFrom,
    DateTimeOffset? UpdatedTo,
    string? Sort,
    int? Page,
    int? PageSize);

public sealed record CreateUnknownItemCommand(
    long SystemId,
    string Question,
    string? Context,
    string Priority,
    UnknownTargetCommand? PrimaryTarget,
    IReadOnlyList<UnknownTargetCommand>? RelatedTargets,
    PersonSnapshotCommand? Creator);

public sealed record UpdateUnknownItemRelatedTargetsCommand(
    long UnknownItemId,
    IReadOnlyList<UnknownTargetCommand>? RelatedTargets,
    UnknownActorCommand? Actor,
    string ConcurrencyToken);

public sealed record UnknownActorCommand(string DisplayName, string? Role);

public sealed record StartInvestigationCommand(
    long UnknownItemId,
    PersonSnapshotCommand? Actor,
    string ConcurrencyToken);

public sealed record AddFindingCommand(
    long UnknownItemId,
    string Content,
    PersonSnapshotCommand? Recorder,
    string ConcurrencyToken);

public sealed record AddInvestigationEvidenceCommand(
    long UnknownItemId,
    AddEvidenceCommand Evidence,
    string ConcurrencyToken);

public sealed record KnowledgeUpdateDraftCommand(
    long? Id,
    UnknownTargetCommand? Target,
    string? SubjectDetailKey,
    string ApplyAction,
    string ChangeSummary,
    JsonElement? Before,
    JsonElement? After,
    string? KnowledgeStatusBefore,
    string? KnowledgeStatusAfter);

public sealed record SaveResolutionDraftCommand(
    long UnknownItemId,
    string Conclusion,
    IReadOnlyList<KnowledgeUpdateDraftCommand>? KnowledgeUpdates,
    PersonSnapshotCommand? Actor,
    string ConcurrencyToken);

public sealed record KnowledgeStatusChangeCommand(string TargetStatus, string? Reason);

public sealed record ApplyColumnKnownValueCommand(
    long UnknownItemId, long KnowledgeUpdateId, long ColumnId, string Value, string Meaning, int SortOrder,
    KnowledgeStatusChangeCommand? KnowledgeStatusChange, PersonSnapshotCommand? Applier,
    string ConcurrencyToken, string TargetConcurrencyToken);

public sealed record ApplyDatabaseColumnKnowledgeCommand(
    long UnknownItemId, long KnowledgeUpdateId, long ColumnId, string BusinessDescription,
    KnowledgeStatusChangeCommand? KnowledgeStatusChange, PersonSnapshotCommand? Applier,
    string ConcurrencyToken, string TargetConcurrencyToken);

public sealed record BusinessFunctionOverviewCommand(
    string Name, string? DisplayName, string FunctionType, string? Purpose, string? Caller,
    string? Input, string? Output, string RewriteStatus);

public sealed record ApplyBusinessFunctionCommand(
    long UnknownItemId, long KnowledgeUpdateId, long BusinessFunctionId, BusinessFunctionOverviewCommand? Overview,
    KnowledgeStatusChangeCommand? KnowledgeStatusChange, PersonSnapshotCommand? Applier,
    string ConcurrencyToken, string TargetConcurrencyToken);

public sealed record BusinessRuleUpdateCommand(
    string Name, string Description, string? Condition, string? Result,
    IReadOnlyList<BusinessRuleInputDataCommand>? InputData);
public sealed record BusinessRuleInputDataCommand(string Name, string? Description);
public sealed record ApplyBusinessRuleCommand(
    long UnknownItemId, long KnowledgeUpdateId, long BusinessRuleId, BusinessRuleUpdateCommand? Rule,
    KnowledgeStatusChangeCommand? KnowledgeStatusChange, PersonSnapshotCommand? Applier,
    string ConcurrencyToken, string TargetConcurrencyToken);

public sealed record ConfirmConclusionCommand(long UnknownItemId, PersonSnapshotCommand? Confirmer, string ConcurrencyToken);
public sealed record CloseUnknownItemCommand(long UnknownItemId, string? CloseNote, PersonSnapshotCommand? Actor, string ConcurrencyToken);
public sealed record ReopenUnknownItemCommand(long UnknownItemId, string Reason, PersonSnapshotCommand? Actor, string ConcurrencyToken);

public sealed record UnknownTargetResponse(string Type, long Id);
public sealed record UnknownTargetSummaryResponse(UnknownTargetResponse Target, string Display, bool Primary);
public sealed record UnknownPrimaryTargetResponse(string Type, long Id, string Display);
public sealed record UnknownSystemResponse(long Id, string Name);
public sealed record UnknownActivityResponse(string Type, string Summary, DateTimeOffset OccurredAt);

public sealed record UnknownItemListRowResponse(
    long Id,
    string ItemCode,
    string Question,
    UnknownSystemResponse System,
    UnknownPrimaryTargetResponse PrimaryTarget,
    string Priority,
    string Status,
    int FindingCount,
    int EvidenceCount,
    DateTimeOffset UpdatedAt);

public sealed record UnknownItemsListResponse(
    IReadOnlyList<UnknownItemListRowResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record UnknownItemQuestionResponse(
    string Text,
    string? Context,
    string Priority,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record FindingResponse(long Id, string Content, PersonSnapshotResponse RecordedBy);
public sealed record InvestigationEvidenceResponse(long Id, UnknownTargetResponse Subject, string EvidenceType, string SourceTitle);
public sealed record ResolutionResponse(long Id, string Conclusion, PersonSnapshotResponse? ConfirmedBy, DateTimeOffset? ConfirmedAt);

public sealed record KnowledgeUpdateResponse(
    long Id,
    UnknownTargetResponse Target,
    string? SubjectDetailKey,
    string ChangeSummary,
    JsonElement Before,
    JsonElement After,
    string Status);

public sealed record UnknownItemActivityResponse(
    string Type,
    string Summary,
    DateTimeOffset OccurredAt);

public sealed record UnknownItemContextRailResponse(
    IReadOnlyList<string> KnowledgeImpact,
    int EvidenceCount,
    int OpenGapCount);

public sealed record UnknownItemDetailResponse(
    long Id,
    string ItemCode,
    UnknownSystemResponse System,
    string ConcurrencyToken,
    UnknownItemQuestionResponse Question,
    IReadOnlyList<UnknownTargetSummaryResponse> RelatedObjects,
    IReadOnlyList<FindingResponse> Findings,
    IReadOnlyList<InvestigationEvidenceResponse> Evidence,
    ResolutionResponse? Resolution,
    IReadOnlyList<KnowledgeUpdateResponse> KnowledgeUpdates,
    IReadOnlyList<UnknownItemActivityResponse> Activity,
    UnknownItemContextRailResponse ContextRail,
    IReadOnlyList<string> AvailableActions);

public sealed record CreateUnknownItemResponse(
    long Id,
    string ItemCode,
    string Status,
    UnknownTargetResponse PrimaryTarget,
    IReadOnlyList<UnknownTargetResponse> RelatedTargets,
    UnknownActivityResponse LatestActivity,
    string ConcurrencyToken,
    IReadOnlyList<string> AvailableActions);

public sealed record UpdateUnknownTargetsResponse(
    UnknownTargetResponse PrimaryTarget,
    IReadOnlyList<UnknownTargetResponse> RelatedTargets,
    string ConcurrencyToken);

public sealed record StartInvestigationResponse(
    long Id,
    string PreviousStatus,
    string Status,
    DateTimeOffset InvestigationStartedAt,
    UnknownActivityResponse LatestActivity,
    string ConcurrencyToken,
    IReadOnlyList<string> AvailableActions);

public sealed record AddFindingResponse(
    FindingResponse Finding,
    UnknownActivityResponse LatestActivity,
    string Status,
    string ConcurrencyToken);

public sealed record AddInvestigationEvidenceResponse(
    InvestigationEvidenceResponse Evidence,
    UnknownActivityResponse LatestActivity,
    string Status,
    string ConcurrencyToken);

public sealed record SaveResolutionDraftResponse(
    ResolutionResponse Resolution,
    IReadOnlyList<KnowledgeUpdateResponse> KnowledgeUpdates,
    string Status,
    UnknownActivityResponse LatestActivity,
    string ConcurrencyToken);

public sealed record AppliedKnowledgeUpdateSummary(long Id, string Status, DateTimeOffset AppliedAt);
public sealed record ApplyKnowledgeUpdateResponse(
    long UnknownItemId,
    string UnknownItemStatus,
    AppliedKnowledgeUpdateSummary KnowledgeUpdate,
    UnknownTargetResponse Target,
    string TargetKnowledgeStatus,
    UnknownActivityResponse LatestActivity,
    string ConcurrencyToken,
    string TargetConcurrencyToken,
    IReadOnlyList<string> AvailableActions);

public sealed record ConfirmConclusionResponse(
    long Id, string PreviousStatus, string Status, DateTimeOffset ConclusionConfirmedAt,
    UnknownActivityResponse LatestActivity, string ConcurrencyToken, IReadOnlyList<string> AvailableActions);
public sealed record CloseUnknownItemResponse(
    long Id, string PreviousStatus, string Status, DateTimeOffset ClosedAt,
    UnknownActivityResponse LatestActivity, string ConcurrencyToken, IReadOnlyList<string> AvailableActions);
public sealed record ReopenUnknownItemResponse(
    long Id, string PreviousStatus, string Status, DateTimeOffset? ClosedAt, bool AppliedKnowledgeUpdatesRetained,
    UnknownActivityResponse LatestActivity, string ConcurrencyToken, IReadOnlyList<string> AvailableActions);

public enum UnknownItemFailure
{
    None,
    Validation,
    NotFound,
    ReferenceInvalid,
    InvalidState,
    Conflict,
    UnsupportedUpdate,
}

public sealed record UnknownItemCommandResult(
    object? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    UnknownItemFailure Failure,
    string? Message = null);

public sealed record UnknownItemsListQueryResult(
    UnknownItemsListResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors);

public sealed record UnknownItemDetailQueryResult(
    UnknownItemDetailResponse? Response,
    UnknownItemFailure Failure,
    string? Message = null);
