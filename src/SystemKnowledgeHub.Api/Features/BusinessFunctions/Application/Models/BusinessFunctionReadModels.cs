namespace SystemKnowledgeHub.Api.Features.BusinessFunctions.Application.Models;

public sealed record BusinessFunctionsListQuery(
    long? SystemId,
    string? Search,
    string? FunctionType,
    string? RewriteStatus,
    string? KnowledgeStatus,
    string? HasUnknownItems,
    string? Sort,
    int? Page,
    int? PageSize);

public sealed record KnowledgeSystemReferenceResponse(long Id, string Name);

public sealed record BusinessFunctionSummaryResponse(
    long Id,
    string Name,
    KnowledgeSystemReferenceResponse System,
    string FunctionType,
    string? Purpose,
    int RelatedDataCount,
    int RuleCount,
    int UnknownCount,
    string RewriteStatus,
    string KnowledgeStatus,
    DateTimeOffset UpdatedAt);

public sealed record BusinessFunctionsListResponse(
    IReadOnlyList<BusinessFunctionSummaryResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record BusinessFunctionsListQueryResult(
    BusinessFunctionsListResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors);

public sealed record BusinessFunctionHeaderResponse(
    string Name,
    string FunctionType,
    string RewriteStatus,
    string KnowledgeStatus);

public sealed record BusinessFunctionOverviewResponse(
    string? Purpose,
    string? Caller,
    string? Input,
    string? Output);

public sealed record BusinessProcessStepResponse(int Order, string Name, string? Description);

public sealed record KnowledgeTargetReferenceResponse(string Type, long Id);

public sealed record RelatedDataResponse(
    long RelationshipId,
    KnowledgeTargetReferenceResponse Target,
    string Name,
    string RelationType,
    int EvidenceCount);

public sealed record BusinessRuleSummaryResponse(
    long RelationshipId,
    long Id,
    string Name,
    string KnowledgeStatus,
    int EvidenceCount);

public sealed record IntegrationSummaryResponse(
    long RelationshipId,
    long Id,
    string Name,
    string RelationType);

public sealed record EvidenceSummaryResponse(long Id, string EvidenceType, string SourceTitle);

public sealed record UnknownItemSummaryResponse(long Id, string Question, string Status);

public sealed record BusinessFunctionContextRailResponse(
    IReadOnlyList<string> Callers,
    IReadOnlyList<string> AdjacentFunctions,
    int IntegrationCount,
    int OpenUnknownCount);

public sealed record BusinessFunctionDetailResponse(
    long Id,
    KnowledgeSystemReferenceResponse System,
    string ConcurrencyToken,
    BusinessFunctionHeaderResponse Header,
    BusinessFunctionOverviewResponse Overview,
    IReadOnlyList<BusinessProcessStepResponse> BusinessProcess,
    IReadOnlyList<RelatedDataResponse> RelatedData,
    IReadOnlyList<BusinessRuleSummaryResponse> BusinessRules,
    IReadOnlyList<IntegrationSummaryResponse> Integrations,
    IReadOnlyList<EvidenceSummaryResponse> Evidence,
    IReadOnlyList<UnknownItemSummaryResponse> UnknownItems,
    BusinessFunctionContextRailResponse ContextRail,
    IReadOnlyList<string> AvailableActions);
