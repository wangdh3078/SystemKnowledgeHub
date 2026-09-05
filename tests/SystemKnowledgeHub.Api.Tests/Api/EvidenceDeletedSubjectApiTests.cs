using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class EvidenceDeletedSubjectApiTests
{
    [Theory]
    [InlineData(AccessLevel.Viewer)]
    [InlineData(AccessLevel.Editor)]
    [InlineData(AccessLevel.Administrator)]
    public async Task Deleted_subject_rejects_evidence_correction_and_creation_but_retains_historical_read(AccessLevel access)
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var administrator = factory.CreateAuthenticatedClient();
        using var createSystem = await administrator.PostAsJsonAsync("/api/systems", new
        {
            name = "STABILITY-EVIDENCE",
            displayName = "证据历史测试",
            systemType = "Quality Management System",
            lifecycle = "Running",
            purpose = "验证删除后证据保护",
            actor = new { displayName = "测试" },
        });
        Assert.Equal(HttpStatusCode.Created, createSystem.StatusCode);
        var system = await createSystem.Content.ReadFromJsonAsync<JsonElement>();
        var subjectId = system.GetProperty("id").GetInt64();
        var provider = new { displayName = "测试", roleOrIdentity = "工程师", occurredAt = DateTimeOffset.UtcNow };
        var createRequest = new
        {
            evidenceType = "CodeReference",
            subject = new { type = "System", id = subjectId },
            sourceTitle = "Before",
            sourceReference = "source.cs",
            supportReason = "代码支持该知识",
            provider,
        };
        using var create = await administrator.PostAsJsonAsync("/api/evidence", createRequest);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var evidence = await create.Content.ReadFromJsonAsync<JsonElement>();
        var evidenceId = evidence.GetProperty("id").GetInt64();
        var token = evidence.GetProperty("concurrencyToken").GetString();
        long userId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var user = new User
            {
                DisplayName = "Evidence role test",
                IsActive = true,
                AccessLevel = access,
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }
        using var client = await factory.CreateAuthenticatedClientAsync(userId);
        object Update(string? currentToken) => new
        {
            sourceTitle = "Corrected",
            sourceReference = "corrected.cs",
            supportReason = "更正来源",
            provider,
            actor = new { displayName = "测试" },
            concurrencyToken = currentToken,
        };
        using var current = await client.PutAsJsonAsync($"/api/evidence/{evidenceId}", Update(token));
        Assert.Equal(access == AccessLevel.Viewer ? HttpStatusCode.Forbidden : HttpStatusCode.OK, current.StatusCode);
        if (access != AccessLevel.Viewer)
            token = (await current.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("concurrencyToken").GetString();

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/systems/{subjectId}")
        {
            Content = JsonContent.Create(new { concurrencyToken = system.GetProperty("concurrencyToken").GetString() }),
        };
        using var deleted = await administrator.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        await using var verification = factory.Services.CreateAsyncScope();
        var context = verification.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var before = await context.Evidence.AsNoTracking().SingleAsync(x => x.Id == evidenceId);
        using var rejected = await client.PutAsJsonAsync($"/api/evidence/{evidenceId}", Update(token));
        Assert.Equal(access == AccessLevel.Viewer ? HttpStatusCode.Forbidden : HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        if (access != AccessLevel.Viewer)
            Assert.Equal("reference_invalid", (await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var after = await context.Evidence.AsNoTracking().SingleAsync(x => x.Id == evidenceId);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
        using var historical = await client.GetAsync($"/api/evidence/{evidenceId}");
        Assert.Equal(HttpStatusCode.OK, historical.StatusCode);
        using var addAfterDelete = await administrator.PostAsJsonAsync("/api/evidence", createRequest);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, addAfterDelete.StatusCode);
    }
}
