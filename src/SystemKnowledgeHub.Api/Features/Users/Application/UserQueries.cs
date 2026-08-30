using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;

namespace SystemKnowledgeHub.Api.Features.Users.Application;

/// <summary>
/// 提供 User 与 KnowledgeRole 管理页面所需的只读投影。
/// </summary>
public sealed class UserQueries(
    KnowledgeHubDbContext dbContext,
    ConcurrencyTokenCodec concurrencyTokenCodec,
    IOptions<LocalAuthenticationOptions> localOptions,
    IOptions<OidcAuthenticationOptions> oidcOptions)
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    /// <summary>
    /// 按受控筛选、排序和分页条件返回 User 管理列表及当前 KnowledgeRole assignment。
    /// </summary>
    /// <param name="request">关键词、Active 筛选、受控排序和分页条件。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步操作完成后，返回列表响应；无效查询参数通过 FieldErrors 表达。</returns>
    public async Task<UsersListQueryResult> GetUsersList(
        UsersListQuery request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateList(request, out var sort);
        if (errors.Count > 0)
        {
            return new UsersListQueryResult(null, errors);
        }

        var page = request.Page ?? 1;
        var pageSize = request.PageSize ?? DefaultPageSize;
        var keyword = NormalizeOptional(request.Keyword);
        var query = dbContext.Users.AsNoTracking();

        if (keyword is not null)
        {
            var pattern = $"%{keyword}%";
            query = query.Where(user =>
                EF.Functions.Like(user.DisplayName, pattern)
                || (user.EmployeeNo != null && EF.Functions.Like(user.EmployeeNo, pattern))
                || (user.Email != null && EF.Functions.Like(user.Email, pattern)));
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(user => user.IsActive == request.IsActive.Value);
        }

        query = sort switch
        {
            UserSort.DisplayNameDescending => query.OrderByDescending(user => user.DisplayName).ThenBy(user => user.Id),
            UserSort.UpdatedAtAscending => query.OrderBy(user => user.UpdatedAt).ThenBy(user => user.DisplayName),
            UserSort.UpdatedAtDescending => query.OrderByDescending(user => user.UpdatedAt).ThenBy(user => user.DisplayName),
            _ => query.OrderBy(user => user.DisplayName).ThenBy(user => user.Id),
        };

        var total = await query.CountAsync(cancellationToken);
        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new UserRow(
                user.Id,
                user.EmployeeNo,
                user.DisplayName,
                user.Email,
                user.DepartmentOrTeam,
                user.JobTitle,
                user.IsActive,
                user.UpdatedAt))
            .ToArrayAsync(cancellationToken);

        var rolesByUser = await GetRolesByUser(users.Select(user => user.Id).ToArray(), cancellationToken);
        var items = users.Select(user => new UserSummaryResponse(
            user.Id,
            user.EmployeeNo,
            user.DisplayName,
            user.Email,
            user.DepartmentOrTeam,
            user.JobTitle,
            user.IsActive,
            rolesByUser.GetValueOrDefault(user.Id, []),
            user.UpdatedAt)).ToArray();

        return new UsersListQueryResult(
            new UsersListResponse(items, page, pageSize, total),
            null);
    }

    /// <summary>
    /// 返回单个 canonical User 的可编辑详情及其当前 KnowledgeRole assignment。
    /// </summary>
    /// <param name="userId">要读取的 canonical User ID。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步操作完成后，返回包含 opaque concurrencyToken 的详情；若 User 不存在则返回 null。</returns>
    public async Task<UserDetailResponse?> GetUser(
        long userId,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new
            {
                item.Id,
                item.EmployeeNo,
                item.DisplayName,
                item.Email,
                item.DepartmentOrTeam,
                item.JobTitle,
                item.IsActive,
                item.CreatedAt,
                item.UpdatedAt,
                item.Version,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        var rolesByUser = await GetRolesByUser([user.Id], cancellationToken);
        return new UserDetailResponse(
            user.Id,
            user.EmployeeNo,
            user.DisplayName,
            user.Email,
            user.DepartmentOrTeam,
            user.JobTitle,
            user.IsActive,
            rolesByUser.GetValueOrDefault(user.Id, []),
            user.CreatedAt,
            user.UpdatedAt,
            concurrencyTokenCodec.Encode(user.Version));
    }

    /// <summary>返回新增用户页面可使用的服务器登录方式配置。</summary>
    public UserLoginSetupOptionsResponse GetLoginSetupOptions()
    {
        var approvedProvider = string.IsNullOrWhiteSpace(oidcOptions.Value.Provider)
            ? null
            : oidcOptions.Value.Provider;
        return new UserLoginSetupOptionsResponse(
            localOptions.Value.Enabled,
            oidcOptions.Value.Enabled,
            approvedProvider is not null,
            approvedProvider);
    }

    /// <summary>返回管理员可见的 User 登录方式元数据，不投影任何密码哈希或 SessionVersion。</summary>
    public async Task<UserLoginMethodsResponse?> GetUserLoginMethods(
        long userId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AsNoTracking().AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return null;
        }

        var credential = await dbContext.LocalLoginCredentials
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new
            {
                item.Username,
                item.IsActive,
                item.MustChangePassword,
                item.LastPasswordChangedAt,
                item.LockedUntil,
            })
            .SingleOrDefaultAsync(cancellationToken);
        var local = credential is null
            ? new LocalLoginMethodResponse(false, null, null, null, null, null, localOptions.Value.Enabled)
            : new LocalLoginMethodResponse(
                true,
                credential.Username,
                credential.IsActive,
                credential.MustChangePassword,
                credential.LastPasswordChangedAt,
                credential.LockedUntil,
                localOptions.Value.Enabled);

        var approvedProvider = oidcOptions.Value.Provider;
        var identities = await dbContext.LoginIdentities
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.Provider)
            .ThenBy(item => item.Subject)
            .Select(item => new OidcLoginMethodResponse(
                item.Provider,
                item.Subject,
                item.IsActive,
                oidcOptions.Value.Enabled && item.Provider == approvedProvider))
            .ToArrayAsync(cancellationToken);

        return new UserLoginMethodsResponse(userId, local, identities);
    }

    /// <summary>
    /// 返回按名称排序的 KnowledgeRole 管理列表。
    /// </summary>
    /// <param name="request">可选 Active 筛选条件；为 null 时包含 Active 与 inactive KnowledgeRole。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步操作完成后，返回每项均带 opaque concurrencyToken 的当前列表。</returns>
    public async Task<IReadOnlyList<KnowledgeRoleListItemResponse>> GetKnowledgeRoles(
        KnowledgeRolesListQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.KnowledgeRoles.AsNoTracking();
        if (request.IsActive.HasValue)
        {
            query = query.Where(role => role.IsActive == request.IsActive.Value);
        }

        var roles = await query
            .OrderBy(role => role.Name)
            .Select(role => new
            {
                role.Id,
                role.Name,
                role.Description,
                role.IsActive,
                role.UpdatedAt,
                role.Version,
            })
            .ToArrayAsync(cancellationToken);

        return roles.Select(role => new KnowledgeRoleListItemResponse(
            role.Id,
            role.Name,
            role.Description,
            role.IsActive,
            role.UpdatedAt,
            concurrencyTokenCodec.Encode(role.Version))).ToArray();
    }

    /// <summary>
    /// 返回单个 KnowledgeRole 的可编辑详情。
    /// </summary>
    /// <param name="knowledgeRoleId">要读取的 KnowledgeRole ID。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步操作完成后，返回包含 opaque concurrencyToken 的详情；若 KnowledgeRole 不存在则返回 null。</returns>
    public async Task<KnowledgeRoleDetailResponse?> GetKnowledgeRole(
        long knowledgeRoleId,
        CancellationToken cancellationToken)
    {
        var role = await dbContext.KnowledgeRoles
            .AsNoTracking()
            .Where(item => item.Id == knowledgeRoleId)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.Description,
                item.IsActive,
                item.CreatedAt,
                item.UpdatedAt,
                item.Version,
            })
            .SingleOrDefaultAsync(cancellationToken);

        return role is null
            ? null
            : new KnowledgeRoleDetailResponse(
                role.Id,
                role.Name,
                role.Description,
                role.IsActive,
                role.CreatedAt,
                role.UpdatedAt,
                concurrencyTokenCodec.Encode(role.Version));
    }

    private async Task<Dictionary<long, IReadOnlyList<KnowledgeRoleSummaryResponse>>> GetRolesByUser(
        IReadOnlyCollection<long> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var rows = await (
            from assignment in dbContext.UserKnowledgeRoles.AsNoTracking()
            join role in dbContext.KnowledgeRoles.AsNoTracking()
                on assignment.KnowledgeRoleId equals role.Id
            where userIds.Contains(assignment.UserId)
            orderby role.Name
            select new
            {
                assignment.UserId,
                Role = new KnowledgeRoleSummaryResponse(
                    role.Id,
                    role.Name,
                    role.Description,
                    role.IsActive),
            }).ToArrayAsync(cancellationToken);

        return rows
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<KnowledgeRoleSummaryResponse>)group.Select(row => row.Role).ToArray());
    }

    private static Dictionary<string, string[]> ValidateList(
        UsersListQuery request,
        out UserSort sort)
    {
        var errors = new Dictionary<string, string[]>();
        sort = UserSort.DisplayNameAscending;

        if (request.Page is < 1)
        {
            errors["page"] = ["页码必须从 1 开始。"]; 
        }

        if (request.PageSize is < 1 or > MaximumPageSize)
        {
            errors["pageSize"] = ["每页数量必须在 1 到 100 之间。"]; 
        }

        if (request.Sort is not null && !TryParseSort(request.Sort, out sort))
        {
            errors["sort"] = ["排序值无效。"]; 
        }

        return errors;
    }

    private static bool TryParseSort(string value, out UserSort sort)
    {
        sort = value switch
        {
            "displayName:asc" => UserSort.DisplayNameAscending,
            "displayName:desc" => UserSort.DisplayNameDescending,
            "updatedAt:asc" => UserSort.UpdatedAtAscending,
            "updatedAt:desc" => UserSort.UpdatedAtDescending,
            _ => UserSort.Invalid,
        };
        return sort != UserSort.Invalid;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private sealed record UserRow(
        long Id,
        string? EmployeeNo,
        string DisplayName,
        string? Email,
        string? DepartmentOrTeam,
        string? JobTitle,
        bool IsActive,
        DateTimeOffset UpdatedAt);

    private enum UserSort
    {
        Invalid,
        DisplayNameAscending,
        DisplayNameDescending,
        UpdatedAtAscending,
        UpdatedAtDescending,
    }
}
