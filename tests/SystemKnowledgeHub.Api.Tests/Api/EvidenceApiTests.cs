using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class EvidenceApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public EvidenceApiTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task AddEvidence_persists_complete_provider_and_does_not_change_subject_status()
    {
        using var response = await _client.PostAsJsonAsync("/api/evidence", CreateCodeEvidenceRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CodeReference", created.GetProperty("evidenceType").GetString());
        Assert.Equal("Inferred", created.GetProperty("subjectKnowledgeStatus").GetString());
        Assert.False(created.GetProperty("knowledgeStatusChanged").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(created.GetProperty("concurrencyToken").GetString()));

        var id = created.GetProperty("id").GetInt64();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var stored = await dbContext.Evidence.AsNoTracking().SingleAsync(item => item.Id == id);
        var subject = await dbContext.BusinessFunctions.AsNoTracking().SingleAsync(item => item.Id == 77);
        Assert.Equal("制造系统组", stored.ProviderTeam);
        Assert.Equal("Manual", stored.ProviderSource);
        Assert.Null(stored.ProviderUserId);
        Assert.Null(stored.ProviderKnowledgeRoleId);
        Assert.Null(stored.ProviderEmployeeNo);
        Assert.Null(stored.ProviderJobTitle);
        Assert.Equal(KnowledgeStatus.Inferred, subject.KnowledgeStatus);

        using var detailResponse = await _client.GetAsync($"/api/evidence/{id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("MES · Equipment Status Query", detail.GetProperty("subjectContext").GetProperty("title").GetString());
        Assert.Equal("EquipmentStatusService.cs", detail.GetProperty("sourceLocator").GetProperty("file").GetString());
    }

    [Fact]
    public async Task UpdateEvidence_changes_only_correctable_fields_and_rejects_stale_token()
    {
        using var createResponse = await _client.PostAsJsonAsync("/api/evidence", CreateCodeEvidenceRequest());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetInt64();
        var staleToken = created.GetProperty("concurrencyToken").GetString();

        var updateRequest = new
        {
            sourceTitle = "EquipmentStatusService.cs : line 184-190",
            sourceReference = "EquipmentStatusService.cs",
            sourceLocator = new { repository = "mes-legacy", file = "EquipmentStatusService.cs", startLine = 185, endLine = 190 },
            summary = "状态分支经过人工核对",
            supportReason = "代码分支直接支持该功能的状态计算",
            confidence = "High",
            provider = Person("Manual correction", "修正行号"),
            actor = new { displayName = "王敏", role = "知识整理人员" },
            concurrencyToken = staleToken,
        };
        using var updateResponse = await _client.PutAsJsonAsync($"/api/evidence/{id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("EquipmentStatusService.cs : line 184-190", updated.GetProperty("sourceTitle").GetString());
        Assert.Equal(185, updated.GetProperty("sourceLocator").GetProperty("startLine").GetInt32());
        Assert.Equal("BusinessFunction", updated.GetProperty("subject").GetProperty("type").GetString());
        Assert.Equal(77, updated.GetProperty("subject").GetProperty("id").GetInt64());
        Assert.NotEqual(staleToken, updated.GetProperty("concurrencyToken").GetString());

        using var staleResponse = await _client.PutAsJsonAsync($"/api/evidence/{id}", updateRequest);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
    }

    [Fact]
    public async Task AddHumanConfirmation_requires_available_current_user_context()
    {
        using var unauthenticated = await _factory.CreateClient().PostAsJsonAsync(
            "/api/evidence/human-confirmations", HumanConfirmationRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.Equal("unauthenticated", await ErrorCode(unauthenticated));

        var inactiveUser = await CreateUser([]);
        var inactiveUserId = inactiveUser.GetProperty("id").GetInt64();
        using var disable = await _client.PutAsJsonAsync($"/api/users/{inactiveUserId}/active-state", new
        {
            isActive = false,
            actor = Actor(),
            concurrencyToken = inactiveUser.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);

        using var inactiveClient = await _factory.CreateAuthenticatedClientAsync(inactiveUserId);
        using var inactive = await inactiveClient.PostAsJsonAsync(
            "/api/evidence/human-confirmations", HumanConfirmationRequest());
        Assert.Equal(HttpStatusCode.Forbidden, inactive.StatusCode);
        Assert.Equal("account_inactive", await ErrorCode(inactive));
    }

    [Fact]
    public async Task AddHumanConfirmation_without_active_role_uses_canonical_fallback_snapshot()
    {
        var user = await CreateUser(
            [],
            displayName: "U04 无角色确认人",
            employeeNo: $"U04-ZERO-{UniqueSuffix()}",
            departmentOrTeam: "制造知识组",
            jobTitle: "知识工程师");

        using var response = await PostHumanConfirmation(
            user.GetProperty("id").GetInt64().ToString(),
            HumanConfirmationRequest(confirmationMethod: "Meeting"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("HumanConfirmation", created.GetProperty("evidenceType").GetString());
        Assert.Equal("Inferred", created.GetProperty("subjectKnowledgeStatus").GetString());
        Assert.False(created.GetProperty("knowledgeStatusChanged").GetBoolean());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("knowledgeDocumentRevisionNumberSnapshot").ValueKind);
        Assert.Equal(
            new[]
            {
                "concurrencyToken", "evidenceType", "id", "knowledgeDocumentRevisionNumberSnapshot", "knowledgeStatusChanged",
                "sourceTitle", "subject", "subjectDetailKey", "subjectKnowledgeStatus",
            },
            created.EnumerateObject().Select(property => property.Name).Order().ToArray());

        var id = created.GetProperty("id").GetInt64();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var stored = await dbContext.Evidence.AsNoTracking().SingleAsync(item => item.Id == id);
        var subject = await dbContext.BusinessFunctions.AsNoTracking().SingleAsync(item => item.Id == 77);
        Assert.Equal(EvidenceType.HumanConfirmation, stored.EvidenceType);
        Assert.Equal(user.GetProperty("id").GetInt64(), stored.ProviderUserId);
        Assert.Null(stored.ProviderKnowledgeRoleId);
        Assert.Equal(user.GetProperty("employeeNo").GetString(), stored.ProviderEmployeeNo);
        Assert.Equal("U04 无角色确认人", stored.ProviderName);
        Assert.Equal("知识提供者（未配置知识身份）", stored.ProviderRole);
        Assert.Equal("制造知识组", stored.ProviderTeam);
        Assert.Equal("知识工程师", stored.ProviderJobTitle);
        Assert.Null(stored.ProviderExternalKey);
        Assert.Null(stored.ProviderSource);
        Assert.Null(stored.ProviderNote);
        Assert.Equal(DateTimeOffset.Parse("2026-08-22T02:30:00Z"), stored.ProvidedAt);
        using var locator = JsonDocument.Parse(stored.SourceLocatorJson!);
        Assert.Equal("Meeting", locator.RootElement.GetProperty("confirmationMethod").GetString());
        Assert.Equal(KnowledgeStatus.Inferred, subject.KnowledgeStatus);
    }

    [Fact]
    public async Task AddHumanConfirmation_uses_authenticated_principal_when_current_user_header_is_forged()
    {
        var userA = await CreateUser([], displayName: "SEC-01 身份用户 A");
        var userB = await CreateUser([], displayName: "SEC-01 伪造目标用户 B");
        var userAId = userA.GetProperty("id").GetInt64();
        var userBId = userB.GetProperty("id").GetInt64();
        using var clientA = await _factory.CreateAuthenticatedClientAsync(userAId);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/evidence/human-confirmations")
        {
            Content = JsonContent.Create(HumanConfirmationRequest()),
        };
        request.Headers.TryAddWithoutValidation("X-Current-User-Id", userBId.ToString());

        using var response = await clientA.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var evidenceId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var stored = await dbContext.Evidence.AsNoTracking().SingleAsync(item => item.Id == evidenceId);
        Assert.Equal(userAId, stored.ProviderUserId);
        Assert.NotEqual(userBId, stored.ProviderUserId);
    }

    [Fact]
    public async Task AddHumanConfirmation_auto_selects_single_role_and_keeps_historical_snapshot()
    {
        var role = await CreateKnowledgeRole("U04 单角色");
        var roleId = role.GetProperty("id").GetInt64();
        var user = await CreateUser(
            [roleId],
            displayName: "U04 历史姓名",
            employeeNo: $"U04-ONE-{UniqueSuffix()}",
            departmentOrTeam: "历史团队",
            jobTitle: "历史职位");
        var userId = user.GetProperty("id").GetInt64();

        using var response = await PostHumanConfirmation(userId.ToString(), HumanConfirmationRequest());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var evidenceId = created.GetProperty("id").GetInt64();

        using var updateUser = await _client.PutAsJsonAsync($"/api/users/{userId}", new
        {
            employeeNo = $"U04-NEW-{UniqueSuffix()}",
            displayName = "U04 新姓名",
            email = (string?)null,
            departmentOrTeam = "新团队",
            jobTitle = "新职位",
            knowledgeRoleIds = new[] { roleId },
            actor = Actor(),
            concurrencyToken = user.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, updateUser.StatusCode);

        using var renameRole = await _client.PutAsJsonAsync($"/api/knowledge-roles/{roleId}", new
        {
            name = $"U04 已改名-{UniqueSuffix()}",
            description = "renamed after confirmation",
            actor = Actor(),
            concurrencyToken = role.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, renameRole.StatusCode);
        var renamedRole = await renameRole.Content.ReadFromJsonAsync<JsonElement>();
        using var disableRole = await _client.PutAsJsonAsync($"/api/knowledge-roles/{roleId}/active-state", new
        {
            isActive = false,
            actor = Actor(),
            concurrencyToken = renamedRole.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, disableRole.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var stored = await dbContext.Evidence.AsNoTracking().SingleAsync(item => item.Id == evidenceId);
        Assert.Equal(userId, stored.ProviderUserId);
        Assert.Equal(roleId, stored.ProviderKnowledgeRoleId);
        Assert.Equal(user.GetProperty("employeeNo").GetString(), stored.ProviderEmployeeNo);
        Assert.Equal("U04 历史姓名", stored.ProviderName);
        Assert.Equal("历史团队", stored.ProviderTeam);
        Assert.Equal("历史职位", stored.ProviderJobTitle);
        Assert.Equal(role.GetProperty("name").GetString(), stored.ProviderRole);
    }

    [Fact]
    public async Task AddHumanConfirmation_enforces_multiple_role_selection_and_role_validity()
    {
        var roleOne = await CreateKnowledgeRole("U04 多角色一");
        var roleTwo = await CreateKnowledgeRole("U04 多角色二");
        var unassigned = await CreateKnowledgeRole("U04 未分配角色");
        var inactive = await CreateKnowledgeRole("U04 停用角色");
        var inactiveId = inactive.GetProperty("id").GetInt64();
        using var disable = await _client.PutAsJsonAsync($"/api/knowledge-roles/{inactiveId}/active-state", new
        {
            isActive = false,
            actor = Actor(),
            concurrencyToken = inactive.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);

        var user = await CreateUser([
            roleOne.GetProperty("id").GetInt64(),
            roleTwo.GetProperty("id").GetInt64(),
        ]);
        var userId = user.GetProperty("id").GetInt64().ToString();

        using var missingRole = await PostHumanConfirmation(userId, HumanConfirmationRequest());
        Assert.Equal(HttpStatusCode.BadRequest, missingRole.StatusCode);
        var missingError = await missingRole.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(missingError.GetProperty("fieldErrors").TryGetProperty("knowledgeRoleId", out _));

        using var selected = await PostHumanConfirmation(
            userId,
            HumanConfirmationRequest(roleTwo.GetProperty("id").GetInt64()));
        Assert.Equal(HttpStatusCode.Created, selected.StatusCode);

        using var notFound = await PostHumanConfirmation(userId, HumanConfirmationRequest(9007199254740991));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, notFound.StatusCode);
        Assert.Equal("reference_invalid", await ErrorCode(notFound));

        using var inactiveRole = await PostHumanConfirmation(userId, HumanConfirmationRequest(inactiveId));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, inactiveRole.StatusCode);
        Assert.Equal("invalid_state", await ErrorCode(inactiveRole));

        using var notAssigned = await PostHumanConfirmation(
            userId,
            HumanConfirmationRequest(unassigned.GetProperty("id").GetInt64()));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, notAssigned.StatusCode);
        Assert.Equal("reference_invalid", await ErrorCode(notAssigned));
    }

    [Fact]
    public async Task LegacyHumanConfirmation_keeps_null_references_and_provider_source_method()
    {
        long evidenceId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var legacy = new Evidence
            {
                EvidenceType = EvidenceType.HumanConfirmation,
                SubjectType = EvidenceSubjectType.BusinessFunction,
                SubjectId = 77,
                SubjectDetailKey = "Purpose",
                SourceTitle = "人工确认 · 历史人员",
                SourceReference = "历史评审",
                SourceLocatorJson = "{\"confirmationStatement\":\"历史确认\",\"sourceNote\":\"历史评审\"}",
                Summary = "历史确认",
                SupportReason = "历史人员确认",
                ProviderName = "历史人员",
                ProviderRole = "历史业务专家",
                ProviderSource = "OnSite",
                ProvidedAt = DateTimeOffset.Parse("2026-08-15T02:00:00Z"),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Version = 1,
            };
            dbContext.Evidence.Add(legacy);
            await dbContext.SaveChangesAsync();
            evidenceId = legacy.Id;
        }

        using var response = await _client.GetAsync($"/api/evidence/{evidenceId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OnSite", detail.GetProperty("provider").GetProperty("source").GetString());

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var stored = await verificationDb.Evidence.AsNoTracking().SingleAsync(item => item.Id == evidenceId);
        Assert.Null(stored.ProviderUserId);
        Assert.Null(stored.ProviderKnowledgeRoleId);
        Assert.Null(stored.ProviderEmployeeNo);
        Assert.Null(stored.ProviderJobTitle);
    }

    private static object CreateCodeEvidenceRequest()
    {
        return new
        {
            evidenceType = "CodeReference",
            subject = new { type = "BusinessFunction", id = 77 },
            subjectDetailKey = "Purpose",
            sourceTitle = "EquipmentStatusService.cs : line 184",
            sourceReference = "EquipmentStatusService.cs",
            sourceLocator = new { repository = "mes-legacy", file = "EquipmentStatusService.cs", startLine = 184, endLine = 190 },
            summary = "状态查询分支",
            supportReason = "代码分支直接支持该功能用途",
            confidence = "High",
            provider = Person("Manual", null),
        };
    }

    private static object Person(
        string source,
        string? note,
        string displayName = "王敏",
        string role = "证据提供人")
    {
        return new
        {
            displayName,
            roleOrIdentity = role,
            occurredAt = "2026-08-15T02:00:00Z",
            team = role == "MES 业务专家" ? "MES 运维组" : "制造系统组",
            externalUserKey = (string?)null,
            source,
            note,
        };
    }

    private async Task<JsonElement> CreateKnowledgeRole(string namePrefix)
    {
        using var response = await _client.PostAsJsonAsync("/api/knowledge-roles", new
        {
            name = $"{namePrefix}-{UniqueSuffix()}",
            description = "U04 Evidence integration test",
            actor = Actor(),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private async Task<JsonElement> CreateUser(
        IReadOnlyList<long> roleIds,
        string displayName = "U04 当前操作者",
        string? employeeNo = null,
        string? departmentOrTeam = "知识平台组",
        string? jobTitle = "知识工程师")
    {
        var suffix = UniqueSuffix();
        using var response = await _client.PostAsJsonAsync("/api/users", new
        {
            employeeNo = employeeNo ?? $"U04-{suffix}",
            displayName,
            email = $"u04-{suffix}@example.com",
            departmentOrTeam,
            jobTitle,
            knowledgeRoleIds = roleIds,
            loginSetup = new { type = "none" },
            actor = Actor(),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        var userId = created.GetProperty("id").GetInt64();
        using var accessLevel = await _client.PutAsJsonAsync($"/api/users/{userId}/access-level", new
        {
            accessLevel = "Editor",
            concurrencyToken = created.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, accessLevel.StatusCode);
        using var detail = await _client.GetAsync($"/api/users/{userId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        return (await detail.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private async Task<HttpResponseMessage> PostHumanConfirmation(string currentUserId, object body)
    {
        var client = await _factory.CreateAuthenticatedClientAsync(long.Parse(currentUserId));
        return await client.PostAsJsonAsync("/api/evidence/human-confirmations", body);
    }

    private static object HumanConfirmationRequest(
        long? knowledgeRoleId = null,
        string confirmationMethod = "InSystem") => new
        {
            subject = new { type = "BusinessFunction", id = 77 },
            subjectDetailKey = "Purpose",
            knowledgeRoleId,
            confirmationMethod,
            confirmedAt = "2026-08-22T02:30:00Z",
            confirmationStatement = "确认该功能用于查询设备状态并计算展示状态。",
            supportReason = "当前业务操作者确认生产语义。",
            sourceNote = "U04 评审会议",
        };

    private static async Task<string?> ErrorCode(HttpResponseMessage response)
    {
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        return error.GetProperty("code").GetString();
    }

    private static object Actor() => new { displayName = "U04 测试人员", role = "知识整理人员" };

    private static string UniqueSuffix() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
