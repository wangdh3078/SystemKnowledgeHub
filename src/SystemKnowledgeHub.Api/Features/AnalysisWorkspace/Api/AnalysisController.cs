using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Api.Contracts;
using SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Application;
using SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Application.Models;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application.Models;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;
using SystemKnowledgeHub.Api.Shared.Security;

namespace SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Api;

[ApiController]
[Route("api/analysis")]
[Authorize] // The application's default policy is canonical Viewer access.
public sealed class AnalysisController(AnalysisWorkspaceService service, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet("tree")]
    public Task<IActionResult> Tree(CancellationToken cancellationToken) =>
        Respond(async () => await service.GetTree(cancellationToken));

    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpPost("folders")]
    public Task<IActionResult> CreateFolder(CreateAnalysisFolderRequest request, CancellationToken cancellationToken) =>
        Respond(async () => await service.CreateFolder(request, cancellationToken), StatusCodes.Status201Created);

    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpPut("folders/{id:long}/title")]
    public Task<IActionResult> RenameFolder(long id, RenameAnalysisFolderRequest request, CancellationToken cancellationToken) =>
        Respond(async () => await service.RenameFolder(id, request, cancellationToken));

    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpPost("document-placements")]
    public Task<IActionResult> AddPlacement(AddAnalysisPlacementRequest request, CancellationToken cancellationToken) =>
        Respond(async () => await service.AddPlacement(request, cancellationToken), StatusCodes.Status201Created);

    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpPost("documents")]
    public async Task<IActionResult> CreateDocument(CreateAnalysisDocumentRequest request, CancellationToken cancellationToken)
    {
        var actor = await CurrentUserApiResolution.ResolveCreator(currentUser, cancellationToken);
        if (actor.Error is not null) return StatusCode(actor.StatusCode!.Value, actor.Error);
        return await Respond(async () => await service.CreateDocument(request,
            new KnowledgeDocumentAuthor(actor.Creator!.UserId, actor.Creator.DisplayName), cancellationToken), StatusCodes.Status201Created);
    }

    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpPost("nodes/{id:long}/move")]
    public Task<IActionResult> Move(long id, MoveAnalysisNodeRequest request, CancellationToken cancellationToken) =>
        Respond(async () => await service.Move(id, request, cancellationToken));

    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpPut("children/order")]
    public Task<IActionResult> Reorder(ReorderAnalysisChildrenRequest request, CancellationToken cancellationToken) =>
        Respond(async () => await service.Reorder(request, cancellationToken));

    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpDelete("document-placements/{id:long}")]
    public Task<IActionResult> RemovePlacement(long id, RemoveAnalysisNodeRequest request, CancellationToken cancellationToken) =>
        Respond(async () => await service.RemovePlacement(id, request, cancellationToken));

    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpDelete("folders/{id:long}")]
    public Task<IActionResult> DeleteFolder(long id, RemoveAnalysisNodeRequest request, CancellationToken cancellationToken) =>
        Respond(async () => await service.DeleteFolder(id, request, cancellationToken));

    private async Task<IActionResult> Respond(Func<Task<object>> action, int successStatus = StatusCodes.Status200OK)
    {
        try { return StatusCode(successStatus, await action()); }
        catch (AnalysisRequestException failure)
        {
            var status = failure.Code switch
            {
                "validation_error" => StatusCodes.Status400BadRequest,
                "not_found" => StatusCodes.Status404NotFound,
                "conflict" => StatusCodes.Status409Conflict,
                "reference_invalid" or "business_rule_violation" => StatusCodes.Status422UnprocessableEntity,
                _ => throw new InvalidOperationException("Unexpected analysis failure code.", failure),
            };
            return StatusCode(status, new ApiErrorResponse(failure.Code, failure.Message, failure.FieldErrors, null));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new ApiErrorResponse("conflict", "节点已被其它操作修改，请重新加载后重试。", null, null));
        }
    }
}
