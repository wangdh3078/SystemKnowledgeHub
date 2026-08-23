using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.Users.Application;

public static class LocalCredentialSecurity
{
    public const int MinimumPasswordLength = 8;
    public const int MaximumPasswordLength = 128;
    public const int MinimumUsernameLength = 3;
    public const int MaximumUsernameLength = 64;

    public static bool TryNormalizeUsername(string? username, out string displayUsername, out string normalizedUsername)
    {
        displayUsername = username?.Trim() ?? string.Empty;
        normalizedUsername = string.Empty;
        if (displayUsername.Length is < MinimumUsernameLength or > MaximumUsernameLength)
        {
            return false;
        }

        foreach (var character in displayUsername.EnumerateRunes())
        {
            if (Rune.IsControl(character) || Rune.IsWhiteSpace(character)
                || (!Rune.IsLetterOrDigit(character) && character.Value is not '.' and not '_' and not '-' and not '@'))
            {
                return false;
            }
        }

        normalizedUsername = displayUsername.Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        return normalizedUsername.Length > 0;
    }

    public static bool IsValidPassword(string? password) => password is not null
        && password.Length >= MinimumPasswordLength
        && password.Length <= MaximumPasswordLength;
}

/// <summary>
/// 持有 PasswordHasher 与进程级 dummy hash，避免不存在用户名时跳过同等级 KDF 验证。
/// </summary>
public sealed class LocalPasswordService
{
    private readonly PasswordHasher<LocalLoginCredential> _hasher;
    private readonly string _dummyHash;

    public LocalPasswordService(IOptions<PasswordHasherOptions> options)
    {
        _hasher = new PasswordHasher<LocalLoginCredential>(options);
        _dummyHash = _hasher.HashPassword(new LocalLoginCredential(), Guid.NewGuid().ToString("N"));
    }

    public string Hash(LocalLoginCredential credential, string password) => _hasher.HashPassword(credential, password);

    public PasswordVerificationResult Verify(LocalLoginCredential credential, string hash, string password) =>
        _hasher.VerifyHashedPassword(credential, hash, password);

    public void VerifyDummy(string password) =>
        _ = _hasher.VerifyHashedPassword(new LocalLoginCredential(), _dummyHash, password);
}
