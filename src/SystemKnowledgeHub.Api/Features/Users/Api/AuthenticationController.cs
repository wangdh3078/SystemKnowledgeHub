using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.Users.Api;

[ApiController]
[Route("auth")]
public sealed class AuthenticationController(
    IOptions<LocalAuthenticationOptions> localOptions,
    IOptions<OidcAuthenticationOptions> oidcOptions,
    LocalLoginService localLoginService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl)
    {
        if (!oidcOptions.Value.Enabled) return NotFound();
        var redirectUri = IsSafeLocalReturnUrl(returnUrl) ? returnUrl! : "/";
        return Challenge(
            new AuthenticationProperties { RedirectUri = redirectUri },
            "EnterpriseOidc");
    }

    [AllowAnonymous]
    [HttpGet("/api/auth/options")]
    public IActionResult GetOptions()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(new
        {
            localLoginEnabled = localOptions.Value.Enabled,
            oidcLoginEnabled = oidcOptions.Value.Enabled,
            oidcDisplayName = oidcOptions.Value.Enabled ? oidcOptions.Value.DisplayName : null,
        });
    }

    [AllowAnonymous]
    [EnableRateLimiting(LocalLoginRateLimitPolicy.Name)]
    [HttpPost("local/login")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> LocalLogin([FromBody] LocalLoginRequest? request, CancellationToken cancellationToken)
    {
        if (!localOptions.Value.Enabled) return NotFound();
        if (User.Identity?.IsAuthenticated == true)
        {
            return Conflict(new ApiErrorResponse(
                "already_authenticated",
                "当前已有有效登录会话，请先退出后再登录。",
                null,
                null));
        }

        var result = await localLoginService.LoginAsync(request?.Username, request?.Password, cancellationToken);
        if (result.Failure != LocalLoginFailure.None || result.Principal is null)
        {
            return Unauthorized(new ApiErrorResponse(
                "invalid_credentials",
                "用户名或密码错误，或当前账号暂不可用。",
                null,
                null));
        }

        await HttpContext.SignInAsync(CurrentUserContext.CookieScheme, result.Principal);
        return NoContent();
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await HttpContext.SignOutAsync(CurrentUserContext.CookieScheme);
        return NoContent();
    }

    private static bool IsSafeLocalReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl)
        && returnUrl.StartsWith("/", StringComparison.Ordinal)
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        && !returnUrl.StartsWith("/\\", StringComparison.Ordinal);

    public sealed record LocalLoginRequest(string? Username, string? Password);
}
