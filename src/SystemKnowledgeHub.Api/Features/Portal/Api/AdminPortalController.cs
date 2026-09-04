using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.Portal.Application;
using SystemKnowledgeHub.Api.Features.Portal.Application.Models;
using SystemKnowledgeHub.Api.Features.Portal.Domain;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;
using SystemKnowledgeHub.Api.Shared.Security;

namespace SystemKnowledgeHub.Api.Features.Portal.Api;

[ApiController]
[Authorize(Policy = AccessPolicies.Administrator)]
[Route("api/admin/portal")]
public sealed class AdminPortalController(
    AdminPortalQueries queries,
    AdminPortalService service,
    ICurrentUserContext currentUserContext) : ControllerBase
{
    [HttpGet("pages")]
    public async Task<IActionResult> GetPages(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await queries.GetPagesAsync(page, pageSize, search, cancellationToken);
        return Query(result);
    }

    [HttpPost("pages")]
    public async Task<IActionResult> CreatePage(
        [FromBody] CreateAdminPortalPageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreatePageAsync(request, await Actor(cancellationToken), cancellationToken);
        return Command(result, created: true);
    }

    [HttpGet("pages/{id}")]
    public async Task<IActionResult> GetPage(string id, CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(id, out var pageId)) return InvalidId("id");
        return Query(await queries.GetPageAsync(pageId, cancellationToken));
    }

    [HttpPut("pages/{id}")]
    public async Task<IActionResult> UpdatePage(
        string id,
        [FromBody] UpdateAdminPortalPageRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(id, out var pageId)) return InvalidId("id");
        return Command(await service.UpdatePageAsync(pageId, request, await Actor(cancellationToken), cancellationToken));
    }

    [HttpDelete("pages/{id}")]
    public async Task<IActionResult> DeletePage(
        string id,
        [FromBody] AdminPortalConcurrencyRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(id, out var pageId)) return InvalidId("id");
        return Command(await service.DeletePageAsync(pageId, request, await Actor(cancellationToken), cancellationToken), noContent: true);
    }

    [HttpGet("pages/{id}/preview")]
    public async Task<IActionResult> PreviewPage(string id, CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(id, out var pageId)) return InvalidId("id");
        return Query(await queries.GetPreviewAsync(pageId, cancellationToken));
    }

    [HttpPost("pages/{id}/publish")]
    public async Task<IActionResult> PublishPage(
        string id,
        [FromBody] AdminPortalConcurrencyRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(id, out var pageId)) return InvalidId("id");
        return Command(await service.PublishPageAsync(pageId, request, await Actor(cancellationToken), cancellationToken));
    }

    [HttpPost("pages/{id}/unpublish")]
    public async Task<IActionResult> UnpublishPage(
        string id,
        [FromBody] AdminPortalConcurrencyRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(id, out var pageId)) return InvalidId("id");
        return Command(await service.UnpublishPageAsync(pageId, request, await Actor(cancellationToken), cancellationToken));
    }

    [HttpGet("tree")]
    public async Task<IActionResult> GetTree(CancellationToken cancellationToken) =>
        Query(await queries.GetTreeAsync(cancellationToken));

    [HttpPost("nodes")]
    public async Task<IActionResult> CreateNode(
        [FromBody] CreateAdminPortalNodeRequest request,
        CancellationToken cancellationToken) =>
        Command(await service.CreateNodeAsync(request, await Actor(cancellationToken), cancellationToken), created: true);

    [HttpPut("nodes/reorder")]
    public async Task<IActionResult> ReorderNodes(
        [FromBody] ReorderAdminPortalNodesRequest request,
        CancellationToken cancellationToken) =>
        Command(await service.ReorderNodesAsync(request, await Actor(cancellationToken), cancellationToken));

    [HttpPut("nodes/{id}")]
    public async Task<IActionResult> UpdateNode(
        string id,
        [FromBody] UpdateAdminPortalNodeRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(id, out var nodeId)) return InvalidId("id");
        return Command(await service.UpdateNodeAsync(nodeId, request, await Actor(cancellationToken), cancellationToken));
    }

    [HttpDelete("nodes/{id}")]
    public async Task<IActionResult> DeleteNode(
        string id,
        [FromBody] AdminPortalConcurrencyRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(id, out var nodeId)) return InvalidId("id");
        return Command(await service.DeleteNodeAsync(nodeId, request, await Actor(cancellationToken), cancellationToken), noContent: true);
    }

    [HttpPost("nodes/{id}/publish")]
    public async Task<IActionResult> PublishNode(
        string id,
        [FromBody] AdminPortalConcurrencyRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(id, out var nodeId)) return InvalidId("id");
        return Command(await service.PublishNodeAsync(nodeId, request, await Actor(cancellationToken), cancellationToken));
    }

    [HttpPost("nodes/{id}/unpublish")]
    public async Task<IActionResult> UnpublishNode(
        string id,
        [FromBody] AdminPortalConcurrencyRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(id, out var nodeId)) return InvalidId("id");
        return Command(await service.UnpublishNodeAsync(nodeId, request, await Actor(cancellationToken), cancellationToken));
    }

    [HttpGet("targets")]
    public async Task<IActionResult> GetTargets(
        [FromQuery] string? type,
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        PortalTargetType? targetType = Enum.TryParse<PortalTargetType>(type, ignoreCase: false, out var parsed)
            && Enum.IsDefined(parsed)
            ? parsed
            : null;
        return Query(await queries.GetTargetsAsync(targetType, search, page, pageSize, cancellationToken));
    }

    private async Task<PortalCommandActor> Actor(CancellationToken cancellationToken)
    {
        var resolution = await currentUserContext.ResolveAsync(cancellationToken);
        if (resolution.Status != CurrentUserResolutionStatus.Available || resolution.CurrentUser is null)
            throw new InvalidOperationException("Administrator policy succeeded without a current canonical user.");
        return new(resolution.CurrentUser.Id, resolution.CurrentUser.DisplayName);
    }

    private IActionResult Query<T>(AdminPortalQueryResult<T> result) => result.Failure switch
    {
        AdminPortalFailure.None when result.FieldErrors is null => Ok(result.Response),
        AdminPortalFailure.Validation or AdminPortalFailure.None => BadRequest(Error("validation_error", "请求内容无效。", result.FieldErrors)),
        AdminPortalFailure.NotFound => NotFound(Error("not_found", "未找到指定的门户配置。")),
        AdminPortalFailure.LimitExceeded => UnprocessableEntity(Error("portal_limit_exceeded", "知识门户树超过 2,000 个节点的安全限制。")),
        _ => throw new InvalidOperationException("Unsupported Portal Admin query result."),
    };

    private IActionResult Command<T>(AdminPortalCommandResult<T> result, bool created = false, bool noContent = false) => result.Failure switch
    {
        AdminPortalFailure.None when noContent => NoContent(),
        AdminPortalFailure.None when created => StatusCode(StatusCodes.Status201Created, result.Response),
        AdminPortalFailure.None => Ok(result.Response),
        AdminPortalFailure.Validation => BadRequest(Error("validation_error", "请求内容无效。", result.FieldErrors)),
        AdminPortalFailure.NotFound => NotFound(Error("not_found", "未找到指定的门户配置。")),
        AdminPortalFailure.Conflict => Conflict(Error("conflict", "页面或节点已被其他操作修改，请重新加载后再继续。")),
        AdminPortalFailure.InvalidState => Conflict(Error("invalid_state", result.Message ?? "当前状态不允许执行此操作。")),
        AdminPortalFailure.ReferenceInvalid => UnprocessableEntity(new ApiErrorResponse(
            "reference_invalid",
            result.Message ?? "门户引用无效。",
            null,
            result.Readiness)),
        AdminPortalFailure.LimitExceeded => UnprocessableEntity(Error("portal_limit_exceeded", "知识门户内容超过安全限制。")),
        _ => throw new InvalidOperationException("Unsupported Portal Admin command result."),
    };

    private BadRequestObjectResult InvalidId(string field) => BadRequest(Error(
        "validation_error",
        "请求内容无效。",
        new Dictionary<string, string[]> { [field] = ["ID 必须是 JavaScript 安全范围内的正整数。"] }));

    private static ApiErrorResponse Error(
        string code,
        string message,
        IReadOnlyDictionary<string, string[]>? fieldErrors = null) =>
        new(code, message, fieldErrors, null);
}
