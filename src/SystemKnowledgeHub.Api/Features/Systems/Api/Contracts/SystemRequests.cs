namespace SystemKnowledgeHub.Api.Features.Systems.Api.Contracts;

public sealed record ActorContextRequest(string DisplayName, string? Role);

public sealed record CreateSystemRequest(
    string Name,
    string DisplayName,
    string SystemType,
    string Lifecycle,
    string? Purpose,
    ActorContextRequest Actor);

public sealed record SystemRepositoryRequest(string? Name, string? Url);

public sealed record SystemDeploymentRequest(string Environment, string Description);

public sealed record UpdateSystemOverviewRequest(
    string DisplayName,
    string SystemType,
    string? Purpose,
    IReadOnlyList<string> MainUsers,
    SystemRepositoryRequest Repository,
    IReadOnlyList<SystemDeploymentRequest> Deployment,
    IReadOnlyList<string> MainProjects,
    IReadOnlyList<string> MainEntryPoints,
    string? Notes,
    ActorContextRequest Actor,
    string ConcurrencyToken);

public sealed record UpdateSystemTechnologyRequest(
    IReadOnlyList<string> Technologies,
    ActorContextRequest Actor,
    string ConcurrencyToken);

public sealed record UpdateSystemLifecycleRequest(
    string TargetLifecycle,
    ActorContextRequest Actor,
    string ConcurrencyToken);
