using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.Integrations.Api.Contracts;
using SystemKnowledgeHub.Api.Features.Integrations.Application;
using SystemKnowledgeHub.Api.Features.Integrations.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.Integrations.Api;

[ApiController]
[Route("api/integrations")]
public sealed class IntegrationsController(IntegrationQueries queries, IntegrationService service) : ControllerBase
{
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetDetail(long id, CancellationToken cancellationToken)
    {
        var response = await queries.GetDetail(id, cancellationToken);
        return response is null ? NotFound(Error("not_found", "未找到集成关系。")) : Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateIntegrationRequest request, CancellationToken cancellationToken)
        => Command(await service.Create(new(ToOverview(request), Actor(request.Actor)), cancellationToken), true);

    [HttpPut("{id:long}/overview")]
    public async Task<IActionResult> UpdateOverview(long id, [FromBody] UpdateIntegrationOverviewRequest request, CancellationToken cancellationToken)
        => Command(await service.UpdateOverview(new(id, ToOverview(request), Actor(request.Actor), request.ConcurrencyToken ?? string.Empty), cancellationToken));

    [HttpPut("{id:long}/contract-fields")]
    public async Task<IActionResult> ReplaceContractFields(long id, [FromBody] ReplaceIntegrationContractFieldsRequest request, CancellationToken cancellationToken)
        => Command(await service.ReplaceContractFields(new(id, request.Fields?.Select(item => new IntegrationContractFieldCommand(item.Order, item.FieldName ?? string.Empty, item.DataType, item.Required, item.Description, item.SampleValue)).ToArray(), Actor(request.Actor), request.ConcurrencyToken ?? string.Empty), cancellationToken));

    private IActionResult Command(IntegrationCommandResult result, bool created = false) => result.Failure switch
    {
        IntegrationFailure.None => created ? StatusCode(StatusCodes.Status201Created, result.Response) : Ok(result.Response),
        IntegrationFailure.Validation => BadRequest(Error("validation_error", "请求内容无效。", result.FieldErrors)),
        IntegrationFailure.NotFound => NotFound(Error("not_found", "未找到集成关系。")),
        IntegrationFailure.ReferenceInvalid => UnprocessableEntity(Error("reference_invalid", result.Message ?? "关联对象无效。")),
        IntegrationFailure.Duplicate => Conflict(Error("conflict", result.Message ?? "已存在相同集成关系。")),
        IntegrationFailure.Conflict => Conflict(Error("conflict", "内容已被修改，请重新加载后重试。")),
        _ => throw new InvalidOperationException("Unsupported Integration result."),
    };
    private static IntegrationOverviewCommand ToOverview(CreateIntegrationRequest request) => new(request.Name ?? string.Empty, request.IntegrationType ?? string.Empty, Party(request.SourceParty), Party(request.TargetParty), request.FlowDirection ?? string.Empty, request.Purpose, request.Endpoint, request.DatabaseSourceId, request.DatabaseObjectId);
    private static IntegrationOverviewCommand ToOverview(UpdateIntegrationOverviewRequest request) => new(request.Name ?? string.Empty, request.IntegrationType ?? string.Empty, Party(request.SourceParty), Party(request.TargetParty), request.FlowDirection ?? string.Empty, request.Purpose, request.Endpoint, request.DatabaseSourceId, request.DatabaseObjectId);
    private static IntegrationParty? Party(IntegrationPartyRequest? value) => value is null ? null : new(value.SystemId, value.DisplayName ?? string.Empty);
    private static IntegrationActor Actor(IntegrationActorRequest? value) => new(value?.DisplayName ?? string.Empty, value?.Role);
    private static ApiErrorResponse Error(string code, string message, IReadOnlyDictionary<string, string[]>? fields = null) => new(code, message, fields, null);
}
