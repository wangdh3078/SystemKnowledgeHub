using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Api.Contracts;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Application;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Application.Models;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;
using SystemKnowledgeHub.Api.Features.SoftDelete.Application;

namespace SystemKnowledgeHub.Api.Features.BusinessFunctions.Api;

[ApiController]
[Route("api/business-functions")]
public sealed class BusinessFunctionsController(
    BusinessFunctionQueries queries,
    BusinessFunctionService service,
    BusinessFunctionDeleteService deleteService,
    ICurrentUserContext currentUserContext) : ControllerBase
{
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteBusinessFunction(long id, [FromBody] DeleteBusinessFunctionRequest request, CancellationToken cancellationToken)
    {
        var actor = await CurrentUserApiResolution.ResolveSoftDeleteActor(currentUserContext, cancellationToken);
        if (actor.Error is not null) return StatusCode(actor.StatusCode!.Value, actor.Error);
        var result = await deleteService.DeleteBusinessFunction(id, request.ConcurrencyToken, actor.Actor!, cancellationToken);
        return result.Failure switch
        {
            SoftDeleteFailure.None => NoContent(),
            SoftDeleteFailure.Validation => BadRequest(SoftDeleteApiResponses.Validation(result.FieldErrors!)),
            SoftDeleteFailure.NotFound => NotFound(SoftDeleteApiResponses.NotFound("BusinessFunction", id)),
            SoftDeleteFailure.Forbidden => StatusCode(StatusCodes.Status403Forbidden, SoftDeleteApiResponses.Forbidden("BusinessFunction", id)),
            SoftDeleteFailure.Conflict => Conflict(SoftDeleteApiResponses.Conflict("BusinessFunction", id)),
            SoftDeleteFailure.Dependencies => UnprocessableEntity(SoftDeleteApiResponses.Dependencies("BusinessFunction", id, result.Blockers!)),
            _ => throw new InvalidOperationException("Unsupported BusinessFunction delete result."),
        };
    }

    [HttpGet]
    [ProducesResponseType<BusinessFunctionsListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BusinessFunctionsListResponse>> GetBusinessFunctionsList(
        [FromQuery] long? systemId,
        [FromQuery] string? search,
        [FromQuery] string? functionType,
        [FromQuery] string? rewriteStatus,
        [FromQuery] string? knowledgeStatus,
        [FromQuery] string? hasUnknownItems,
        [FromQuery] string? sort,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await queries.GetBusinessFunctionsList(
            new BusinessFunctionsListQuery(
                systemId,
                search,
                functionType,
                rewriteStatus,
                knowledgeStatus,
                hasUnknownItems,
                sort,
                page,
                pageSize),
            cancellationToken);

        if (result.FieldErrors is not null)
        {
            return BadRequest(ValidationError(result.FieldErrors));
        }

        return Ok(result.Response);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost]
    [ProducesResponseType<CreateBusinessFunctionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateBusinessFunctionResponse>> CreateBusinessFunction(
        [FromBody] CreateBusinessFunctionRequest request,
        CancellationToken cancellationToken)
    {
        var creator = await CurrentUserApiResolution.ResolveCreator(currentUserContext, cancellationToken);
        if (creator.Error is not null) return StatusCode(creator.StatusCode!.Value, creator.Error);

        var result = await service.CreateBusinessFunction(
            new CreateBusinessFunctionCommand(
                request.SystemId,
                request.Name ?? string.Empty,
                request.DisplayName,
                request.FunctionType ?? string.Empty,
                request.Purpose,
                request.RewriteStatus ?? string.Empty,
                new BusinessFunctionActorContext(
                    request.Actor?.DisplayName ?? string.Empty,
                    request.Actor?.Role),
                creator.Creator!),
            cancellationToken);

        return result.Failure switch
        {
            CreateBusinessFunctionFailure.None => StatusCode(StatusCodes.Status201Created, result.Response),
            CreateBusinessFunctionFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            CreateBusinessFunctionFailure.SystemNotFound => UnprocessableEntity(new ApiErrorResponse(
                "reference_invalid",
                "所属系统不存在或已不可用。",
                null,
                new { resourceType = "System", resourceId = request.SystemId })),
            CreateBusinessFunctionFailure.DuplicateName => Conflict(new ApiErrorResponse(
                "conflict",
                "当前系统中已存在同名业务功能。",
                new Dictionary<string, string[]>
                {
                    ["name"] = ["请在当前系统中使用唯一的业务功能名称。"],
                },
                null)),
            _ => throw new InvalidOperationException("Unsupported Business Function create result."),
        };
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<BusinessFunctionDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BusinessFunctionDetailResponse>> GetBusinessFunctionDetail(
        long id,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id))
        {
            return BadRequest(ValidationError(new Dictionary<string, string[]>
            {
                ["id"] = ["业务功能 ID 必须是 JavaScript 安全范围内的正整数。"],
            }));
        }

        var response = await queries.GetBusinessFunctionDetail(id, cancellationToken);
        if (response is null)
        {
            return NotFound(new ApiErrorResponse(
                "not_found",
                "未找到指定业务功能。",
                null,
                new { resourceType = "BusinessFunction", resourceId = id }));
        }

        return Ok(response);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPut("{id:long}/overview")]
    [ProducesResponseType<UpdateBusinessFunctionOverviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UpdateBusinessFunctionOverviewResponse>> UpdateBusinessFunctionOverview(
        long id,
        [FromBody] UpdateBusinessFunctionOverviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id))
        {
            return BadRequest(InvalidIdError());
        }

        var result = await service.UpdateBusinessFunctionOverview(
            new UpdateBusinessFunctionOverviewCommand(
                id,
                request.Name ?? string.Empty,
                request.DisplayName,
                request.FunctionType ?? string.Empty,
                request.Purpose,
                request.Caller,
                request.Input,
                request.Output,
                request.RewriteStatus ?? string.Empty,
                new BusinessFunctionActorContext(
                    request.Actor?.DisplayName ?? string.Empty,
                    request.Actor?.Role),
                request.ConcurrencyToken ?? string.Empty),
            cancellationToken);

        return HandleUpdateResult(result, id);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPut("{id:long}/process-steps")]
    [ProducesResponseType<ReplaceBusinessProcessStepsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReplaceBusinessProcessStepsResponse>> ReplaceBusinessProcessSteps(
        long id,
        [FromBody] ReplaceBusinessProcessStepsRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id))
        {
            return BadRequest(InvalidIdError());
        }

        var result = await service.ReplaceBusinessProcessSteps(
            new ReplaceBusinessProcessStepsCommand(
                id,
                request.Steps?.Select(step =>
                    new BusinessProcessStepCommand(step.Order, step.Name, step.Description)).ToArray(),
                new BusinessFunctionActorContext(
                    request.Actor?.DisplayName ?? string.Empty,
                    request.Actor?.Role),
                request.ConcurrencyToken ?? string.Empty),
            cancellationToken);

        return result.Failure switch
        {
            UpdateBusinessFunctionFailure.None => Ok(result.Response),
            UpdateBusinessFunctionFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            UpdateBusinessFunctionFailure.NotFound => NotFound(NotFoundError(id)),
            UpdateBusinessFunctionFailure.Conflict => Conflict(ConcurrencyConflictError(id)),
            _ => throw new InvalidOperationException("Unsupported Business Process update result."),
        };
    }

    private ActionResult<UpdateBusinessFunctionOverviewResponse> HandleUpdateResult(
        UpdateBusinessFunctionOverviewResult result,
        long id)
    {
        return result.Failure switch
        {
            UpdateBusinessFunctionFailure.None => Ok(result.Response),
            UpdateBusinessFunctionFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            UpdateBusinessFunctionFailure.NotFound => NotFound(NotFoundError(id)),
            UpdateBusinessFunctionFailure.DuplicateName => Conflict(new ApiErrorResponse(
                "conflict",
                "当前系统中已存在同名业务功能。",
                new Dictionary<string, string[]>
                {
                    ["name"] = ["请在当前系统中使用唯一的业务功能名称。"],
                },
                null)),
            UpdateBusinessFunctionFailure.Conflict => Conflict(ConcurrencyConflictError(id)),
            _ => throw new InvalidOperationException("Unsupported Business Function overview result."),
        };
    }

    private static ApiErrorResponse InvalidIdError()
    {
        return ValidationError(new Dictionary<string, string[]>
        {
            ["id"] = ["业务功能 ID 必须是 JavaScript 安全范围内的正整数。"],
        });
    }

    private static ApiErrorResponse NotFoundError(long id)
    {
        return new ApiErrorResponse(
            "not_found",
            "未找到指定业务功能。",
            null,
            new { resourceType = "BusinessFunction", resourceId = id });
    }

    private static ApiErrorResponse ConcurrencyConflictError(long id)
    {
        return new ApiErrorResponse(
            "conflict",
            "内容已被其他操作修改，请重新加载后重试。",
            null,
            new { resourceType = "BusinessFunction", resourceId = id });
    }

    private static ApiErrorResponse ValidationError(
        IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        return new ApiErrorResponse(
            "validation_error",
            "请求内容无效。",
            fieldErrors,
            null);
    }
}
