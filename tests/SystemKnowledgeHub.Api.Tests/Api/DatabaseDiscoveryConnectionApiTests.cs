using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class DatabaseDiscoveryConnectionApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task Profile_lifecycle_validates_source_engine_uniqueness_and_configuration_revision()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();
        var sourceId = await CreateSource(factory, "Oracle");

        using var createdResponse = await client.PostAsJsonAsync(
            "/api/admin/database-connection-profiles", ProfileRequest(sourceId));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await ReadProfile(createdResponse);
        Assert.Equal(1, created.ConfigurationRevision);
        Assert.Equal(DatabaseConnectionStatus.Unknown, created.ConnectionStatus);
        Assert.False(created.HasSecret);

        using var duplicate = await client.PostAsJsonAsync(
            "/api/admin/database-connection-profiles", ProfileRequest(sourceId, name: "Duplicate"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var staleUpdate = await client.PutAsJsonAsync(
            $"/api/admin/database-connection-profiles/{created.Id}",
            UpdateRequest(created, created.ConcurrencyToken, host: "db2.example.test"));
        Assert.Equal(HttpStatusCode.OK, staleUpdate.StatusCode);
        var updated = await ReadProfile(staleUpdate);
        Assert.Equal(2, updated.ConfigurationRevision);
        Assert.Equal("db2.example.test", updated.Host);
        using var stale = await client.PutAsJsonAsync(
            $"/api/admin/database-connection-profiles/{created.Id}",
            UpdateRequest(created, created.ConcurrencyToken, host: "db3.example.test"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        using var disabledResponse = await client.PutAsJsonAsync(
            $"/api/admin/database-connection-profiles/{created.Id}/enabled-state",
            new { isEnabled = false, concurrencyToken = updated.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, disabledResponse.StatusCode);
        var disabled = await ReadProfile(disabledResponse);
        Assert.False(disabled.IsEnabled);
        Assert.Equal(3, disabled.ConfigurationRevision);
        using var enabledResponse = await client.PutAsJsonAsync(
            $"/api/admin/database-connection-profiles/{created.Id}/enabled-state",
            new { isEnabled = true, concurrencyToken = disabled.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, enabledResponse.StatusCode);
        var enabled = await ReadProfile(enabledResponse);
        Assert.True(enabled.IsEnabled);
        Assert.Equal(4, enabled.ConfigurationRevision);

        using var missingSource = await client.PostAsJsonAsync(
            "/api/admin/database-connection-profiles", ProfileRequest(9_007_199_254_740_990));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missingSource.StatusCode);
        var sqlServerSource = await CreateSource(factory, "SQL Server");
        using var mismatch = await client.PostAsJsonAsync(
            "/api/admin/database-connection-profiles", ProfileRequest(sqlServerSource));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, mismatch.StatusCode);
        var deletedSource = await CreateSource(factory, "Oracle", deleted: true);
        using var deleted = await client.PostAsJsonAsync(
            "/api/admin/database-connection-profiles", ProfileRequest(deletedSource));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, deleted.StatusCode);
        var unsafeOptionsSource = await CreateSource(factory, "Oracle");
        using var unsafeOptions = await client.PostAsJsonAsync(
            "/api/admin/database-connection-profiles",
            new
            {
                databaseSourceId = unsafeOptionsSource,
                name = $"Unsafe-{Guid.NewGuid():N}",
                providerType = "Oracle",
                host = "db.example.test",
                port = 1521,
                serviceName = "APP_PDB",
                authenticationMode = "UsernamePassword",
                username = "METADATA_READER",
                providerSpecificOptions = new { version = 1, password = "bypass" },
                includedSchemas = Array.Empty<string>(),
            });
        Assert.Equal(HttpStatusCode.BadRequest, unsafeOptions.StatusCode);
        var unsafeJson = await unsafeOptions.Content.ReadAsStringAsync();
        Assert.Contains("providerSpecificOptions", unsafeJson, StringComparison.Ordinal);
        Assert.Contains("includedSchemas", unsafeJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Secret_set_replace_clear_are_explicit_concurrent_and_never_disclosed()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();
        var profile = await CreateProfile(factory, client, enabled: true);
        const string canary = "DBDISC_CANARY_SECRET_1=!;Data Source=raw";

        using var empty = await client.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/secret",
            new { password = "", concurrencyToken = profile.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        using var setResponse = await client.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/secret",
            new { password = canary, concurrencyToken = profile.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, setResponse.StatusCode);
        var set = await ReadProfile(setResponse);
        Assert.True(set.HasSecret);
        Assert.NotNull(set.SecretUpdatedAt);
        var setJson = await setResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(canary, setJson, StringComparison.Ordinal);
        Assert.DoesNotContain("protectedPayload", setJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secretReference", setJson, StringComparison.OrdinalIgnoreCase);

        using var setAgain = await client.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/secret",
            new { password = "second", concurrencyToken = set.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, setAgain.StatusCode);

        using var replaceResponse = await client.PutAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/secret",
            new { password = "replacement", concurrencyToken = set.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, replaceResponse.StatusCode);
        var replaced = await ReadProfile(replaceResponse);
        Assert.True(replaced.HasSecret);
        Assert.Equal(DatabaseConnectionStatus.Unknown, replaced.ConnectionStatus);

        using var stale = await client.PutAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/secret",
            new { password = "stale", concurrencyToken = set.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        using var clearResponse = await SendDeleteJson(client,
            $"/api/admin/database-connection-profiles/{profile.Id}/secret",
            new { concurrencyToken = replaced.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        var cleared = await ReadProfile(clearResponse);
        Assert.False(cleared.HasSecret);
        Assert.Null(cleared.SecretUpdatedAt);
        using var clearAgain = await SendDeleteJson(client,
            $"/api/admin/database-connection-profiles/{profile.Id}/secret",
            new { concurrencyToken = cleared.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, clearAgain.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var secret = await db.DatabaseConnectionSecrets.SingleAsync(item => item.ProfileId == profile.Id);
        Assert.Null(secret.ProtectedPayload);
        Assert.True(secret.Version >= 3);
        var ordinary = string.Join('|', await db.DatabaseConnectionProfiles
            .Where(item => item.Id == profile.Id)
            .Select(item => new[] { item.Name, item.Host, item.Username, item.IncludedSchemasJson, item.ProviderSpecificOptionsJson })
            .SingleAsync());
        var auditFields = await db.DatabaseConnectionAuditEvents
            .Where(item => item.ProfileId == profile.Id)
            .Select(item => new[] { item.Action.ToString(), item.Outcome.ToString(), item.ErrorCode ?? "", item.VendorCode ?? "", item.ActorDisplayName })
            .ToArrayAsync();
        var audit = string.Join('|', auditFields.SelectMany(item => item));
        Assert.DoesNotContain(canary, ordinary, StringComparison.Ordinal);
        Assert.DoesNotContain(canary, audit, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Test_connection_success_and_normalized_failure_are_audited_without_canary_or_raw_provider_data()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();
        var profile = await CreateProfile(factory, client, enabled: true);
        const string canary = "DBDISC_CANARY_SECRET_RAW_EXCEPTION_29";
        profile = await SetSecret(client, profile, canary);

        using var successResponse = await client.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/test-connection",
            new { concurrencyToken = profile.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, successResponse.StatusCode);
        var success = await successResponse.Content.ReadFromJsonAsync<DatabaseConnectionTestResponse>(JsonOptions);
        Assert.NotNull(success);
        Assert.True(success.Succeeded);
        Assert.Equal("19.0.0.0.0", success.ProviderVersion);

        factory.Tester.Handler = (connection, _) =>
            throw new InvalidOperationException($"provider raw: {connection.Password}; (DESCRIPTION=raw); SELECT secret");
        using var failureResponse = await client.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/test-connection",
            new { concurrencyToken = success.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, failureResponse.StatusCode);
        var failureJson = await failureResponse.Content.ReadAsStringAsync();
        Assert.Contains("ConnectionFailed", failureJson, StringComparison.Ordinal);
        Assert.DoesNotContain(canary, failureJson, StringComparison.Ordinal);
        Assert.DoesNotContain("DESCRIPTION", failureJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT secret", failureJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(canary, string.Join('|', factory.LogSink.Entries), StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var storedProfile = await db.DatabaseConnectionProfiles.SingleAsync(item => item.Id == profile.Id);
        Assert.Equal(DatabaseConnectionStatus.Failed, storedProfile.ConnectionStatus);
        Assert.Equal("ConnectionFailed", storedProfile.LastConnectionTestErrorCode);
        Assert.DoesNotContain(canary, storedProfile.LastConnectionTestSummary ?? string.Empty, StringComparison.Ordinal);
        var audit = await db.DatabaseConnectionAuditEvents.Where(item => item.ProfileId == profile.Id).ToArrayAsync();
        Assert.Contains(audit, item => item.Action == DatabaseConnectionAuditAction.ConnectionTestStarted);
        Assert.Contains(audit, item => item.Action == DatabaseConnectionAuditAction.ConnectionTestResult);
        Assert.DoesNotContain(audit, item =>
            (item.ErrorCode ?? string.Empty).Contains(canary, StringComparison.Ordinal)
            || (item.VendorCode ?? string.Empty).Contains(canary, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Unsupported_provider_and_missing_secret_fail_explicitly_without_creating_discovery_artifacts()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();
        var oracle = await CreateProfile(factory, client, enabled: true);
        using var missingSecret = await client.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{oracle.Id}/test-connection",
            new { concurrencyToken = oracle.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missingSecret.StatusCode);
        Assert.Contains("SecretMissing", await missingSecret.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var pgSource = await CreateSource(factory, "PostgreSQL");
        using var pgCreate = await client.PostAsJsonAsync(
            "/api/admin/database-connection-profiles",
            new
            {
                databaseSourceId = pgSource,
                name = $"PG-{Guid.NewGuid():N}",
                providerType = "PostgreSql",
                host = "pg.example.test",
                port = 5432,
                databaseName = "appdb",
                serviceName = (string?)null,
                authenticationMode = "UsernamePassword",
                username = "metadata_reader",
                providerSpecificOptions = new { version = 1 },
                includedSchemas = new[] { "public" },
                isEnabled = true,
            });
        var pg = await ReadProfile(pgCreate);
        pg = await SetSecret(client, pg, "pg-secret");
        using var unavailable = await client.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{pg.Id}/test-connection",
            new { concurrencyToken = pg.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unavailable.StatusCode);
        Assert.Contains("ProviderUnavailable", await unavailable.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var tables = await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type='table'").ToArrayAsync();
        Assert.DoesNotContain(tables, name => name.Contains("discovery_run", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(tables, name => name.Contains("snapshot", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(tables, name => name.Contains("difference", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Superseded_profile_and_secret_changes_cannot_overwrite_newer_state()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory();
        factory.Tester.GateCalls = true;
        using var client = factory.CreateAuthenticatedClient();
        var profile = await CreateProfile(factory, client, enabled: true);
        profile = await SetSecret(client, profile, "initial");

        var firstTask = client.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/test-connection",
            new { concurrencyToken = profile.ConcurrencyToken });
        var firstCall = await factory.Tester.WaitForCall();
        var afterStart = await GetProfile(client, profile.Id);
        using var changedResponse = await client.PutAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}",
            UpdateRequest(afterStart, afterStart.ConcurrencyToken, host: "new-target.example.test"));
        var changed = await ReadProfile(changedResponse);
        firstCall.Completion.SetResult(ControlledDatabaseConnectionTester.Success());
        using var supersededByProfile = await firstTask;
        Assert.Equal(HttpStatusCode.Conflict, supersededByProfile.StatusCode);
        var afterProfileConflict = await GetProfile(client, profile.Id);
        Assert.Equal(DatabaseConnectionStatus.Unknown, afterProfileConflict.ConnectionStatus);
        Assert.Equal(changed.ConfigurationRevision, afterProfileConflict.ConfigurationRevision);

        var secondTask = client.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/test-connection",
            new { concurrencyToken = afterProfileConflict.ConcurrencyToken });
        var secondCall = await factory.Tester.WaitForCall();
        var afterSecondStart = await GetProfile(client, profile.Id);
        using var replacedResponse = await client.PutAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/secret",
            new { password = "rotated", concurrencyToken = afterSecondStart.ConcurrencyToken });
        var replaced = await ReadProfile(replacedResponse);
        secondCall.Completion.SetResult(ControlledDatabaseConnectionTester.Success());
        using var supersededBySecret = await secondTask;
        Assert.Equal(HttpStatusCode.Conflict, supersededBySecret.StatusCode);
        var final = await GetProfile(client, profile.Id);
        Assert.Equal(DatabaseConnectionStatus.Unknown, final.ConnectionStatus);
        Assert.Equal(replaced.ConfigurationRevision, final.ConfigurationRevision);
    }

    [Fact]
    public async Task Latest_concurrent_test_wins_and_older_attempt_returns_conflict()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory();
        factory.Tester.GateCalls = true;
        using var client = factory.CreateAuthenticatedClient();
        var profile = await CreateProfile(factory, client, enabled: true);
        profile = await SetSecret(client, profile, "parallel");

        var firstTask = client.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/test-connection",
            new { concurrencyToken = profile.ConcurrencyToken });
        var first = await factory.Tester.WaitForCall();
        var afterFirstStart = await GetProfile(client, profile.Id);
        var secondTask = client.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/test-connection",
            new { concurrencyToken = afterFirstStart.ConcurrencyToken });
        var second = await factory.Tester.WaitForCall();
        second.Completion.SetResult(ControlledDatabaseConnectionTester.Success());
        using var secondResponse = await secondTask;
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        first.Completion.SetResult(DatabaseConnectionTestResult.Fail(
            DatabaseConnectionFailure.AuthenticationFailed,
            "Oracle 用户名或密码验证失败。",
            "ORA-01017"));
        using var firstResponse = await firstTask;
        Assert.Equal(HttpStatusCode.Conflict, firstResponse.StatusCode);
        var final = await GetProfile(client, profile.Id);
        Assert.Equal(DatabaseConnectionStatus.Succeeded, final.ConnectionStatus);
    }

    [Fact]
    public async Task Enabled_profile_blocks_source_soft_delete_and_all_profile_actions_are_administrator_only()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory();
        using var administrator = factory.CreateAuthenticatedClient();
        var sourceId = await CreateSource(factory, "Oracle");
        var profile = await CreateProfile(factory, administrator, enabled: true, sourceId: sourceId);

        var sourceToken = factory.Services.GetRequiredService<ConcurrencyTokenCodec>().Encode(1);
        using var blocked = await SendDeleteJson(administrator, $"/api/database-sources/{sourceId}", new { concurrencyToken = sourceToken });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
        Assert.Contains("enabledDatabaseConnectionProfiles", await blocked.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using var disabledResponse = await administrator.PutAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/enabled-state",
            new { isEnabled = false, concurrencyToken = profile.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, disabledResponse.StatusCode);
        using var deleted = await SendDeleteJson(administrator, $"/api/database-sources/{sourceId}", new { concurrencyToken = sourceToken });
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var viewerId = await CreateUser(factory, AccessLevel.Viewer);
        var editorId = await CreateUser(factory, AccessLevel.Editor);
        using var viewer = await factory.CreateAuthenticatedClientAsync(viewerId);
        using var editor = await factory.CreateAuthenticatedClientAsync(editorId);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/admin/database-connection-profiles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync("/api/admin/database-connection-profiles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.PostAsJsonAsync(
            "/api/admin/database-connection-profiles", ProfileRequest(await CreateSource(factory, "Oracle")))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.PutAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}", UpdateRequest(profile, profile.ConcurrencyToken))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.PutAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/enabled-state",
            new { isEnabled = true, concurrencyToken = profile.ConcurrencyToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/secret",
            new { password = "forbidden", concurrencyToken = profile.ConcurrencyToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/test-connection",
            new { concurrencyToken = profile.ConcurrencyToken })).StatusCode);
    }

    private static object ProfileRequest(long sourceId, string? name = null, bool enabled = true) => new
    {
        databaseSourceId = sourceId,
        name = name ?? $"Oracle-{Guid.NewGuid():N}",
        providerType = "Oracle",
        host = "db.example.test",
        port = 1521,
        databaseName = (string?)null,
        serviceName = "APP_PDB",
        authenticationMode = "UsernamePassword",
        username = "METADATA_READER",
        providerSpecificOptions = new { version = 1 },
        includedSchemas = new[] { "APP_OWNER" },
        isEnabled = enabled,
    };

    private static object UpdateRequest(
        DatabaseConnectionProfileResponse profile,
        string token,
        string? host = null) => new
    {
        name = profile.Name,
        providerType = profile.ProviderType.ToString(),
        host = host ?? profile.Host,
        port = profile.Port,
        databaseName = profile.DatabaseName,
        serviceName = profile.ServiceName,
        authenticationMode = profile.AuthenticationMode.ToString(),
        username = profile.Username,
        providerSpecificOptions = new { version = 1 },
        includedSchemas = profile.IncludedSchemas,
        concurrencyToken = token,
    };

    private static async Task<DatabaseConnectionProfileResponse> CreateProfile(
        DatabaseDiscoveryWebApplicationFactory factory,
        HttpClient client,
        bool enabled,
        long? sourceId = null)
    {
        sourceId ??= await CreateSource(factory, "Oracle");
        using var response = await client.PostAsJsonAsync(
            "/api/admin/database-connection-profiles", ProfileRequest(sourceId.Value, enabled: enabled));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadProfile(response);
    }

    private static async Task<DatabaseConnectionProfileResponse> SetSecret(
        HttpClient client,
        DatabaseConnectionProfileResponse profile,
        string password)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/secret",
            new { password, concurrencyToken = profile.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadProfile(response);
    }

    private static async Task<DatabaseConnectionProfileResponse> GetProfile(HttpClient client, long id)
    {
        using var response = await client.GetAsync($"/api/admin/database-connection-profiles/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadProfile(response);
    }

    private static async Task<DatabaseConnectionProfileResponse> ReadProfile(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<DatabaseConnectionProfileResponse>(JsonOptions)
        ?? throw new InvalidOperationException("Profile response was empty.");

    private static Task<HttpResponseMessage> SendDeleteJson(HttpClient client, string path, object body) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, path) { Content = JsonContent.Create(body) });

    private static async Task<long> CreateSource(
        DatabaseDiscoveryWebApplicationFactory factory,
        string engine,
        bool deleted = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var systemId = await db.Systems.Select(item => item.Id).FirstAsync();
        var user = await db.Users.FirstAsync(item => item.AccessLevel == AccessLevel.Administrator);
        var now = DateTimeOffset.UtcNow;
        var source = new DatabaseSource
        {
            SystemId = systemId,
            Name = $"DBDISC-{Guid.NewGuid():N}",
            Engine = engine,
            CreatedAt = now,
            CreatedByUserId = user.Id,
            CreatedByName = user.DisplayName,
            UpdatedAt = now,
            Version = 1,
            IsDeleted = deleted,
            DeletedAt = deleted ? now : null,
            DeletedByUserId = deleted ? user.Id : null,
            DeletedByDisplayName = deleted ? user.DisplayName : null,
        };
        db.DatabaseSources.Add(source);
        await db.SaveChangesAsync();
        return source.Id;
    }

    private static async Task<long> CreateUser(DatabaseDiscoveryWebApplicationFactory factory, AccessLevel accessLevel)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            DisplayName = $"DBDISC {accessLevel} {Guid.NewGuid():N}",
            IsActive = true,
            AccessLevel = accessLevel,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
