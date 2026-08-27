using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Domain;
using SystemKnowledgeHub.Api.Features.BusinessRules.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Evidence.Application.Models;
using SystemKnowledgeHub.Api.Features.Integrations.Application;
using SystemKnowledgeHub.Api.Features.Integrations.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.UnknownItems.Application.Models;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.UnknownItems.Application;

public sealed class KnowledgeResolutionService(KnowledgeHubDbContext dbContext, ConcurrencyTokenCodec tokenCodec)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public Task<UnknownItemCommandResult> ApplyColumnKnownValue(
        ApplyColumnKnownValueCommand request, CancellationToken cancellationToken) =>
        ApplyColumn(request, "AddColumnKnownValue", cancellationToken);

    public Task<UnknownItemCommandResult> ApplyDatabaseColumnKnowledge(
        ApplyDatabaseColumnKnowledgeCommand request, CancellationToken cancellationToken) =>
        ApplyColumn(request, "UpdateDatabaseColumnKnowledge", cancellationToken);

    public async Task<UnknownItemCommandResult> ApplyBusinessFunction(
        ApplyBusinessFunctionCommand request, CancellationToken cancellationToken)
    {
        var errors = ValidateApply(request.Applier, request.ConcurrencyToken, request.TargetConcurrencyToken,
            out var itemVersion, out var targetVersion);
        if (!IsSafeId(request.BusinessFunctionId)) errors["businessFunctionId"] = ["业务功能 ID 无效。"]; 
        if (request.Overview is null) errors["overview"] = ["业务功能概览不能为空。"]; 
        else ValidateOverview(request.Overview, errors);
        if (errors.Count > 0) return Validation(errors);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var context = await LoadApplyContext(request.UnknownItemId, request.KnowledgeUpdateId, itemVersion, cancellationToken);
        if (context.Failure is not null) return context.Failure;
        var (item, update) = context.Value;
        if (update.TargetType != KnowledgeTargetType.BusinessFunction || update.TargetId != request.BusinessFunctionId)
            return Failure(UnknownItemFailure.ReferenceInvalid, "KnowledgeUpdate 与业务功能目标不匹配。");

        var function = await dbContext.BusinessFunctions.SingleOrDefaultAsync(value => value.Id == request.BusinessFunctionId, cancellationToken);
        if (function is null) return Failure(UnknownItemFailure.NotFound, "未找到业务功能。");
        if (function.SystemId != item.SystemId) return Failure(UnknownItemFailure.ReferenceInvalid, "业务功能不属于当前事项的系统上下文。");
        if (function.Version != targetVersion) return Conflict("业务功能已被修改，请刷新知识更新预览。");

        var overview = request.Overview!;
        var before = FunctionSnapshot(function);
        var after = FunctionSnapshot(overview);
        if (!JsonEquals(update.BeforeJson, before) || !JsonEquals(update.AfterJson, after))
            return Failure(UnknownItemFailure.Conflict, "KnowledgeUpdate Preview 已过期或与明确修改值不一致。");
        if (!Enum.TryParse<RewriteStatus>(overview.RewriteStatus, false, out var rewriteStatus)
            || rewriteStatus.ToString() != overview.RewriteStatus)
            return Validation(new Dictionary<string, string[]> { ["overview.rewriteStatus"] = ["RewriteStatus 无效。"] });
        var duplicate = await dbContext.BusinessFunctions.AsNoTracking().AnyAsync(value => value.SystemId == function.SystemId
            && value.Id != function.Id && value.Name.ToLower() == overview.Name.Trim().ToLower(), cancellationToken);
        if (duplicate) return Failure(UnknownItemFailure.Conflict, "同一系统中已存在同名业务功能。");

        var statusFailure = await ApplyKnowledgeStatus(function.KnowledgeStatus, request.KnowledgeStatusChange, update,
            EvidenceSubjectType.BusinessFunction, function.Id, request.Applier!,
            (status, reason) => SetKnowledgeStatus(function, status, reason, request.Applier!), cancellationToken);
        if (statusFailure is not null) return statusFailure;

        function.Name = overview.Name.Trim();
        function.DisplayName = Normalize(overview.DisplayName);
        function.FunctionType = overview.FunctionType.Trim();
        function.Purpose = Normalize(overview.Purpose);
        function.CallerSummary = Normalize(overview.Caller);
        function.InputDescription = Normalize(overview.Input);
        function.OutputDescription = Normalize(overview.Output);
        function.RewriteStatus = rewriteStatus;
        function.UpdatedAt = DateTimeOffset.UtcNow;
        function.Version = targetVersion + 1;

        return await CompleteApply(item, update, request.Applier!, itemVersion, function.KnowledgeStatus,
            "已应用业务功能概览更新", tokenCodec.Encode(function.Version), cancellationToken);
    }

    public async Task<UnknownItemCommandResult> ApplyBusinessRule(
        ApplyBusinessRuleCommand request, CancellationToken cancellationToken)
    {
        var errors = ValidateApply(request.Applier, request.ConcurrencyToken, request.TargetConcurrencyToken,
            out var itemVersion, out var targetVersion);
        if (!IsSafeId(request.BusinessRuleId)) errors["businessRuleId"] = ["业务规则 ID 无效。"]; 
        if (request.Rule is null) errors["rule"] = ["业务规则内容不能为空。"]; 
        else ValidateRule(request.Rule, errors);
        if (errors.Count > 0) return Validation(errors);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var context = await LoadApplyContext(request.UnknownItemId, request.KnowledgeUpdateId, itemVersion, cancellationToken);
        if (context.Failure is not null) return context.Failure;
        var (item, update) = context.Value;
        if (update.TargetType != KnowledgeTargetType.BusinessRule || update.TargetId != request.BusinessRuleId
            || ApplyAction(update) != "UpdateBusinessRule")
            return Failure(UnknownItemFailure.ReferenceInvalid, "KnowledgeUpdate 与业务规则目标或具体 Apply 操作不匹配。");

        var rule = await dbContext.BusinessRules.SingleOrDefaultAsync(value => value.Id == request.BusinessRuleId, cancellationToken);
        if (rule is null) return Failure(UnknownItemFailure.NotFound, "未找到业务规则。");
        if (rule.SystemId != item.SystemId) return Failure(UnknownItemFailure.ReferenceInvalid, "业务规则不属于当前事项的系统上下文。");
        if (rule.Version != targetVersion) return Conflict("业务规则已被修改，请刷新知识更新预览。");

        var requested = request.Rule!;
        var before = RuleSnapshot(rule);
        var after = RuleSnapshot(requested);
        if (!JsonEquals(update.BeforeJson, before) || !JsonEquals(update.AfterJson, after))
            return Conflict("KnowledgeUpdate Preview 已过期或与明确修改值不一致。");
        var duplicate = await dbContext.BusinessRules.AsNoTracking().AnyAsync(value => value.SystemId == rule.SystemId
            && value.Id != rule.Id && value.Name.ToLower() == requested.Name.Trim().ToLower(), cancellationToken);
        if (duplicate) return Failure(UnknownItemFailure.Conflict, "同一系统中已存在同名业务规则。");

        var statusFailure = await ApplyKnowledgeStatus(rule.KnowledgeStatus, request.KnowledgeStatusChange, update,
            EvidenceSubjectType.BusinessRule, rule.Id, request.Applier!,
            (status, reason) => SetKnowledgeStatus(rule, status, reason, request.Applier!), cancellationToken);
        if (statusFailure is not null) return statusFailure;

        rule.Name = requested.Name.Trim();
        rule.Description = requested.Description.Trim();
        rule.ConditionText = Normalize(requested.Condition);
        rule.ResultText = Normalize(requested.Result);
        rule.InputDataJson = JsonSerializer.Serialize((requested.InputData ?? []).Select(value => new
        {
            name = value.Name.Trim(),
            description = Normalize(value.Description),
        }), JsonOptions);
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        rule.Version = targetVersion + 1;
        return await CompleteApply(item, update, request.Applier!, itemVersion, rule.KnowledgeStatus,
            "已应用业务规则更新", tokenCodec.Encode(rule.Version), cancellationToken);
    }

    public async Task<UnknownItemCommandResult> ApplyIntegration(
        ApplyIntegrationCommand request, CancellationToken cancellationToken)
    {
        var errors = ValidateApply(request.Applier, request.ConcurrencyToken, request.TargetConcurrencyToken, out var itemVersion, out var targetVersion);
        if (!IsSafeId(request.IntegrationId)) errors["integrationId"] = ["集成关系 ID 无效。"];
        if (request.Integration is null) errors["integration"] = ["集成内容不能为空."];
        else await ValidateIntegration(request.Integration, errors, cancellationToken);
        if (errors.Count > 0) return Validation(errors);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var context = await LoadApplyContext(request.UnknownItemId, request.KnowledgeUpdateId, itemVersion, cancellationToken);
        if (context.Failure is not null) return context.Failure;
        var (item, update) = context.Value;
        if (update.TargetType != KnowledgeTargetType.Integration || update.TargetId != request.IntegrationId || ApplyAction(update) != "UpdateIntegration")
            return Failure(UnknownItemFailure.ReferenceInvalid, "KnowledgeUpdate 与集成关系目标或具体 Apply 操作不匹配。");
        var integration = await dbContext.Integrations.SingleOrDefaultAsync(value => value.Id == request.IntegrationId, cancellationToken);
        if (integration is null) return Failure(UnknownItemFailure.NotFound, "未找到集成关系。");
        if (integration.SourceSystemId != item.SystemId && integration.TargetSystemId != item.SystemId)
            return Failure(UnknownItemFailure.ReferenceInvalid, "集成关系不属于当前事项的系统上下文。");
        if (integration.Version != targetVersion) return Conflict("集成关系已被修改，请刷新知识更新预览。");

        var requested = request.Integration!;
        var before = IntegrationSnapshot(integration);
        var after = IntegrationSnapshot(requested);
        if (!JsonEquals(update.BeforeJson, before) || !JsonEquals(update.AfterJson, after))
            return Conflict("KnowledgeUpdate Preview 已过期或与明确修改值不一致。");
        var type = Enum.Parse<IntegrationType>(requested.IntegrationType, false);
        var direction = Enum.Parse<IntegrationFlowDirection>(requested.FlowDirection, false);
        IntegrationEndpointParser.TryParse(type, requested.Endpoint, out var endpoint, out var display, out _);
        var duplicate = await dbContext.Integrations.AsNoTracking().AnyAsync(value => value.Id != integration.Id && value.IntegrationType == type
            && value.Name == requested.Name.Trim() && value.SourcePartyName == requested.SourceParty!.DisplayName.Trim()
            && value.TargetPartyName == requested.TargetParty!.DisplayName.Trim(), cancellationToken);
        if (duplicate) return Conflict("相同类型、名称和参与方的集成已存在。");
        var statusFailure = await ApplyKnowledgeStatus(integration.KnowledgeStatus, request.KnowledgeStatusChange, update,
            EvidenceSubjectType.Integration, integration.Id, request.Applier!,
            (status, reason) => SetKnowledgeStatus(integration, status, reason, request.Applier!), cancellationToken);
        if (statusFailure is not null) return statusFailure;

        integration.Name = requested.Name.Trim(); integration.IntegrationType = type;
        integration.SourceSystemId = requested.SourceParty!.SystemId; integration.SourcePartyName = requested.SourceParty.DisplayName.Trim();
        integration.TargetSystemId = requested.TargetParty!.SystemId; integration.TargetPartyName = requested.TargetParty.DisplayName.Trim();
        integration.FlowDirection = direction; integration.Purpose = Normalize(requested.Purpose);
        integration.TopicOrQueue = type == IntegrationType.RabbitMq ? endpoint.Topic ?? endpoint.Queue : null;
        integration.EndpointDisplay = display; integration.EndpointJson = IntegrationEndpointParser.Serialize(endpoint, type);
        integration.DatabaseSourceId = requested.DatabaseSourceId; integration.DatabaseObjectId = requested.DatabaseObjectId;
        integration.UpdatedAt = DateTimeOffset.UtcNow; integration.Version = targetVersion + 1;
        return await CompleteApply(item, update, request.Applier!, itemVersion, integration.KnowledgeStatus,
            "已应用集成关系更新", tokenCodec.Encode(integration.Version), cancellationToken);
    }

    public async Task<UnknownItemCommandResult> ConfirmConclusion(
        ConfirmConclusionCommand request, CancellationToken cancellationToken)
    {
        var errors = ValidateWorkflow(request.Confirmer, request.ConcurrencyToken, out var expectedVersion, "confirmer");
        if (errors.Count > 0) return Validation(errors);
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var item = await dbContext.UnknownItems.Include(value => value.Resolution).Include(value => value.KnowledgeUpdates)
            .SingleOrDefaultAsync(value => value.Id == request.UnknownItemId, cancellationToken);
        if (item is null) return Failure(UnknownItemFailure.NotFound, "未找到待确认事项。");
        if (item.Version != expectedVersion) return Conflict();
        if (item.Status != UnknownItemStatus.Investigating) return InvalidState("只有调查中的事项可以确认结论。");
        if (item.Resolution is null) return Failure(UnknownItemFailure.ReferenceInvalid, "必须先保存结论草稿。");
        if (item.KnowledgeUpdates.Any(value => value.Status != KnowledgeUpdateStatus.Applied))
            return Failure(UnknownItemFailure.ReferenceInvalid, "所有结论中声明的知识更新必须先明确应用。");
        if (!await HasSupportingEvidence(item.Id, item.Resolution.Id, cancellationToken))
            return Failure(UnknownItemFailure.ReferenceInvalid, "确认结论前至少需要一条当前调查的支持证据。");

        var confirmer = request.Confirmer!;
        item.Resolution.ConfirmedByName = confirmer.DisplayName.Trim();
        item.Resolution.ConfirmedByRole = confirmer.RoleOrIdentity.Trim();
        item.Resolution.ConfirmedByTeam = Normalize(confirmer.Team);
        item.Resolution.ConfirmedByExternalKey = Normalize(confirmer.ExternalUserKey);
        item.Resolution.ConfirmedBySource = Normalize(confirmer.Source);
        item.Resolution.ConfirmedByNote = Normalize(confirmer.Note);
        item.Resolution.ConfirmedAt = confirmer.OccurredAt;
        item.Resolution.UpdatedAt = DateTimeOffset.UtcNow;
        item.Status = UnknownItemStatus.ConclusionConfirmed;
        item.ConclusionConfirmedAt = confirmer.OccurredAt;
        Touch(item, expectedVersion);
        var activity = Activity(item, UnknownItemActivityType.StatusChanged, confirmer,
            $"{confirmer.DisplayName.Trim()}（{confirmer.RoleOrIdentity.Trim()}）确认调查结论", "Resolution", item.Resolution.Id);
        dbContext.UnknownItemActivities.Add(activity);
        var failure = await Save(cancellationToken);
        if (failure is not null) return failure;
        await transaction.CommitAsync(cancellationToken);
        return Success(new ConfirmConclusionResponse(item.Id, "Investigating", item.Status.ToString(),
            item.ConclusionConfirmedAt.Value, ActivityResponse(activity), tokenCodec.Encode(item.Version), ["CloseUnknownItem"]));
    }

    public async Task<UnknownItemCommandResult> CloseUnknownItem(
        CloseUnknownItemCommand request, CancellationToken cancellationToken)
    {
        var errors = ValidateWorkflow(request.Actor, request.ConcurrencyToken, out var expectedVersion);
        if (errors.Count > 0) return Validation(errors);
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var item = await dbContext.UnknownItems.Include(value => value.Resolution).Include(value => value.KnowledgeUpdates)
            .SingleOrDefaultAsync(value => value.Id == request.UnknownItemId, cancellationToken);
        if (item is null) return Failure(UnknownItemFailure.NotFound, "未找到待确认事项。");
        if (item.Version != expectedVersion) return Conflict();
        if (item.Status != UnknownItemStatus.ConclusionConfirmed) return InvalidState("只有结论已确认的事项可以关闭。");
        if (item.Resolution?.ConfirmedAt is null || string.IsNullOrWhiteSpace(item.Resolution.ConfirmedByName)
            || string.IsNullOrWhiteSpace(item.Resolution.ConfirmedByRole)
            || item.KnowledgeUpdates.Any(value => value.Status != KnowledgeUpdateStatus.Applied))
            return Failure(UnknownItemFailure.ReferenceInvalid, "结论确认信息或知识更新状态不完整，不能关闭。");

        var actor = request.Actor!;
        item.Status = UnknownItemStatus.Closed;
        item.ClosedAt = actor.OccurredAt;
        Touch(item, expectedVersion);
        var summary = string.IsNullOrWhiteSpace(request.CloseNote)
            ? $"{actor.DisplayName.Trim()}关闭待确认事项"
            : $"{actor.DisplayName.Trim()}关闭待确认事项：{request.CloseNote.Trim()}";
        var activity = Activity(item, UnknownItemActivityType.Closed, actor, summary);
        dbContext.UnknownItemActivities.Add(activity);
        var failure = await Save(cancellationToken);
        if (failure is not null) return failure;
        await transaction.CommitAsync(cancellationToken);
        return Success(new CloseUnknownItemResponse(item.Id, "ConclusionConfirmed", item.Status.ToString(), item.ClosedAt.Value,
            ActivityResponse(activity), tokenCodec.Encode(item.Version), ["ReopenUnknownItem"]));
    }

    public async Task<UnknownItemCommandResult> ReopenUnknownItem(
        ReopenUnknownItemCommand request, CancellationToken cancellationToken)
    {
        var errors = ValidateWorkflow(request.Actor, request.ConcurrencyToken, out var expectedVersion);
        if (string.IsNullOrWhiteSpace(request.Reason)) errors["reason"] = ["重新打开原因不能为空。"]; 
        if (errors.Count > 0) return Validation(errors);
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var item = await dbContext.UnknownItems.SingleOrDefaultAsync(value => value.Id == request.UnknownItemId, cancellationToken);
        if (item is null) return Failure(UnknownItemFailure.NotFound, "未找到待确认事项。");
        if (item.Version != expectedVersion) return Conflict();
        if (item.Status != UnknownItemStatus.Closed) return InvalidState("只有已关闭事项可以重新打开。");

        var actor = request.Actor!;
        item.Status = UnknownItemStatus.Investigating;
        item.ClosedAt = null;
        Touch(item, expectedVersion);
        var activity = Activity(item, UnknownItemActivityType.Reopened, actor, request.Reason.Trim());
        dbContext.UnknownItemActivities.Add(activity);
        var failure = await Save(cancellationToken);
        if (failure is not null) return failure;
        await transaction.CommitAsync(cancellationToken);
        return Success(new ReopenUnknownItemResponse(item.Id, "Closed", item.Status.ToString(), null, true,
            ActivityResponse(activity), tokenCodec.Encode(item.Version), ["AddFinding", "AddEvidenceToInvestigation", "SaveResolutionDraft"]));
    }

    private async Task<UnknownItemCommandResult> ApplyColumn(object request, string action, CancellationToken cancellationToken)
    {
        var knownValue = request as ApplyColumnKnownValueCommand;
        var knowledge = request as ApplyDatabaseColumnKnowledgeCommand;
        var itemId = knownValue?.UnknownItemId ?? knowledge!.UnknownItemId;
        var updateId = knownValue?.KnowledgeUpdateId ?? knowledge!.KnowledgeUpdateId;
        var columnId = knownValue?.ColumnId ?? knowledge!.ColumnId;
        var person = knownValue?.Applier ?? knowledge!.Applier;
        var itemToken = knownValue?.ConcurrencyToken ?? knowledge!.ConcurrencyToken;
        var targetToken = knownValue?.TargetConcurrencyToken ?? knowledge!.TargetConcurrencyToken;
        var statusChange = knownValue is not null ? knownValue.KnowledgeStatusChange : knowledge!.KnowledgeStatusChange;
        var errors = ValidateApply(person, itemToken, targetToken, out var itemVersion, out var targetVersion);
        if (!IsSafeId(columnId)) errors["columnId"] = ["字段 ID 无效。"]; 
        if (knownValue is not null)
        {
            if (string.IsNullOrWhiteSpace(knownValue.Value)) errors["value"] = ["已知值不能为空。"]; 
            if (string.IsNullOrWhiteSpace(knownValue.Meaning)) errors["meaning"] = ["业务含义不能为空。"]; 
        }
        else if (string.IsNullOrWhiteSpace(knowledge!.BusinessDescription)) errors["businessDescription"] = ["字段业务含义不能为空。"]; 
        if (errors.Count > 0) return Validation(errors);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var context = await LoadApplyContext(itemId, updateId, itemVersion, cancellationToken);
        if (context.Failure is not null) return context.Failure;
        var (item, update) = context.Value;
        if (update.TargetType != KnowledgeTargetType.DatabaseColumn || update.TargetId != columnId
            || ApplyAction(update) != action) return Failure(UnknownItemFailure.ReferenceInvalid, "KnowledgeUpdate 与字段目标或具体 Apply 操作不匹配。");
        var column = await dbContext.DatabaseColumns.Include(value => value.DatabaseObject).ThenInclude(value => value.DatabaseSource)
            .Include(value => value.KnownValues).SingleOrDefaultAsync(value => value.Id == columnId, cancellationToken);
        if (column is null) return Failure(UnknownItemFailure.NotFound, "未找到数据库字段。");
        if (column.DatabaseObject.DatabaseSource.SystemId != item.SystemId) return Failure(UnknownItemFailure.ReferenceInvalid, "字段不属于当前事项的系统上下文。");
        if (column.Version != targetVersion) return Conflict("字段知识已被修改，请刷新知识更新预览。");

        string before;
        string after;
        string summary;
        if (knownValue is not null)
        {
            if (column.KnownValues.Any(value => value.ValueText == knownValue.Value.Trim()))
                return Failure(UnknownItemFailure.Conflict, "该字段已存在相同已知值，Preview 已过期。");
            before = "null";
            after = JsonSerializer.Serialize(new { value = knownValue.Value.Trim(), meaning = knownValue.Meaning.Trim() });
            if (!JsonEquals(update.BeforeJson, before) || !JsonEquals(update.AfterJson, after)) return Conflict("KnowledgeUpdate Preview 已过期或与明确修改值不一致。");
            column.KnownValues.Add(new ColumnKnownValue { ValueText = knownValue.Value.Trim(), Meaning = knownValue.Meaning.Trim(), SortOrder = knownValue.SortOrder, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            summary = "已应用字段已知值更新";
        }
        else
        {
            before = JsonSerializer.Serialize(new { businessDescription = column.BusinessDescription });
            after = JsonSerializer.Serialize(new { businessDescription = knowledge!.BusinessDescription.Trim() });
            if (!JsonEquals(update.BeforeJson, before) || !JsonEquals(update.AfterJson, after)) return Conflict("KnowledgeUpdate Preview 已过期或与明确修改值不一致。");
            column.BusinessDescription = knowledge.BusinessDescription.Trim();
            summary = "已应用字段业务含义更新";
        }

        var statusFailure = await ApplyKnowledgeStatus(column.KnowledgeStatus, statusChange, update,
            EvidenceSubjectType.DatabaseColumn, column.Id, person!,
            (status, reason) => SetKnowledgeStatus(column, status, reason, person!), cancellationToken);
        if (statusFailure is not null) return statusFailure;
        column.UpdatedAt = DateTimeOffset.UtcNow;
        column.Version = targetVersion + 1;
        update.BeforeJson = before;
        update.AfterJson = after;
        return await CompleteApply(item, update, person!, itemVersion, column.KnowledgeStatus,
            summary, tokenCodec.Encode(column.Version), cancellationToken);
    }

    private async Task<UnknownItemCommandResult> CompleteApply(
        UnknownItem item, KnowledgeUpdate update, PersonSnapshotCommand applier, long expectedVersion,
        KnowledgeStatus targetStatus, string summary, string targetToken, CancellationToken cancellationToken)
    {
        var now = applier.OccurredAt;
        update.Status = KnowledgeUpdateStatus.Applied;
        update.AppliedByName = applier.DisplayName.Trim();
        update.AppliedByRole = applier.RoleOrIdentity.Trim();
        update.AppliedByTeam = Normalize(applier.Team);
        update.AppliedByExternalKey = Normalize(applier.ExternalUserKey);
        update.AppliedBySource = Normalize(applier.Source);
        update.AppliedByNote = Normalize(applier.Note);
        update.AppliedAt = now;
        update.UpdatedAt = DateTimeOffset.UtcNow;
        Touch(item, expectedVersion);
        var activity = Activity(item, UnknownItemActivityType.KnowledgeUpdateApplied, applier,
            $"{applier.DisplayName.Trim()}（{applier.RoleOrIdentity.Trim()}）{summary}", "KnowledgeUpdate", update.Id);
        dbContext.UnknownItemActivities.Add(activity);
        var failure = await Save(cancellationToken);
        if (failure is not null) return failure;
        var remaining = await dbContext.KnowledgeUpdates.AsNoTracking().AnyAsync(value => value.UnknownItemId == item.Id
            && value.Status == KnowledgeUpdateStatus.Proposed, cancellationToken);
        await dbContext.Database.CommitTransactionAsync(cancellationToken);
        return Success(new ApplyKnowledgeUpdateResponse(item.Id, item.Status.ToString(),
            new(update.Id, update.Status.ToString(), update.AppliedAt.Value), new(update.TargetType.ToString(), update.TargetId),
            targetStatus.ToString(), ActivityResponse(activity), tokenCodec.Encode(item.Version), targetToken,
            remaining ? [] : ["ConfirmConclusion"]));
    }

    private async Task<((UnknownItem Item, KnowledgeUpdate Update) Value, UnknownItemCommandResult? Failure)> LoadApplyContext(
        long itemId, long updateId, long itemVersion, CancellationToken cancellationToken)
    {
        var item = await dbContext.UnknownItems.SingleOrDefaultAsync(value => value.Id == itemId, cancellationToken);
        if (item is null) return (default, Failure(UnknownItemFailure.NotFound, "未找到待确认事项。"));
        if (item.Version != itemVersion) return (default, Conflict());
        if (item.Status != UnknownItemStatus.Investigating) return (default, InvalidState("只有调查中的事项可以应用知识更新。"));
        if (!await dbContext.Resolutions.AsNoTracking().AnyAsync(value => value.UnknownItemId == itemId, cancellationToken))
            return (default, Failure(UnknownItemFailure.ReferenceInvalid, "必须先保存结论草稿。"));
        var update = await dbContext.KnowledgeUpdates.SingleOrDefaultAsync(value => value.Id == updateId && value.UnknownItemId == itemId, cancellationToken);
        if (update is null) return (default, Failure(UnknownItemFailure.NotFound, "未找到 KnowledgeUpdate。"));
        if (update.Status != KnowledgeUpdateStatus.Proposed) return (default, InvalidState("KnowledgeUpdate 已应用，不能重复 Apply。"));
        return ((item, update), null);
    }

    private async Task<UnknownItemCommandResult?> ApplyKnowledgeStatus(
        KnowledgeStatus current, KnowledgeStatusChangeCommand? change, KnowledgeUpdate update,
        EvidenceSubjectType subjectType, long subjectId, PersonSnapshotCommand actor,
        Action<KnowledgeStatus, string?> apply, CancellationToken cancellationToken)
    {
        if (change is null)
        {
            if (update.KnowledgeStatusBefore.HasValue || update.KnowledgeStatusAfter.HasValue)
                return Failure(UnknownItemFailure.ReferenceInvalid, "Preview 声明了 KnowledgeStatus 变化，Apply 时必须明确提交该变化。");
            return null;
        }
        if (!Enum.TryParse<KnowledgeStatus>(change.TargetStatus, false, out var target) || target.ToString() != change.TargetStatus)
            return Validation(new Dictionary<string, string[]> { ["knowledgeStatusChange.targetStatus"] = ["KnowledgeStatus 无效。"] });
        if (update.KnowledgeStatusBefore != current || update.KnowledgeStatusAfter != target)
            return Conflict("KnowledgeStatus Preview 已过期或与 Apply 请求不一致。");
        if (current == target) return Failure(UnknownItemFailure.ReferenceInvalid, "KnowledgeStatus 没有发生变化。");
        if (current == KnowledgeStatus.Unknown && target == KnowledgeStatus.Confirmed)
            return Failure(UnknownItemFailure.ReferenceInvalid, "MVP 不允许 KnowledgeStatus 从 Unknown 直接进入 Confirmed。");
        var rollback = (int)target < (int)current;
        if (rollback && string.IsNullOrWhiteSpace(change.Reason))
            return Validation(new Dictionary<string, string[]> { ["knowledgeStatusChange.reason"] = ["KnowledgeStatus 回退必须填写原因。"] });
        if (!rollback)
        {
            var requiredType = target == KnowledgeStatus.Confirmed ? EvidenceType.HumanConfirmation : (EvidenceType?)null;
            var evidence = dbContext.Evidence.AsNoTracking().Where(value => value.SubjectType == subjectType && value.SubjectId == subjectId);
            if (!string.IsNullOrWhiteSpace(update.SubjectDetailKey)) evidence = evidence.Where(value => value.SubjectDetailKey == update.SubjectDetailKey);
            if (requiredType.HasValue) evidence = evidence.Where(value => value.EvidenceType == requiredType.Value);
            if (!await evidence.AnyAsync(cancellationToken))
                return Failure(UnknownItemFailure.ReferenceInvalid, target == KnowledgeStatus.Confirmed
                    ? "进入 Confirmed 前必须存在相关 HumanConfirmation Evidence。"
                    : "进入 Inferred 前必须存在相关 Evidence。");
        }
        apply(target, Normalize(change.Reason));
        return null;
    }

    private async Task<bool> HasSupportingEvidence(long itemId, long resolutionId, CancellationToken cancellationToken)
    {
        var findingIds = await dbContext.Findings.AsNoTracking().Where(value => value.UnknownItemId == itemId).Select(value => value.Id).ToArrayAsync(cancellationToken);
        return await dbContext.Evidence.AsNoTracking().AnyAsync(value =>
            value.SubjectType == EvidenceSubjectType.UnknownItem && value.SubjectId == itemId
            || value.SubjectType == EvidenceSubjectType.Finding && findingIds.Contains(value.SubjectId)
            || value.SubjectType == EvidenceSubjectType.Resolution && value.SubjectId == resolutionId, cancellationToken);
    }

    private static void ValidateOverview(BusinessFunctionOverviewCommand value, IDictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value.Name)) errors["overview.name"] = ["业务功能名称不能为空。"]; 
        if (string.IsNullOrWhiteSpace(value.FunctionType)) errors["overview.functionType"] = ["功能类型不能为空。"]; 
        if (string.IsNullOrWhiteSpace(value.RewriteStatus)) errors["overview.rewriteStatus"] = ["RewriteStatus 不能为空。"]; 
    }

    private static void ValidateRule(BusinessRuleUpdateCommand value, IDictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value.Name)) errors["rule.name"] = ["规则名称不能为空。"]; 
        if (string.IsNullOrWhiteSpace(value.Description)) errors["rule.description"] = ["规则描述不能为空。"]; 
        if (value.InputData?.Any(item => string.IsNullOrWhiteSpace(item.Name)) == true)
            errors["rule.inputData"] = ["输入数据名称不能为空。"]; 
    }

    private async Task ValidateIntegration(IntegrationOverviewUpdateCommand value, IDictionary<string, string[]> errors, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value.Name)) errors["integration.name"] = ["集成名称不能为空。"];
        if (value.SourceParty is null || string.IsNullOrWhiteSpace(value.SourceParty.DisplayName)) errors["integration.sourceParty"] = ["必须填写来源方名称。"];
        if (value.TargetParty is null || string.IsNullOrWhiteSpace(value.TargetParty.DisplayName)) errors["integration.targetParty"] = ["必须填写目标方名称。"];
        if (value.SourceParty is not null && value.TargetParty is not null && value.SourceParty.SystemId is null && value.TargetParty.SystemId is null)
            errors["integration.sourceParty.systemId"] = ["来源方或目标方至少一端必须关联已登记系统。"];
        if (!Enum.TryParse<IntegrationType>(value.IntegrationType, false, out var type) || type.ToString() != value.IntegrationType)
            errors["integration.integrationType"] = ["IntegrationType 无效。"];
        if (!Enum.TryParse<IntegrationFlowDirection>(value.FlowDirection, false, out var direction) || direction.ToString() != value.FlowDirection)
            errors["integration.flowDirection"] = ["FlowDirection 无效。"];
        if (errors.Count > 0) return;
        if (value.SourceParty!.SystemId is long sourceId && !await dbContext.Systems.AnyAsync(item => item.Id == sourceId, cancellationToken))
            errors["integration.sourceParty.systemId"] = ["未找到来源方关联系统。"];
        if (value.TargetParty!.SystemId is long targetId && !await dbContext.Systems.AnyAsync(item => item.Id == targetId, cancellationToken))
            errors["integration.targetParty.systemId"] = ["未找到目标方关联系统。"];
        if (!IntegrationEndpointParser.TryParse(type, value.Endpoint, out _, out _, out var endpointError)) errors["integration.endpoint"] = [endpointError!];
        if (type != IntegrationType.DatabaseDependency && (value.DatabaseSourceId is not null || value.DatabaseObjectId is not null))
            errors["integration.databaseSourceId"] = ["只有 DatabaseDependency 可以关联数据库来源或对象。"];
        if (type == IntegrationType.DatabaseDependency && value.DatabaseSourceId is null && value.DatabaseObjectId is null)
            errors["integration.databaseSourceId"] = ["DatabaseDependency 必须关联数据库来源或对象。"];
        if (value.DatabaseSourceId is long databaseSourceId && !await dbContext.DatabaseSources.AnyAsync(item => item.Id == databaseSourceId, cancellationToken))
            errors["integration.databaseSourceId"] = ["未找到数据库来源。"];
        if (value.DatabaseObjectId is long databaseObjectId)
        {
            var databaseObject = await dbContext.DatabaseObjects.AsNoTracking().SingleOrDefaultAsync(item => item.Id == databaseObjectId, cancellationToken);
            if (databaseObject is null) errors["integration.databaseObjectId"] = ["未找到数据库对象。"];
            else if (value.DatabaseSourceId is long selectedSourceId && databaseObject.DatabaseSourceId != selectedSourceId)
                errors["integration.databaseObjectId"] = ["数据库对象不属于指定数据库来源。"];
        }
    }

    private Dictionary<string, string[]> ValidateApply(PersonSnapshotCommand? person, string itemToken, string targetToken,
        out long itemVersion, out long targetVersion)
    {
        var errors = new Dictionary<string, string[]>();
        ValidatePerson(person, "applier", errors);
        if (!tokenCodec.TryDecode(itemToken, out itemVersion)) errors["concurrencyToken"] = ["待确认事项并发标记无效。"]; 
        if (!tokenCodec.TryDecode(targetToken, out targetVersion)) errors["targetConcurrencyToken"] = ["目标知识并发标记无效。"]; 
        return errors;
    }

    private Dictionary<string, string[]> ValidateWorkflow(PersonSnapshotCommand? person, string token, out long expectedVersion, string prefix = "actor")
    {
        var errors = new Dictionary<string, string[]>();
        ValidatePerson(person, prefix, errors);
        if (!tokenCodec.TryDecode(token, out expectedVersion)) errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"];
        return errors;
    }

    private static void ValidatePerson(PersonSnapshotCommand? person, string prefix, IDictionary<string, string[]> errors)
    {
        if (person is null) { errors[prefix] = ["必须记录人员快照。"]; return; }
        if (string.IsNullOrWhiteSpace(person.DisplayName)) errors[$"{prefix}.displayName"] = ["人员名称不能为空。"]; 
        if (string.IsNullOrWhiteSpace(person.RoleOrIdentity)) errors[$"{prefix}.roleOrIdentity"] = ["角色或身份不能为空。"]; 
        if (person.OccurredAt == default) errors[$"{prefix}.occurredAt"] = ["发生时间不能为空。"]; 
    }

    private async Task<UnknownItemCommandResult?> Save(CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); return null; }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        catch (DbUpdateException) { return Failure(UnknownItemFailure.Conflict, "知识更新与当前数据冲突，事务未提交。"); }
    }

    private static string ApplyAction(KnowledgeUpdate update) => update.TargetType switch
    {
        KnowledgeTargetType.DatabaseColumn when update.SubjectDetailKey?.StartsWith("KnownValues:", StringComparison.Ordinal) == true => "AddColumnKnownValue",
        KnowledgeTargetType.DatabaseColumn => "UpdateDatabaseColumnKnowledge",
        KnowledgeTargetType.BusinessFunction => "UpdateBusinessFunction",
        KnowledgeTargetType.BusinessRule => "UpdateBusinessRule",
        KnowledgeTargetType.Integration => "UpdateIntegration",
        _ => string.Empty,
    };

    private static string FunctionSnapshot(BusinessFunction value) => JsonSerializer.Serialize(new
    {
        name = value.Name, displayName = value.DisplayName, functionType = value.FunctionType, purpose = value.Purpose,
        caller = value.CallerSummary, input = value.InputDescription, output = value.OutputDescription, rewriteStatus = value.RewriteStatus.ToString(),
    });
    private static string FunctionSnapshot(BusinessFunctionOverviewCommand value) => JsonSerializer.Serialize(new
    {
        name = value.Name.Trim(), displayName = Normalize(value.DisplayName), functionType = value.FunctionType.Trim(), purpose = Normalize(value.Purpose),
        caller = Normalize(value.Caller), input = Normalize(value.Input), output = Normalize(value.Output), rewriteStatus = value.RewriteStatus,
    });
    private static string RuleSnapshot(BusinessRule value) => JsonSerializer.Serialize(new
    {
        name = value.Name,
        description = value.Description,
        condition = value.ConditionText,
        result = value.ResultText,
        inputData = string.IsNullOrWhiteSpace(value.InputDataJson)
            ? Array.Empty<BusinessRuleInputDataCommand>()
            : JsonSerializer.Deserialize<BusinessRuleInputDataCommand[]>(value.InputDataJson, JsonOptions) ?? [],
    }, JsonOptions);
    private static string RuleSnapshot(BusinessRuleUpdateCommand value) => JsonSerializer.Serialize(new
    {
        name = value.Name.Trim(),
        description = value.Description.Trim(),
        condition = Normalize(value.Condition),
        result = Normalize(value.Result),
        inputData = (value.InputData ?? []).Select(item => new
        {
            name = item.Name.Trim(),
            description = Normalize(item.Description),
        }),
    }, JsonOptions);
    private static string IntegrationSnapshot(Integration value) => JsonSerializer.Serialize(new
    {
        name = value.Name, integrationType = value.IntegrationType.ToString(),
        sourceParty = new { systemId = value.SourceSystemId, displayName = value.SourcePartyName },
        targetParty = new { systemId = value.TargetSystemId, displayName = value.TargetPartyName },
        flowDirection = value.FlowDirection.ToString(), purpose = value.Purpose,
        endpoint = string.IsNullOrWhiteSpace(value.EndpointJson) ? new JsonObject() : JsonNode.Parse(value.EndpointJson), databaseSourceId = value.DatabaseSourceId, databaseObjectId = value.DatabaseObjectId,
    }, JsonOptions);
    private static string IntegrationSnapshot(IntegrationOverviewUpdateCommand value)
    {
        var type = Enum.Parse<IntegrationType>(value.IntegrationType, false);
        IntegrationEndpointParser.TryParse(type, value.Endpoint, out var endpoint, out _, out _);
        var endpointJson = IntegrationEndpointParser.Serialize(endpoint, type);
        return JsonSerializer.Serialize(new
        {
            name = value.Name.Trim(), integrationType = value.IntegrationType,
            sourceParty = new { systemId = value.SourceParty!.SystemId, displayName = value.SourceParty.DisplayName.Trim() },
            targetParty = new { systemId = value.TargetParty!.SystemId, displayName = value.TargetParty.DisplayName.Trim() },
            flowDirection = value.FlowDirection, purpose = Normalize(value.Purpose), endpoint = string.IsNullOrWhiteSpace(endpointJson) ? new JsonObject() : JsonNode.Parse(endpointJson),
            databaseSourceId = value.DatabaseSourceId, databaseObjectId = value.DatabaseObjectId,
        }, JsonOptions);
    }
    private static bool JsonEquals(string actual, string expected) => JsonNode.DeepEquals(JsonNode.Parse(actual), JsonNode.Parse(expected));
    private static bool IsSafeId(long id) => id is >= 1 and <= 9_007_199_254_740_991;
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void Touch(UnknownItem item, long version) { item.UpdatedAt = DateTimeOffset.UtcNow; item.Version = version + 1; }
    private static void SetKnowledgeStatus(DatabaseColumn value, KnowledgeStatus status, string? reason, PersonSnapshotCommand actor)
    { value.KnowledgeStatus = status; value.KnowledgeStatusReason = reason; value.KnowledgeStatusChangedAt = actor.OccurredAt; value.KnowledgeStatusChangedByName = actor.DisplayName.Trim(); value.KnowledgeStatusChangedByRole = actor.RoleOrIdentity.Trim(); }
    private static void SetKnowledgeStatus(BusinessFunction value, KnowledgeStatus status, string? reason, PersonSnapshotCommand actor)
    { value.KnowledgeStatus = status; value.KnowledgeStatusReason = reason; value.KnowledgeStatusChangedAt = actor.OccurredAt; value.KnowledgeStatusChangedByName = actor.DisplayName.Trim(); value.KnowledgeStatusChangedByRole = actor.RoleOrIdentity.Trim(); }
    private static void SetKnowledgeStatus(BusinessRule value, KnowledgeStatus status, string? reason, PersonSnapshotCommand actor)
    { value.KnowledgeStatus = status; value.KnowledgeStatusReason = reason; value.KnowledgeStatusChangedAt = actor.OccurredAt; value.KnowledgeStatusChangedByName = actor.DisplayName.Trim(); value.KnowledgeStatusChangedByRole = actor.RoleOrIdentity.Trim(); }
    private static void SetKnowledgeStatus(Integration value, KnowledgeStatus status, string? reason, PersonSnapshotCommand actor)
    { value.KnowledgeStatus = status; value.KnowledgeStatusReason = reason; value.KnowledgeStatusChangedAt = actor.OccurredAt; value.KnowledgeStatusChangedByName = actor.DisplayName.Trim(); value.KnowledgeStatusChangedByRole = actor.RoleOrIdentity.Trim(); }
    private static UnknownItemActivity Activity(UnknownItem item, UnknownItemActivityType type, PersonSnapshotCommand actor, string summary, string? relatedType = null, long? relatedId = null) => new()
    { UnknownItemId = item.Id, ActivityType = type, ActorName = actor.DisplayName.Trim(), ActorRole = actor.RoleOrIdentity.Trim(), ActorTeam = Normalize(actor.Team), ActorExternalKey = Normalize(actor.ExternalUserKey), ActorSource = Normalize(actor.Source), ActorNote = Normalize(actor.Note), OccurredAt = actor.OccurredAt, Note = summary, RelatedType = relatedType, RelatedId = relatedId };
    private static UnknownActivityResponse ActivityResponse(UnknownItemActivity value) => new(value.ActivityType.ToString(), value.Note ?? value.ActivityType.ToString(), value.OccurredAt);
    private static UnknownItemCommandResult Success(object response) => new(response, null, UnknownItemFailure.None);
    private static UnknownItemCommandResult Validation(IReadOnlyDictionary<string, string[]> errors) => new(null, errors, UnknownItemFailure.Validation);
    private static UnknownItemCommandResult Failure(UnknownItemFailure failure, string message) => new(null, null, failure, message);
    private static UnknownItemCommandResult Conflict(string message = "待确认事项已被修改，请重新加载后重试。") => Failure(UnknownItemFailure.Conflict, message);
    private static UnknownItemCommandResult InvalidState(string message) => Failure(UnknownItemFailure.InvalidState, message);
}
