using Microsoft.Extensions.Configuration;
using SystemKnowledgeHub.Api.Features.Attachments.Application;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Application;

public sealed class AttachmentOptionsTests
{
    [Fact]
    public void Defaults_use_the_closed_supported_catalog_and_safe_request_limits()
    {
        var result = AttachmentOptions.TryCreate(
            Configuration(),
            Environment(),
            out var options,
            out var error);

        Assert.True(result, error);
        Assert.NotNull(options);
        Assert.Equal([".png", ".jpg", ".jpeg", ".gif", ".webp"], options.AllowedImageExtensions);
        Assert.Equal(
            [".pdf", ".docx", ".xlsx", ".pptx", ".txt", ".log", ".sql", ".md", ".csv", ".json", ".xml", ".zip"],
            options.AllowedFileExtensions);
        Assert.Equal(10L * 1024 * 1024, options.MaxImageBytes);
        Assert.Equal(50L * 1024 * 1024, options.MaxFileBytes);
        Assert.Equal(100, options.MaxStoredAttachmentsPerDocument);
        Assert.Equal(52L * 1024 * 1024, options.MaximumRequestBodyBytes);
    }

    [Fact]
    public void Configured_subset_and_limits_are_preserved_and_gate_new_uploads()
    {
        var result = AttachmentOptions.TryCreate(
            Configuration(new Dictionary<string, string?>
            {
                ["Attachments:AllowedImageExtensions"] = ".webp,.png",
                ["Attachments:AllowedFileExtensions"] = ".md,.pdf",
                ["Attachments:MaxImageBytes"] = "4096",
                ["Attachments:MaxFileBytes"] = "8192",
                ["Attachments:MaxStoredAttachmentsPerDocument"] = "7",
                ["Attachments:MemoryBufferThresholdBytes"] = "2048",
                ["Attachments:UploadRequestOverheadBytes"] = "1024",
            }),
            Environment(),
            out var options,
            out var error);

        Assert.True(result, error);
        Assert.NotNull(options);
        Assert.Equal([".webp", ".png"], options.AllowedImageExtensions);
        Assert.Equal([".md", ".pdf"], options.AllowedFileExtensions);
        Assert.Equal(9_216, options.MaximumRequestBodyBytes);
        var policy = new AttachmentFilePolicy(options);
        Assert.Equal(".png", policy.ValidateRequest("enabled.png", "image/png").Extension);
        Assert.Equal(".md", policy.ValidateRequest("enabled.md", "text/markdown").Extension);
        Assert.Contains(
            "未启用",
            Assert.Throws<AttachmentTypeRejectedException>(() =>
                policy.ValidateRequest("disabled.jpg", "image/jpeg")).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "未启用",
            Assert.Throws<AttachmentTypeRejectedException>(() =>
                policy.ValidateRequest("disabled.txt", "text/plain")).Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Attachments:AllowedImageExtensions", ".png,.png", "cannot contain duplicates")]
    [InlineData("Attachments:AllowedImageExtensions", ".png,.pdf", "unsupported or non-image")]
    [InlineData("Attachments:AllowedImageExtensions", ".PNG", "unsupported or non-image")]
    [InlineData("Attachments:AllowedFileExtensions", ".pdf,.png", "unsupported or non-file")]
    [InlineData("Attachments:AllowedFileExtensions", ".exe", "unsupported or non-file")]
    public void Invalid_extension_configuration_fails_closed(
        string key,
        string value,
        string expectedError)
    {
        var result = AttachmentOptions.TryCreate(
            Configuration(new Dictionary<string, string?> { [key] = value }),
            Environment(),
            out var options,
            out var error);

        Assert.False(result);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Contains(expectedError, error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Attachments:MaxFileBytes", "0")]
    [InlineData("Attachments:MemoryBufferThresholdBytes", "4194305")]
    [InlineData("Attachments:UploadRequestOverheadBytes", "16777217")]
    [InlineData("Attachments:PreviewCsvMaxRows", "2001")]
    public void Invalid_attachment_limits_fail_closed(string key, string value)
    {
        var result = AttachmentOptions.TryCreate(
            Configuration(new Dictionary<string, string?> { [key] = value }),
            Environment(),
            out var options,
            out var error);

        Assert.False(result);
        Assert.Null(options);
        Assert.Contains("safety ceilings", error, StringComparison.Ordinal);
    }

    private static IConfiguration Configuration(
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Attachments:StorageRoot"] = "attachments",
        };
        if (overrides is not null)
        {
            foreach (var value in overrides) values[value.Key] = value.Value;
        }
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static TestWebHostEnvironment Environment() => new(Path.Combine(
        Path.GetTempPath(),
        "SystemKnowledgeHub.Api.Tests",
        "attachment-options",
        Guid.NewGuid().ToString("N")));
}
