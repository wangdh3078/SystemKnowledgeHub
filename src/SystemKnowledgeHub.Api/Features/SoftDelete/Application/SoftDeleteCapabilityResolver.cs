using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.SoftDelete.Application;

/// <summary>Projects the current user's delete capability without exposing creator identifiers to the UI.</summary>
public sealed class SoftDeleteCapabilityResolver(ICurrentUserContext currentUserContext)
{
    public async Task<SoftDeleteActor?> ResolveActor(CancellationToken cancellationToken)
    {
        var resolution = await currentUserContext.ResolveAsync(cancellationToken);
        if (resolution.Status != CurrentUserResolutionStatus.Available
            || resolution.CurrentUser is null
            || !Enum.TryParse<AccessLevel>(resolution.CurrentUser.AccessLevel, out var accessLevel))
        {
            return null;
        }

        return new SoftDeleteActor(
            resolution.CurrentUser.Id,
            resolution.CurrentUser.DisplayName,
            accessLevel);
    }

    public static bool CanDelete(SoftDeleteActor? actor, long? createdByUserId) =>
        actor is not null && SoftDeleteAuthorization.CanDelete(actor, createdByUserId);
}
