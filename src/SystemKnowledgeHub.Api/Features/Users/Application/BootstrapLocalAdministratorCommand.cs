using Microsoft.Extensions.DependencyInjection;

namespace SystemKnowledgeHub.Api.Features.Users.Application;

public static class BootstrapLocalAdministratorCommand
{
    public static bool IsRequested(string[] args) =>
        args.Length > 0 && string.Equals(args[0], "bootstrap-local-admin", StringComparison.Ordinal);

    public static async Task<int> RunAsync(
        string[] args,
        IServiceProvider services,
        LocalAuthenticationOptions options,
        TextReader? input = null,
        TextWriter? output = null,
        TextWriter? error = null)
    {
        output ??= Console.Out;
        error ??= Console.Error;
        input ??= Console.In;
        if (!options.Enabled)
        {
            await error.WriteLineAsync("本地认证未启用，拒绝 bootstrap-local-admin。");
            return 1;
        }
        if (!TryParse(args, out var request, out var passwordFromStdin, out var parseError))
        {
            await error.WriteLineAsync(parseError);
            return 1;
        }

        string? password;
        if (passwordFromStdin)
        {
            password = await input.ReadLineAsync();
        }
        else
        {
            password = ReadHiddenPassword(output, error);
            if (password is not null)
            {
                var confirmation = ReadHiddenPassword(output, error, "Confirm password: ");
                if (!string.Equals(password, confirmation, StringComparison.Ordinal))
                {
                    await error.WriteLineAsync("两次密码输入不一致。");
                    return 1;
                }
            }
        }
        if (password is null)
        {
            await error.WriteLineAsync("未读取到密码。");
            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<LocalAdminBootstrapService>();
        var result = await service.BootstrapAsync(new LocalAdminBootstrapRequest(
            request.Username,
            request.DisplayName,
            request.UserId,
            password), CancellationToken.None);
        if (!result.Succeeded)
        {
            await error.WriteLineAsync(result.Error);
            return 1;
        }
        await output.WriteLineAsync("本地 Administrator bootstrap 已完成。");
        return 0;
    }

    private static bool TryParse(string[] args, out BootstrapRequest request, out bool passwordFromStdin, out string error)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        passwordFromStdin = false;
        for (var index = 1; index < args.Length; index++)
        {
            if (args[index] == "--password-stdin")
            {
                if (passwordFromStdin) break;
                passwordFromStdin = true;
                continue;
            }
            if (!args[index].StartsWith("--", StringComparison.Ordinal)
                || index + 1 >= args.Length
                || values.ContainsKey(args[index]))
            {
                request = default!;
                error = "用法：bootstrap-local-admin --username <username> [--display-name <name>] [--user-id <id>] [--password-stdin]";
                return false;
            }
            values[args[index]] = args[++index];
        }
        if (!values.TryGetValue("--username", out var username) || string.IsNullOrWhiteSpace(username)
            || values.ContainsKey("--password"))
        {
            request = default!;
            error = "--username 为必填参数；不支持 --password，请使用隐藏输入或 --password-stdin。";
            return false;
        }
        long? userId = null;
        if (values.TryGetValue("--user-id", out var rawUserId))
        {
            if (!long.TryParse(rawUserId, out var parsedUserId))
            {
                request = default!;
                error = "--user-id 必须是正整数。";
                return false;
            }
            userId = parsedUserId;
        }
        request = new BootstrapRequest(username, values.GetValueOrDefault("--display-name") ?? username, userId);
        error = string.Empty;
        return true;
    }

    private static string? ReadHiddenPassword(TextWriter output, TextWriter error, string prompt = "Password: ")
    {
        if (Console.IsInputRedirected)
        {
            error.WriteLine("标准输入已重定向，请使用 --password-stdin。 ");
            return null;
        }
        output.Write(prompt);
        var characters = new List<char>();
        ConsoleKeyInfo key;
        while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
        {
            if (key.Key == ConsoleKey.Backspace)
            {
                if (characters.Count > 0) characters.RemoveAt(characters.Count - 1);
                continue;
            }
            if (!char.IsControl(key.KeyChar)) characters.Add(key.KeyChar);
        }
        output.WriteLine();
        return new string(characters.ToArray());
    }

    private sealed record BootstrapRequest(string Username, string DisplayName, long? UserId);
}
