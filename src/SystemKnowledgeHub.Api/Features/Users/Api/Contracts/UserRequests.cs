using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.Users.Api.Contracts;

/// <summary>
/// User 管理写操作在 request body 中显式提交的操作人标签。
/// </summary>
/// <remarks>
/// DisplayName 是当前 User Feature 的必填操作事实；该对象不是 Current User、authenticated user 或
/// security principal。
/// </remarks>
public sealed record UserActorRequest(string? DisplayName, string? Role);

/// <summary>
/// 创建 canonical User 并提交其初始 KnowledgeRole assignment 的 API request。
/// </summary>
/// <remarks>
/// <c>KnowledgeRoleIds</c> 为 null 或空集合时不创建初始 assignment；非空集合可包含多个 ID，且每个 ID 必须引用
/// Active KnowledgeRole。<c>Actor</c> 是本次管理操作显式提交的操作人标签。
/// </remarks>
public sealed record CreateUserRequest(
    string? EmployeeNo,
    string? DisplayName,
    string? Email,
    string? DepartmentOrTeam,
    string? JobTitle,
    IReadOnlyList<long>? KnowledgeRoleIds,
    UserActorRequest? Actor);

/// <summary>
/// 更新 canonical User Profile 并替换当前 KnowledgeRole assignment 的 API request。
/// </summary>
/// <remarks>
/// <c>KnowledgeRoleIds</c> 为 null 与空集合均表示目标 assignment 为空；非空集合替换当前集合而非增量追加。新增
/// assignment 只能使用 Active KnowledgeRole，既有 inactive assignment 可被保留。<c>Actor</c> 是显式操作人标签；
/// <c>ConcurrencyToken</c> 必须从最近一次详情读取保存并原样回传，客户端不得解析、生成或推断其内部值。
/// </remarks>
public sealed record UpdateUserRequest(
    string? EmployeeNo,
    string? DisplayName,
    string? Email,
    string? DepartmentOrTeam,
    string? JobTitle,
    IReadOnlyList<long>? KnowledgeRoleIds,
    UserActorRequest? Actor,
    string? ConcurrencyToken);

/// <summary>
/// 显式切换 canonical User Active 状态的 API request。
/// </summary>
/// <remarks>
/// <c>Actor</c> 是本次管理操作显式提交的操作人标签；<c>ConcurrencyToken</c> 是必须原样回传、不得解析或生成的 opaque token。
/// </remarks>
public sealed record SetUserActiveStateRequest(
    bool IsActive,
    UserActorRequest? Actor,
    string? ConcurrencyToken);

/// <summary>独立修改 User AccessLevel 的管理员 API request。</summary>
/// <remarks><c>ConcurrencyToken</c> 是必须原样回传、不得解析或生成的 opaque token。</remarks>
public sealed record SetUserAccessLevelRequest(AccessLevel? AccessLevel, string? ConcurrencyToken);

/// <summary>为 canonical User 建立外部 LoginIdentity 显式映射的管理员 API request。</summary>
public sealed record CreateLoginIdentityRequest(string? Provider, string? Subject);

/// <summary>切换 LoginIdentity 是否可登录的管理员 API request。</summary>
/// <remarks><c>ConcurrencyToken</c> 是必须原样回传、不得解析或生成的 opaque token。</remarks>
public sealed record SetLoginIdentityActiveStateRequest(bool IsActive, string? ConcurrencyToken);

/// <summary>
/// 创建可供新 User assignment 使用的 KnowledgeRole 的 API request。
/// </summary>
/// <remarks>
/// <c>Actor</c> 是本次管理操作显式提交的操作人标签。
/// </remarks>
public sealed record CreateKnowledgeRoleRequest(
    string? Name,
    string? Description,
    UserActorRequest? Actor);

/// <summary>
/// 更新 KnowledgeRole 名称和说明的 API request。
/// </summary>
/// <remarks>
/// <c>Actor</c> 是本次管理操作显式提交的操作人标签；<c>ConcurrencyToken</c> 是必须原样回传、不得解析或生成的 opaque token。
/// </remarks>
public sealed record UpdateKnowledgeRoleRequest(
    string? Name,
    string? Description,
    UserActorRequest? Actor,
    string? ConcurrencyToken);

/// <summary>
/// 显式切换 KnowledgeRole 是否可用于新 assignment 的 API request。
/// </summary>
/// <remarks>
/// <c>Actor</c> 是本次管理操作显式提交的操作人标签；<c>ConcurrencyToken</c> 是必须原样回传、不得解析或生成的 opaque token。
/// </remarks>
public sealed record SetKnowledgeRoleActiveStateRequest(
    bool IsActive,
    UserActorRequest? Actor,
    string? ConcurrencyToken);
