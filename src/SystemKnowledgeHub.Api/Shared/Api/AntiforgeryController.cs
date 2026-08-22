using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SystemKnowledgeHub.Api.Shared.Api;

[ApiController]
[Route("api/antiforgery")]
public sealed class AntiforgeryController(IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("token")]
    public IActionResult GetToken() => Ok(new
    {
        requestToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken,
    });
}
