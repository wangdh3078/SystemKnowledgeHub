using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class UsersApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UsersApiTests(BootstrapWebApplicationFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task User_create_list_detail_and_case_insensitive_uniqueness_use_sqlite()
    {
        var role = await CreateKnowledgeRole("MES 业务专家");
        var suffix = UniqueSuffix();
        var employeeNo = $"EMP-{suffix}";
        var email = $"user-{suffix}@example.com";

        var created = await CreateUser(employeeNo, email, [role.GetProperty("id").GetInt64()]);

        Assert.True(created.GetProperty("isActive").GetBoolean());
        Assert.Equal(employeeNo, created.GetProperty("employeeNo").GetString());
        Assert.Single(created.GetProperty("knowledgeRoles").EnumerateArray());

        using var listResponse = await _client.GetAsync($"/api/users?keyword={Uri.EscapeDataString(employeeNo.ToLowerInvariant())}&isActive=true&page=1&pageSize=10&sort=displayName:asc");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        Assert.Equal(created.GetProperty("id").GetInt64(), list.GetProperty("items")[0].GetProperty("id").GetInt64());

        using var detailResponse = await _client.GetAsync($"/api/users/{created.GetProperty("id").GetInt64()}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(detail.GetProperty("concurrencyToken").GetString()));

        using var duplicateEmployee = await _client.PostAsJsonAsync("/api/users", new
        {
            employeeNo = employeeNo.ToLowerInvariant(),
            displayName = "重复工号",
            email = $"other-{suffix}@example.com",
            departmentOrTeam = (string?)null,
            jobTitle = (string?)null,
            knowledgeRoleIds = Array.Empty<long>(),
            loginSetup = new { type = "none" },
            actor = Actor(),
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateEmployee.StatusCode);

        using var duplicateEmail = await _client.PostAsJsonAsync("/api/users", new
        {
            employeeNo = $"OTHER-{suffix}",
            displayName = "重复邮箱",
            email = email.ToUpperInvariant(),
            departmentOrTeam = (string?)null,
            jobTitle = (string?)null,
            knowledgeRoleIds = Array.Empty<long>(),
            loginSetup = new { type = "none" },
            actor = Actor(),
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateEmail.StatusCode);
    }

    [Fact]
    public async Task User_update_replaces_role_assignment_and_rejects_stale_token()
    {
        var firstRole = await CreateKnowledgeRole("Oracle DBA");
        var secondRole = await CreateKnowledgeRole("Equipment Integration Expert");
        var updatedRole = await UpdateKnowledgeRole(
            secondRole.GetProperty("id").GetInt64(),
            "设备集成专家",
            "负责设备集成知识",
            secondRole.GetProperty("concurrencyToken").GetString()!);
        var suffix = UniqueSuffix();
        var created = await CreateUser(
            $"UPD-{suffix}",
            $"update-{suffix}@example.com",
            [firstRole.GetProperty("id").GetInt64()]);
        var userId = created.GetProperty("id").GetInt64();
        var staleToken = created.GetProperty("concurrencyToken").GetString();

        var request = new
        {
            employeeNo = $"UPD-{suffix}",
            displayName = "王敏",
            email = $"update-{suffix}@example.com",
            departmentOrTeam = "Manufacturing IT",
            jobTitle = "Senior Engineer",
            knowledgeRoleIds = new[]
            {
                firstRole.GetProperty("id").GetInt64(),
                updatedRole.GetProperty("id").GetInt64(),
            },
            actor = Actor(),
            concurrencyToken = staleToken,
        };

        using var updateResponse = await _client.PutAsJsonAsync($"/api/users/{userId}", request);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Manufacturing IT", updated.GetProperty("departmentOrTeam").GetString());
        Assert.Equal("Senior Engineer", updated.GetProperty("jobTitle").GetString());
        Assert.Equal(2, updated.GetProperty("knowledgeRoles").GetArrayLength());
        Assert.Contains(
            updated.GetProperty("knowledgeRoles").EnumerateArray(),
            role => role.GetProperty("name").GetString()!.StartsWith("设备集成专家-", StringComparison.Ordinal));

        using var staleResponse = await _client.PutAsJsonAsync($"/api/users/{userId}", request);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
    }

    [Fact]
    public async Task Active_state_preserves_inactive_role_mapping_and_blocks_new_assignment()
    {
        var role = await CreateKnowledgeRole("停用语义验证角色");
        var roleId = role.GetProperty("id").GetInt64();
        var suffix = UniqueSuffix();
        var user = await CreateUser(
            $"STATE-{suffix}",
            $"state-{suffix}@example.com",
            [roleId]);
        var userId = user.GetProperty("id").GetInt64();

        using var disableRoleResponse = await _client.PutAsJsonAsync($"/api/knowledge-roles/{roleId}/active-state", new
        {
            isActive = false,
            actor = Actor(),
            concurrencyToken = role.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, disableRoleResponse.StatusCode);

        using var detailResponse = await _client.GetAsync($"/api/users/{userId}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        var retainedRole = Assert.Single(detail.GetProperty("knowledgeRoles").EnumerateArray());
        Assert.False(retainedRole.GetProperty("isActive").GetBoolean());

        using var inactiveAssignment = await _client.PostAsJsonAsync("/api/users", new
        {
            employeeNo = $"BLOCK-{suffix}",
            displayName = "不可分配停用角色",
            email = $"blocked-{suffix}@example.com",
            departmentOrTeam = (string?)null,
            jobTitle = (string?)null,
            knowledgeRoleIds = new[] { roleId },
            loginSetup = new { type = "none" },
            actor = Actor(),
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, inactiveAssignment.StatusCode);

        var activeToken = detail.GetProperty("concurrencyToken").GetString();
        using var disableUserResponse = await _client.PutAsJsonAsync($"/api/users/{userId}/active-state", new
        {
            isActive = false,
            actor = Actor(),
            concurrencyToken = activeToken,
        });
        Assert.Equal(HttpStatusCode.OK, disableUserResponse.StatusCode);
        var disabled = await disableUserResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(disabled.GetProperty("isActive").GetBoolean());

        var disabledToken = disabled.GetProperty("concurrencyToken").GetString();
        using var enableUserResponse = await _client.PutAsJsonAsync($"/api/users/{userId}/active-state", new
        {
            isActive = true,
            actor = Actor(),
            concurrencyToken = disabledToken,
        });
        Assert.Equal(HttpStatusCode.OK, enableUserResponse.StatusCode);
        var enabled = await enableUserResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(enabled.GetProperty("isActive").GetBoolean());
        Assert.Single(enabled.GetProperty("knowledgeRoles").EnumerateArray());

        using var staleResponse = await _client.PutAsJsonAsync($"/api/users/{userId}/active-state", new
        {
            isActive = false,
            actor = Actor(),
            concurrencyToken = disabledToken,
        });
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
    }

    private async Task<JsonElement> CreateKnowledgeRole(string prefix)
    {
        using var response = await _client.PostAsJsonAsync("/api/knowledge-roles", new
        {
            name = $"{prefix}-{UniqueSuffix()}",
            description = "U01 integration test",
            actor = Actor(),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private async Task<JsonElement> UpdateKnowledgeRole(long id, string name, string description, string token)
    {
        using var response = await _client.PutAsJsonAsync($"/api/knowledge-roles/{id}", new
        {
            name = $"{name}-{UniqueSuffix()}",
            description,
            actor = Actor(),
            concurrencyToken = token,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private async Task<JsonElement> CreateUser(string employeeNo, string email, IReadOnlyList<long> roleIds)
    {
        using var response = await _client.PostAsJsonAsync("/api/users", new
        {
            employeeNo,
            displayName = "王敏",
            email,
            departmentOrTeam = "制造系统组",
            jobTitle = "Senior Engineer",
            knowledgeRoleIds = roleIds,
            loginSetup = new { type = "none" },
            actor = Actor(),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static object Actor() => new { displayName = "U01 测试人员", role = "知识整理人员" };

    private static string UniqueSuffix() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
