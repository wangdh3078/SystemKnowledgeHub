using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class PostgreSqlRealIntegrationTests
{
    private const string BusinessRowCanary = "DBDISC_PG_BUSINESS_ROW_CANARY_4B97A361";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task Environment_enabled_real_PostgreSql_18_proves_test_worker_snapshot_diff_and_least_privilege()
    {
        if (!PostgreSqlRealIntegrationEnvironment.TryLoad(out var loaded)) return;
        var environment = loaded!;
        await ResetFixture(environment);
        try
        {
            await AssertDiscoveryGrantMatrix(environment);

            using var factory = new PostgreSqlRealIntegrationWebApplicationFactory();
            using var administrator = factory.CreateAuthenticatedClient();
            var apiPayloads = new List<string>();

            var primary = await CreateProfile(
                factory,
                administrator,
                environment,
                ["dbdisc_a", "dbdisc_b"]);
            primary = await SetSecret(administrator, primary, environment.DiscoveryPassword);

            using (var testResponse = await administrator.PostAsJsonAsync(
                $"/api/admin/database-connection-profiles/{primary.Id}/test-connection",
                new { concurrencyToken = primary.ConcurrencyToken }))
            {
                var raw = await testResponse.Content.ReadAsStringAsync();
                apiPayloads.Add(raw);
                Assert.Equal(HttpStatusCode.OK, testResponse.StatusCode);
                var result = JsonSerializer.Deserialize<DatabaseConnectionTestResponse>(raw, JsonOptions);
                Assert.NotNull(result);
                Assert.True(result.Succeeded);
                Assert.True(result.ProviderVersion?.StartsWith("18.", StringComparison.Ordinal) == true);
                Assert.Equal(environment.Database, result.DatabaseName);
                Assert.Null(result.ServiceName);
            }

            primary = await GetProfile(administrator, primary.Id);
            var first = await Trigger(administrator, primary);
            first = await WaitForTerminal(administrator, first.Id);
            Assert.True(
                first.Status == DatabaseDiscoveryRunStatus.Succeeded,
                $"First PostgreSQL discovery failed: {first.ErrorCode} / {first.ErrorSummary}");
            Assert.Null(first.BaseSnapshotId);
            Assert.NotNull(first.SnapshotId);
            Assert.NotNull(first.DifferenceId);
            Assert.Equal(DatabaseProviderType.PostgreSql, first.ProviderType);
            Assert.Contains(first.Capabilities, item =>
                item.Name == "SupportsSequences"
                && item.State == DatabaseDiscoveryCapabilityState.Supported);

            var (firstSnapshot, firstSnapshotRaw) = await GetSnapshot(administrator, first.SnapshotId!.Value);
            apiPayloads.Add(firstSnapshotRaw);
            var firstCanonical = firstSnapshot.Content.Deserialize<CanonicalDatabaseDiscoverySnapshot>(JsonOptions);
            Assert.NotNull(firstCanonical);
            Assert.Equal(DatabaseProviderType.PostgreSql, firstCanonical.ProviderType);
            Assert.Equal(DatabaseDiscoveryCompleteness.Complete, firstCanonical.Completeness);
            Assert.Contains(firstCanonical.Objects, item => item.Name == "CaseSensitiveTable");
            Assert.Contains(firstCanonical.Columns, item => item.Name == "MiXeDColumn");
            Assert.Contains(firstCanonical.Objects, item =>
                item.Name == "customers" && item.DatabaseComment == "Customer master state 1");
            Assert.Contains(firstCanonical.Columns, item =>
                item.Name == "code" && item.DatabaseComment == "Stable customer code");
            Assert.Contains(firstCanonical.PrimaryKeys, item => item.ColumnLogicalIdentities.Count == 2);
            Assert.Contains(firstCanonical.UniqueConstraints, item => item.ColumnLogicalIdentities.Count == 2);
            Assert.Contains(firstCanonical.ForeignKeys, item => item.ColumnLogicalIdentities.Count == 2);
            Assert.NotEmpty(firstCanonical.ForeignKeyReferenceClosure);
            Assert.Contains(firstCanonical.Indexes, item => item.Name == "ix_customers_active");
            Assert.Contains(firstCanonical.Indexes, item =>
                item.Name == "ix_customers_lower_code"
                && item.KeyParts.Any(part => part.NativeExpression is not null)
                && item.NativePredicate is not null);
            Assert.Contains(firstCanonical.Indexes, item =>
                item.Name == "ix_customers_amount_include" && item.NonKeyParts.Count == 1);
            Assert.Contains(firstCanonical.Sequences, item => item.Name == "manual_sequence");
            AssertNativeType(firstCanonical, "integer");
            AssertNativeType(firstCanonical, "bigint");
            AssertNativeType(firstCanonical, "numeric(12,2)");
            AssertNativeType(firstCanonical, "character varying(32)");
            AssertNativeType(firstCanonical, "text");
            AssertNativeType(firstCanonical, "boolean");
            AssertNativeType(firstCanonical, "date");
            AssertNativeType(firstCanonical, "timestamp without time zone");
            AssertNativeType(firstCanonical, "timestamp with time zone");
            AssertNativeType(firstCanonical, "uuid");

            var (firstDifference, firstDifferenceRaw) = await GetDifference(
                administrator,
                first.DifferenceId!.Value);
            apiPayloads.Add(firstDifferenceRaw);
            Assert.Null(firstDifference.BaseSnapshotId);
            Assert.True(firstDifference.SummaryCounts.Added > 0);
            Assert.Equal(0, firstDifference.SummaryCounts.Changed);
            Assert.Equal(0, firstDifference.SummaryCounts.MissingFromSource);
            Assert.Equal(0, firstDifference.SummaryCounts.Unchanged);

            await ApplyStateTwo(environment);
            primary = await GetProfile(administrator, primary.Id);
            var second = await WaitForTerminal(
                administrator,
                (await Trigger(administrator, primary)).Id);
            Assert.True(
                second.Status == DatabaseDiscoveryRunStatus.Succeeded,
                $"Second PostgreSQL discovery failed: {second.ErrorCode} / {second.ErrorSummary}");
            Assert.Equal(first.SnapshotId, second.BaseSnapshotId);
            Assert.Equal(first.ScopeGenerationId, second.ScopeGenerationId);
            Assert.NotNull(second.DifferenceId);

            var (secondDifference, secondDifferenceRaw) = await GetDifference(
                administrator,
                second.DifferenceId!.Value);
            apiPayloads.Add(secondDifferenceRaw);
            Assert.True(secondDifference.SummaryCounts.Added > 0);
            Assert.True(secondDifference.SummaryCounts.Changed > 0);
            Assert.True(secondDifference.SummaryCounts.MissingFromSource > 0);
            Assert.True(secondDifference.SummaryCounts.Unchanged > 0);

            var added = await GetDifferenceEntries(
                administrator,
                second.DifferenceId.Value,
                DatabaseDiscoveryDifferenceState.Added,
                apiPayloads);
            var changed = await GetDifferenceEntries(
                administrator,
                second.DifferenceId.Value,
                DatabaseDiscoveryDifferenceState.Changed,
                apiPayloads);
            var missing = await GetDifferenceEntries(
                administrator,
                second.DifferenceId.Value,
                DatabaseDiscoveryDifferenceState.MissingFromSource,
                apiPayloads);
            var unchanged = await GetDifferenceEntries(
                administrator,
                second.DifferenceId.Value,
                DatabaseDiscoveryDifferenceState.Unchanged,
                apiPayloads);
            Assert.Contains(added.Items, item => item.DisplayName.EndsWith(".added_entity", StringComparison.Ordinal));
            Assert.Contains(added.Items, item => item.DisplayName.EndsWith(".rename_after", StringComparison.Ordinal));
            Assert.Contains(missing.Items, item => item.DisplayName.EndsWith(".missing_entity", StringComparison.Ordinal));
            Assert.Contains(missing.Items, item => item.DisplayName.EndsWith(".rename_before", StringComparison.Ordinal));
            Assert.Contains(changed.Items, item => item.DisplayName.EndsWith(".customers", StringComparison.Ordinal));
            Assert.Contains(unchanged.Items, item =>
                item.EntityKind == DatabaseDiscoveryEntityKind.Sequence
                && item.DisplayName == "manual_sequence");

            var badPassword = environment.DiscoveryPassword + "-WRONG-DBDISC-PG-28P01";
            var badProfile = await CreateProfile(
                factory,
                administrator,
                environment,
                ["dbdisc_a", "dbdisc_b"]);
            badProfile = await SetSecret(administrator, badProfile, badPassword);
            using (var badTest = await administrator.PostAsJsonAsync(
                $"/api/admin/database-connection-profiles/{badProfile.Id}/test-connection",
                new { concurrencyToken = badProfile.ConcurrencyToken }))
            {
                var raw = await badTest.Content.ReadAsStringAsync();
                apiPayloads.Add(raw);
                Assert.Equal(HttpStatusCode.UnprocessableEntity, badTest.StatusCode);
                Assert.Contains("AuthenticationFailed", raw, StringComparison.Ordinal);
                Assert.Contains("SQLSTATE-28P01", raw, StringComparison.Ordinal);
                Assert.DoesNotContain(badPassword, raw, StringComparison.Ordinal);
            }
            badProfile = await GetProfile(administrator, badProfile.Id);
            var badRun = await WaitForTerminal(
                administrator,
                (await Trigger(administrator, badProfile)).Id);
            Assert.Equal(DatabaseDiscoveryRunStatus.Failed, badRun.Status);
            Assert.Equal("AuthenticationFailed", badRun.ErrorCode);
            Assert.Null(badRun.SnapshotId);
            Assert.Null(badRun.DifferenceId);

            var insufficient = await CreateProfile(
                factory,
                administrator,
                environment,
                ["dbdisc_denied"]);
            insufficient = await SetSecret(administrator, insufficient, environment.DiscoveryPassword);
            using (var privilegeTest = await administrator.PostAsJsonAsync(
                $"/api/admin/database-connection-profiles/{insufficient.Id}/test-connection",
                new { concurrencyToken = insufficient.ConcurrencyToken }))
            {
                var raw = await privilegeTest.Content.ReadAsStringAsync();
                apiPayloads.Add(raw);
                Assert.Equal(HttpStatusCode.UnprocessableEntity, privilegeTest.StatusCode);
                Assert.Contains("InsufficientPrivilege", raw, StringComparison.Ordinal);
                Assert.DoesNotContain(BusinessRowCanary, raw, StringComparison.Ordinal);
            }
            insufficient = await GetProfile(administrator, insufficient.Id);
            var privilegeRun = await WaitForTerminal(
                administrator,
                (await Trigger(administrator, insufficient)).Id);
            Assert.Equal(DatabaseDiscoveryRunStatus.Failed, privilegeRun.Status);
            Assert.Equal("InsufficientPrivilege", privilegeRun.ErrorCode);
            Assert.Null(privilegeRun.SnapshotId);
            Assert.Null(privilegeRun.DifferenceId);

            var cancellable = await CreateProfile(
                factory,
                administrator,
                environment,
                ["dbdisc_a", "dbdisc_b"]);
            cancellable = await SetSecret(administrator, cancellable, environment.DiscoveryPassword);
            var cancellationTarget = await Trigger(administrator, cancellable);
            using (var cancelResponse = await administrator.PostAsJsonAsync(
                $"/api/database-discovery/runs/{cancellationTarget.Id}/cancel",
                new { concurrencyToken = cancellationTarget.ConcurrencyToken }))
            {
                Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
            }
            var cancelled = await WaitForTerminal(administrator, cancellationTarget.Id);
            Assert.Equal(DatabaseDiscoveryRunStatus.Cancelled, cancelled.Status);
            Assert.Equal("Cancelled", cancelled.ErrorCode);
            Assert.Null(cancelled.SnapshotId);
            Assert.Null(cancelled.DifferenceId);

            await AssertNoCanaryLeak(
                factory,
                apiPayloads,
                environment.DiscoveryPassword,
                badPassword);
        }
        finally
        {
            await DropFixtureSchemas(environment);
        }
    }

    private static async Task ResetFixture(PostgreSqlRealIntegrationEnvironment environment)
    {
        await using var connection = await Open(environment, owner: true);
        var discoveryRole = QuoteIdentifier(environment.DiscoveryUsername);
        await Execute(connection, """
            DROP SCHEMA IF EXISTS dbdisc_a CASCADE;
            DROP SCHEMA IF EXISTS dbdisc_b CASCADE;
            DROP SCHEMA IF EXISTS dbdisc_ref CASCADE;
            DROP SCHEMA IF EXISTS dbdisc_denied CASCADE;
            CREATE SCHEMA dbdisc_a;
            CREATE SCHEMA dbdisc_b;
            CREATE SCHEMA dbdisc_ref;
            CREATE SCHEMA dbdisc_denied;

            CREATE TABLE dbdisc_ref.parent_entity (
                tenant_id integer NOT NULL,
                parent_id bigint NOT NULL,
                label text,
                CONSTRAINT pk_parent_entity PRIMARY KEY (tenant_id, parent_id)
            );

            CREATE TABLE dbdisc_a.customers (
                tenant_id integer NOT NULL,
                customer_id bigint GENERATED BY DEFAULT AS IDENTITY,
                code character varying(32) NOT NULL,
                amount numeric(12,2),
                description text,
                active boolean NOT NULL DEFAULT true,
                business_date date,
                created_at timestamp without time zone,
                created_with_tz timestamp with time zone,
                external_id uuid,
                CONSTRAINT pk_customers PRIMARY KEY (tenant_id, customer_id),
                CONSTRAINT uq_customers_tenant_code UNIQUE (tenant_id, code)
            );
            COMMENT ON TABLE dbdisc_a.customers IS 'Customer master state 1';
            COMMENT ON COLUMN dbdisc_a.customers.code IS 'Stable customer code';

            CREATE TABLE dbdisc_a.orders (
                tenant_id integer NOT NULL,
                order_id bigint NOT NULL,
                parent_id bigint NOT NULL,
                customer_tenant_id integer NOT NULL,
                customer_id bigint NOT NULL,
                CONSTRAINT pk_orders PRIMARY KEY (tenant_id, order_id),
                CONSTRAINT fk_orders_parent FOREIGN KEY (tenant_id, parent_id)
                    REFERENCES dbdisc_ref.parent_entity (tenant_id, parent_id),
                CONSTRAINT fk_orders_customer FOREIGN KEY (customer_tenant_id, customer_id)
                    REFERENCES dbdisc_a.customers (tenant_id, customer_id)
            );

            CREATE TABLE dbdisc_a."CaseSensitiveTable" (
                id integer NOT NULL,
                "MiXeDColumn" character varying(20),
                CONSTRAINT "PK_CaseSensitiveTable" PRIMARY KEY (id)
            );
            CREATE TABLE dbdisc_b.rename_before (id integer PRIMARY KEY);
            CREATE TABLE dbdisc_b.missing_entity (id integer PRIMARY KEY);
            CREATE TABLE dbdisc_a.business_canary (
                id integer PRIMARY KEY,
                canary_value text NOT NULL
            );
            CREATE TABLE dbdisc_denied.private_marker (id integer PRIMARY KEY);

            CREATE VIEW dbdisc_b.customer_summary AS
                SELECT tenant_id, customer_id, code FROM dbdisc_a.customers;
            CREATE INDEX ix_customers_active ON dbdisc_a.customers (active);
            CREATE UNIQUE INDEX ux_customers_external_id ON dbdisc_a.customers (external_id);
            CREATE INDEX ix_customers_lower_code
                ON dbdisc_a.customers ((lower(code))) WHERE active;
            CREATE INDEX ix_customers_amount_include
                ON dbdisc_a.customers (amount DESC) INCLUDE (description);
            CREATE SEQUENCE dbdisc_a.manual_sequence
                AS bigint START WITH 100 INCREMENT BY 5 MINVALUE 100 MAXVALUE 999999 CACHE 7 NO CYCLE;
            """);

        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText = "INSERT INTO dbdisc_a.business_canary (id, canary_value) VALUES (1, @canary)";
            insert.Parameters.AddWithValue("canary", BusinessRowCanary);
            await insert.ExecuteNonQueryAsync();
        }

        await Execute(connection, $"""
            REVOKE ALL PRIVILEGES ON SCHEMA dbdisc_a, dbdisc_b, dbdisc_ref, dbdisc_denied FROM PUBLIC;
            REVOKE ALL PRIVILEGES ON SCHEMA dbdisc_a, dbdisc_b, dbdisc_ref, dbdisc_denied FROM {discoveryRole};
            GRANT USAGE ON SCHEMA dbdisc_a, dbdisc_b, dbdisc_ref TO {discoveryRole};
            REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA dbdisc_a, dbdisc_b, dbdisc_ref, dbdisc_denied
                FROM PUBLIC, {discoveryRole};
            REVOKE ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA dbdisc_a, dbdisc_b, dbdisc_ref, dbdisc_denied
                FROM PUBLIC, {discoveryRole};
            """);
    }

    private static async Task ApplyStateTwo(PostgreSqlRealIntegrationEnvironment environment)
    {
        await using var connection = await Open(environment, owner: true);
        await Execute(connection, """
            COMMENT ON TABLE dbdisc_a.customers IS 'Customer master state 2 changed';
            ALTER TABLE dbdisc_b.rename_before RENAME TO rename_after;
            DROP TABLE dbdisc_b.missing_entity;
            CREATE TABLE dbdisc_b.added_entity (id bigint PRIMARY KEY, note text);
            SELECT pg_catalog.setval('dbdisc_a.manual_sequence'::regclass, 5000, true);
            """);
    }

    private static async Task AssertDiscoveryGrantMatrix(PostgreSqlRealIntegrationEnvironment environment)
    {
        await using var connection = await Open(environment, owner: false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT role_row.rolsuper,
                   pg_catalog.has_database_privilege(CURRENT_USER, CURRENT_DATABASE(), 'CONNECT'),
                   pg_catalog.has_schema_privilege(CURRENT_USER, 'dbdisc_a', 'USAGE'),
                   pg_catalog.has_schema_privilege(CURRENT_USER, 'dbdisc_b', 'USAGE'),
                   pg_catalog.has_schema_privilege(CURRENT_USER, 'dbdisc_ref', 'USAGE'),
                   pg_catalog.has_schema_privilege(CURRENT_USER, 'dbdisc_denied', 'USAGE'),
                   pg_catalog.has_schema_privilege(CURRENT_USER, 'dbdisc_a', 'CREATE'),
                   pg_catalog.has_table_privilege(CURRENT_USER, 'dbdisc_a.business_canary', 'SELECT')
            FROM pg_catalog.pg_roles AS role_row
            WHERE role_row.rolname = CURRENT_USER
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.False(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
        Assert.True(reader.GetBoolean(3));
        Assert.True(reader.GetBoolean(4));
        Assert.False(reader.GetBoolean(5));
        Assert.False(reader.GetBoolean(6));
        Assert.False(reader.GetBoolean(7));
        await reader.DisposeAsync();

        await using var forbidden = connection.CreateCommand();
        forbidden.CommandText = "SELECT canary_value FROM dbdisc_a.business_canary";
        var exception = await Assert.ThrowsAsync<PostgresException>(() => forbidden.ExecuteScalarAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
        Assert.DoesNotContain(BusinessRowCanary, exception.MessageText, StringComparison.Ordinal);
    }

    private static async Task AssertNoCanaryLeak(
        PostgreSqlRealIntegrationWebApplicationFactory factory,
        IReadOnlyList<string> apiPayloads,
        string discoveryPassword,
        string badPassword)
    {
        var apiText = string.Join('|', apiPayloads);
        Assert.DoesNotContain(BusinessRowCanary, apiText, StringComparison.Ordinal);
        Assert.DoesNotContain(discoveryPassword, apiText, StringComparison.Ordinal);
        Assert.DoesNotContain(badPassword, apiText, StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var persisted = new List<string>();
        persisted.AddRange(await database.DatabaseConnectionProfiles.Select(item =>
            string.Join('|', item.Name, item.Host, item.DatabaseName, item.ServiceName,
                item.Username, item.IncludedSchemasJson, item.ProviderSpecificOptionsJson,
                item.LastConnectionTestErrorCode, item.LastConnectionTestVendorCode,
                item.LastConnectionTestSummary)).ToArrayAsync());
        persisted.AddRange(await database.DatabaseConnectionSecrets.Select(item => item.ProtectedPayload ?? string.Empty).ToArrayAsync());
        persisted.AddRange(await database.DatabaseConnectionAuditEvents.Select(item =>
            string.Join('|', item.ErrorCode, item.VendorCode, item.ActorDisplayName)).ToArrayAsync());
        persisted.AddRange(await database.DatabaseDiscoveryRuns.Select(item =>
            string.Join('|', item.ErrorCode, item.ErrorSummary, item.SafeErrorMetadataJson,
                item.CapabilitySnapshotJson, item.ObjectCountsJson)).ToArrayAsync());
        persisted.AddRange(await database.DatabaseDiscoverySnapshots.Select(item =>
            string.Join('|', item.CanonicalContentJson, item.CountsJson, item.ContentSha256)).ToArrayAsync());
        persisted.AddRange(await database.DatabaseDiscoveryDifferences.Select(item =>
            string.Join('|', item.SummaryCountsJson, item.ContentSha256)).ToArrayAsync());
        persisted.AddRange(await database.DatabaseDiscoveryDifferenceEntries.Select(item =>
            string.Join('|', item.DisplayName, item.LogicalIdentity, item.ParentLogicalIdentity,
                item.BeforeJson, item.AfterJson)).ToArrayAsync());
        var persistedText = string.Join('|', persisted);
        Assert.DoesNotContain(BusinessRowCanary, persistedText, StringComparison.Ordinal);
        Assert.DoesNotContain(discoveryPassword, persistedText, StringComparison.Ordinal);
        Assert.DoesNotContain(badPassword, persistedText, StringComparison.Ordinal);

        var logDirectory = Path.GetDirectoryName(factory.IntegrationLogFilePath)!;
        var logText = string.Join(
            '|',
            (await Task.WhenAll(Directory
                .EnumerateFiles(logDirectory, "postgresql-integration-*.log")
                .Select(ReadSharedFile)))
            .Select(bytes => Encoding.UTF8.GetString(bytes)));
        Assert.DoesNotContain(BusinessRowCanary, logText, StringComparison.Ordinal);
        Assert.DoesNotContain(discoveryPassword, logText, StringComparison.Ordinal);
        Assert.DoesNotContain(badPassword, logText, StringComparison.Ordinal);

        foreach (var path in new[]
        {
            factory.DatabasePath,
            factory.DatabasePath + "-wal",
            factory.DatabasePath + "-shm",
        }.Where(File.Exists))
        {
            var bytes = await ReadSharedFile(path);
            Assert.False(ContainsBytes(bytes, Encoding.UTF8.GetBytes(BusinessRowCanary)));
            Assert.False(ContainsBytes(bytes, Encoding.UTF8.GetBytes(discoveryPassword)));
            Assert.False(ContainsBytes(bytes, Encoding.UTF8.GetBytes(badPassword)));
        }
    }

    private static void AssertNativeType(CanonicalDatabaseDiscoverySnapshot snapshot, string declaration) =>
        Assert.Contains(snapshot.Columns, item =>
            string.Equals(item.NativeDataType.Declaration, declaration, StringComparison.Ordinal));

    private static async Task<DatabaseConnectionProfileResponse> CreateProfile(
        PostgreSqlRealIntegrationWebApplicationFactory factory,
        HttpClient client,
        PostgreSqlRealIntegrationEnvironment environment,
        IReadOnlyList<string> includedSchemas)
    {
        var sourceId = await CreateSource(factory);
        using var response = await client.PostAsJsonAsync(
            "/api/admin/database-connection-profiles",
            new
            {
                databaseSourceId = sourceId,
                name = $"PostgreSQL-Real-{Guid.NewGuid():N}",
                providerType = "PostgreSql",
                host = environment.Host,
                port = environment.Port,
                databaseName = environment.Database,
                serviceName = (string?)null,
                authenticationMode = "UsernamePassword",
                username = environment.DiscoveryUsername,
                providerSpecificOptions = new { version = 1 },
                includedSchemas,
                isEnabled = true,
            });
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

    private static async Task<DatabaseDiscoveryRunResponse> Trigger(
        HttpClient client,
        DatabaseConnectionProfileResponse profile)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/discovery-runs",
            new { concurrencyToken = profile.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<DatabaseDiscoveryRunResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Discovery trigger response was empty.");
    }

    private static async Task<DatabaseDiscoveryRunResponse> WaitForTerminal(HttpClient client, long runId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(50);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync($"/api/database-discovery/runs/{runId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var run = await response.Content.ReadFromJsonAsync<DatabaseDiscoveryRunResponse>(JsonOptions)
                ?? throw new InvalidOperationException("Discovery Run response was empty.");
            if (run.Status is DatabaseDiscoveryRunStatus.Succeeded
                or DatabaseDiscoveryRunStatus.Failed
                or DatabaseDiscoveryRunStatus.Cancelled)
            {
                return run;
            }
            await Task.Delay(50);
        }
        throw new TimeoutException($"PostgreSQL Discovery Run {runId} did not become terminal.");
    }

    private static async Task<(DatabaseDiscoverySnapshotResponse Snapshot, string Raw)> GetSnapshot(
        HttpClient client,
        long snapshotId)
    {
        using var response = await client.GetAsync($"/api/database-discovery/snapshots/{snapshotId}");
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (
            JsonSerializer.Deserialize<DatabaseDiscoverySnapshotResponse>(raw, JsonOptions)
                ?? throw new InvalidOperationException("Snapshot response was empty."),
            raw);
    }

    private static async Task<(DatabaseDiscoveryDifferenceResponse Difference, string Raw)> GetDifference(
        HttpClient client,
        long differenceId)
    {
        using var response = await client.GetAsync($"/api/database-discovery/differences/{differenceId}");
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (
            JsonSerializer.Deserialize<DatabaseDiscoveryDifferenceResponse>(raw, JsonOptions)
                ?? throw new InvalidOperationException("Difference response was empty."),
            raw);
    }

    private static async Task<DatabaseDiscoveryDifferenceEntryPageResponse> GetDifferenceEntries(
        HttpClient client,
        long differenceId,
        DatabaseDiscoveryDifferenceState state,
        ICollection<string> payloads)
    {
        using var response = await client.GetAsync(
            $"/api/database-discovery/differences/{differenceId}/entries?state={state}&pageSize=200");
        var raw = await response.Content.ReadAsStringAsync();
        payloads.Add(raw);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonSerializer.Deserialize<DatabaseDiscoveryDifferenceEntryPageResponse>(raw, JsonOptions)
            ?? throw new InvalidOperationException("Difference entries response was empty.");
    }

    private static async Task<long> CreateSource(PostgreSqlRealIntegrationWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var systemId = await database.Systems.Select(item => item.Id).FirstAsync();
        var user = await database.Users.FirstAsync(item => item.AccessLevel == AccessLevel.Administrator);
        var now = DateTimeOffset.UtcNow;
        var source = new DatabaseSource
        {
            SystemId = systemId,
            Name = $"DBDISC-PG-REAL-{Guid.NewGuid():N}",
            Engine = "PostgreSQL",
            CreatedAt = now,
            CreatedByUserId = user.Id,
            CreatedByName = user.DisplayName,
            UpdatedAt = now,
            Version = 1,
        };
        database.DatabaseSources.Add(source);
        await database.SaveChangesAsync();
        return source.Id;
    }

    private static async Task<NpgsqlConnection> Open(
        PostgreSqlRealIntegrationEnvironment environment,
        bool owner)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = environment.Host,
            Port = environment.Port,
            Database = environment.Database,
            Username = owner ? environment.OwnerUsername : environment.DiscoveryUsername,
            Password = owner ? environment.OwnerPassword : environment.DiscoveryPassword,
            Pooling = false,
            Enlist = false,
            Timeout = 10,
            CommandTimeout = 30,
            IncludeErrorDetail = false,
            ApplicationName = "SystemKnowledgeHub.PostgreSqlRealIntegrationTests",
        };
        var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task Execute(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<byte[]> ReadSharedFile(string path)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private static bool ContainsBytes(byte[] value, byte[] candidate) =>
        value.AsSpan().IndexOf(candidate) >= 0;

    private static async Task DropFixtureSchemas(PostgreSqlRealIntegrationEnvironment environment)
    {
        await using var connection = await Open(environment, owner: true);
        await Execute(connection, """
            DROP SCHEMA IF EXISTS dbdisc_a CASCADE;
            DROP SCHEMA IF EXISTS dbdisc_b CASCADE;
            DROP SCHEMA IF EXISTS dbdisc_ref CASCADE;
            DROP SCHEMA IF EXISTS dbdisc_denied CASCADE;
            """);
    }

    private static string QuoteIdentifier(string value)
    {
        using var builder = new NpgsqlCommandBuilder();
        return builder.QuoteIdentifier(value);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
