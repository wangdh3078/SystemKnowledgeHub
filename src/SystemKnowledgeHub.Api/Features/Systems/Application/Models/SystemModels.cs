namespace SystemKnowledgeHub.Api.Features.Systems.Application.Models;

public sealed record SystemsListQuery(
    string? Search,
    string? Lifecycle,
    string? Technology,
    string? KnowledgeStatus,
    string? Sort,
    int? Page,
    int? PageSize);

public sealed record SystemSummaryResponse(
    long Id,
    string Name,
    string DisplayName,
    string SystemType,
    string? Purpose,
    IReadOnlyList<string> Technologies,
    int FunctionCount,
    int DatabaseObjectCount,
    int OpenUnknownCount,
    string Lifecycle,
    string KnowledgeStatus,
    DateTimeOffset UpdatedAt);

public sealed record SystemsListResponse(
    IReadOnlyList<SystemSummaryResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record SystemsListQueryResult(
    SystemsListResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors);

public sealed record ActorContext(string DisplayName, string? Role);

public sealed record CreateSystemCommand(
    string Name,
    string DisplayName,
    string SystemType,
    string Lifecycle,
    string? Purpose,
    ActorContext Actor);

public sealed record CreateSystemResponse(
    long Id,
    string Name,
    string DisplayName,
    string Lifecycle,
    string KnowledgeStatus,
    string ConcurrencyToken);

public sealed record CreateSystemResult(
    CreateSystemResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    bool DuplicateName);

public sealed record SystemOverviewResponse(
    string Name,
    string DisplayName,
    string SystemType,
    string Lifecycle,
    string? Purpose,
    IReadOnlyList<string> MainUsers,
    IReadOnlyList<string> Technologies,
    SystemRepositoryResponse Repository,
    IReadOnlyList<SystemDeploymentResponse> Deployment,
    string? Notes,
    string KnowledgeStatus);

public sealed record SystemRepositoryResponse(string? Name, string? Url);

public sealed record SystemDeploymentResponse(string Environment, string Description);

public sealed record SystemKnowledgeSummaryResponse(
    int Confirmed,
    int Inferred,
    int Unknown,
    int OpenUnknownItems);

public sealed record SystemBusinessFunctionSummaryResponse(
    long Id,
    string Name,
    string? Purpose,
    string KnowledgeStatus,
    int UnknownCount);

public sealed record SystemDatabaseObjectSummaryResponse(
    long Id,
    string QualifiedName,
    string ObjectType,
    string KnowledgeStatus,
    int UnknownCount);

public sealed record SystemIntegrationSummaryResponse(
    long Id,
    string Name,
    string IntegrationType,
    string RelatedSystem,
    string KnowledgeStatus);

public sealed record SystemUnknownItemSummaryResponse(
    long Id,
    string ItemCode,
    string Question,
    string Priority,
    string Status);

public sealed record RelatedSystemSummaryResponse(long Id, string Name);

public sealed record MainDatabaseSummaryResponse(long Id, string Name);

public sealed record SystemContextRailResponse(
    IReadOnlyList<RelatedSystemSummaryResponse> RelatedSystems,
    int IntegrationCount,
    MainDatabaseSummaryResponse? MainDatabase,
    int HighPriorityUnknownCount,
    IReadOnlyList<string> KnowledgeGaps);

public sealed record SystemDetailResponse(
    long Id,
    string ConcurrencyToken,
    SystemOverviewResponse Overview,
    SystemKnowledgeSummaryResponse KnowledgeSummary,
    IReadOnlyList<SystemBusinessFunctionSummaryResponse> BusinessFunctions,
    IReadOnlyList<SystemDatabaseObjectSummaryResponse> DatabaseObjects,
    IReadOnlyList<SystemIntegrationSummaryResponse> Integrations,
    IReadOnlyList<SystemUnknownItemSummaryResponse> UnknownItems,
    SystemContextRailResponse ContextRail,
    IReadOnlyList<string> AvailableActions);

public sealed record UpdateSystemRepository(string? Name, string? Url);

public sealed record UpdateSystemDeployment(string Environment, string Description);

public sealed record UpdateSystemOverviewCommand(
    long SystemId,
    string DisplayName,
    string SystemType,
    string? Purpose,
    IReadOnlyList<string>? MainUsers,
    UpdateSystemRepository? Repository,
    IReadOnlyList<UpdateSystemDeployment>? Deployment,
    IReadOnlyList<string>? MainProjects,
    IReadOnlyList<string>? MainEntryPoints,
    string? Notes,
    ActorContext Actor,
    string ConcurrencyToken);

public sealed record UpdatedSystemOverviewResponse(
    string DisplayName,
    string? Purpose,
    string? Notes);

public sealed record UpdateSystemOverviewResponse(
    long Id,
    UpdatedSystemOverviewResponse Overview,
    string ConcurrencyToken);

public enum UpdateSystemOverviewFailure
{
    None,
    Validation,
    NotFound,
    Conflict,
}

public sealed record UpdateSystemOverviewResult(
    UpdateSystemOverviewResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    UpdateSystemOverviewFailure Failure);
