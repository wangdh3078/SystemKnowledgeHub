using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;
using UserEntity = SystemKnowledgeHub.Api.Features.Users.Domain.User;

namespace SystemKnowledgeHub.Api.Features.Users.Application;

/// <summary>
/// 执行 User 与 KnowledgeRole 管理的写入 Use Case。
/// </summary>
public sealed class UserService(
    KnowledgeHubDbContext dbContext,
    UserQueries queries,
    ConcurrencyTokenCodec concurrencyTokenCodec,
    UsableAdministratorResolver usableAdministrators,
    LocalPasswordService passwords,
    IOptions<OidcAuthenticationOptions> oidcOptions,
    IHttpContextAccessor httpContextAccessor,
    ILogger<UserService> logger)
{
    /// <summary>
    /// 创建 canonical User，并建立请求中的初始 KnowledgeRole assignment。
    /// </summary>
    /// <remarks>
    /// User 创建与初始 assignment 写入位于同一 transaction。请求中的 KnowledgeRole 必须存在且 Active；
    /// 失败通过 <see cref="UserWriteResult"/> 表达，不以 validation 或 duplicate 情形抛出 CLR exception。
    /// </remarks>
    /// <param name="request">待创建的 Profile、可选初始 assignment 与显式操作人标签。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步操作完成后，返回创建后的详情，或明确的 validation、duplicate、reference failure。</returns>
    public async Task<UserWriteResult> CreateUser(
        CreateUserCommand request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateUserInput(request.DisplayName, request.Actor, request.KnowledgeRoleIds, out var roleIds);
        var setup = request.LoginSetup;
        var setupType = setup?.Type;
        var username = string.Empty;
        var normalizedUsername = string.Empty;

        if (setup is null)
        {
            errors["loginSetup.type"] = ["请选择登录方式。"];
        }
        else
        {
            switch (setupType)
            {
                case "local":
                    if (!LocalCredentialSecurity.TryNormalizeUsername(setup.Username, out username, out normalizedUsername))
                    {
                        errors["loginSetup.username"] = ["登录用户名必须为 3～64 个字符，且只能包含字母、数字、点、下划线、连字符或 @。"];
                    }
                    if (!LocalCredentialSecurity.IsValidPassword(setup.InitialPassword))
                    {
                        errors["loginSetup.initialPassword"] = ["初始密码长度必须为 8～128 个字符。"];
                    }
                    if (setup.Provider is not null || setup.Subject is not null)
                    {
                        errors["loginSetup"] = ["本地账号不能携带企业统一登录字段。"];
                    }
                    break;
                case "oidc":
                    if (setup.Username is not null || setup.InitialPassword is not null)
                    {
                        errors["loginSetup"] = ["企业统一登录不能携带本地账号或密码字段。"];
                    }
                    if (string.IsNullOrWhiteSpace(oidcOptions.Value.Provider))
                    {
                        errors["loginSetup.provider"] = ["当前服务器未配置可用的身份提供方。"];
                    }
                    else if (!string.Equals(setup.Provider, oidcOptions.Value.Provider, StringComparison.Ordinal))
                    {
                        errors["loginSetup.provider"] = ["身份提供方不在当前服务器允许范围内。"];
                    }
                    if (string.IsNullOrWhiteSpace(setup.Subject))
                    {
                        errors["loginSetup.subject"] = ["请输入 Subject / sub。"];
                    }
                    break;
                case "none":
                    if (setup.Username is not null || setup.InitialPassword is not null
                        || setup.Provider is not null || setup.Subject is not null)
                    {
                        errors["loginSetup"] = ["暂不配置登录时不能携带登录凭据字段。"];
                    }
                    break;
                default:
                    errors["loginSetup.type"] = ["登录方式无效。"];
                    break;
            }
        }
        if (errors.Count > 0)
        {
            return new UserWriteResult(null, errors, UserWriteFailure.Validation);
        }

        var unavailableRoles = await GetUnavailableRoleIds(roleIds, new HashSet<long>(), cancellationToken);
        if (unavailableRoles.Length > 0)
        {
            return KnowledgeRoleUnavailable(unavailableRoles);
        }

        var employeeNo = NormalizeOptional(request.EmployeeNo);
        var email = NormalizeOptional(request.Email);
        var duplicateErrors = await GetDuplicateUserFields(employeeNo, email, null, cancellationToken);
        if (duplicateErrors.Count > 0)
        {
            return new UserWriteResult(null, duplicateErrors, UserWriteFailure.Duplicate);
        }
        if (setupType == "local"
            && await dbContext.LocalLoginCredentials.AnyAsync(
                credential => credential.NormalizedUsername == normalizedUsername,
                cancellationToken))
        {
            return new UserWriteResult(
                null,
                new Dictionary<string, string[]> { ["loginSetup.username"] = ["登录用户名已存在。"] },
                UserWriteFailure.Duplicate);
        }
        if (setupType == "oidc"
            && await dbContext.LoginIdentities.AnyAsync(
                identity => identity.Provider == setup!.Provider && identity.Subject == setup.Subject,
                cancellationToken))
        {
            return new UserWriteResult(
                null,
                new Dictionary<string, string[]> { ["loginSetup.subject"] = ["Provider 与 Subject / sub 映射已存在。"] },
                UserWriteFailure.Duplicate);
        }

        var timestamp = DateTimeOffset.UtcNow;
        var user = new UserEntity
        {
            EmployeeNo = employeeNo,
            DisplayName = request.DisplayName.Trim(),
            Email = email,
            DepartmentOrTeam = NormalizeOptional(request.DepartmentOrTeam),
            JobTitle = NormalizeOptional(request.JobTitle),
            AccessLevel = AccessLevel.Viewer,
            IsActive = true,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        };
        LocalLoginCredential? createdCredential = null;
        LoginIdentity? createdIdentity = null;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
            foreach (var roleId in roleIds)
            {
                dbContext.UserKnowledgeRoles.Add(new UserKnowledgeRole
                {
                    UserId = user.Id,
                    KnowledgeRoleId = roleId,
                });
            }

            if (setupType == "local")
            {
                createdCredential = new LocalLoginCredential
                {
                    UserId = user.Id,
                    Username = username,
                    NormalizedUsername = normalizedUsername,
                    IsActive = true,
                    MustChangePassword = true,
                    FailedLoginAttempts = 0,
                    FailedLoginWindowStartedAt = null,
                    LockedUntil = null,
                    SessionVersion = 1,
                    CreatedAt = timestamp,
                    UpdatedAt = timestamp,
                    LastPasswordChangedAt = timestamp,
                    Version = 1,
                };
                createdCredential.PasswordHash = passwords.Hash(createdCredential, setup!.InitialPassword!);
                dbContext.LocalLoginCredentials.Add(createdCredential);
            }
            else if (setupType == "oidc")
            {
                createdIdentity = new LoginIdentity
                {
                    UserId = user.Id,
                    Provider = setup!.Provider!,
                    Subject = setup.Subject!,
                    IsActive = true,
                    CreatedAt = timestamp,
                    UpdatedAt = timestamp,
                    Version = 1,
                };
                dbContext.LoginIdentities.Add(createdIdentity);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new UserWriteResult(
                null,
                setupType switch
                {
                    "local" => new Dictionary<string, string[]> { ["loginSetup.username"] = ["用户资料或登录用户名已存在。"] },
                    "oidc" => new Dictionary<string, string[]> { ["loginSetup.subject"] = ["用户资料或 Provider 与 Subject / sub 映射已存在。"] },
                    _ => new Dictionary<string, string[]> { ["user"] = ["工号或邮箱已存在。"] },
                },
                UserWriteFailure.Duplicate);
        }

        if (createdCredential is not null)
        {
            LogSecurityEvent(
                "LocalCredentialCreated",
                user.Id,
                createdCredential.Id,
                null,
                "success",
                "created",
                timestamp);
        }
        else if (createdIdentity is not null)
        {
            LogSecurityEvent(
                "LoginIdentityCreated",
                user.Id,
                null,
                createdIdentity.Id,
                "success",
                "created",
                timestamp);
        }

        var response = await queries.GetUser(user.Id, cancellationToken);
        return new UserWriteResult(response, null, UserWriteFailure.None);
    }

    /// <summary>
    /// 更新 canonical User Profile，并以请求集合替换当前 KnowledgeRole assignment。
    /// </summary>
    /// <remarks>
    /// 该 Use Case 要求最近读取的 opaque concurrencyToken；有效但已过期的 token 返回 Conflict，不能覆盖较新的修改。
    /// 新 assignment 必须引用 Active KnowledgeRole；已经存在的 inactive assignment 可被保留。
    /// </remarks>
    /// <param name="request">目标 User、完整 Profile、目标 assignment 集合与 opaque concurrency token。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步操作完成后，返回更新后的详情，或明确的 validation、not found、conflict、duplicate 或 reference failure。</returns>
    public async Task<UserWriteResult> UpdateUser(
        UpdateUserCommand request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateUserInput(request.DisplayName, request.Actor, request.KnowledgeRoleIds, out var roleIds);
        ValidateToken(request.ConcurrencyToken, errors, out var expectedVersion);
        if (errors.Count > 0)
        {
            return new UserWriteResult(null, errors, UserWriteFailure.Validation);
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return new UserWriteResult(null, null, UserWriteFailure.NotFound);
        }
        if (user.Version != expectedVersion)
        {
            return new UserWriteResult(null, null, UserWriteFailure.Conflict);
        }

        var existingAssignments = await dbContext.UserKnowledgeRoles
            .Where(item => item.UserId == request.UserId)
            .ToArrayAsync(cancellationToken);
        var existingRoleIds = existingAssignments.Select(item => item.KnowledgeRoleId).ToHashSet();
        var unavailableRoles = await GetUnavailableRoleIds(roleIds, existingRoleIds, cancellationToken);
        if (unavailableRoles.Length > 0)
        {
            return KnowledgeRoleUnavailable(unavailableRoles);
        }

        var employeeNo = NormalizeOptional(request.EmployeeNo);
        var email = NormalizeOptional(request.Email);
        var duplicateErrors = await GetDuplicateUserFields(employeeNo, email, request.UserId, cancellationToken);
        if (duplicateErrors.Count > 0)
        {
            return new UserWriteResult(null, duplicateErrors, UserWriteFailure.Duplicate);
        }

        user.EmployeeNo = employeeNo;
        user.DisplayName = request.DisplayName.Trim();
        user.Email = email;
        user.DepartmentOrTeam = NormalizeOptional(request.DepartmentOrTeam);
        user.JobTitle = NormalizeOptional(request.JobTitle);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.Version = expectedVersion + 1;

        var requestedRoleIds = roleIds.ToHashSet();
        dbContext.UserKnowledgeRoles.RemoveRange(
            existingAssignments.Where(item => !requestedRoleIds.Contains(item.KnowledgeRoleId)));
        foreach (var roleId in requestedRoleIds.Except(existingRoleIds))
        {
            dbContext.UserKnowledgeRoles.Add(new UserKnowledgeRole
            {
                UserId = user.Id,
                KnowledgeRoleId = roleId,
            });
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new UserWriteResult(null, null, UserWriteFailure.Conflict);
        }
        catch (DbUpdateException)
        {
            return new UserWriteResult(
                null,
                new Dictionary<string, string[]> { ["user"] = ["工号或邮箱已存在。"] },
                UserWriteFailure.Duplicate);
        }

        var response = await queries.GetUser(user.Id, cancellationToken);
        return new UserWriteResult(response, null, UserWriteFailure.None);
    }

    /// <summary>
    /// 显式激活或停用 canonical User，同时保留其 Profile 与既有引用。
    /// </summary>
    /// <remarks>
    /// 该 Use Case 要求最近读取的 opaque concurrencyToken；stale token 返回 Conflict。同一状态重复提交返回 NoChange，
    /// 不将停用解释为删除。
    /// </remarks>
    /// <param name="request">目标 User、所需 Active 状态、显式操作人标签与 opaque concurrency token。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步操作完成后，返回状态变更后的详情，或明确的 validation、not found、conflict 或 no-change 结果。</returns>
    public async Task<UserWriteResult> SetUserActiveState(
        SetUserActiveStateCommand request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateActor(request.Actor);
        ValidateToken(request.ConcurrencyToken, errors, out var expectedVersion);
        if (errors.Count > 0)
        {
            return new UserWriteResult(null, errors, UserWriteFailure.Validation);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return new UserWriteResult(null, null, UserWriteFailure.NotFound);
        }
        if (user.Version != expectedVersion)
        {
            return new UserWriteResult(null, null, UserWriteFailure.Conflict);
        }
        if (user.IsActive == request.IsActive)
        {
            return new UserWriteResult(null, null, UserWriteFailure.NoChange);
        }
        if (!request.IsActive
            && await usableAdministrators.IsUserUsableAsync(user.Id, cancellationToken)
            && !await usableAdministrators.HasAnyAsync(excludedUserId: user.Id, cancellationToken: cancellationToken))
        {
            return new UserWriteResult(null, null, UserWriteFailure.LastUsableAdministrator);
        }

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.Version = expectedVersion + 1;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new UserWriteResult(null, null, UserWriteFailure.Conflict);
        }

        var response = await queries.GetUser(user.Id, cancellationToken);
        return new UserWriteResult(response, null, UserWriteFailure.None);
    }

    /// <summary>独立修改 User 的 AccessLevel，并保护至少一个可登录 Active Administrator。</summary>
    public async Task<UserWriteResult> SetUserAccessLevel(SetUserAccessLevelCommand request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateToken(request.ConcurrencyToken, errors, out var expectedVersion);
        if (errors.Count > 0) return new UserWriteResult(null, errors, UserWriteFailure.Validation);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null) return new UserWriteResult(null, null, UserWriteFailure.NotFound);
        if (user.Version != expectedVersion) return new UserWriteResult(null, null, UserWriteFailure.Conflict);
        if (user.AccessLevel == request.AccessLevel) return new UserWriteResult(null, null, UserWriteFailure.NoChange);
        if (user.AccessLevel == AccessLevel.Administrator && request.AccessLevel != AccessLevel.Administrator
            && await usableAdministrators.IsUserUsableAsync(user.Id, cancellationToken)
            && !await usableAdministrators.HasAnyAsync(excludedUserId: user.Id, cancellationToken: cancellationToken))
        {
            return new UserWriteResult(null, null, UserWriteFailure.LastUsableAdministrator);
        }

        user.AccessLevel = request.AccessLevel;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.Version = expectedVersion + 1;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new UserWriteResult(null, null, UserWriteFailure.Conflict);
        }

        return new UserWriteResult(await queries.GetUser(user.Id, cancellationToken), null, UserWriteFailure.None);
    }

    public async Task<IReadOnlyList<LoginIdentityResponse>> GetLoginIdentities(long userId, CancellationToken cancellationToken)
    {
        var identities = await dbContext.LoginIdentities.AsNoTracking().Where(item => item.UserId == userId)
            .OrderBy(item => item.Provider).ThenBy(item => item.Subject).ToArrayAsync(cancellationToken);
        return identities.Select(ToLoginIdentityResponse).ToArray();
    }

    public async Task<LoginIdentityWriteResult> CreateLoginIdentity(CreateLoginIdentityCommand request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(request.UserId)) errors["userId"] = ["用户 ID 必须是 JavaScript 安全范围内的正整数。"];
        var approvedProvider = oidcOptions.Value.Provider;
        if (string.IsNullOrWhiteSpace(approvedProvider))
        {
            errors["provider"] = ["当前服务器未配置可用的身份提供方。"];
        }
        else if (!string.Equals(request.Provider, approvedProvider, StringComparison.Ordinal))
        {
            errors["provider"] = ["身份提供方不在当前服务器允许范围内。"];
        }
        if (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Length > 240)
        {
            errors["subject"] = ["Subject / sub 必须为 1～240 个字符。"];
        }
        if (errors.Count > 0)
        {
            LogSecurityEvent("LoginIdentityCreated", request.UserId, null, null, "rejected", "validation_failed", DateTimeOffset.UtcNow);
            return new LoginIdentityWriteResult(null, errors, LoginIdentityWriteFailure.Validation);
        }
        if (!await dbContext.Users.AnyAsync(item => item.Id == request.UserId, cancellationToken))
        {
            LogSecurityEvent("LoginIdentityCreated", request.UserId, null, null, "rejected", "user_not_found", DateTimeOffset.UtcNow);
            return new LoginIdentityWriteResult(null, null, LoginIdentityWriteFailure.NotFound);
        }
        var timestamp = DateTimeOffset.UtcNow;
        var identity = new LoginIdentity
        {
            UserId = request.UserId,
            Provider = request.Provider,
            Subject = request.Subject,
            IsActive = true,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        };
        dbContext.LoginIdentities.Add(identity);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            LogSecurityEvent("LoginIdentityCreated", request.UserId, null, null, "rejected", "mapping_duplicate", DateTimeOffset.UtcNow);
            return new LoginIdentityWriteResult(null, new Dictionary<string, string[]> { ["subject"] = ["Provider 和 Subject / sub 映射已存在。"] }, LoginIdentityWriteFailure.Duplicate);
        }
        LogSecurityEvent("LoginIdentityCreated", request.UserId, null, identity.Id, "success", "created", timestamp);
        return new LoginIdentityWriteResult(ToLoginIdentityResponse(identity), null, LoginIdentityWriteFailure.None);
    }

    public async Task<LoginIdentityWriteResult> SetLoginIdentityActiveState(SetLoginIdentityActiveStateCommand request, CancellationToken cancellationToken)
    {
        var eventType = request.IsActive ? "LoginIdentityEnabled" : "LoginIdentityDisabled";
        var errors = new Dictionary<string, string[]>();
        ValidateToken(request.ConcurrencyToken, errors, out var expectedVersion);
        if (errors.Count > 0)
        {
            LogSecurityEvent(eventType, request.UserId, null, request.LoginIdentityId, "rejected", "invalid_concurrency_token", DateTimeOffset.UtcNow);
            return new LoginIdentityWriteResult(null, errors, LoginIdentityWriteFailure.Validation);
        }
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var identity = await dbContext.LoginIdentities.SingleOrDefaultAsync(item => item.Id == request.LoginIdentityId && item.UserId == request.UserId, cancellationToken);
        if (identity is null)
        {
            LogSecurityEvent(eventType, request.UserId, null, request.LoginIdentityId, "rejected", "identity_not_found", DateTimeOffset.UtcNow);
            return new LoginIdentityWriteResult(null, null, LoginIdentityWriteFailure.NotFound);
        }
        if (identity.Version != expectedVersion)
        {
            LogSecurityEvent(eventType, request.UserId, null, identity.Id, "rejected", "concurrency_conflict", DateTimeOffset.UtcNow);
            return new LoginIdentityWriteResult(null, null, LoginIdentityWriteFailure.Conflict);
        }
        if (identity.IsActive == request.IsActive)
        {
            LogSecurityEvent(eventType, request.UserId, null, identity.Id, "rejected", "state_unchanged", DateTimeOffset.UtcNow);
            return new LoginIdentityWriteResult(null, null, LoginIdentityWriteFailure.NoChange);
        }
        if (!request.IsActive
            && await usableAdministrators.IsLoginIdentityUsableAdministratorAccessAsync(identity.Id, cancellationToken)
            && !await usableAdministrators.HasAnyAsync(excludedLoginIdentityId: identity.Id, cancellationToken: cancellationToken))
        {
            LogSecurityEvent(eventType, request.UserId, null, identity.Id, "rejected", "last_usable_administrator", DateTimeOffset.UtcNow);
            return new LoginIdentityWriteResult(null, null, LoginIdentityWriteFailure.LastUsableAdministrator);
        }
        identity.IsActive = request.IsActive; identity.UpdatedAt = DateTimeOffset.UtcNow; identity.Version = expectedVersion + 1;
        try { await dbContext.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            LogSecurityEvent(eventType, request.UserId, null, identity.Id, "rejected", "concurrency_conflict", DateTimeOffset.UtcNow);
            return new LoginIdentityWriteResult(null, null, LoginIdentityWriteFailure.Conflict);
        }
        LogSecurityEvent(eventType, request.UserId, null, identity.Id, "success", request.IsActive ? "enabled" : "disabled", identity.UpdatedAt);
        return new LoginIdentityWriteResult(ToLoginIdentityResponse(identity), null, LoginIdentityWriteFailure.None);
    }

    /// <summary>
    /// 创建可供新 User assignment 选择的 KnowledgeRole。
    /// </summary>
    /// <param name="request">名称、可选说明与显式操作人标签。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步操作完成后，返回创建后的详情，或明确的 validation 或 duplicate 结果。</returns>
    public async Task<KnowledgeRoleWriteResult> CreateKnowledgeRole(
        CreateKnowledgeRoleCommand request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateRoleInput(request.Name, request.Actor);
        if (errors.Count > 0)
        {
            return new KnowledgeRoleWriteResult(null, errors, KnowledgeRoleWriteFailure.Validation);
        }

        var name = request.Name.Trim();
        if (await dbContext.KnowledgeRoles.AnyAsync(role => role.Name == name, cancellationToken))
        {
            return DuplicateRole();
        }

        var timestamp = DateTimeOffset.UtcNow;
        var role = new KnowledgeRole
        {
            Name = name,
            Description = NormalizeOptional(request.Description),
            IsActive = true,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        };
        dbContext.KnowledgeRoles.Add(role);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return DuplicateRole();
        }

        var response = await queries.GetKnowledgeRole(role.Id, cancellationToken);
        return new KnowledgeRoleWriteResult(response, null, KnowledgeRoleWriteFailure.None);
    }

    /// <summary>
    /// 更新 KnowledgeRole 的名称和说明。
    /// </summary>
    /// <remarks>
    /// 该 Use Case 要求最近读取的 opaque concurrencyToken；stale token 返回 Conflict，不能覆盖较新的修改。
    /// </remarks>
    /// <param name="request">目标 KnowledgeRole、更新后的内容、显式操作人标签与 opaque concurrency token。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步操作完成后，返回更新后的详情，或明确的 validation、not found、conflict 或 duplicate 结果。</returns>
    public async Task<KnowledgeRoleWriteResult> UpdateKnowledgeRole(
        UpdateKnowledgeRoleCommand request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateRoleInput(request.Name, request.Actor);
        ValidateToken(request.ConcurrencyToken, errors, out var expectedVersion);
        if (errors.Count > 0)
        {
            return new KnowledgeRoleWriteResult(null, errors, KnowledgeRoleWriteFailure.Validation);
        }

        var role = await dbContext.KnowledgeRoles.SingleOrDefaultAsync(item => item.Id == request.KnowledgeRoleId, cancellationToken);
        if (role is null)
        {
            return new KnowledgeRoleWriteResult(null, null, KnowledgeRoleWriteFailure.NotFound);
        }
        if (role.Version != expectedVersion)
        {
            return new KnowledgeRoleWriteResult(null, null, KnowledgeRoleWriteFailure.Conflict);
        }

        var name = request.Name.Trim();
        if (await dbContext.KnowledgeRoles.AnyAsync(
                item => item.Id != request.KnowledgeRoleId && item.Name == name,
                cancellationToken))
        {
            return DuplicateRole();
        }

        role.Name = name;
        role.Description = NormalizeOptional(request.Description);
        role.UpdatedAt = DateTimeOffset.UtcNow;
        role.Version = expectedVersion + 1;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new KnowledgeRoleWriteResult(null, null, KnowledgeRoleWriteFailure.Conflict);
        }
        catch (DbUpdateException)
        {
            return DuplicateRole();
        }

        var response = await queries.GetKnowledgeRole(role.Id, cancellationToken);
        return new KnowledgeRoleWriteResult(response, null, KnowledgeRoleWriteFailure.None);
    }

    /// <summary>
    /// 显式切换 KnowledgeRole 是否可用于新 User assignment。
    /// </summary>
    /// <remarks>
    /// 停用不会删除既有 UserKnowledgeRole assignment。该 Use Case 要求最近读取的 opaque concurrencyToken；同一状态重复提交
    /// 返回 NoChange，stale token 返回 Conflict。
    /// </remarks>
    /// <param name="request">目标 KnowledgeRole、所需 Active 状态、显式操作人标签与 opaque concurrency token。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步操作完成后，返回状态变更后的详情，或明确的 validation、not found、conflict 或 no-change 结果。</returns>
    public async Task<KnowledgeRoleWriteResult> SetKnowledgeRoleActiveState(
        SetKnowledgeRoleActiveStateCommand request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateActor(request.Actor);
        ValidateToken(request.ConcurrencyToken, errors, out var expectedVersion);
        if (errors.Count > 0)
        {
            return new KnowledgeRoleWriteResult(null, errors, KnowledgeRoleWriteFailure.Validation);
        }

        var role = await dbContext.KnowledgeRoles.SingleOrDefaultAsync(item => item.Id == request.KnowledgeRoleId, cancellationToken);
        if (role is null)
        {
            return new KnowledgeRoleWriteResult(null, null, KnowledgeRoleWriteFailure.NotFound);
        }
        if (role.Version != expectedVersion)
        {
            return new KnowledgeRoleWriteResult(null, null, KnowledgeRoleWriteFailure.Conflict);
        }
        if (role.IsActive == request.IsActive)
        {
            return new KnowledgeRoleWriteResult(null, null, KnowledgeRoleWriteFailure.NoChange);
        }

        role.IsActive = request.IsActive;
        role.UpdatedAt = DateTimeOffset.UtcNow;
        role.Version = expectedVersion + 1;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new KnowledgeRoleWriteResult(null, null, KnowledgeRoleWriteFailure.Conflict);
        }

        var response = await queries.GetKnowledgeRole(role.Id, cancellationToken);
        return new KnowledgeRoleWriteResult(response, null, KnowledgeRoleWriteFailure.None);
    }

    private async Task<long[]> GetUnavailableRoleIds(
        IReadOnlyCollection<long> requestedRoleIds,
        IReadOnlySet<long> existingRoleIds,
        CancellationToken cancellationToken)
    {
        var newRoleIds = requestedRoleIds.Where(id => !existingRoleIds.Contains(id)).ToArray();
        if (newRoleIds.Length == 0)
        {
            return [];
        }

        var availableIds = await dbContext.KnowledgeRoles
            .AsNoTracking()
            .Where(role => newRoleIds.Contains(role.Id) && role.IsActive)
            .Select(role => role.Id)
            .ToArrayAsync(cancellationToken);
        return newRoleIds.Except(availableIds).ToArray();
    }

    private async Task<Dictionary<string, string[]>> GetDuplicateUserFields(
        string? employeeNo,
        string? email,
        long? excludingUserId,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (employeeNo is not null && await dbContext.Users.AnyAsync(
                user => user.Id != excludingUserId && user.EmployeeNo == employeeNo,
                cancellationToken))
        {
            errors["employeeNo"] = ["工号已存在。"]; 
        }
        if (email is not null && await dbContext.Users.AnyAsync(
                user => user.Id != excludingUserId && user.Email == email,
                cancellationToken))
        {
            errors["email"] = ["邮箱已存在。"]; 
        }
        return errors;
    }

    private void ValidateToken(
        string token,
        IDictionary<string, string[]> errors,
        out long expectedVersion)
    {
        if (!concurrencyTokenCodec.TryDecode(token, out expectedVersion))
        {
            errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"]; 
        }
    }

    private static Dictionary<string, string[]> ValidateUserInput(
        string displayName,
        UserActorContext actor,
        IReadOnlyList<long>? roleIds,
        out long[] normalizedRoleIds)
    {
        var errors = ValidateActor(actor);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            errors["displayName"] = ["姓名不能为空。"]; 
        }

        normalizedRoleIds = (roleIds ?? []).Distinct().ToArray();
        if (normalizedRoleIds.Any(id => !ApiIdParser.IsSafePositive(id)))
        {
            errors["knowledgeRoleIds"] = ["知识身份 ID 必须是 JavaScript 安全范围内的正整数。"]; 
        }
        return errors;
    }

    private static Dictionary<string, string[]> ValidateRoleInput(
        string name,
        UserActorContext actor)
    {
        var errors = ValidateActor(actor);
        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["知识身份名称不能为空。"]; 
        }
        return errors;
    }

    private static Dictionary<string, string[]> ValidateActor(UserActorContext actor)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(actor.DisplayName))
        {
            errors["actor.displayName"] = ["操作人姓名不能为空。"]; 
        }
        return errors;
    }

    private static UserWriteResult KnowledgeRoleUnavailable(IReadOnlyCollection<long> roleIds) => new(
        null,
        new Dictionary<string, string[]>
        {
            ["knowledgeRoleIds"] = [$"知识身份不存在或已停用：{string.Join(", ", roleIds)}。"],
        },
        UserWriteFailure.InactiveKnowledgeRole);

    private static KnowledgeRoleWriteResult DuplicateRole() => new(
        null,
        new Dictionary<string, string[]> { ["name"] = ["知识身份名称已存在。"] },
        KnowledgeRoleWriteFailure.Duplicate);

    private LoginIdentityResponse ToLoginIdentityResponse(LoginIdentity identity) => new(
        identity.Id, identity.UserId, identity.Provider, identity.Subject, identity.IsActive,
        identity.CreatedAt, identity.UpdatedAt, concurrencyTokenCodec.Encode(identity.Version));

    private void LogSecurityEvent(
        string eventType,
        long targetUserId,
        long? credentialId,
        long? loginIdentityId,
        string result,
        string reasonCode,
        DateTimeOffset occurredAt)
    {
        long? actorUserId = null;
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.User is not null
            && AuthenticationSessionDescriptorReader.TryRead(httpContext.User, out var descriptor))
        {
            actorUserId = descriptor.UserId;
        }

        logger.LogInformation(
            "SecurityEvent EventType={EventType} ActorUserId={ActorUserId} TargetUserId={TargetUserId} CredentialId={CredentialId} LoginIdentityId={LoginIdentityId} Result={Result} ReasonCode={ReasonCode} OccurredAt={OccurredAt} CorrelationId={CorrelationId}",
            eventType,
            actorUserId,
            targetUserId,
            credentialId,
            loginIdentityId,
            result,
            reasonCode,
            occurredAt,
            httpContext?.TraceIdentifier);
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
