namespace SystemKnowledgeHub.Api.Features.Users.Application;

public sealed class LocalAuthenticationOptions
{
    public bool Enabled { get; init; }
    public LocalLockoutOptions Lockout { get; init; } = new();
    public LocalRateLimitOptions RateLimit { get; init; } = new();
}

public sealed class LocalLockoutOptions
{
    public int MaxFailedAttempts { get; init; } = 5;
    public int WindowMinutes { get; init; } = 15;
    public int DurationMinutes { get; init; } = 15;
}

public sealed class LocalRateLimitOptions
{
    public int PermitLimit { get; init; } = 20;
    public int WindowMinutes { get; init; } = 5;
}
