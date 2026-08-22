namespace SystemKnowledgeHub.Api.Features.Users.Domain;

/// <summary>
/// 供本地用户名和密码认证使用的凭据，不承载 canonical User 的业务 Profile 或授权信息。
/// </summary>
public sealed class LocalLoginCredential
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public User? User { get; set; }
    public string Username { get; set; } = string.Empty;
    public string NormalizedUsername { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int FailedLoginAttempts { get; set; }
    public DateTimeOffset? FailedLoginWindowStartedAt { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public long SessionVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset LastPasswordChangedAt { get; set; }
    public long Version { get; set; } = 1;
}
