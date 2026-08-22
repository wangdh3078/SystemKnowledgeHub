using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.Evidence.Application.Models;
using SystemKnowledgeHub.Api.Features.UnknownItems.Api.Contracts;
using SystemKnowledgeHub.Api.Features.UnknownItems.Application;
using SystemKnowledgeHub.Api.Features.UnknownItems.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.UnknownItems.Api;

[ApiController]
[Route("api/unknown-items")]
public sealed class UnknownItemsController(
    UnknownItemQueries queries,
    UnknownItemService service,
    KnowledgeResolutionService resolutionService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? keyword,
        [FromQuery] long? systemId,
        [FromQuery] string? relatedObjectType,
        [FromQuery] string? priority,
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? updatedFrom,
        [FromQuery] DateTimeOffset? updatedTo,
        [FromQuery] string? sort,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await queries.GetList(new(keyword, systemId, relatedObjectType, priority, status, updatedFrom, updatedTo, sort, page, pageSize), cancellationToken);
        return result.FieldErrors is null ? Ok(result.Response) : BadRequest(Error("validation_error", "请求内容无效。", result.FieldErrors));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetDetail(long id, CancellationToken cancellationToken)
    {
        var result = await queries.GetDetail(id, cancellationToken);
        return result.Failure switch
        {
            UnknownItemFailure.None => Ok(result.Response),
            UnknownItemFailure.Validation => BadRequest(Error("validation_error", result.Message ?? "请求内容无效。")),
            UnknownItemFailure.NotFound => NotFound(Error("not_found", result.Message ?? "未找到待确认事项。")),
            _ => throw new InvalidOperationException("Unsupported Unknown Item query result."),
        };
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUnknownItemRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateUnknownItem(new(
            request.SystemId,
            request.Question ?? string.Empty,
            request.Context,
            request.Priority ?? string.Empty,
            Target(request.PrimaryTarget),
            request.RelatedTargets?.Select(TargetRequired).ToArray(),
            Person(request.Creator)), cancellationToken);
        return Command(result, created: true);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPut("{id:long}/related-targets")]
    public async Task<IActionResult> UpdateRelatedTargets(long id, [FromBody] UpdateUnknownItemRelatedTargetsRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateUnknownItemRelatedTargets(new(
            id,
            request.RelatedTargets?.Select(TargetRequired).ToArray(),
            request.Actor is null ? null : new(request.Actor.DisplayName ?? string.Empty, request.Actor.Role),
            request.ConcurrencyToken ?? string.Empty), cancellationToken);
        return Command(result);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost("{id:long}/start-investigation")]
    public async Task<IActionResult> StartInvestigation(long id, [FromBody] StartInvestigationRequest request, CancellationToken cancellationToken)
        => Command(await service.StartInvestigation(new(id, Person(request.Actor), request.ConcurrencyToken ?? string.Empty), cancellationToken));

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost("{id:long}/findings")]
    public async Task<IActionResult> AddFinding(long id, [FromBody] AddFindingRequest request, CancellationToken cancellationToken)
        => Command(await service.AddFinding(new(id, request.Content ?? string.Empty, Person(request.Recorder), request.ConcurrencyToken ?? string.Empty), cancellationToken), created: true);

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost("{id:long}/evidence")]
    public async Task<IActionResult> AddEvidence(long id, [FromBody] AddInvestigationEvidenceRequest request, CancellationToken cancellationToken)
    {
        var evidence = new AddEvidenceCommand(
            request.EvidenceType ?? string.Empty,
            request.Subject is null ? null : new(request.Subject.Type ?? string.Empty, request.Subject.Id),
            request.SubjectDetailKey,
            request.SourceTitle ?? string.Empty,
            request.SourceReference,
            request.SourceLocator,
            request.Summary,
            request.SupportReason ?? string.Empty,
            request.Confidence,
            Person(request.Provider));
        return Command(await service.AddEvidenceToInvestigation(new(id, evidence, request.ConcurrencyToken ?? string.Empty), cancellationToken), created: true);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPut("{id:long}/resolution")]
    public async Task<IActionResult> SaveResolution(long id, [FromBody] SaveResolutionDraftRequest request, CancellationToken cancellationToken)
    {
        var updates = request.KnowledgeUpdates?.Select(update => new KnowledgeUpdateDraftCommand(
            update.Id,
            Target(update.Target),
            update.SubjectDetailKey,
            update.ApplyAction ?? string.Empty,
            update.ChangeSummary ?? string.Empty,
            update.Before,
            update.After,
            update.KnowledgeStatusBefore,
            update.KnowledgeStatusAfter)).ToArray();
        return Command(await service.SaveResolutionDraft(new(
            id, request.Conclusion ?? string.Empty, updates, Person(request.Actor), request.ConcurrencyToken ?? string.Empty), cancellationToken));
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost("{id:long}/knowledge-updates/{updateId:long}/apply-column-known-value")]
    public async Task<IActionResult> ApplyColumnKnownValue(long id, long updateId, [FromBody] ApplyColumnKnownValueRequest request, CancellationToken cancellationToken)
        => Command(await resolutionService.ApplyColumnKnownValue(new(id, updateId, request.ColumnId,
            request.Value ?? string.Empty, request.Meaning ?? string.Empty, request.SortOrder, StatusChange(request.KnowledgeStatusChange),
            Person(request.Applier), request.ConcurrencyToken ?? string.Empty, request.TargetConcurrencyToken ?? string.Empty), cancellationToken));

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost("{id:long}/knowledge-updates/{updateId:long}/apply-column-knowledge")]
    public async Task<IActionResult> ApplyColumnKnowledge(long id, long updateId, [FromBody] ApplyDatabaseColumnKnowledgeRequest request, CancellationToken cancellationToken)
        => Command(await resolutionService.ApplyDatabaseColumnKnowledge(new(id, updateId, request.ColumnId,
            request.BusinessDescription ?? string.Empty, StatusChange(request.KnowledgeStatusChange), Person(request.Applier),
            request.ConcurrencyToken ?? string.Empty, request.TargetConcurrencyToken ?? string.Empty), cancellationToken));

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost("{id:long}/knowledge-updates/{updateId:long}/apply-business-function")]
    public async Task<IActionResult> ApplyBusinessFunction(long id, long updateId, [FromBody] ApplyBusinessFunctionRequest request, CancellationToken cancellationToken)
        => Command(await resolutionService.ApplyBusinessFunction(new(id, updateId, request.BusinessFunctionId,
            request.Overview is null ? null : new(request.Overview.Name ?? string.Empty, request.Overview.DisplayName,
                request.Overview.FunctionType ?? string.Empty, request.Overview.Purpose, request.Overview.Caller,
                request.Overview.Input, request.Overview.Output, request.Overview.RewriteStatus ?? string.Empty),
            StatusChange(request.KnowledgeStatusChange), Person(request.Applier), request.ConcurrencyToken ?? string.Empty,
            request.TargetConcurrencyToken ?? string.Empty), cancellationToken));

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost("{id:long}/knowledge-updates/{updateId:long}/apply-business-rule")]
    public async Task<IActionResult> ApplyBusinessRule(long id, long updateId, [FromBody] ApplyBusinessRuleRequest request, CancellationToken cancellationToken)
        => Command(await resolutionService.ApplyBusinessRule(new(id, updateId, request.BusinessRuleId,
            request.Rule is null ? null : new(request.Rule.Name ?? string.Empty, request.Rule.Description ?? string.Empty,
                request.Rule.Condition, request.Rule.Result,
                request.Rule.InputData?.Select(item => new BusinessRuleInputDataCommand(item.Name ?? string.Empty, item.Description)).ToArray()),
            StatusChange(request.KnowledgeStatusChange), Person(request.Applier), request.ConcurrencyToken ?? string.Empty,
            request.TargetConcurrencyToken ?? string.Empty), cancellationToken));

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost("{id:long}/knowledge-updates/{updateId:long}/apply-integration")]
    public async Task<IActionResult> ApplyIntegration(long id, long updateId, [FromBody] ApplyIntegrationRequest request, CancellationToken cancellationToken)
        => Command(await resolutionService.ApplyIntegration(new(id, updateId, request.IntegrationId,
            request.Integration is null ? null : new(request.Integration.Name ?? string.Empty, request.Integration.IntegrationType ?? string.Empty,
                Party(request.Integration.SourceParty), Party(request.Integration.TargetParty), request.Integration.FlowDirection ?? string.Empty,
                request.Integration.Purpose, request.Integration.Endpoint, request.Integration.DatabaseSourceId, request.Integration.DatabaseObjectId),
            StatusChange(request.KnowledgeStatusChange), Person(request.Applier), request.ConcurrencyToken ?? string.Empty,
            request.TargetConcurrencyToken ?? string.Empty), cancellationToken));

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost("{id:long}/confirm-conclusion")]
    public async Task<IActionResult> ConfirmConclusion(long id, [FromBody] ConfirmConclusionRequest request, CancellationToken cancellationToken)
        => Command(await resolutionService.ConfirmConclusion(new(id, Person(request.Confirmer), request.ConcurrencyToken ?? string.Empty), cancellationToken));

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost("{id:long}/close")]
    public async Task<IActionResult> Close(long id, [FromBody] CloseUnknownItemRequest request, CancellationToken cancellationToken)
        => Command(await resolutionService.CloseUnknownItem(new(id, request.CloseNote, Person(request.Actor), request.ConcurrencyToken ?? string.Empty), cancellationToken));

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost("{id:long}/reopen")]
    public async Task<IActionResult> Reopen(long id, [FromBody] ReopenUnknownItemRequest request, CancellationToken cancellationToken)
        => Command(await resolutionService.ReopenUnknownItem(new(id, request.Reason ?? string.Empty, Person(request.Actor), request.ConcurrencyToken ?? string.Empty), cancellationToken));

    private IActionResult Command(UnknownItemCommandResult result, bool created = false)
    {
        return result.Failure switch
        {
            UnknownItemFailure.None => created ? StatusCode(StatusCodes.Status201Created, result.Response) : Ok(result.Response),
            UnknownItemFailure.Validation => BadRequest(Error("validation_error", "请求内容无效。", result.FieldErrors)),
            UnknownItemFailure.NotFound => NotFound(Error("not_found", result.Message ?? "未找到待确认事项。")),
            UnknownItemFailure.Conflict => Conflict(Error("conflict", result.Message ?? "内容已被修改，请重新加载。")),
            UnknownItemFailure.InvalidState => Conflict(Error("invalid_state", result.Message ?? "当前状态不允许该操作。")),
            UnknownItemFailure.ReferenceInvalid => UnprocessableEntity(Error("reference_invalid", result.Message ?? "关联对象无效。")),
            UnknownItemFailure.UnsupportedUpdate => UnprocessableEntity(Error("business_rule_violation", result.Message ?? "知识更新意图无效。")),
            _ => throw new InvalidOperationException("Unsupported Unknown Item command result."),
        };
    }

    private static UnknownTargetCommand? Target(UnknownTargetRequest? target) =>
        target is null ? null : new(target.Type ?? string.Empty, target.Id);
    private static UnknownTargetCommand TargetRequired(UnknownTargetRequest target) => new(target.Type ?? string.Empty, target.Id);
    private static PersonSnapshotCommand? Person(UnknownPersonSnapshotRequest? person) => person is null ? null : new(
        person.DisplayName ?? string.Empty,
        person.RoleOrIdentity ?? string.Empty,
        person.OccurredAt ?? default,
        person.Team,
        person.ExternalUserKey,
        person.Source,
        person.Note);
    private static KnowledgeStatusChangeCommand? StatusChange(KnowledgeStatusChangeRequest? change) =>
        change is null ? null : new(change.TargetStatus ?? string.Empty, change.Reason);
    private static IntegrationPartyUpdateCommand? Party(IntegrationPartyUpdateRequest? party) =>
        party is null ? null : new(party.SystemId, party.DisplayName ?? string.Empty);
    private static ApiErrorResponse Error(string code, string message, IReadOnlyDictionary<string, string[]>? fields = null) =>
        new(code, message, fields, null);
}
