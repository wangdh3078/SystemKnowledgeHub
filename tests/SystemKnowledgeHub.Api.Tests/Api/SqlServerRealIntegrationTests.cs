using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Providers.SqlServer;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class SqlServerRealIntegrationTests
{
    private const string BusinessRowCanary = "DBDISC_SQLSERVER_BUSINESS_CANARY_9C6A5B31";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task Environment_enabled_real_SQL_Server_2022_proves_full_provider_and_B04_pipeline()
    {
        if (!SqlServerRealIntegrationEnvironment.TryLoad(out var loaded)) return;
        var environment = loaded!;
        await CreateStateOne(environment);
        await AssertLeastPrivilege(environment);
        await AssertCatalogQueryInventory(environment);
        await AssertDirectProvider(environment);

        using var factory = new SqlServerRealIntegrationWebApplicationFactory();
        using var administrator = factory.CreateAuthenticatedClient();
        var apiPayloads = new List<string>();

        var profile = await CreateProfile(factory, administrator, environment, ["dbdisc_a", "dbdisc_b"]);
        profile = await SetSecret(administrator, profile, environment.DiscoveryPassword);

        using (var response = await administrator.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/test-connection",
            new { concurrencyToken = profile.ConcurrencyToken }))
        {
            var raw = await response.Content.ReadAsStringAsync();
            apiPayloads.Add(raw);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = JsonSerializer.Deserialize<DatabaseConnectionTestResponse>(raw, JsonOptions);
            Assert.NotNull(result);
            Assert.True(result.Succeeded,
                $"SQL Server Test Connection failed: {result.ErrorCode} / {result.VendorCode} / {result.Summary}");
            Assert.True(result.ProviderVersion?.StartsWith("16.", StringComparison.Ordinal) == true);
            Assert.Equal(environment.Database, result.DatabaseName);
            Assert.Null(result.ServiceName);
        }

        profile = await GetProfile(administrator, profile.Id);
        var first = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        Assert.True(
            first.Status == DatabaseDiscoveryRunStatus.Succeeded,
            $"First SQL Server discovery failed: {first.ErrorCode} / {first.ErrorSummary}");
        Assert.Equal(DatabaseProviderType.SqlServer, first.ProviderType);
        Assert.Null(first.BaseSnapshotId);
        Assert.NotNull(first.SnapshotId);
        Assert.NotNull(first.DifferenceId);
        Assert.Contains(first.Capabilities, item =>
            item.Name == "SupportsSequences"
            && item.State == DatabaseDiscoveryCapabilityState.Supported);

        var (firstSnapshot, firstRaw) = await GetSnapshot(administrator, first.SnapshotId!.Value);
        apiPayloads.Add(firstRaw);
        var canonical = firstSnapshot.Content.Deserialize<CanonicalDatabaseDiscoverySnapshot>(JsonOptions);
        Assert.NotNull(canonical);
        Assert.Equal(DatabaseProviderType.SqlServer, canonical.ProviderType);
        Assert.Equal(DatabaseDiscoveryCompleteness.Complete, canonical.Completeness);
        Assert.Equal(environment.Database, canonical.DatabaseInfo.CurrentDatabaseOrService);
        Assert.Contains(canonical.Objects, item => item.Name == "CaseSensitiveTable");
        Assert.Contains(canonical.Columns, item => item.Name == "MiXeDColumn");
        Assert.Contains(canonical.Objects, item =>
            item.Name == "customers" && item.DatabaseComment == "Customer master state 1");
        Assert.Contains(canonical.Columns, item =>
            item.Name == "code" && item.DatabaseComment == "Stable customer code");
        Assert.Contains(canonical.PrimaryKeys, item => item.ColumnLogicalIdentities.Count == 2);
        Assert.Contains(canonical.UniqueConstraints, item => item.ColumnLogicalIdentities.Count == 2);
        Assert.Contains(canonical.ForeignKeys, item => item.ColumnLogicalIdentities.Count == 2);
        Assert.NotEmpty(canonical.ForeignKeyReferenceClosure);
        Assert.Contains(canonical.Indexes, item => item.Name == "ix_customers_active");
        Assert.Contains(canonical.Indexes, item =>
            item.Name == "ix_customers_amount_include" && item.NonKeyParts.Count == 1);
        Assert.Contains(canonical.Indexes, item =>
            item.Name == "ix_customers_filtered" && item.NativePredicate is not null);
        Assert.Contains(canonical.Sequences, item => item.Name == "manual_sequence");
        foreach (var declaration in new[]
        {
            "int", "bigint", "decimal(12,2)", "varchar(100)", "nvarchar(100)",
            "nvarchar(max)", "bit",
            "date", "datetime2(3)", "datetimeoffset(3)", "uniqueidentifier", "varbinary(16)",
        }) AssertNativeType(canonical, declaration);

        var (firstDifference, differenceRaw) = await GetDifference(administrator, first.DifferenceId!.Value);
        apiPayloads.Add(differenceRaw);
        Assert.Null(firstDifference.BaseSnapshotId);
        Assert.True(firstDifference.SummaryCounts.Added > 0);
        Assert.Equal(0, firstDifference.SummaryCounts.Changed);
        Assert.Equal(0, firstDifference.SummaryCounts.MissingFromSource);
        Assert.Equal(0, firstDifference.SummaryCounts.Unchanged);

        profile = await GetProfile(administrator, profile.Id);
        var repeat = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        Assert.Equal(DatabaseDiscoveryRunStatus.Succeeded, repeat.Status);
        Assert.Equal(first.SnapshotId, repeat.BaseSnapshotId);
        var (repeatSnapshot, repeatRaw) = await GetSnapshot(administrator, repeat.SnapshotId!.Value);
        apiPayloads.Add(repeatRaw);
        Assert.NotEqual(firstSnapshot.ContentSha256, repeatSnapshot.ContentSha256);
        Assert.Equal(firstSnapshot.Counts, repeatSnapshot.Counts);
        var (repeatDifference, repeatDifferenceRaw) = await GetDifference(
            administrator, repeat.DifferenceId!.Value);
        apiPayloads.Add(repeatDifferenceRaw);
        Assert.Equal(0, repeatDifference.SummaryCounts.Added);
        Assert.Equal(0, repeatDifference.SummaryCounts.Changed);
        Assert.Equal(0, repeatDifference.SummaryCounts.MissingFromSource);
        Assert.True(repeatDifference.SummaryCounts.Unchanged > 0);

        await ApplyStateTwo(environment);
        profile = await GetProfile(administrator, profile.Id);
        var changedRun = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        Assert.True(
            changedRun.Status == DatabaseDiscoveryRunStatus.Succeeded,
            $"Changed SQL Server discovery failed: {changedRun.ErrorCode} / {changedRun.ErrorSummary}");
        Assert.Equal(repeat.SnapshotId, changedRun.BaseSnapshotId);
        Assert.Equal(first.ScopeGenerationId, changedRun.ScopeGenerationId);

        var (changedDifference, changedDifferenceRaw) = await GetDifference(
            administrator, changedRun.DifferenceId!.Value);
        apiPayloads.Add(changedDifferenceRaw);
        Assert.True(changedDifference.SummaryCounts.Added > 0);
        Assert.True(changedDifference.SummaryCounts.Changed > 0);
        Assert.True(changedDifference.SummaryCounts.MissingFromSource > 0);
        Assert.True(changedDifference.SummaryCounts.Unchanged > 0);
        var added = await Entries(administrator, changedRun.DifferenceId.Value,
            DatabaseDiscoveryDifferenceState.Added, apiPayloads);
        var changed = await Entries(administrator, changedRun.DifferenceId.Value,
            DatabaseDiscoveryDifferenceState.Changed, apiPayloads);
        var missing = await Entries(administrator, changedRun.DifferenceId.Value,
            DatabaseDiscoveryDifferenceState.MissingFromSource, apiPayloads);
        var unchanged = await Entries(administrator, changedRun.DifferenceId.Value,
            DatabaseDiscoveryDifferenceState.Unchanged, apiPayloads);
        Assert.Contains(added.Items, item => item.DisplayName.EndsWith(".added_entity", StringComparison.Ordinal));
        Assert.Contains(added.Items, item => item.DisplayName.EndsWith(".rename_after", StringComparison.Ordinal));
        Assert.Contains(missing.Items, item => item.DisplayName.EndsWith(".missing_entity", StringComparison.Ordinal));
        Assert.Contains(missing.Items, item => item.DisplayName.EndsWith(".rename_before", StringComparison.Ordinal));
        Assert.Contains(changed.Items, item => item.DisplayName.EndsWith(".customers", StringComparison.Ordinal));
        Assert.Contains(unchanged.Items, item =>
            item.EntityKind == DatabaseDiscoveryEntityKind.Sequence
            && item.DisplayName == "manual_sequence");

        await AssertB04Apply(factory, administrator, profile, changedRun.SnapshotId!.Value);

        var badPassword = environment.DiscoveryPassword + "-WRONG-18456";
        var bad = await CreateProfile(factory, administrator, environment, ["dbdisc_a"]);
        bad = await SetSecret(administrator, bad, badPassword);
        using (var response = await administrator.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{bad.Id}/test-connection",
            new { concurrencyToken = bad.ConcurrencyToken }))
        {
            var raw = await response.Content.ReadAsStringAsync();
            apiPayloads.Add(raw);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Contains("AuthenticationFailed", raw, StringComparison.Ordinal);
            Assert.Contains("MSSQL-18456", raw, StringComparison.Ordinal);
            Assert.DoesNotContain(badPassword, raw, StringComparison.Ordinal);
        }
        bad = await GetProfile(administrator, bad.Id);
        var badRun = await WaitForTerminal(administrator, (await Trigger(administrator, bad)).Id);
        Assert.Equal(DatabaseDiscoveryRunStatus.Failed, badRun.Status);
        Assert.Equal("AuthenticationFailed", badRun.ErrorCode);
        Assert.Null(badRun.SnapshotId);

        var denied = await CreateProfile(factory, administrator, environment, ["dbdisc_denied"]);
        denied = await SetSecret(administrator, denied, environment.DiscoveryPassword);
        using (var response = await administrator.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{denied.Id}/test-connection",
            new { concurrencyToken = denied.ConcurrencyToken }))
        {
            var raw = await response.Content.ReadAsStringAsync();
            apiPayloads.Add(raw);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Contains("InsufficientPrivilege", raw, StringComparison.Ordinal);
            Assert.DoesNotContain(BusinessRowCanary, raw, StringComparison.Ordinal);
        }
        denied = await GetProfile(administrator, denied.Id);
        var deniedRun = await WaitForTerminal(administrator, (await Trigger(administrator, denied)).Id);
        Assert.Equal(DatabaseDiscoveryRunStatus.Failed, deniedRun.Status);
        Assert.Equal("InsufficientPrivilege", deniedRun.ErrorCode);
        Assert.Null(deniedRun.SnapshotId);

        var cancellable = await CreateProfile(factory, administrator, environment, ["dbdisc_a"]);
        cancellable = await SetSecret(administrator, cancellable, environment.DiscoveryPassword);
        var queued = await Trigger(administrator, cancellable);
        using (var response = await administrator.PostAsJsonAsync(
            $"/api/database-discovery/runs/{queued.Id}/cancel",
            new { concurrencyToken = queued.ConcurrencyToken }))
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cancelled = await WaitForTerminal(administrator, queued.Id);
        Assert.Equal(DatabaseDiscoveryRunStatus.Cancelled, cancelled.Status);
        Assert.Null(cancelled.SnapshotId);

        await AssertNoLeak(factory, apiPayloads, environment.DiscoveryPassword, badPassword);
    }

    private static async Task CreateStateOne(SqlServerRealIntegrationEnvironment environment)
    {
        await using var connection = await Open(environment, owner: true);
        await Execute(connection, """
            IF OBJECT_ID(N'dbdisc_b.customer_summary', N'V') IS NOT NULL DROP VIEW dbdisc_b.customer_summary;
            IF OBJECT_ID(N'dbdisc_a.customer_names', N'V') IS NOT NULL DROP VIEW dbdisc_a.customer_names;
            IF OBJECT_ID(N'dbdisc_a.orders', N'U') IS NOT NULL DROP TABLE dbdisc_a.orders;
            IF OBJECT_ID(N'dbdisc_a.customers', N'U') IS NOT NULL DROP TABLE dbdisc_a.customers;
            IF OBJECT_ID(N'dbdisc_a.CaseSensitiveTable', N'U') IS NOT NULL DROP TABLE dbdisc_a.CaseSensitiveTable;
            IF OBJECT_ID(N'dbdisc_a.business_canary', N'U') IS NOT NULL DROP TABLE dbdisc_a.business_canary;
            IF OBJECT_ID(N'dbdisc_b.rename_before', N'U') IS NOT NULL DROP TABLE dbdisc_b.rename_before;
            IF OBJECT_ID(N'dbdisc_b.rename_after', N'U') IS NOT NULL DROP TABLE dbdisc_b.rename_after;
            IF OBJECT_ID(N'dbdisc_b.missing_entity', N'U') IS NOT NULL DROP TABLE dbdisc_b.missing_entity;
            IF OBJECT_ID(N'dbdisc_b.added_entity', N'U') IS NOT NULL DROP TABLE dbdisc_b.added_entity;
            IF OBJECT_ID(N'dbdisc_b.sync_target', N'U') IS NOT NULL DROP TABLE dbdisc_b.sync_target;
            IF OBJECT_ID(N'dbdisc_b.protected_target', N'U') IS NOT NULL DROP TABLE dbdisc_b.protected_target;
            IF OBJECT_ID(N'dbdisc_ref.parent_entity', N'U') IS NOT NULL DROP TABLE dbdisc_ref.parent_entity;
            IF OBJECT_ID(N'dbdisc_denied.private_marker', N'U') IS NOT NULL DROP TABLE dbdisc_denied.private_marker;
            IF OBJECT_ID(N'dbdisc_a.manual_sequence', N'SO') IS NOT NULL DROP SEQUENCE dbdisc_a.manual_sequence;
            IF SCHEMA_ID(N'dbdisc_a') IS NOT NULL EXEC(N'DROP SCHEMA dbdisc_a');
            IF SCHEMA_ID(N'dbdisc_b') IS NOT NULL EXEC(N'DROP SCHEMA dbdisc_b');
            IF SCHEMA_ID(N'dbdisc_ref') IS NOT NULL EXEC(N'DROP SCHEMA dbdisc_ref');
            IF SCHEMA_ID(N'dbdisc_denied') IS NOT NULL EXEC(N'DROP SCHEMA dbdisc_denied');
            EXEC(N'CREATE SCHEMA dbdisc_a AUTHORIZATION dbo');
            EXEC(N'CREATE SCHEMA dbdisc_b AUTHORIZATION dbo');
            EXEC(N'CREATE SCHEMA dbdisc_ref AUTHORIZATION dbo');
            EXEC(N'CREATE SCHEMA dbdisc_denied AUTHORIZATION dbo');

            CREATE TABLE dbdisc_ref.parent_entity (
                tenant_id int NOT NULL,
                parent_id bigint NOT NULL,
                label nvarchar(max) NULL,
                CONSTRAINT pk_parent_entity PRIMARY KEY (tenant_id, parent_id)
            );
            CREATE TABLE dbdisc_a.customers (
                tenant_id int NOT NULL,
                customer_id bigint IDENTITY(1,1) NOT NULL,
                code nvarchar(32) NOT NULL,
                legacy_code varchar(100) NULL,
                unicode_name nvarchar(100) NULL,
                amount decimal(12,2) NULL,
                description nvarchar(max) NULL,
                active bit NOT NULL CONSTRAINT df_customers_active DEFAULT (1),
                business_date date NULL,
                created_at datetime2(3) NULL,
                created_with_tz datetimeoffset(3) NULL,
                external_id uniqueidentifier NULL,
                payload varbinary(16) NULL,
                CONSTRAINT pk_customers PRIMARY KEY (tenant_id, customer_id),
                CONSTRAINT uq_customers_tenant_code UNIQUE (tenant_id, code)
            );
            CREATE TABLE dbdisc_a.orders (
                tenant_id int NOT NULL,
                order_id bigint NOT NULL,
                parent_id bigint NOT NULL,
                customer_tenant_id int NOT NULL,
                customer_id bigint NOT NULL,
                CONSTRAINT pk_orders PRIMARY KEY (tenant_id, order_id),
                CONSTRAINT fk_orders_parent FOREIGN KEY (tenant_id, parent_id)
                    REFERENCES dbdisc_ref.parent_entity (tenant_id, parent_id),
                CONSTRAINT fk_orders_customer FOREIGN KEY (customer_tenant_id, customer_id)
                    REFERENCES dbdisc_a.customers (tenant_id, customer_id)
            );
            CREATE TABLE dbdisc_a.CaseSensitiveTable (
                id int NOT NULL,
                MiXeDColumn nvarchar(20) NULL,
                CONSTRAINT PK_CaseSensitiveTable PRIMARY KEY (id)
            );
            CREATE TABLE dbdisc_b.rename_before (id int NOT NULL PRIMARY KEY);
            CREATE TABLE dbdisc_b.missing_entity (id int NOT NULL PRIMARY KEY);
            CREATE TABLE dbdisc_a.business_canary (id int NOT NULL PRIMARY KEY, canary_value nvarchar(200) NOT NULL);
            CREATE TABLE dbdisc_denied.private_marker (id int NOT NULL PRIMARY KEY);
            EXEC(N'CREATE VIEW dbdisc_b.customer_summary AS
                SELECT tenant_id, customer_id, code FROM dbdisc_a.customers');
            EXEC(N'CREATE VIEW dbdisc_a.customer_names AS
                SELECT tenant_id, customer_id, unicode_name FROM dbdisc_a.customers');
            CREATE INDEX ix_customers_active ON dbdisc_a.customers (active);
            CREATE UNIQUE INDEX ux_customers_external_id ON dbdisc_a.customers (external_id);
            CREATE INDEX ix_customers_filtered ON dbdisc_a.customers (code) WHERE active = 1;
            CREATE INDEX ix_customers_amount_include ON dbdisc_a.customers (amount DESC) INCLUDE (description);
            CREATE SEQUENCE dbdisc_a.manual_sequence AS bigint
                START WITH 100 INCREMENT BY 5 MINVALUE 100 MAXVALUE 999999 CACHE 7 NO CYCLE;
            INSERT INTO dbdisc_a.business_canary (id, canary_value)
                VALUES (1, N'DBDISC_SQLSERVER_BUSINESS_CANARY_9C6A5B31');
            EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Customer master state 1',
                @level0type=N'SCHEMA', @level0name=N'dbdisc_a', @level1type=N'TABLE', @level1name=N'customers';
            EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stable customer code',
                @level0type=N'SCHEMA', @level0name=N'dbdisc_a', @level1type=N'TABLE', @level1name=N'customers',
                @level2type=N'COLUMN', @level2name=N'code';
            """);
        var principal = QuoteIdentifier(environment.DiscoveryUsername);
        await Execute(connection, $"""
            GRANT CONNECT TO {principal};
            GRANT VIEW DEFINITION ON SCHEMA::dbdisc_a TO {principal};
            GRANT VIEW DEFINITION ON SCHEMA::dbdisc_b TO {principal};
            GRANT VIEW DEFINITION ON SCHEMA::dbdisc_ref TO {principal};
            """);
    }

    private static async Task ApplyStateTwo(SqlServerRealIntegrationEnvironment environment)
    {
        await using var connection = await Open(environment, owner: true);
        await Execute(connection, """
            EXEC sys.sp_updateextendedproperty @name=N'MS_Description', @value=N'Customer master state 2 changed',
                @level0type=N'SCHEMA', @level0name=N'dbdisc_a', @level1type=N'TABLE', @level1name=N'customers';
            DROP TABLE dbdisc_b.rename_before;
            CREATE TABLE dbdisc_b.rename_after (id int NOT NULL PRIMARY KEY);
            DROP TABLE dbdisc_b.missing_entity;
            CREATE TABLE dbdisc_b.added_entity (id bigint NOT NULL PRIMARY KEY, note nvarchar(max) NULL);
            CREATE TABLE dbdisc_b.sync_target (id bigint NOT NULL PRIMARY KEY, display_name nvarchar(80) NULL);
            CREATE TABLE dbdisc_b.protected_target (id bigint NOT NULL PRIMARY KEY, display_name nvarchar(80) NULL);
            SELECT NEXT VALUE FOR dbdisc_a.manual_sequence;
            """);
    }

    private static async Task AssertLeastPrivilege(SqlServerRealIntegrationEnvironment environment)
    {
        await using var connection = await Open(environment, owner: false);
        await using var command = new SqlCommand("""
            SELECT IS_SRVROLEMEMBER('sysadmin'), IS_MEMBER('db_owner'),
                   IS_MEMBER('db_datareader'), IS_MEMBER('db_datawriter'),
                   HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CONNECT'),
                   HAS_PERMS_BY_NAME('dbdisc_a', 'SCHEMA', 'VIEW DEFINITION'),
                   HAS_PERMS_BY_NAME('dbdisc_denied', 'SCHEMA', 'VIEW DEFINITION'),
                   HAS_PERMS_BY_NAME('dbdisc_a.business_canary', 'OBJECT', 'SELECT')
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(0, reader.GetInt32(0));
        Assert.Equal(0, reader.GetInt32(1));
        Assert.Equal(0, reader.GetInt32(2));
        Assert.Equal(0, reader.GetInt32(3));
        Assert.Equal(1, reader.GetInt32(4));
        Assert.Equal(1, reader.GetInt32(5));
        Assert.Equal(0, reader.GetInt32(6));
        Assert.Equal(0, reader.GetInt32(7));
        await reader.DisposeAsync();
        await using var forbidden = new SqlCommand(
            "SELECT canary_value FROM dbdisc_a.business_canary", connection);
        var exception = await Assert.ThrowsAsync<SqlException>(() => forbidden.ExecuteScalarAsync());
        Assert.Equal(229, exception.Number);
        Assert.DoesNotContain(BusinessRowCanary, exception.Message, StringComparison.Ordinal);
    }

    private static async Task AssertCatalogQueryInventory(SqlServerRealIntegrationEnvironment environment)
    {
        await using var connection = await Open(environment, owner: false);
        foreach (var (name, sql) in new Dictionary<string, string>
        {
            ["Objects"] = SqlServerCatalogSql.Objects,
            ["Columns"] = SqlServerCatalogSql.Columns,
            ["Constraints"] = SqlServerCatalogSql.Constraints,
            ["IndexParts"] = SqlServerCatalogSql.IndexParts,
            ["Sequences"] = SqlServerCatalogSql.Sequences,
        })
        {
            await using var command = SqlServerCatalogSql.CreateScopedCommand(
                connection, sql, ["dbdisc_a", "dbdisc_b"], 15);
            try
            {
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync()) { }
            }
            catch (SqlException exception)
            {
                Assert.Fail($"SQL Server catalog query {name} failed with MSSQL-{exception.Number}.");
            }
        }
    }

    private static async Task AssertDirectProvider(SqlServerRealIntegrationEnvironment environment)
    {
        var settings = new DatabaseDiscoveryOptions
        {
            ConnectionTimeoutSeconds = 5,
            CatalogCommandTimeoutSeconds = 15,
            SqlServerTrustServerCertificate = true,
        };
        var connection = new DatabaseDiscoveryConnectionContext(
            1, 1, 1, DatabaseProviderType.SqlServer, environment.Host, environment.Port,
            environment.Database, null, environment.DiscoveryUsername, environment.DiscoveryPassword,
            ["dbdisc_a", "dbdisc_b"]);
        var request = new DatabaseDiscoveryRequest(connection.IncludedSchemas, settings.Limits);
        var reader = new SqlClientSqlServerDiscoveryCatalogReader(Options.Create(settings));
        SqlServerCapabilityProbe capabilities;
        try
        {
            capabilities = await reader.ReadCapabilitiesAsync(connection, CancellationToken.None);
        }
        catch (DatabaseDiscoveryProviderException exception)
        {
            Assert.Fail($"SQL Server capability reader failed with {exception.ErrorCode} / {exception.VendorCode} / {exception.SafeSummary}");
            return;
        }
        SqlServerCatalogSnapshot catalog;
        try
        {
            catalog = await reader.ReadCatalogAsync(connection, request, CancellationToken.None);
        }
        catch (DatabaseDiscoveryProviderException exception)
        {
            Assert.Fail($"SQL Server catalog reader failed with {exception.ErrorCode} / {exception.VendorCode} / {exception.SafeSummary}");
            return;
        }

        var provider = new SqlServerDiscoveryProvider(
            new CapturedSqlServerCatalogReader(capabilities, catalog), TimeProvider.System);
        try
        {
            await provider.DiscoverAsync(
                connection,
                request,
                new DatabaseProviderCapabilities(capabilities.Capabilities),
                CancellationToken.None);
        }
        catch (DatabaseDiscoveryProviderException exception)
        {
            Assert.Fail($"SQL Server canonical mapping failed with {exception.ErrorCode}.");
        }
    }

    private static async Task AssertB04Apply(
        SqlServerRealIntegrationWebApplicationFactory factory,
        HttpClient client,
        DatabaseConnectionProfileResponse profile,
        long snapshotId)
    {
        await SeedProtectedKnowledge(factory, profile.DatabaseSourceId);
        using var response = await client.GetAsync(
            $"/api/database-discovery/reconciliation?profileId={profile.Id}&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reconciliation = await response.Content.ReadFromJsonAsync<DatabaseDiscoveryReconciliationPageResponse>(JsonOptions)
            ?? throw new InvalidOperationException("SQL Server reconciliation response was empty.");
        Assert.Equal(DatabaseProviderType.SqlServer, reconciliation.ProviderType);
        var actions = reconciliation.Items
            .Where(item => item.ObjectName is "sync_target" or "protected_target"
                && item.SuggestedAction is not null)
            .Select(item => new
            {
                actionType = item.SuggestedAction!.Value.ToString(),
                item.LogicalIdentity,
                item.TargetId,
            }).Cast<object>().ToArray();
        Assert.Equal(6, actions.Length);
        Assert.Contains(reconciliation.Items, item =>
            item.EntityKind == DatabaseDiscoveryEntityKind.ForeignKey
            && item.Status == DatabaseDiscoveryReconciliationStatus.Unsupported
            && item.SuggestedAction is null);
        Assert.Contains(reconciliation.Items, item =>
            item.EntityKind == DatabaseDiscoveryEntityKind.UniqueConstraint
            && item.Status == DatabaseDiscoveryReconciliationStatus.Unsupported
            && item.SuggestedAction is null);
        Assert.Contains(reconciliation.Items, item =>
            item.EntityKind == DatabaseDiscoveryEntityKind.Index
            && item.Status == DatabaseDiscoveryReconciliationStatus.Unsupported
            && item.SuggestedAction is null);
        Assert.Contains(reconciliation.Items, item =>
            item.EntityKind == DatabaseDiscoveryEntityKind.Sequence
            && item.Status == DatabaseDiscoveryReconciliationStatus.Unsupported
            && item.SuggestedAction is null);

        using var create = await client.PostAsJsonAsync(
            "/api/database-discovery/sync-plans",
            new { profileId = profile.Id, targetSnapshotId = snapshotId, actions });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var plan = await ReadPlan(create);
        using var preview = await client.PostAsJsonAsync(
            $"/api/database-discovery/sync-plans/{plan.Id}/preview",
            new { concurrencyToken = plan.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        plan = await ReadPlan(preview);
        Assert.NotNull(plan.Preview);
        using var confirm = await client.PostAsJsonAsync(
            $"/api/database-discovery/sync-plans/{plan.Id}/confirm",
            new { previewHash = plan.Preview!.PreviewHash, concurrencyToken = plan.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        plan = await ReadPlan(confirm);
        using var apply = await client.PostAsJsonAsync(
            $"/api/database-discovery/sync-plans/{plan.Id}/apply",
            new { previewHash = plan.Preview!.PreviewHash, concurrencyToken = plan.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, apply.StatusCode);
        plan = await ReadPlan(apply);
        Assert.Equal(DatabaseDiscoverySyncPlanStatus.Applied, plan.Status);
        Assert.Equal(1, plan.Result!.CreatedObjects);
        Assert.Equal(2, plan.Result.CreatedColumns);
        Assert.Equal(1, plan.Result.LinkedObjects);
        Assert.Equal(2, plan.Result.LinkedColumns);

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var created = await database.DatabaseObjects
            .Include(item => item.Columns)
            .SingleAsync(item => item.DatabaseSourceId == profile.DatabaseSourceId
                && item.SchemaName == "dbdisc_b" && item.ObjectName == "sync_target");
        Assert.Equal(2, created.Columns.Count);
        var protectedObject = await database.DatabaseObjects
            .Include(item => item.Columns)
            .ThenInclude(item => item.KnownValues)
            .SingleAsync(item => item.DatabaseSourceId == profile.DatabaseSourceId
                && item.SchemaName == "dbdisc_b" && item.ObjectName == "protected_target");
        Assert.Equal("人工维护的 SQL Server 对象说明", protectedObject.BusinessDescription);
        Assert.Equal("[\"display_name\"]", protectedObject.BusinessKeyColumnsJson);
        Assert.Equal(DatabaseAccessMode.Read, protectedObject.AccessMode);
        Assert.Equal(KnowledgeStatus.Confirmed, protectedObject.KnowledgeStatus);
        Assert.False(protectedObject.IsDeleted);
        var protectedColumn = protectedObject.Columns.Single(item => item.ColumnName == "display_name");
        Assert.Equal("人工维护的 SQL Server 字段说明", protectedColumn.BusinessDescription);
        Assert.Equal("VIP", Assert.Single(protectedColumn.KnownValues).ValueText);
        Assert.Equal(2, await database.Evidence.CountAsync(item =>
            item.SubjectType == EvidenceSubjectType.DatabaseColumn
            && item.SubjectId == protectedColumn.Id));
    }

    private static async Task SeedProtectedKnowledge(
        SqlServerRealIntegrationWebApplicationFactory factory,
        long databaseSourceId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var administrator = await database.Users.FirstAsync(item => item.AccessLevel == AccessLevel.Administrator);
        var now = DateTimeOffset.UtcNow;
        var databaseObject = new DatabaseObject
        {
            DatabaseSourceId = databaseSourceId,
            SchemaName = "dbdisc_b",
            ObjectName = "protected_target",
            ObjectType = DatabaseObjectType.Table,
            DatabaseComment = "人工保留的旧技术备注",
            BusinessDescription = "人工维护的 SQL Server 对象说明",
            AccessMode = DatabaseAccessMode.Read,
            BusinessKeyColumnsJson = "[\"display_name\"]",
            PrimaryKeyColumnsJson = "[\"id\"]",
            CreatedAt = now,
            CreatedByUserId = administrator.Id,
            CreatedByName = administrator.DisplayName,
            UpdatedAt = now,
            KnowledgeStatus = KnowledgeStatus.Confirmed,
            KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = administrator.DisplayName,
            KnowledgeStatusChangedByRole = "Administrator",
            Version = 1,
        };
        databaseObject.Columns.Add(new DatabaseColumn
        {
            OrdinalPosition = 1,
            ColumnName = "id",
            DataType = "bigint",
            IsNullable = false,
            CreatedAt = now,
            CreatedByUserId = administrator.Id,
            CreatedByDisplayName = administrator.DisplayName,
            UpdatedAt = now,
            KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = administrator.DisplayName,
            KnowledgeStatusChangedByRole = "Administrator",
            Version = 1,
        });
        var label = new DatabaseColumn
        {
            OrdinalPosition = 2,
            ColumnName = "display_name",
            DataType = "nvarchar(80)",
            IsNullable = true,
            BusinessDescription = "人工维护的 SQL Server 字段说明",
            CreatedAt = now,
            CreatedByUserId = administrator.Id,
            CreatedByDisplayName = administrator.DisplayName,
            UpdatedAt = now,
            KnowledgeStatus = KnowledgeStatus.Confirmed,
            KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = administrator.DisplayName,
            KnowledgeStatusChangedByRole = "Administrator",
            Version = 1,
        };
        label.KnownValues.Add(new ColumnKnownValue
        {
            ValueText = "VIP",
            Meaning = "人工维护的重要值",
            SortOrder = 1,
            CreatedAt = now,
            UpdatedAt = now,
        });
        databaseObject.Columns.Add(label);
        database.DatabaseObjects.Add(databaseObject);
        await database.SaveChangesAsync();
        database.Evidence.AddRange(
            HumanEvidence(EvidenceType.DatabaseComment, label.Id, "SQL Server 人工证据"),
            HumanEvidence(EvidenceType.HumanConfirmation, label.Id, "SQL Server 人工确认"));
        await database.SaveChangesAsync();
    }

    private static Evidence HumanEvidence(EvidenceType type, long subjectId, string title) => new()
    {
        EvidenceType = type,
        SubjectType = EvidenceSubjectType.DatabaseColumn,
        SubjectId = subjectId,
        SourceTitle = title,
        SourceReference = "manual://dbdisc-sqlserver-b01",
        SupportReason = "SQL Server B04 人工知识保护回归",
        ProviderName = "人工审查人",
        ProviderRole = "Administrator",
        ProvidedAt = DateTimeOffset.UtcNow,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        Version = 1,
    };

    private static async Task AssertNoLeak(
        SqlServerRealIntegrationWebApplicationFactory factory,
        IReadOnlyList<string> apiPayloads,
        string password,
        string badPassword)
    {
        var api = string.Join('|', apiPayloads);
        foreach (var value in new[] { BusinessRowCanary, password, badPassword })
            Assert.DoesNotContain(value, api, StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var persisted = new List<string>();
        persisted.AddRange(await database.DatabaseConnectionProfiles.Select(item =>
            string.Join('|', item.Name, item.Host, item.DatabaseName, item.Username,
                item.LastConnectionTestErrorCode, item.LastConnectionTestVendorCode,
                item.LastConnectionTestSummary)).ToArrayAsync());
        persisted.AddRange(await database.DatabaseConnectionSecrets.Select(item => item.ProtectedPayload ?? "").ToArrayAsync());
        persisted.AddRange(await database.DatabaseDiscoveryRuns.Select(item =>
            string.Join('|', item.ErrorCode, item.ErrorSummary, item.SafeErrorMetadataJson)).ToArrayAsync());
        persisted.AddRange(await database.DatabaseDiscoverySnapshots.Select(item => item.CanonicalContentJson).ToArrayAsync());
        persisted.AddRange(await database.DatabaseDiscoveryDifferenceEntries.Select(item =>
            string.Join('|', item.BeforeJson, item.AfterJson, item.DisplayName)).ToArrayAsync());
        var persistedText = string.Join('|', persisted);
        foreach (var value in new[] { BusinessRowCanary, password, badPassword })
            Assert.DoesNotContain(value, persistedText, StringComparison.Ordinal);

        var directory = Path.GetDirectoryName(factory.IntegrationLogFilePath)!;
        var logText = Encoding.UTF8.GetString((await Task.WhenAll(Directory
            .EnumerateFiles(directory, "sqlserver-integration-*.log")
            .Select(ReadSharedFile))).SelectMany(value => value).ToArray());
        foreach (var value in new[] { BusinessRowCanary, password, badPassword })
            Assert.DoesNotContain(value, logText, StringComparison.Ordinal);

        foreach (var path in new[] { factory.DatabasePath, factory.DatabasePath + "-wal", factory.DatabasePath + "-shm" }
            .Where(File.Exists))
        {
            var bytes = await ReadSharedFile(path);
            foreach (var value in new[] { BusinessRowCanary, password, badPassword })
                Assert.False(bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(value)) >= 0);
        }
    }

    private static async Task<DatabaseConnectionProfileResponse> CreateProfile(
        SqlServerRealIntegrationWebApplicationFactory factory,
        HttpClient client,
        SqlServerRealIntegrationEnvironment environment,
        IReadOnlyList<string> schemas)
    {
        var sourceId = await CreateSource(factory);
        using var response = await client.PostAsJsonAsync("/api/admin/database-connection-profiles", new
        {
            databaseSourceId = sourceId,
            name = $"SQLServer-Real-{Guid.NewGuid():N}",
            providerType = "SqlServer",
            host = environment.Host,
            port = environment.Port,
            databaseName = environment.Database,
            serviceName = (string?)null,
            authenticationMode = "UsernamePassword",
            username = environment.DiscoveryUsername,
            providerSpecificOptions = new { version = 1 },
            includedSchemas = schemas,
            isEnabled = true,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadProfile(response);
    }

    private static async Task<long> CreateSource(SqlServerRealIntegrationWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var systemId = await database.Systems.Select(item => item.Id).FirstAsync();
        var user = await database.Users.FirstAsync(item => item.AccessLevel == AccessLevel.Administrator);
        var now = DateTimeOffset.UtcNow;
        var source = new DatabaseSource
        {
            SystemId = systemId,
            Name = $"DBDISC-SQLSERVER-REAL-{Guid.NewGuid():N}",
            Engine = "SQL Server",
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

    private static async Task<DatabaseConnectionProfileResponse> SetSecret(
        HttpClient client, DatabaseConnectionProfileResponse profile, string password)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/secret",
            new { password, concurrencyToken = profile.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadProfile(response);
    }

    private static async Task<DatabaseConnectionProfileResponse> GetProfile(HttpClient client, long id) =>
        await client.GetFromJsonAsync<DatabaseConnectionProfileResponse>(
            $"/api/admin/database-connection-profiles/{id}", JsonOptions)
        ?? throw new InvalidOperationException("SQL Server Profile response was empty.");

    private static async Task<DatabaseConnectionProfileResponse> ReadProfile(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<DatabaseConnectionProfileResponse>(JsonOptions)
        ?? throw new InvalidOperationException("SQL Server Profile response was empty.");

    private static async Task<DatabaseDiscoveryRunResponse> Trigger(
        HttpClient client, DatabaseConnectionProfileResponse profile)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/discovery-runs",
            new { concurrencyToken = profile.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<DatabaseDiscoveryRunResponse>(JsonOptions)
            ?? throw new InvalidOperationException("SQL Server Run response was empty.");
    }

    private static async Task<DatabaseDiscoveryRunResponse> WaitForTerminal(HttpClient client, long id)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(50);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var run = await client.GetFromJsonAsync<DatabaseDiscoveryRunResponse>(
                $"/api/database-discovery/runs/{id}", JsonOptions)
                ?? throw new InvalidOperationException("SQL Server Run response was empty.");
            if (run.Status is DatabaseDiscoveryRunStatus.Succeeded
                or DatabaseDiscoveryRunStatus.Failed
                or DatabaseDiscoveryRunStatus.Cancelled) return run;
            await Task.Delay(50);
        }
        throw new TimeoutException($"SQL Server Discovery Run {id} did not become terminal.");
    }

    private static async Task<(DatabaseDiscoverySnapshotResponse Snapshot, string Raw)> GetSnapshot(
        HttpClient client, long id)
    {
        using var response = await client.GetAsync($"/api/database-discovery/snapshots/{id}");
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (JsonSerializer.Deserialize<DatabaseDiscoverySnapshotResponse>(raw, JsonOptions)!, raw);
    }

    private static async Task<(DatabaseDiscoveryDifferenceResponse Difference, string Raw)> GetDifference(
        HttpClient client, long id)
    {
        using var response = await client.GetAsync($"/api/database-discovery/differences/{id}");
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (JsonSerializer.Deserialize<DatabaseDiscoveryDifferenceResponse>(raw, JsonOptions)!, raw);
    }

    private static async Task<DatabaseDiscoveryDifferenceEntryPageResponse> Entries(
        HttpClient client,
        long id,
        DatabaseDiscoveryDifferenceState state,
        ICollection<string> payloads)
    {
        using var response = await client.GetAsync(
            $"/api/database-discovery/differences/{id}/entries?state={state}&pageSize=100");
        var raw = await response.Content.ReadAsStringAsync();
        payloads.Add(raw);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonSerializer.Deserialize<DatabaseDiscoveryDifferenceEntryPageResponse>(raw, JsonOptions)!;
    }

    private static async Task<DatabaseDiscoverySyncPlanResponse> ReadPlan(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<DatabaseDiscoverySyncPlanResponse>(JsonOptions)
        ?? throw new InvalidOperationException("SQL Server sync plan response was empty.");

    private static void AssertNativeType(CanonicalDatabaseDiscoverySnapshot snapshot, string declaration) =>
        Assert.Contains(snapshot.Columns, item => item.NativeDataType.Declaration == declaration);

    private sealed class CapturedSqlServerCatalogReader(
        SqlServerCapabilityProbe capabilities,
        SqlServerCatalogSnapshot catalog) : ISqlServerDiscoveryCatalogReader
    {
        public Task<SqlServerCapabilityProbe> ReadCapabilitiesAsync(
            DatabaseDiscoveryConnectionContext connection,
            CancellationToken cancellationToken) => Task.FromResult(capabilities);

        public Task<SqlServerCatalogSnapshot> ReadCatalogAsync(
            DatabaseDiscoveryConnectionContext connection,
            DatabaseDiscoveryRequest request,
            CancellationToken cancellationToken) => Task.FromResult(catalog);
    }

    private static async Task<SqlConnection> Open(
        SqlServerRealIntegrationEnvironment environment, bool owner)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{environment.Host},{environment.Port}",
            InitialCatalog = environment.Database,
            UserID = owner ? environment.OwnerUsername : environment.DiscoveryUsername,
            Password = owner ? environment.OwnerPassword : environment.DiscoveryPassword,
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = true,
            Pooling = false,
            Enlist = false,
            ConnectTimeout = 10,
            ApplicationName = "SystemKnowledgeHub.SqlServerRealIntegrationTests",
        };
        var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task Execute(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 30 };
        await command.ExecuteNonQueryAsync();
    }

    private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static async Task<byte[]> ReadSharedFile(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
