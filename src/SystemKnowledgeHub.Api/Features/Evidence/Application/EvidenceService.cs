using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Evidence.Application.Models;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;
using EvidenceEntity = SystemKnowledgeHub.Api.Features.Evidence.Domain.Evidence;

namespace SystemKnowledgeHub.Api.Features.Evidence.Application;

/// <summary>执行普通 Evidence 创建、C24 correction 与 C25 HumanConfirmation 写入的应用服务。</summary>
public sealed class EvidenceService(
    KnowledgeHubDbContext dbContext,
    EvidenceSubjectResolver subjectResolver,
    EvidenceQueries queries,
    ConcurrencyTokenCodec concurrencyTokenCodec)
{
    private const string KnowledgeRoleFallback = "知识提供者（未配置知识身份）";

    /// <summary>保存普通 Evidence 及其客户端提供的 Provider Snapshot。</summary>
    /// <remarks>保存可为后续显式 KnowledgeStatus 推进提供依据，但本操作不改变 Subject 的 KnowledgeStatus。</remarks>
    /// <param name="request">普通 Evidence 的来源、支持理由和人员事实输入。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步完成后返回创建结果；字段无效或 Subject 不可用时以 <see cref="EvidenceCommandResult.Failure"/> 表达。</returns>
    public async Task<EvidenceCommandResult> AddEvidence(
        AddEvidenceCommand request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateEvidence(
            request.EvidenceType,
            request.Subject,
            request.SourceTitle,
            request.SourceReference,
            request.SourceLocator,
            request.SupportReason,
            request.Confidence,
            request.Provider,
            allowHumanConfirmation: false,
            out var evidenceType,
            out var subjectType,
            out var confidence,
            out var sourceLocatorJson);
        if (errors.Count > 0)
        {
            return new EvidenceCommandResult(null, errors, EvidenceFailure.Validation);
        }

        var subjectContext = await subjectResolver.Resolve(subjectType, request.Subject!.Id, cancellationToken);
        if (subjectContext is null)
        {
            return new EvidenceCommandResult(null, null, EvidenceFailure.SubjectNotFound);
        }

        var timestamp = DateTimeOffset.UtcNow;
        var item = CreateEvidence(
            evidenceType,
            subjectType,
            request.Subject.Id,
            request.SubjectDetailKey,
            request.SourceTitle,
            request.SourceReference,
            sourceLocatorJson,
            request.Summary,
            request.SupportReason,
            confidence,
            request.Provider!,
            timestamp);
        dbContext.Evidence.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new EvidenceCommandResult(
            CreateAddResponse(item, subjectContext.KnowledgeStatus.ToString()),
            null,
            EvidenceFailure.None);
    }

    /// <summary>将当前可信操作者的确认事实追加为 HumanConfirmation Evidence。</summary>
    /// <remarks>
    /// 在同一事务内重新读取 canonical User、解析或验证 KnowledgeRole、验证 Subject、物化历史 Snapshot 并插入 Evidence。
    /// 创建不会自动推进 KnowledgeStatus；角色数量为 0/1/多个时分别使用 fallback、自动采用唯一角色、或要求显式选择。
    /// </remarks>
    /// <param name="request">由 API 从 Current User 注入 canonical User ID 的确认事实输入。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步完成后返回创建结果；Current User、角色或 Subject 不满足条件时以失败分类表达。</returns>
    public async Task<EvidenceCommandResult> AddHumanConfirmation(
        AddHumanConfirmationCommand request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var errors = new Dictionary<string, string[]>();
        if (request.Subject is null)
        {
            errors["subject"] = ["必须选择一个明确的知识对象。"];
        }
        else if (!TryParseSubject(request.Subject, errors, out _))
        {
            // Error recorded by TryParseSubject.
        }
        if (string.IsNullOrWhiteSpace(request.ConfirmationStatement))
        {
            errors["confirmationStatement"] = ["确认结论不能为空。"];
        }
        if (string.IsNullOrWhiteSpace(request.SupportReason))
        {
            errors["supportReason"] = ["请说明人工确认为什么支持当前知识。"];
        }
        if (!IsConfirmationMethod(request.ConfirmationMethod))
        {
            errors["confirmationMethod"] = ["确认方式无效。"];
        }
        if (request.ConfirmedAt is null || request.ConfirmedAt == default)
        {
            errors["confirmedAt"] = ["确认时间不能为空。"];
        }
        if (request.KnowledgeRoleId.HasValue && !ApiIdParser.IsSafePositive(request.KnowledgeRoleId.Value))
        {
            errors["knowledgeRoleId"] = ["知识身份 ID 必须是 JavaScript 安全范围内的正整数。"];
        }
        if (errors.Count > 0)
        {
            return new EvidenceCommandResult(null, errors, EvidenceFailure.Validation);
        }

        var currentUser = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == request.CurrentUserId)
            .Select(user => new
            {
                user.Id,
                user.EmployeeNo,
                user.DisplayName,
                user.DepartmentOrTeam,
                user.JobTitle,
                user.IsActive,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (currentUser is null)
        {
            return new EvidenceCommandResult(null, null, EvidenceFailure.CurrentUserNotFound);
        }
        if (!currentUser.IsActive)
        {
            return new EvidenceCommandResult(null, null, EvidenceFailure.CurrentUserInactive);
        }

        KnowledgeRoleSnapshot? selectedRole = null;
        if (request.KnowledgeRoleId.HasValue)
        {
            selectedRole = await dbContext.KnowledgeRoles
                .AsNoTracking()
                .Where(role => role.Id == request.KnowledgeRoleId.Value)
                .Select(role => new KnowledgeRoleSnapshot(role.Id, role.Name, role.IsActive))
                .SingleOrDefaultAsync(cancellationToken);
            if (selectedRole is null)
            {
                return new EvidenceCommandResult(null, null, EvidenceFailure.KnowledgeRoleNotFound);
            }
            if (!selectedRole.IsActive)
            {
                return new EvidenceCommandResult(null, null, EvidenceFailure.KnowledgeRoleInactive);
            }

            var assigned = await dbContext.UserKnowledgeRoles
                .AsNoTracking()
                .AnyAsync(
                    assignment => assignment.UserId == currentUser.Id
                        && assignment.KnowledgeRoleId == selectedRole.Id,
                    cancellationToken);
            if (!assigned)
            {
                return new EvidenceCommandResult(null, null, EvidenceFailure.KnowledgeRoleNotAssigned);
            }
        }
        else
        {
            var activeRoles = await (
                from assignment in dbContext.UserKnowledgeRoles.AsNoTracking()
                join role in dbContext.KnowledgeRoles.AsNoTracking()
                    on assignment.KnowledgeRoleId equals role.Id
                where assignment.UserId == currentUser.Id && role.IsActive
                orderby role.Name
                select new KnowledgeRoleSnapshot(role.Id, role.Name, role.IsActive))
                .Take(2)
                .ToArrayAsync(cancellationToken);

            if (activeRoles.Length == 1)
            {
                selectedRole = activeRoles[0];
            }
            else if (activeRoles.Length > 1)
            {
                return new EvidenceCommandResult(
                    null,
                    new Dictionary<string, string[]>
                    {
                        ["knowledgeRoleId"] = ["当前操作者有多个启用的知识身份，请选择本次确认身份。"],
                    },
                    EvidenceFailure.Validation);
            }
        }

        _ = TryParseSubject(request.Subject!, errors, out var subjectType);
        var subjectContext = await subjectResolver.Resolve(subjectType, request.Subject!.Id, cancellationToken);
        if (subjectContext is null)
        {
            return new EvidenceCommandResult(null, null, EvidenceFailure.SubjectNotFound);
        }

        var locatorJson = JsonSerializer.Serialize(new
        {
            confirmationMethod = request.ConfirmationMethod,
            confirmationStatement = request.ConfirmationStatement.Trim(),
            sourceNote = NormalizeOptional(request.SourceNote),
        });
        var timestamp = DateTimeOffset.UtcNow;
        var confirmedAt = request.ConfirmedAt!.Value.ToUniversalTime();
        var provider = new PersonSnapshotCommand(
            currentUser.DisplayName,
            selectedRole?.Name ?? KnowledgeRoleFallback,
            confirmedAt,
            currentUser.DepartmentOrTeam,
            null,
            null,
            null);
        var item = CreateEvidence(
            EvidenceType.HumanConfirmation,
            subjectType,
            request.Subject.Id,
            request.SubjectDetailKey,
            $"人工确认 · {currentUser.DisplayName.Trim()}",
            NormalizeOptional(request.SourceNote),
            locatorJson,
            request.ConfirmationStatement,
            request.SupportReason,
            null,
            provider,
            timestamp);
        item.ProviderUserId = currentUser.Id;
        item.ProviderKnowledgeRoleId = selectedRole?.Id;
        item.ProviderEmployeeNo = NormalizeOptional(currentUser.EmployeeNo);
        item.ProviderJobTitle = NormalizeOptional(currentUser.JobTitle);
        dbContext.Evidence.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new EvidenceCommandResult(
            CreateAddResponse(item, subjectContext.KnowledgeStatus.ToString()),
            null,
            EvidenceFailure.None);
    }

    /// <summary>显式纠正既有 Evidence 的可编辑内容和 Provider Snapshot。</summary>
    /// <remarks>这不是 canonical User 或 KnowledgeRole 的动态传播；<c>concurrencyToken</c> 过期时返回 Conflict。</remarks>
    /// <param name="request">包含 opaque concurrencyToken 的 correction 输入。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步完成后返回更新后的详情；不存在、验证失败或并发冲突通过结果分类返回。</returns>
    public async Task<EvidenceCommandResult> UpdateEvidence(
        UpdateEvidenceCommand request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.SourceTitle)) errors["sourceTitle"] = ["来源标题不能为空。"];
        if (string.IsNullOrWhiteSpace(request.SupportReason)) errors["supportReason"] = ["请说明该证据为什么支持当前知识。"];
        if (!TryNormalizeLocator(request.SourceReference, request.SourceLocator, errors, out var sourceLocatorJson)) { }
        _ = TryParseConfidence(request.Confidence, errors, out var confidence);
        ValidatePerson(request.Provider, "provider", errors);
        if (request.Actor is null || string.IsNullOrWhiteSpace(request.Actor.DisplayName)) errors["actor.displayName"] = ["操作人不能为空。"];
        if (!concurrencyTokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion)) errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"];
        if (errors.Count > 0)
        {
            return new EvidenceCommandResult(null, errors, EvidenceFailure.Validation);
        }

        var item = await dbContext.Evidence.SingleOrDefaultAsync(
            evidence => evidence.Id == request.EvidenceId,
            cancellationToken);
        if (item is null)
        {
            return new EvidenceCommandResult(null, null, EvidenceFailure.NotFound);
        }
        if (item.Version != expectedVersion)
        {
            return new EvidenceCommandResult(null, null, EvidenceFailure.Conflict);
        }

        item.SourceTitle = request.SourceTitle.Trim();
        item.SourceReference = NormalizeOptional(request.SourceReference);
        item.SourceLocatorJson = sourceLocatorJson;
        item.Summary = NormalizeOptional(request.Summary);
        item.SupportReason = request.SupportReason.Trim();
        item.Confidence = confidence;
        ApplyPerson(item, request.Provider!);
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.Version = expectedVersion + 1;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new EvidenceCommandResult(null, null, EvidenceFailure.Conflict);
        }

        var detail = await queries.GetEvidenceDetail(item.Id, cancellationToken);
        return new EvidenceCommandResult(detail.Response, null, detail.Failure);
    }

    private static EvidenceEntity CreateEvidence(
        EvidenceType evidenceType,
        EvidenceSubjectType subjectType,
        long subjectId,
        string? subjectDetailKey,
        string sourceTitle,
        string? sourceReference,
        string? sourceLocatorJson,
        string? summary,
        string supportReason,
        EvidenceConfidence? confidence,
        PersonSnapshotCommand provider,
        DateTimeOffset timestamp)
    {
        var item = new EvidenceEntity
        {
            EvidenceType = evidenceType,
            SubjectType = subjectType,
            SubjectId = subjectId,
            SubjectDetailKey = NormalizeOptional(subjectDetailKey),
            SourceTitle = sourceTitle.Trim(),
            SourceReference = NormalizeOptional(sourceReference),
            SourceLocatorJson = sourceLocatorJson,
            Summary = NormalizeOptional(summary),
            SupportReason = supportReason.Trim(),
            Confidence = confidence,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        };
        ApplyPerson(item, provider);
        return item;
    }

    private static void ApplyPerson(EvidenceEntity item, PersonSnapshotCommand person)
    {
        item.ProviderName = person.DisplayName.Trim();
        item.ProviderRole = person.RoleOrIdentity.Trim();
        item.ProviderTeam = NormalizeOptional(person.Team);
        item.ProviderExternalKey = NormalizeOptional(person.ExternalUserKey);
        item.ProviderSource = NormalizeOptional(person.Source);
        item.ProviderNote = NormalizeOptional(person.Note);
        item.ProvidedAt = person.OccurredAt;
    }

    private AddEvidenceResponse CreateAddResponse(EvidenceEntity item, string knowledgeStatus)
    {
        return new AddEvidenceResponse(
            item.Id,
            item.EvidenceType.ToString(),
            new EvidenceTargetResponse(item.SubjectType.ToString(), item.SubjectId),
            item.SubjectDetailKey,
            item.SourceTitle,
            knowledgeStatus,
            false,
            concurrencyTokenCodec.Encode(item.Version));
    }

    private static Dictionary<string, string[]> ValidateEvidence(
        string evidenceTypeValue,
        EvidenceTargetCommand? subject,
        string sourceTitle,
        string? sourceReference,
        JsonElement? sourceLocator,
        string supportReason,
        string? confidenceValue,
        PersonSnapshotCommand? provider,
        bool allowHumanConfirmation,
        out EvidenceType evidenceType,
        out EvidenceSubjectType subjectType,
        out EvidenceConfidence? confidence,
        out string? sourceLocatorJson)
    {
        var errors = new Dictionary<string, string[]>();
        evidenceType = default;
        subjectType = default;
        confidence = null;
        sourceLocatorJson = null;
        if (!Enum.TryParse(evidenceTypeValue, false, out evidenceType)
            || evidenceType.ToString() != evidenceTypeValue
            || (!allowHumanConfirmation && evidenceType == EvidenceType.HumanConfirmation))
        {
            errors["evidenceType"] = ["证据类型无效；人工确认请使用专用操作。"];
        }
        if (subject is null)
        {
            errors["subject"] = ["必须选择一个明确的知识对象。"];
        }
        else
        {
            _ = TryParseSubject(subject, errors, out subjectType);
        }
        if (string.IsNullOrWhiteSpace(sourceTitle)) errors["sourceTitle"] = ["来源标题不能为空。"];
        if (string.IsNullOrWhiteSpace(supportReason)) errors["supportReason"] = ["请说明该证据为什么支持当前知识。"];
        _ = TryNormalizeLocator(sourceReference, sourceLocator, errors, out sourceLocatorJson);
        _ = TryParseConfidence(confidenceValue, errors, out confidence);
        ValidatePerson(provider, "provider", errors);
        return errors;
    }

    private static bool TryParseSubject(
        EvidenceTargetCommand subject,
        IDictionary<string, string[]> errors,
        out EvidenceSubjectType subjectType)
    {
        subjectType = default;
        var validType = Enum.TryParse(subject.Type, false, out subjectType)
            && subjectType.ToString() == subject.Type;
        if (!validType) errors["subject.type"] = ["证据 Subject 类型无效。"];
        if (!ApiIdParser.IsSafePositive(subject.Id)) errors["subject.id"] = ["Subject ID 必须是 JavaScript 安全范围内的正整数。"];
        return validType && ApiIdParser.IsSafePositive(subject.Id);
    }

    private static bool TryParseConfidence(
        string? value,
        IDictionary<string, string[]> errors,
        out EvidenceConfidence? confidence)
    {
        confidence = null;
        var normalized = NormalizeOptional(value);
        if (normalized is null) return true;
        if (!Enum.TryParse<EvidenceConfidence>(normalized, false, out var parsed) || parsed.ToString() != normalized)
        {
            errors["confidence"] = ["证据可信度无效。"];
            return false;
        }
        confidence = parsed;
        return true;
    }

    private static bool IsConfirmationMethod(string value)
    {
        return value is "InSystem" or "OnSite" or "Meeting" or "Email" or "Document" or "Other";
    }

    private static bool TryNormalizeLocator(
        string? sourceReference,
        JsonElement? sourceLocator,
        IDictionary<string, string[]> errors,
        out string? sourceLocatorJson)
    {
        sourceLocatorJson = null;
        if (sourceLocator.HasValue && sourceLocator.Value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            if (sourceLocator.Value.ValueKind != JsonValueKind.Object || !sourceLocator.Value.EnumerateObject().Any())
            {
                errors["sourceLocator"] = ["来源定位必须是包含明确定位字段的对象。"];
                return false;
            }
            sourceLocatorJson = sourceLocator.Value.GetRawText();
        }
        if (string.IsNullOrWhiteSpace(sourceReference) && sourceLocatorJson is null)
        {
            errors["sourceReference"] = ["来源引用与来源定位至少填写一项。"];
            return false;
        }
        return true;
    }

    private static void ValidatePerson(
        PersonSnapshotCommand? person,
        string prefix,
        IDictionary<string, string[]> errors)
    {
        if (person is null)
        {
            errors[prefix] = ["必须记录人员快照。"];
            return;
        }
        if (string.IsNullOrWhiteSpace(person.DisplayName)) errors[$"{prefix}.displayName"] = ["人员名称不能为空。"];
        if (string.IsNullOrWhiteSpace(person.RoleOrIdentity)) errors[$"{prefix}.roleOrIdentity"] = ["角色或身份不能为空。"];
        if (person.OccurredAt == default) errors[$"{prefix}.occurredAt"] = ["发生时间不能为空。"];
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private sealed record KnowledgeRoleSnapshot(long Id, string Name, bool IsActive);
}
