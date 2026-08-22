using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using Xunit.Abstractions;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class LocalCredentialSecurityTests(ITestOutputHelper output)
{
    [Fact]
    public void Password_policy_hashing_and_username_normalization_preserve_the_approved_boundaries()
    {
        Assert.True(LocalCredentialSecurity.TryNormalizeUsername("  王大虎01  ", out var username, out var normalized));
        Assert.Equal("王大虎01", username);
        Assert.Equal("ADMIN", Normalize("Admin"));
        Assert.Equal(Normalize("Admin"), Normalize("admin"));
        Assert.False(LocalCredentialSecurity.TryNormalizeUsername("local admin", out _, out _));
        Assert.True(LocalCredentialSecurity.IsValidPassword(" leading and trailing whitespace are significant "));
        Assert.False(LocalCredentialSecurity.IsValidPassword("too-short"));
        Assert.False(LocalCredentialSecurity.IsValidPassword(new string('a', 129)));

        var password = "  Unicode 密码与空格保持原样 2026!  ";
        var service = new LocalPasswordService(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            IterationCount = 220_000,
        }));
        var credential = new LocalLoginCredential();
        var hash = service.Hash(credential, password);
        var timer = Stopwatch.StartNew();
        var result = service.Verify(credential, hash, password);
        timer.Stop();

        Assert.NotEqual(password, hash);
        Assert.Equal(PasswordVerificationResult.Success, result);
        Assert.Equal(PasswordVerificationResult.Failed, service.Verify(credential, hash, password.Trim()));
        output.WriteLine($"PasswordHasher IdentityV3 220000 iterations verification: {timer.Elapsed.TotalMilliseconds:F0} ms");
    }

    private static string Normalize(string value)
    {
        Assert.True(LocalCredentialSecurity.TryNormalizeUsername(value, out _, out var normalized));
        return normalized;
    }
}
