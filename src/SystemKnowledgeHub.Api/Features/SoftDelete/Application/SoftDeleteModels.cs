using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.SoftDelete.Application;

public sealed record SoftDeleteActor(
    long UserId,
    string DisplayName,
    AccessLevel AccessLevel);

public sealed record DeleteDependencyBlocker(
    string DependencyType,
    string DisplayName,
    int Count);

public enum SoftDeleteFailure
{
    None,
    Validation,
    NotFound,
    Forbidden,
    Conflict,
    Dependencies,
}

public sealed record SoftDeleteResult(
    SoftDeleteFailure Failure,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    IReadOnlyList<DeleteDependencyBlocker>? Blockers = null);

public static class SoftDeleteAuthorization
{
    public static bool CanDelete(SoftDeleteActor actor, long? createdByUserId) =>
        actor.AccessLevel == AccessLevel.Administrator
        || (actor.AccessLevel == AccessLevel.Editor
            && createdByUserId.HasValue
            && createdByUserId.Value == actor.UserId);
}
