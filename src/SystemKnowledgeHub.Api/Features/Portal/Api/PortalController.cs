using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.Portal.Application;
using SystemKnowledgeHub.Api.Features.Portal.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.Portal.Api;

[ApiController]
[AllowAnonymous]
[Route("api/portal")]
public sealed class PortalController(PortalQueries queries) : ControllerBase
{
    [HttpGet("home")]
    [ProducesResponseType<PortalHomeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PortalHomeResponse>> GetHome(CancellationToken cancellationToken)
    {
        var result = await queries.GetHomeAsync(cancellationToken);
        return result.Failure switch
        {
            PortalReadFailure.None => Ok(result.Response),
            PortalReadFailure.LimitExceeded => UnprocessableEntity(LimitExceeded()),
            _ => throw new InvalidOperationException("Unsupported Portal home result."),
        };
    }

    [HttpGet("tree")]
    [ProducesResponseType<PortalTreeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PortalTreeResponse>> GetTree(CancellationToken cancellationToken)
    {
        var result = await queries.GetTreeAsync(cancellationToken);
        return result.Failure switch
        {
            PortalReadFailure.None => Ok(result.Response),
            PortalReadFailure.LimitExceeded => UnprocessableEntity(LimitExceeded()),
            _ => throw new InvalidOperationException("Unsupported Portal tree result."),
        };
    }

    [HttpGet("search")]
    [ProducesResponseType<PortalSearchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PortalSearchResponse>> Search(
        [FromQuery] string? q,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var query = q?.Trim() ?? string.Empty;
        var actualPage = page ?? 1;
        var actualPageSize = pageSize ?? PortalLimits.DefaultSearchPageSize;
        var errors = new Dictionary<string, string[]>();
        if (query.Length is < 1 or > 100) errors["q"] = ["搜索内容必须为 1 至 100 个字符。"];
        if (actualPage < 1) errors["page"] = ["页码必须大于或等于 1。"];
        if (actualPageSize is < 1 or > PortalLimits.MaximumSearchPageSize)
            errors["pageSize"] = [$"每页数量必须为 1 至 {PortalLimits.MaximumSearchPageSize}。"];
        if (errors.Count > 0)
            return BadRequest(new ApiErrorResponse("validation_error", "请求内容无效。", errors, null));

        var result = await queries.SearchAsync(query, actualPage, actualPageSize, cancellationToken);
        return result.Failure switch
        {
            PortalReadFailure.None => Ok(result.Response),
            PortalReadFailure.LimitExceeded => UnprocessableEntity(LimitExceeded()),
            _ => throw new InvalidOperationException("Unsupported Portal search result."),
        };
    }

    [HttpGet("pages/{id}")]
    [ProducesResponseType<PortalPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PortalPageResponse>> GetPage(
        string id,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(id, out var pageId))
            return BadRequest(new ApiErrorResponse(
                "validation_error",
                "请求内容无效。",
                new Dictionary<string, string[]>
                {
                    ["id"] = ["ID 必须是 JavaScript 安全范围内的正整数。"],
                },
                null));

        var result = await queries.GetPageAsync(pageId, cancellationToken);
        return result.Failure switch
        {
            PortalReadFailure.None => Ok(result.Response),
            PortalReadFailure.NotFound => NotFound(new ApiErrorResponse(
                "not_found",
                "未找到指定页面。",
                null,
                null)),
            PortalReadFailure.LimitExceeded => UnprocessableEntity(LimitExceeded()),
            _ => throw new InvalidOperationException("Unsupported Portal page result."),
        };
    }

    private static ApiErrorResponse LimitExceeded() => new(
        "portal_limit_exceeded",
        "知识门户内容超过安全读取限制。",
        null,
        null);
}
