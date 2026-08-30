using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Attachments.Application;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class RuntimeCapabilitiesApiTests
{
    [Fact]
    public async Task Attachment_capabilities_require_authentication()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/runtime-capabilities/attachments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Attachment_capabilities_return_only_the_public_effective_fields()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.GetAsync("/api/runtime-capabilities/attachments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal)
            {
                "allowedImageExtensions",
                "allowedFileExtensions",
                "maxImageBytes",
                "maxFileBytes",
                "maxStoredAttachmentsPerDocument",
            },
            body.EnumerateObject().Select(item => item.Name).ToHashSet(StringComparer.Ordinal));
        Assert.Equal([".png", ".jpg", ".jpeg", ".gif", ".webp"], Strings(body, "allowedImageExtensions"));
        Assert.Equal(10L * 1024 * 1024, body.GetProperty("maxImageBytes").GetInt64());
        Assert.Equal(50L * 1024 * 1024, body.GetProperty("maxFileBytes").GetInt64());
        Assert.Equal(100, body.GetProperty("maxStoredAttachmentsPerDocument").GetInt32());
        Assert.DoesNotContain("storage", response.Content.Headers.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Attachment_capabilities_and_upload_policy_share_the_configured_subset()
    {
        using var factory = new ConfiguredBootstrapWebApplicationFactory(new Dictionary<string, string>
        {
            ["Attachments:AllowedImageExtensions"] = ".png,.webp",
            ["Attachments:AllowedFileExtensions"] = ".pdf,.md",
            ["Attachments:MaxImageBytes"] = "4096",
            ["Attachments:MaxFileBytes"] = "8192",
            ["Attachments:MaxStoredAttachmentsPerDocument"] = "7",
        });
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.GetAsync("/api/runtime-capabilities/attachments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal([".png", ".webp"], Strings(body, "allowedImageExtensions"));
        Assert.Equal([".pdf", ".md"], Strings(body, "allowedFileExtensions"));
        Assert.Equal(4096, body.GetProperty("maxImageBytes").GetInt64());
        Assert.Equal(8192, body.GetProperty("maxFileBytes").GetInt64());
        Assert.Equal(7, body.GetProperty("maxStoredAttachmentsPerDocument").GetInt32());

        var policy = factory.Services.GetRequiredService<AttachmentFilePolicy>();
        Assert.Equal(".png", policy.ValidateRequest("enabled.png", "image/png").Extension);
        Assert.Throws<AttachmentTypeRejectedException>(() =>
            policy.ValidateRequest("disabled.jpg", "image/jpeg"));
        Assert.Throws<AttachmentTypeRejectedException>(() =>
            policy.ValidateRequest("disabled.txt", "text/plain"));
    }

    private static string[] Strings(JsonElement body, string property) =>
        body.GetProperty(property).EnumerateArray().Select(item => item.GetString()!).ToArray();
}
