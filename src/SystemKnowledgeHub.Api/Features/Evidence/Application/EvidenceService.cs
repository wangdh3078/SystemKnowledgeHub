using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Evidence.Application.Models;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;
using EvidenceEntity = SystemKnowledgeHub.Api.Features.Evidence.Domain.Evidence;

namespace SystemKnowledgeHub.Api.Features.Evidence.Application;

public sealed class EvidenceService(
    KnowledgeHubDbContext dbContext,
    EvidenceSubjectResolver subjectResolver,
    EvidenceQueries queries,
    ConcurrencyTokenCodec concurrencyTokenCodec)
{
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

    public async Task<EvidenceCommandResult> AddHumanConfirmation(
        AddHumanConfirmationCommand request,
        CancellationToken cancellationToken)
    {
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
        ValidatePerson(request.Confirmer, "confirmer", errors);
        if (errors.Count > 0)
        {
            return new EvidenceCommandResult(null, errors, EvidenceFailure.Validation);
        }

        _ = TryParseSubject(request.Subject!, errors, out var subjectType);
        var subjectContext = await subjectResolver.Resolve(subjectType, request.Subject!.Id, cancellationToken);
        if (subjectContext is null)
        {
            return new EvidenceCommandResult(null, null, EvidenceFailure.SubjectNotFound);
        }

        var locatorJson = JsonSerializer.Serialize(new
        {
            confirmationStatement = request.ConfirmationStatement.Trim(),
            sourceNote = NormalizeOptional(request.SourceNote),
        });
        var timestamp = DateTimeOffset.UtcNow;
        var item = CreateEvidence(
            EvidenceType.HumanConfirmation,
            subjectType,
            request.Subject.Id,
            request.SubjectDetailKey,
            $"人工确认 · {request.Confirmer!.DisplayName.Trim()}",
            NormalizeOptional(request.SourceNote),
            locatorJson,
            request.ConfirmationStatement,
            request.SupportReason,
            null,
            request.Confirmer,
            timestamp);
        dbContext.Evidence.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new EvidenceCommandResult(
            CreateAddResponse(item, subjectContext.KnowledgeStatus.ToString()),
            null,
            EvidenceFailure.None);
    }

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
}
