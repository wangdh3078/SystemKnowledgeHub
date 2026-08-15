using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.Evidence.Api.Contracts;
using SystemKnowledgeHub.Api.Features.Evidence.Application;
using SystemKnowledgeHub.Api.Features.Evidence.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.Evidence.Api;

[ApiController]
[Route("api/evidence")]
public sealed class EvidenceController(EvidenceQueries queries, EvidenceService service) : ControllerBase
{
    [HttpGet("{id:long}")]
    [ProducesResponseType<EvidenceDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EvidenceDetailResponse>> GetEvidenceDetail(
        long id,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id))
        {
            return BadRequest(ValidationError(new Dictionary<string, string[]>
            {
                ["id"] = ["证据 ID 必须是 JavaScript 安全范围内的正整数。"],
            }));
        }

        var result = await queries.GetEvidenceDetail(id, cancellationToken);
        return result.Failure switch
        {
            EvidenceFailure.None => Ok(result.Response),
            EvidenceFailure.NotFound => NotFound(NotFoundError(id)),
            EvidenceFailure.SubjectNotFound => UnprocessableEntity(ReferenceInvalidError()),
            _ => throw new InvalidOperationException("Unsupported Evidence detail result."),
        };
    }

    [HttpPost]
    [ProducesResponseType<AddEvidenceResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AddEvidenceResponse>> AddEvidence(
        [FromBody] AddEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.AddEvidence(
            new AddEvidenceCommand(
                request.EvidenceType ?? string.Empty,
                ToTarget(request.Subject),
                request.SubjectDetailKey,
                request.SourceTitle ?? string.Empty,
                request.SourceReference,
                request.SourceLocator,
                request.Summary,
                request.SupportReason ?? string.Empty,
                request.Confidence,
                ToPerson(request.Provider)),
            cancellationToken);

        return result.Failure switch
        {
            EvidenceFailure.None => StatusCode(StatusCodes.Status201Created, result.Response),
            EvidenceFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            EvidenceFailure.SubjectNotFound => UnprocessableEntity(ReferenceInvalidError()),
            _ => throw new InvalidOperationException("Unsupported Add Evidence result."),
        };
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType<EvidenceDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EvidenceDetailResponse>> UpdateEvidence(
        long id,
        [FromBody] UpdateEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id))
        {
            return BadRequest(ValidationError(new Dictionary<string, string[]>
            {
                ["id"] = ["证据 ID 必须是 JavaScript 安全范围内的正整数。"],
            }));
        }

        var result = await service.UpdateEvidence(
            new UpdateEvidenceCommand(
                id,
                request.SourceTitle ?? string.Empty,
                request.SourceReference,
                request.SourceLocator,
                request.Summary,
                request.SupportReason ?? string.Empty,
                request.Confidence,
                ToPerson(request.Provider),
                request.Actor is null
                    ? null
                    : new EvidenceActorCommand(request.Actor.DisplayName ?? string.Empty, request.Actor.Role),
                request.ConcurrencyToken ?? string.Empty),
            cancellationToken);

        return result.Failure switch
        {
            EvidenceFailure.None => Ok(result.Response),
            EvidenceFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            EvidenceFailure.NotFound => NotFound(NotFoundError(id)),
            EvidenceFailure.SubjectNotFound => UnprocessableEntity(ReferenceInvalidError()),
            EvidenceFailure.Conflict => Conflict(new ApiErrorResponse(
                "conflict",
                "证据已被其他操作修改，请重新加载后重试。",
                null,
                new { resourceType = "Evidence", resourceId = id })),
            _ => throw new InvalidOperationException("Unsupported Update Evidence result."),
        };
    }

    [HttpPost("human-confirmations")]
    [ProducesResponseType<AddEvidenceResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AddEvidenceResponse>> AddHumanConfirmation(
        [FromBody] AddHumanConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.AddHumanConfirmation(
            new AddHumanConfirmationCommand(
                ToTarget(request.Subject),
                request.SubjectDetailKey,
                request.ConfirmationStatement ?? string.Empty,
                request.SupportReason ?? string.Empty,
                request.SourceNote,
                ToPerson(request.Confirmer)),
            cancellationToken);

        return result.Failure switch
        {
            EvidenceFailure.None => StatusCode(StatusCodes.Status201Created, result.Response),
            EvidenceFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            EvidenceFailure.SubjectNotFound => UnprocessableEntity(ReferenceInvalidError()),
            _ => throw new InvalidOperationException("Unsupported Add Human Confirmation result."),
        };
    }

    private static EvidenceTargetCommand? ToTarget(EvidenceTargetRequest? target)
    {
        return target is null ? null : new EvidenceTargetCommand(target.Type ?? string.Empty, target.Id);
    }

    private static PersonSnapshotCommand? ToPerson(PersonSnapshotRequest? person)
    {
        return person is null
            ? null
            : new PersonSnapshotCommand(
                person.DisplayName ?? string.Empty,
                person.RoleOrIdentity ?? string.Empty,
                person.OccurredAt ?? default,
                person.Team,
                person.ExternalUserKey,
                person.Source,
                person.Note);
    }

    private static ApiErrorResponse NotFoundError(long id)
    {
        return new ApiErrorResponse(
            "not_found",
            "未找到指定证据。",
            null,
            new { resourceType = "Evidence", resourceId = id });
    }

    private static ApiErrorResponse ReferenceInvalidError()
    {
        return new ApiErrorResponse(
            "reference_invalid",
            "证据关联的知识对象不存在或当前尚不可用。",
            null,
            null);
    }

    private static ApiErrorResponse ValidationError(IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        return new ApiErrorResponse("validation_error", "请求内容无效。", fieldErrors, null);
    }
}
