using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.BusinessRules.Api.Contracts;
using SystemKnowledgeHub.Api.Features.BusinessRules.Application;
using SystemKnowledgeHub.Api.Features.BusinessRules.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.BusinessRules.Api;

[ApiController]
[Route("api/business-rules")]
public sealed class BusinessRulesController(BusinessRuleQueries queries, BusinessRuleService service) : ControllerBase
{
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetDetail(long id, CancellationToken cancellationToken)
    {
        var response = await queries.GetDetail(id, cancellationToken);
        return response is null ? NotFound(Error("not_found", "未找到业务规则。")) : Ok(response);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBusinessRuleRequest request, CancellationToken cancellationToken)
    {
        var result = await service.Create(new(request.SystemId, request.Name ?? string.Empty,
            request.Description ?? string.Empty, request.Condition, request.Result, Inputs(request.InputData),
            Actor(request.Actor)), cancellationToken);
        return Command(result, true);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateBusinessRuleRequest request, CancellationToken cancellationToken)
    {
        var result = await service.Update(new(id, request.Name ?? string.Empty, request.Description ?? string.Empty,
            request.Condition, request.Result, Inputs(request.InputData), Actor(request.Actor),
            request.ConcurrencyToken ?? string.Empty), cancellationToken);
        return Command(result, false);
    }

    private IActionResult Command(BusinessRuleCommandResult result, bool created) => result.Failure switch
    {
        BusinessRuleFailure.None => created ? StatusCode(StatusCodes.Status201Created, result.Response) : Ok(result.Response),
        BusinessRuleFailure.Validation => BadRequest(Error("validation_error", "请求内容无效。", result.FieldErrors)),
        BusinessRuleFailure.SystemNotFound => NotFound(Error("not_found", "未找到所属系统。")),
        BusinessRuleFailure.NotFound => NotFound(Error("not_found", "未找到业务规则。")),
        BusinessRuleFailure.DuplicateName => Conflict(Error("conflict", "同一系统内已存在同名业务规则。")),
        BusinessRuleFailure.Conflict => Conflict(Error("conflict", "内容已被修改，请重新加载后重试。")),
        _ => throw new InvalidOperationException("Unsupported Business Rule result."),
    };

    private static BusinessRuleActor Actor(BusinessRuleActorRequest? actor) =>
        new(actor?.DisplayName ?? string.Empty, actor?.Role);
    private static IReadOnlyList<BusinessRuleInputData>? Inputs(IReadOnlyList<BusinessRuleInputDataRequest>? values) =>
        values?.Select(item => new BusinessRuleInputData(item.Name ?? string.Empty, item.Description)).ToArray();
    private static ApiErrorResponse Error(string code, string message,
        IReadOnlyDictionary<string, string[]>? fields = null) => new(code, message, fields, null);
}
