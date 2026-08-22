using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.Users.Application;

public static class BootstrapAdministratorCommand
{
    public static bool IsRequested(string[] args) =>
        args.Length > 0 && string.Equals(args[0], "bootstrap-admin", StringComparison.Ordinal);

    public static async Task<int> RunAsync(
        string[] args,
        IServiceProvider services,
        OidcAuthenticationOptions oidcOptions)
    {
        if (!TryParse(args, out var request, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        if (string.IsNullOrWhiteSpace(oidcOptions.Provider)
            || !string.Equals(request.Provider, oidcOptions.Provider, StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Provider 不在已配置的 OIDC allowlist 中。");
            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        await dbContext.Database.MigrateAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        if (await dbContext.Users.AnyAsync(user => user.IsActive && user.AccessLevel == AccessLevel.Administrator))
        {
            Console.Error.WriteLine("已存在启用的 Administrator，拒绝再次 bootstrap。");
            return 1;
        }
        if (await dbContext.LoginIdentities.AnyAsync(identity =>
                identity.Provider == request.Provider && identity.Subject == request.Subject))
        {
            Console.Error.WriteLine("该 Provider + Subject 已绑定，拒绝覆盖现有映射。");
            return 1;
        }

        var timestamp = DateTimeOffset.UtcNow;
        User user;
        if (request.UserId is long userId)
        {
            user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId)
                ?? throw new InvalidOperationException("指定的 UserId 不存在。");
            if (!user.IsActive)
            {
                throw new InvalidOperationException("指定的 User 必须为启用状态。");
            }
            user.AccessLevel = AccessLevel.Administrator;
            user.UpdatedAt = timestamp;
            user.Version += 1;
        }
        else
        {
            user = new User
            {
                DisplayName = request.DisplayName,
                EmployeeNo = NormalizeOptional(request.EmployeeNo),
                Email = NormalizeOptional(request.Email),
                IsActive = true,
                AccessLevel = AccessLevel.Administrator,
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
                Version = 1,
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        }

        dbContext.LoginIdentities.Add(new LoginIdentity
        {
            UserId = user.Id,
            Provider = request.Provider,
            Subject = request.Subject,
            IsActive = true,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        });
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        Console.WriteLine($"Bootstrap Administrator 已创建：UserId={user.Id}，Provider={request.Provider}。");
        return 0;
    }

    private static bool TryParse(string[] args, out BootstrapRequest request, out string error)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal)
                || index + 1 >= args.Length
                || values.ContainsKey(args[index]))
            {
                request = default!;
                error = "用法：bootstrap-admin --provider <key> --subject <exact-subject> --display-name <name> [--employee-no <value>] [--email <value>] [--user-id <id>]";
                return false;
            }
            values[args[index]] = args[index + 1];
        }

        if (!values.TryGetValue("--provider", out var provider)
            || string.IsNullOrWhiteSpace(provider)
            || !values.TryGetValue("--subject", out var subject)
            || string.IsNullOrEmpty(subject)
            || !values.TryGetValue("--display-name", out var displayName)
            || string.IsNullOrWhiteSpace(displayName))
        {
            request = default!;
            error = "--provider、--subject 和 --display-name 为必填参数。";
            return false;
        }

        long? userId = null;
        if (values.TryGetValue("--user-id", out var rawUserId))
        {
            if (!long.TryParse(rawUserId, out var parsedUserId) || !ApiIdParser.IsSafePositive(parsedUserId))
            {
                request = default!;
                error = "--user-id 必须是 JavaScript 安全范围内的正整数。";
                return false;
            }
            userId = parsedUserId;
        }
        if (userId.HasValue && (values.ContainsKey("--employee-no") || values.ContainsKey("--email")))
        {
            request = default!;
            error = "绑定既有 User 时不得同时传入 --employee-no 或 --email。";
            return false;
        }

        request = new BootstrapRequest(
            provider,
            subject,
            displayName.Trim(),
            values.GetValueOrDefault("--employee-no"),
            values.GetValueOrDefault("--email"),
            userId);
        error = string.Empty;
        return true;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private sealed record BootstrapRequest(
        string Provider,
        string Subject,
        string DisplayName,
        string? EmployeeNo,
        string? Email,
        long? UserId);
}
