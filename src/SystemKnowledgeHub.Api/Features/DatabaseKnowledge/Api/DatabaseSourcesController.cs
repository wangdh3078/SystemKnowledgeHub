using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Api.Contracts;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Api;

[ApiController]
[Route("api/database-sources")]
public sealed class DatabaseSourcesController(DatabaseKnowledgeService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreateDatabaseSourceResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateDatabaseSourceResponse>> CreateDatabaseSource(
        [FromBody] CreateDatabaseSourceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateDatabaseSource(
            new CreateDatabaseSourceCommand(
                request.SystemId ?? 0,
                request.Name ?? string.Empty,
                request.Engine ?? string.Empty,
                request.Environment,
                request.InstanceName,
                request.ServiceName,
                request.DatabaseName,
                request.Description,
                request.IsPrimary ?? false,
                new DatabaseKnowledgeActorContext(
                    request.Actor?.DisplayName ?? string.Empty,
                    request.Actor?.Role)),
            cancellationToken);

        return result.Failure switch
        {
            CreateDatabaseSourceFailure.None => StatusCode(StatusCodes.Status201Created, result.Response),
            CreateDatabaseSourceFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            CreateDatabaseSourceFailure.SystemNotFound => NotFound(NotFoundError("System", request.SystemId ?? 0)),
            CreateDatabaseSourceFailure.DuplicateName => Conflict(ConflictError("name", "同一系统下数据库来源名称已存在。")),
            CreateDatabaseSourceFailure.PrimaryConflict => Conflict(ConflictError("isPrimary", "同一系统只能登记一个主数据库来源。")),
            _ => throw new InvalidOperationException("Unsupported DatabaseSource result."),
        };
    }

    private static ApiErrorResponse ValidationError(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new("validation_error", "请求内容无效。", fieldErrors, null);

    private static ApiErrorResponse NotFoundError(string resourceType, long resourceId) =>
        new("not_found", "未找到指定系统。", null, new { resourceType, resourceId });

    private static ApiErrorResponse ConflictError(string field, string message) =>
        new("conflict", message, new Dictionary<string, string[]> { [field] = [message] }, null);
}
