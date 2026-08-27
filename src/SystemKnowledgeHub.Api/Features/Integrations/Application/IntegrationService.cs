using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Integrations.Application.Models;
using SystemKnowledgeHub.Api.Features.Integrations.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.Integrations.Application;

public sealed class IntegrationService(KnowledgeHubDbContext dbContext, ConcurrencyTokenCodec tokenCodec)
{
    public async Task<IntegrationCommandResult> Create(CreateIntegrationCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var validation = await ValidateOverview(request.Overview, request.Actor, null, cancellationToken);
        if (validation.Errors.Count > 0) return Failure(IntegrationFailure.Validation, validation.Errors);
        if (validation.Failure is not null) return Failure(validation.Failure.Value, message: validation.Message);
        var overview = request.Overview; var type = validation.Type!.Value; var now = DateTimeOffset.UtcNow;
        var integration = new Integration
        {
            Name = overview.Name.Trim(), IntegrationType = type,
            SourceSystemId = overview.SourceParty!.SystemId, SourcePartyName = overview.SourceParty.DisplayName.Trim(),
            TargetSystemId = overview.TargetParty!.SystemId, TargetPartyName = overview.TargetParty.DisplayName.Trim(),
            FlowDirection = validation.Direction!.Value, Purpose = Normalize(overview.Purpose),
            TopicOrQueue = type == IntegrationType.RabbitMq ? validation.Endpoint!.Topic ?? validation.Endpoint.Queue : null,
            EndpointDisplay = validation.EndpointDisplay, EndpointJson = IntegrationEndpointParser.Serialize(validation.Endpoint!, type),
            DatabaseSourceId = overview.DatabaseSourceId, DatabaseObjectId = overview.DatabaseObjectId,
            CreatedAt = now, CreatedByUserId = request.Creator.UserId, CreatedByName = request.Creator.DisplayName, CreatedByRole = Normalize(request.Actor.Role), UpdatedAt = now,
            KnowledgeStatus = KnowledgeStatus.Unknown, KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = request.Creator.DisplayName, KnowledgeStatusChangedByRole = Normalize(request.Actor.Role) ?? "创建人", Version = 1,
        };
        dbContext.Integrations.Add(integration);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return Failure(IntegrationFailure.Duplicate, message: "相同类型、名称和参与方的集成已存在。"); }
        await transaction.CommitAsync(cancellationToken);
        return Success(integration);
    }

    public async Task<IntegrationCommandResult> UpdateOverview(UpdateIntegrationCommand request, CancellationToken cancellationToken)
    {
        if (!tokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion))
            return Failure(IntegrationFailure.Validation, new Dictionary<string, string[]> { ["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"] });
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var integration = await dbContext.Integrations.SingleOrDefaultAsync(item => item.Id == request.IntegrationId, cancellationToken);
        if (integration is null) return Failure(IntegrationFailure.NotFound);
        if (integration.Version != expectedVersion) return Failure(IntegrationFailure.Conflict);
        var validation = await ValidateOverview(request.Overview, request.Actor, integration.Id, cancellationToken);
        if (validation.Errors.Count > 0) return Failure(IntegrationFailure.Validation, validation.Errors);
        if (validation.Failure is not null) return Failure(validation.Failure.Value, message: validation.Message);
        var overview = request.Overview; var type = validation.Type!.Value;
        integration.Name = overview.Name.Trim(); integration.IntegrationType = type;
        integration.SourceSystemId = overview.SourceParty!.SystemId; integration.SourcePartyName = overview.SourceParty.DisplayName.Trim();
        integration.TargetSystemId = overview.TargetParty!.SystemId; integration.TargetPartyName = overview.TargetParty.DisplayName.Trim();
        integration.FlowDirection = validation.Direction!.Value; integration.Purpose = Normalize(overview.Purpose);
        integration.TopicOrQueue = type == IntegrationType.RabbitMq ? validation.Endpoint!.Topic ?? validation.Endpoint.Queue : null;
        integration.EndpointDisplay = validation.EndpointDisplay; integration.EndpointJson = IntegrationEndpointParser.Serialize(validation.Endpoint!, type);
        integration.DatabaseSourceId = overview.DatabaseSourceId; integration.DatabaseObjectId = overview.DatabaseObjectId;
        integration.UpdatedAt = DateTimeOffset.UtcNow; integration.Version = expectedVersion + 1;
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Failure(IntegrationFailure.Conflict); }
        catch (DbUpdateException) { return Failure(IntegrationFailure.Duplicate, message: "相同类型、名称和参与方的集成已存在。"); }
        await transaction.CommitAsync(cancellationToken);
        return Success(integration);
    }

    public async Task<IntegrationCommandResult> ReplaceContractFields(ReplaceIntegrationContractFieldsCommand request, CancellationToken cancellationToken)
    {
        var errors = ValidateActor(request.Actor);
        if (!tokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion)) errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"]; 
        ValidateFields(request.Fields, errors);
        if (errors.Count > 0) return Failure(IntegrationFailure.Validation, errors);
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var integration = await dbContext.Integrations.Include(item => item.ContractFields).SingleOrDefaultAsync(item => item.Id == request.IntegrationId, cancellationToken);
        if (integration is null) return Failure(IntegrationFailure.NotFound);
        if (integration.Version != expectedVersion) return Failure(IntegrationFailure.Conflict);
        dbContext.IntegrationContractFields.RemoveRange(integration.ContractFields);
        integration.ContractFields = (request.Fields ?? []).OrderBy(item => item.Order).Select(item => new IntegrationContractField
        {
            Ordinal = item.Order, FieldName = item.FieldName.Trim(), DataType = Normalize(item.DataType), IsRequired = item.Required,
            Description = Normalize(item.Description), SampleValue = Normalize(item.SampleValue),
        }).ToList();
        integration.UpdatedAt = DateTimeOffset.UtcNow; integration.Version = expectedVersion + 1;
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Failure(IntegrationFailure.Conflict); }
        await transaction.CommitAsync(cancellationToken);
        return new IntegrationCommandResult(new IntegrationContractFieldsResponse(integration.Id,
            integration.ContractFields.OrderBy(item => item.Ordinal).Select(FieldResponse).ToArray(), tokenCodec.Encode(integration.Version)), null, IntegrationFailure.None);
    }

    private async Task<OverviewValidation> ValidateOverview(IntegrationOverviewCommand overview, IntegrationActor actor, long? currentId, CancellationToken cancellationToken)
    {
        var errors = ValidateActor(actor);
        if (string.IsNullOrWhiteSpace(overview.Name)) errors["name"] = ["集成名称不能为空。"]; 
        if (overview.SourceParty is null) errors["sourceParty"] = ["必须填写来源方。"]; else if (string.IsNullOrWhiteSpace(overview.SourceParty.DisplayName)) errors["sourceParty.displayName"] = ["来源方名称不能为空。"]; 
        if (overview.TargetParty is null) errors["targetParty"] = ["必须填写目标方。"]; else if (string.IsNullOrWhiteSpace(overview.TargetParty.DisplayName)) errors["targetParty.displayName"] = ["目标方名称不能为空。"]; 
        if (overview.SourceParty is not null && overview.TargetParty is not null && overview.SourceParty.SystemId is null && overview.TargetParty.SystemId is null)
            errors["sourceParty.systemId"] = ["来源方或目标方至少一端必须关联已登记系统。"];
        if (!Enum.TryParse<IntegrationType>(overview.IntegrationType, false, out var type) || type.ToString() != overview.IntegrationType)
            errors["integrationType"] = ["IntegrationType 无效。"];
        if (!Enum.TryParse<IntegrationFlowDirection>(overview.FlowDirection, false, out var direction) || direction.ToString() != overview.FlowDirection)
            errors["flowDirection"] = ["FlowDirection 无效。"];
        if (errors.Count > 0) return new(errors, null, null, null, null, null);
        if (overview.SourceParty!.SystemId is long sourceId && !await dbContext.Systems.AnyAsync(item => item.Id == sourceId, cancellationToken))
            return new(errors, IntegrationFailure.ReferenceInvalid, "未找到来源方关联系统。", null, null, null);
        if (overview.TargetParty!.SystemId is long targetId && !await dbContext.Systems.AnyAsync(item => item.Id == targetId, cancellationToken))
            return new(errors, IntegrationFailure.ReferenceInvalid, "未找到目标方关联系统。", null, null, null);
        if (!IntegrationEndpointParser.TryParse(type, overview.Endpoint, out var endpoint, out var display, out var endpointError))
        { errors["endpoint"] = [endpointError!]; return new(errors, null, null, null, null, null); }
        if (type != IntegrationType.DatabaseDependency && (overview.DatabaseSourceId is not null || overview.DatabaseObjectId is not null))
            errors["databaseSourceId"] = ["只有 DatabaseDependency 可以关联数据库来源或对象。"];
        if (type == IntegrationType.DatabaseDependency && overview.DatabaseSourceId is null && overview.DatabaseObjectId is null)
            errors["databaseSourceId"] = ["DatabaseDependency 必须关联数据库来源或对象。"];
        if (errors.Count > 0) return new(errors, null, null, null, null, null);
        if (overview.DatabaseSourceId is long databaseSourceId && !await dbContext.DatabaseSources.AnyAsync(item => item.Id == databaseSourceId, cancellationToken))
            return new(errors, IntegrationFailure.ReferenceInvalid, "未找到数据库来源。", null, null, null);
        if (overview.DatabaseObjectId is long databaseObjectId)
        {
            var databaseObject = await dbContext.DatabaseObjects.AsNoTracking().SingleOrDefaultAsync(item => item.Id == databaseObjectId, cancellationToken);
            if (databaseObject is null) return new(errors, IntegrationFailure.ReferenceInvalid, "未找到数据库对象。", null, null, null);
            if (overview.DatabaseSourceId is long source && databaseObject.DatabaseSourceId != source)
                return new(errors, IntegrationFailure.ReferenceInvalid, "数据库对象不属于指定数据库来源。", null, null, null);
        }
        var exists = await dbContext.Integrations.AsNoTracking().AnyAsync(item => item.IntegrationType == type
            && item.Name == overview.Name.Trim() && item.SourcePartyName == overview.SourceParty.DisplayName.Trim()
            && item.TargetPartyName == overview.TargetParty.DisplayName.Trim() && (!currentId.HasValue || item.Id != currentId.Value), cancellationToken);
        return exists ? new(errors, IntegrationFailure.Duplicate, "相同类型、名称和参与方的集成已存在。", null, null, null)
            : new(errors, null, null, type, direction, endpoint, display);
    }

    private static Dictionary<string, string[]> ValidateActor(IntegrationActor actor)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(actor.DisplayName)) errors["actor.displayName"] = ["操作人姓名不能为空。"];
        return errors;
    }
    private static void ValidateFields(IReadOnlyList<IntegrationContractFieldCommand>? fields, IDictionary<string, string[]> errors)
    {
        var items = fields ?? [];
        if (items.Any(item => item.Order < 1) || !items.Select(item => item.Order).Order().SequenceEqual(Enumerable.Range(1, items.Count))) errors["fields"] = ["契约字段顺序必须从 1 连续编号。"];
        if (items.Any(item => string.IsNullOrWhiteSpace(item.FieldName))) errors["fields"] = ["契约字段名称不能为空。"];
        if (items.GroupBy(item => item.FieldName.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) errors["fields"] = ["契约字段名称不能重复。"];
        if (items.Any(item => item.SampleValue?.Length > 200)) errors["fields"] = ["示例值不能超过 200 个字符。"];
    }
    private IntegrationCommandResult Success(Integration item) => new(new IntegrationWriteResponse(item.Id, item.Name, item.IntegrationType.ToString(), item.KnowledgeStatus.ToString(), tokenCodec.Encode(item.Version)), null, IntegrationFailure.None);
    private static IntegrationCommandResult Failure(IntegrationFailure failure, IReadOnlyDictionary<string, string[]>? errors = null, string? message = null) => new(null, errors, failure, message);
    private static IntegrationContractFieldResponse FieldResponse(IntegrationContractField field) => new(field.Ordinal, field.FieldName, field.DataType, field.IsRequired, field.Description, field.SampleValue);
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private sealed record OverviewValidation(IReadOnlyDictionary<string, string[]> Errors, IntegrationFailure? Failure, string? Message, IntegrationType? Type, IntegrationFlowDirection? Direction, IntegrationEndpoint? Endpoint, string? EndpointDisplay)
    { public OverviewValidation(IReadOnlyDictionary<string, string[]> errors, IntegrationFailure? failure, string? message, IntegrationType? type, IntegrationFlowDirection? direction, IntegrationEndpoint? endpoint) : this(errors, failure, message, type, direction, endpoint, null) { } }
}
