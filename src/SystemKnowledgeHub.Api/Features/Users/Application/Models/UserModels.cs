using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.Users.Application.Models;

/// <summary>
/// 查询 User 管理列表的筛选、排序与分页条件。
/// </summary>
/// <param name="Keyword">为 null 时不按姓名、工号或邮箱筛选；非空值会先去除首尾空白。</param>
/// <param name="IsActive">为 null 时不按 Active 状态筛选。</param>
/// <param name="Sort">
/// 为 null 时按 <c>displayName:asc</c>；仅接受 <c>displayName:asc</c>、<c>displayName:desc</c>、
/// <c>updatedAt:asc</c> 或 <c>updatedAt:desc</c>。
/// </param>
/// <param name="Page">为 null 时使用第 1 页；已提供的页码必须从 1 开始。</param>
/// <param name="PageSize">为 null 时使用 20；已提供的值必须在 1 到 100 之间。</param>
public sealed record UsersListQuery(
    string? Keyword,
    bool? IsActive,
    string? Sort,
    int? Page,
    int? PageSize);

/// <summary>
/// User 管理命令中显式提交的操作人标签。
/// </summary>
/// <remarks>
/// 该值只承载本 Feature 当前的 body contract，DisplayName 必须有效；它不等同于 Current User、
/// authenticated user 或 security principal。
/// </remarks>
public sealed record UserActorContext(string DisplayName, string? Role);

/// <summary>
/// 在 User 列表与详情中展示的当前 KnowledgeRole assignment。
/// </summary>
public sealed record KnowledgeRoleSummaryResponse(
    long Id,
    string Name,
    string? Description,
    bool IsActive);

/// <summary>
/// User 管理列表中的 canonical Profile 及当前 KnowledgeRole assignment。
/// </summary>
public sealed record UserSummaryResponse(
    long Id,
    string? EmployeeNo,
    string DisplayName,
    string? Email,
    string? DepartmentOrTeam,
    string? JobTitle,
    bool IsActive,
    IReadOnlyList<KnowledgeRoleSummaryResponse> KnowledgeRoles,
    DateTimeOffset UpdatedAt);

/// <summary>
/// 带分页信息的 User 管理列表响应。
/// </summary>
public sealed record UsersListResponse(
    IReadOnlyList<UserSummaryResponse> Items,
    int Page,
    int PageSize,
    int Total);

/// <summary>
/// User 列表查询的成功响应或输入字段错误。
/// </summary>
/// <remarks>
/// <see cref="Response"/> 非 null 表示查询成功；<see cref="FieldErrors"/> 非 null 表示筛选、排序或分页参数
/// 无法进入查询，不是未找到 User。
/// </remarks>
public sealed record UsersListQueryResult(
    UsersListResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors);

/// <summary>
/// 可编辑 User 的详情和当前 KnowledgeRole assignment。
/// </summary>
/// <remarks>
/// <c>ConcurrencyToken</c> 在更新或切换 Active 状态时必须原样回传；它是 opaque token，客户端不得解析、生成或推断其内部值。
/// </remarks>
public sealed record UserDetailResponse(
    long Id,
    string? EmployeeNo,
    string DisplayName,
    string? Email,
    string? DepartmentOrTeam,
    string? JobTitle,
    bool IsActive,
    IReadOnlyList<KnowledgeRoleSummaryResponse> KnowledgeRoles,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string ConcurrencyToken);

/// <summary>
/// 由 authenticated Principal 映射得到的当前 canonical User profile。
/// </summary>
/// <remarks>
/// <c>Id</c> 是 canonical User ID，而不是 LoginIdentity ID、OIDC Subject 或 Provider 标识。<c>AccessLevel</c>
/// 表示该 User 当前的系统访问等级；<c>KnowledgeRoles</c> 表示知识身份 assignment，用于知识归属而非权限、
/// authorization role 或 security role。
/// </remarks>
public sealed record CurrentUserResponse(
    long Id,
    string? EmployeeNo,
    string DisplayName,
    string? Email,
    string? DepartmentOrTeam,
    string? JobTitle,
    bool IsActive,
    IReadOnlyList<KnowledgeRoleSummaryResponse> KnowledgeRoles,
    string AccessLevel,
    string AuthenticationMethod,
    bool MustChangePassword);

/// <summary>
/// 解析当前 authenticated Principal 到 Current User context 的结果状态。
/// </summary>
public enum CurrentUserResolutionStatus
{
    Available,
    /// <summary>
    /// 当前 Local 会话有效，但在修改密码前不得进入普通业务或管理功能。
    /// </summary>
    PasswordChangeRequired,
    /// <summary>
    /// 当前请求没有可用的 authenticated Principal，而不是缺少客户端提供的 User 标识。
    /// </summary>
    Unauthenticated,
    /// <summary>
    /// 已有会话无法提供有效的内部身份投影，必须重新认证。
    /// </summary>
    SessionExpired,
    /// <summary>
    /// Principal 指向的 LoginIdentity 或 canonical User 映射不存在或不再一致。
    /// </summary>
    IdentityUnmapped,
    /// <summary>
    /// 已映射的 LoginIdentity 当前已停用。
    /// </summary>
    IdentityInactive,
    /// <summary>
    /// 已映射的 canonical User 当前已停用。
    /// </summary>
    AccountInactive,
}

/// <summary>
/// Current User 解析的显式业务结果，而不是异常列表。
/// </summary>
/// <remarks>
/// 当 <see cref="Status"/> 为 <see cref="CurrentUserResolutionStatus.Available"/> 或
/// <see cref="CurrentUserResolutionStatus.PasswordChangeRequired"/> 时，<see cref="CurrentUser"/> 包含已解析的
/// canonical User profile；其他状态下该值为 null，并保留具体失败边界。
/// </remarks>
public sealed record CurrentUserResolution(
    CurrentUserResolutionStatus Status,
    CurrentUserResponse? CurrentUser,
    string? Reason = null);

/// <summary>当前 Local 用户修改自己密码的显式业务结果。</summary>
public enum LocalPasswordChangeFailure
{
    None,
    Validation,
    Forbidden,
    SessionExpired,
    Conflict,
}

/// <summary>当前 Local 用户修改自己密码的结果；字段错误不包含任何密码原文。</summary>
public sealed record LocalPasswordChangeResult(
    LocalPasswordChangeFailure Failure,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    string? Reason = null);

/// <summary>
/// 创建 canonical User 与初始 KnowledgeRole assignment 的 Application command。
/// </summary>
/// <remarks>
/// <c>KnowledgeRoleIds</c> 为 null 或空集合时不创建初始 assignment；非空集合可包含多个 ID，且每个新 assignment 必须引用
/// Active KnowledgeRole。<c>Actor</c> 是显式提交的操作人标签，不是认证身份。
/// </remarks>
public sealed record CreateUserCommand(
    string? EmployeeNo,
    string DisplayName,
    string? Email,
    string? DepartmentOrTeam,
    string? JobTitle,
    IReadOnlyList<long>? KnowledgeRoleIds,
    CreateUserLoginSetupCommand? LoginSetup,
    UserActorContext Actor);

/// <summary>创建 User 时的显式 discriminated 登录方式命令。</summary>
public sealed record CreateUserLoginSetupCommand(
    string? Type,
    string? Username,
    string? InitialPassword,
    string? Provider,
    string? Subject);

/// <summary>新增用户 Drawer 可用的服务器登录方式配置。</summary>
public sealed record UserLoginSetupOptionsResponse(
    bool LocalGloballyEnabled,
    bool OidcGloballyEnabled,
    bool OidcSetupAvailable,
    string? ApprovedOidcProvider);

/// <summary>管理员读取的单个 User 本地登录方式投影；绝不包含密码哈希或 SessionVersion。</summary>
public sealed record LocalLoginMethodResponse(
    bool Exists,
    string? Username,
    bool? IsActive,
    bool? MustChangePassword,
    DateTimeOffset? LastPasswordChangedAt,
    DateTimeOffset? LockedUntil,
    bool GloballyEnabled);

/// <summary>管理员读取的 OIDC 映射投影。</summary>
public sealed record OidcLoginMethodResponse(
    string Provider,
    string Subject,
    bool IsActive,
    bool GloballyEnabled);

/// <summary>管理员读取的 User 登录方式聚合投影。</summary>
public sealed record UserLoginMethodsResponse(
    long UserId,
    LocalLoginMethodResponse Local,
    IReadOnlyList<OidcLoginMethodResponse> Oidc);

/// <summary>
/// 更新 canonical User Profile 并替换其当前 KnowledgeRole assignment 的 Application command。
/// </summary>
/// <remarks>
/// <c>KnowledgeRoleIds</c> 为 null 与空集合均表示目标 assignment 为空；提供集合会替换当前 assignment，而非增量追加。
/// 新增的 ID 必须引用 Active KnowledgeRole，已存在的 inactive assignment 可被保留。<c>Actor</c> 不是认证身份；
/// <c>ConcurrencyToken</c> 必须从最近一次详情读取后原样回传。
/// </remarks>
public sealed record UpdateUserCommand(
    long UserId,
    string? EmployeeNo,
    string DisplayName,
    string? Email,
    string? DepartmentOrTeam,
    string? JobTitle,
    IReadOnlyList<long>? KnowledgeRoleIds,
    UserActorContext Actor,
    string ConcurrencyToken);

/// <summary>
/// 显式切换 canonical User 的 Active 状态的 Application command。
/// </summary>
/// <remarks>
/// <c>Actor</c> 是显式提交的操作人标签，不是认证身份；<c>ConcurrencyToken</c> 必须从最近一次详情读取后原样回传。
/// </remarks>
public sealed record SetUserActiveStateCommand(
    long UserId,
    bool IsActive,
    UserActorContext Actor,
    string ConcurrencyToken);

/// <summary>独立修改 User AccessLevel 的管理员命令。</summary>
/// <remarks><c>ConcurrencyToken</c> 必须从最近一次详情读取后原样回传，且该操作不更新普通 Profile。</remarks>
public sealed record SetUserAccessLevelCommand(long UserId, AccessLevel AccessLevel, string ConcurrencyToken);

/// <summary>
/// User 写入 Use Case 返回的显式业务结果类别，而不是异常列表。
/// </summary>
public enum UserWriteFailure
{
    None,
    Validation,
    NotFound,
    Conflict,
    Duplicate,
    /// <summary>
    /// 请求了不存在的 KnowledgeRole，或把当前 inactive KnowledgeRole 作为新的 assignment。
    /// </summary>
    InactiveKnowledgeRole,
    /// <summary>
    /// Active 状态已等于请求值，因此没有写入。
    /// </summary>
    NoChange,
    LastUsableAdministrator,
}

/// <summary>
/// User 写入 Use Case 的成功详情或可映射的失败结果。
/// </summary>
/// <remarks>
/// <see cref="Response"/> 非 null 且 <see cref="Failure"/> 为 <see cref="UserWriteFailure.None"/> 时表示成功；
/// validation、duplicate 或 reference 问题通过 <see cref="FieldErrors"/> 返回，不以 CLR exception 表达。
/// </remarks>
public sealed record UserWriteResult(
    UserDetailResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    UserWriteFailure Failure);

/// <summary>
/// 查询 KnowledgeRole 列表的筛选条件。
/// </summary>
/// <param name="IsActive">为 null 时返回 Active 与 inactive KnowledgeRole。</param>
public sealed record KnowledgeRolesListQuery(bool? IsActive);

/// <summary>
/// KnowledgeRole 管理列表中的当前条目和可用于后续写入的并发标记。
/// </summary>
/// <remarks>
/// <c>ConcurrencyToken</c> 是客户端只能原样回传、不得解析或生成的 opaque token。
/// </remarks>
public sealed record KnowledgeRoleListItemResponse(
    long Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset UpdatedAt,
    string ConcurrencyToken);

/// <summary>
/// 可编辑 KnowledgeRole 的详情和当前并发标记。
/// </summary>
/// <remarks>
/// <c>ConcurrencyToken</c> 是客户端只能原样回传、不得解析或生成的 opaque token。
/// </remarks>
public sealed record KnowledgeRoleDetailResponse(
    long Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string ConcurrencyToken);

/// <summary>
/// 创建可供新 User assignment 选择的 KnowledgeRole 的 Application command。
/// </summary>
/// <remarks>
/// <c>Actor</c> 是显式提交的操作人标签，不是认证身份。
/// </remarks>
public sealed record CreateKnowledgeRoleCommand(
    string Name,
    string? Description,
    UserActorContext Actor);

/// <summary>
/// 更新 KnowledgeRole 名称和说明的 Application command。
/// </summary>
/// <remarks>
/// <c>Actor</c> 是显式提交的操作人标签，不是认证身份；<c>ConcurrencyToken</c> 必须从最近一次读取后原样回传。
/// </remarks>
public sealed record UpdateKnowledgeRoleCommand(
    long KnowledgeRoleId,
    string Name,
    string? Description,
    UserActorContext Actor,
    string ConcurrencyToken);

/// <summary>
/// 显式切换 KnowledgeRole 是否可用于新 assignment 的 Application command。
/// </summary>
/// <remarks>
/// <c>Actor</c> 是显式提交的操作人标签，不是认证身份；<c>ConcurrencyToken</c> 必须从最近一次读取后原样回传。
/// </remarks>
public sealed record SetKnowledgeRoleActiveStateCommand(
    long KnowledgeRoleId,
    bool IsActive,
    UserActorContext Actor,
    string ConcurrencyToken);

/// <summary>
/// KnowledgeRole 写入 Use Case 返回的显式业务结果类别，而不是异常列表。
/// </summary>
public enum KnowledgeRoleWriteFailure
{
    None,
    Validation,
    NotFound,
    Conflict,
    Duplicate,
    /// <summary>
    /// Active 状态已等于请求值，因此没有写入。
    /// </summary>
    NoChange,
}

/// <summary>
/// KnowledgeRole 写入 Use Case 的成功详情或可映射的失败结果。
/// </summary>
/// <remarks>
/// <see cref="Response"/> 非 null 且 <see cref="Failure"/> 为 <see cref="KnowledgeRoleWriteFailure.None"/> 时表示成功；
/// <see cref="FieldErrors"/> 承载可返回给调用方的 validation 或 duplicate 字段问题。
/// </remarks>
public sealed record KnowledgeRoleWriteResult(
    KnowledgeRoleDetailResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    KnowledgeRoleWriteFailure Failure);

/// <summary>管理员读取的 LoginIdentity 映射及其当前并发标记。</summary>
/// <remarks><c>ConcurrencyToken</c> 是客户端只能原样回传的 opaque token。</remarks>
public sealed record LoginIdentityResponse(long Id, long UserId, string Provider, string Subject, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string ConcurrencyToken);

/// <summary>创建 canonical User 与外部 identity 显式映射的管理员命令。</summary>
public sealed record CreateLoginIdentityCommand(long UserId, string Provider, string Subject);

/// <summary>切换 LoginIdentity 是否可登录的管理员命令。</summary>
/// <remarks><c>ConcurrencyToken</c> 必须从最近一次读取后原样回传。</remarks>
public sealed record SetLoginIdentityActiveStateCommand(long UserId, long LoginIdentityId, bool IsActive, string ConcurrencyToken);

/// <summary>LoginIdentity 管理 Use Case 的显式业务结果类别。</summary>
public enum LoginIdentityWriteFailure { None, Validation, NotFound, Conflict, Duplicate, NoChange, LastUsableAdministrator }

/// <summary>LoginIdentity 管理 Use Case 的成功详情或可映射的失败结果。</summary>
public sealed record LoginIdentityWriteResult(LoginIdentityResponse? Response, IReadOnlyDictionary<string, string[]>? FieldErrors, LoginIdentityWriteFailure Failure);

/// <summary>独立 AccessLevel 管理操作返回的当前等级与下一次写入所需并发标记。</summary>
/// <remarks><c>ConcurrencyToken</c> 是客户端只能原样回传的 opaque token。</remarks>
public sealed record UserAccessLevelResponse(long UserId, AccessLevel AccessLevel, string ConcurrencyToken);
