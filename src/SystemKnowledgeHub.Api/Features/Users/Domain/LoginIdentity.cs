namespace SystemKnowledgeHub.Api.Features.Users.Domain;

public sealed class LoginIdentity
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; } = 1;
}
