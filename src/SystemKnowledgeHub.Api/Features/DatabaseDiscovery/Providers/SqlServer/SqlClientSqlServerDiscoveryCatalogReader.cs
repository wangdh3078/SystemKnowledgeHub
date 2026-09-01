using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Providers.SqlServer;

internal sealed class SqlClientSqlServerDiscoveryCatalogReader(
    IOptions<DatabaseDiscoveryOptions> options) : ISqlServerDiscoveryCatalogReader
{
    private readonly DatabaseDiscoveryOptions settings = Validate(options.Value);

    internal int ConfiguredConnectionTimeoutSeconds => settings.ConnectionTimeoutSeconds;
    internal int ConfiguredCatalogCommandTimeoutSeconds => settings.CatalogCommandTimeoutSeconds;

    public async Task<SqlServerCapabilityProbe> ReadCapabilitiesAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken)
    {
        var connected = false;
        try
        {
            await using var database = SqlServerConnectionFactory.Create(
                connection,
                settings.ConnectionTimeoutSeconds,
                settings.SqlServerTrustServerCertificate);
            await database.OpenAsync(cancellationToken);
            connected = true;
            var target = await ReadTarget(database, cancellationToken);
            SqlServerDiscoveryRules.ValidateTarget(connection, target);

            IReadOnlyList<CanonicalCapability> capabilities =
            [
                new("SupportsIdentityColumns", DatabaseDiscoveryCapabilityState.NotSupported, "CoreColumnContractDoesNotProjectIdentity"),
                new("SupportsComputedColumns", DatabaseDiscoveryCapabilityState.NotSupported, "CoreColumnContractDoesNotProjectComputedExpression"),
                new("SupportsInvisibleColumns", DatabaseDiscoveryCapabilityState.NotSupported, "SqlServer2022NotSupported"),
                new("SupportsMaterializedViews", DatabaseDiscoveryCapabilityState.NotApplicable, "SqlServerIndexedViewsOutsideCore"),
                new("SupportsPartitions", DatabaseDiscoveryCapabilityState.NotSupported, "CoreScopeExcluded"),
                new("SupportsSequences", DatabaseDiscoveryCapabilityState.Supported, null),
                new("SupportsSynonyms", DatabaseDiscoveryCapabilityState.NotSupported, "CoreScopeExcluded"),
                new("SupportsTriggers", DatabaseDiscoveryCapabilityState.NotSupported, "CoreScopeExcluded"),
                new("SupportsContainerDatabase", DatabaseDiscoveryCapabilityState.NotApplicable, "SqlServerNotApplicable"),
                new("SupportsFullDdl", DatabaseDiscoveryCapabilityState.NotSupported, "CoreScopeExcluded"),
            ];
            return new SqlServerCapabilityProbe(target, capabilities);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DatabaseDiscoveryProviderException)
        {
            throw;
        }
        catch (SqlException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (SqlException exception)
        {
            throw SqlServerDiscoveryErrorMapper.Map(exception, connected, cancellationToken);
        }
        catch (TimeoutException)
        {
            throw SqlServerDiscoveryErrorMapper.Timeout();
        }
        catch
        {
            throw Failure(
                connected ? "MetadataQueryFailed" : "ConnectionFailed",
                connected ? "读取 SQL Server 目录元数据失败。" : "无法建立 SQL Server 连接。");
        }
    }

    public async Task<SqlServerCatalogSnapshot> ReadCatalogAsync(
        DatabaseDiscoveryConnectionContext connection,
        DatabaseDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        var connected = false;
        var safeStage = "连接上下文";
        try
        {
            await using var database = SqlServerConnectionFactory.Create(
                connection,
                settings.ConnectionTimeoutSeconds,
                settings.SqlServerTrustServerCertificate);
            await database.OpenAsync(cancellationToken);
            connected = true;

            var target = await ReadTarget(database, cancellationToken);
            SqlServerDiscoveryRules.ValidateTarget(connection, target);
            safeStage = "Schema";
            var schemas = await SqlServerCatalogSql.ReadResolvedSchemas(
                database,
                request.IncludedSchemas,
                settings.CatalogCommandTimeoutSeconds,
                cancellationToken);
            if (schemas.Count != request.IncludedSchemas.Count)
                throw Failure("InsufficientPrivilege", "SQL Server 账号缺少必要的目录元数据权限。");
            if (schemas.Distinct(StringComparer.Ordinal).Count() != schemas.Count)
                throw Failure("UnsupportedIdentifierCollision", "IncludedSchemas 在目标数据库排序规则下存在歧义。");

            safeStage = "对象";
            var objects = await ReadObjects(
                database, schemas, request.Limits.MaximumObjects, cancellationToken);
            safeStage = "字段";
            var columns = await ReadColumns(
                database, schemas, request.Limits.MaximumColumns, cancellationToken);
            safeStage = "约束";
            var constraints = await ReadConstraints(
                database,
                schemas,
                request.Limits.MaximumColumns,
                request.Limits.MaximumConstraintsAndIndexes,
                cancellationToken);
            var constraintCount = constraints
                .Select(item => (item.SchemaName, item.ObjectName, item.Name))
                .Distinct()
                .Count();
            safeStage = "索引";
            var indexParts = await ReadIndexParts(
                database,
                schemas,
                request.Limits.MaximumColumns,
                request.Limits.MaximumConstraintsAndIndexes - constraintCount,
                cancellationToken);
            safeStage = "序列";
            var sequences = await ReadSequences(
                database, schemas, request.Limits.MaximumSequences, cancellationToken);

            return new SqlServerCatalogSnapshot(
                target,
                target.ConnectedPrincipal,
                schemas,
                objects,
                columns,
                constraints,
                indexParts,
                sequences);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DatabaseDiscoveryProviderException exception)
        {
            if (exception.ErrorCode == "MetadataQueryFailed")
                throw Failure(exception.ErrorCode, $"读取 SQL Server {safeStage}目录元数据失败。");
            throw;
        }
        catch (SqlException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (SqlException exception)
        {
            throw SqlServerDiscoveryErrorMapper.Map(exception, connected, cancellationToken);
        }
        catch (TimeoutException)
        {
            throw SqlServerDiscoveryErrorMapper.Timeout();
        }
        catch (OverflowException)
        {
            throw LimitExceeded();
        }
        catch
        {
            throw Failure(
                connected ? "MetadataQueryFailed" : "ConnectionFailed",
                connected ? $"读取 SQL Server {safeStage}目录元数据失败。" : "无法建立 SQL Server 连接。");
        }
    }

    private async Task<SqlServerTargetContext> ReadTarget(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, SqlServerCatalogSql.TargetContext);
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SingleRow | CommandBehavior.SequentialAccess,
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw Failure("ConnectionFailed", "无法建立 SQL Server 连接。");
        return new SqlServerTargetContext(
            RequiredText(reader, 0),
            RequiredInt(reader, 1),
            $"Microsoft.Data.SqlClient/{typeof(SqlConnection).Assembly.GetName().Version}",
            RequiredText(reader, 2),
            RequiredText(reader, 3),
            RequiredText(reader, 4));
    }

    private async Task<List<SqlServerObjectRow>> ReadObjects(
        SqlConnection connection,
        IReadOnlyList<string> schemas,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, SqlServerCatalogSql.Objects, schemas);
        var result = new List<SqlServerObjectRow>();
        await ReadRows(command, cancellationToken, reader =>
        {
            result.Add(new SqlServerObjectRow(
                RequiredText(reader, 0),
                RequiredText(reader, 1),
                RequiredText(reader, 2).TrimEnd() switch
                {
                    "U" => DatabaseDiscoveryObjectType.Table,
                    "V" => DatabaseDiscoveryObjectType.View,
                    _ => throw Failure("MetadataQueryFailed", "读取 SQL Server 目录元数据失败。"),
                },
                NullableText(reader, 3)));
            EnforceCount(result.Count, limit);
        });
        return result;
    }

    private async Task<List<SqlServerColumnRow>> ReadColumns(
        SqlConnection connection,
        IReadOnlyList<string> schemas,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, SqlServerCatalogSql.Columns, schemas);
        var result = new List<SqlServerColumnRow>();
        await ReadRows(command, cancellationToken, reader =>
        {
            result.Add(new SqlServerColumnRow(
                RequiredText(reader, 0),
                RequiredText(reader, 1),
                RequiredText(reader, 2),
                RequiredInt(reader, 3),
                RequiredText(reader, 4),
                RequiredText(reader, 5),
                RequiredBoolean(reader, 6),
                RequiredBoolean(reader, 7),
                RequiredText(reader, 8),
                RequiredInt(reader, 9),
                RequiredInt(reader, 10),
                RequiredInt(reader, 11),
                RequiredBoolean(reader, 12),
                NullableText(reader, 13),
                NullableText(reader, 14)));
            EnforceCount(result.Count, limit);
        });
        return result;
    }

    private async Task<List<SqlServerConstraintColumnRow>> ReadConstraints(
        SqlConnection connection,
        IReadOnlyList<string> schemas,
        int rowLimit,
        int constraintLimit,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, SqlServerCatalogSql.Constraints, schemas);
        var result = new List<SqlServerConstraintColumnRow>();
        var keys = new HashSet<(string SchemaName, string ObjectName, string Name)>();
        await ReadRows(command, cancellationToken, reader =>
        {
            var row = new SqlServerConstraintColumnRow(
                RequiredText(reader, 0),
                RequiredText(reader, 1),
                RequiredText(reader, 2),
                RequiredText(reader, 3),
                RequiredInt(reader, 4),
                RequiredText(reader, 5),
                NullableText(reader, 6),
                NullableText(reader, 7),
                NullableText(reader, 8),
                NullableText(reader, 9),
                NullableText(reader, 10));
            result.Add(row);
            keys.Add((row.SchemaName, row.ObjectName, row.Name));
            EnforceCount(result.Count, rowLimit);
            EnforceCount(keys.Count, constraintLimit);
        });
        return result;
    }

    private async Task<List<SqlServerIndexPartRow>> ReadIndexParts(
        SqlConnection connection,
        IReadOnlyList<string> schemas,
        int rowLimit,
        int indexLimit,
        CancellationToken cancellationToken)
    {
        if (indexLimit < 0) throw LimitExceeded();
        await using var command = ScopedCommand(connection, SqlServerCatalogSql.IndexParts, schemas);
        var result = new List<SqlServerIndexPartRow>();
        var keys = new HashSet<(string SchemaName, string ObjectName, string Name)>();
        await ReadRows(command, cancellationToken, reader =>
        {
            var row = new SqlServerIndexPartRow(
                RequiredText(reader, 0),
                RequiredText(reader, 1),
                RequiredText(reader, 2),
                RequiredInt(reader, 3),
                RequiredText(reader, 4),
                RequiredBoolean(reader, 5),
                RequiredInt(reader, 6),
                RequiredInt(reader, 7),
                RequiredBoolean(reader, 8),
                RequiredInt(reader, 9),
                RequiredBoolean(reader, 10),
                RequiredText(reader, 11),
                NullableText(reader, 12),
                NullableText(reader, 13),
                RequiredBoolean(reader, 14));
            result.Add(row);
            keys.Add((row.SchemaName, row.ObjectName, row.Name));
            EnforceCount(result.Count, rowLimit);
            EnforceCount(keys.Count, indexLimit);
        });
        return result;
    }

    private async Task<List<SqlServerSequenceRow>> ReadSequences(
        SqlConnection connection,
        IReadOnlyList<string> schemas,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, SqlServerCatalogSql.Sequences, schemas);
        var result = new List<SqlServerSequenceRow>();
        await ReadRows(command, cancellationToken, reader =>
        {
            result.Add(new SqlServerSequenceRow(
                RequiredText(reader, 0),
                RequiredText(reader, 1),
                RequiredText(reader, 2),
                RequiredText(reader, 3),
                RequiredBoolean(reader, 4),
                RequiredBoolean(reader, 5),
                RequiredText(reader, 6),
                RequiredInt(reader, 7),
                RequiredInt(reader, 8),
                RequiredText(reader, 9),
                RequiredText(reader, 10),
                RequiredText(reader, 11),
                RequiredText(reader, 12),
                NullableInt(reader, 13),
                RequiredBoolean(reader, 14)));
            EnforceCount(result.Count, limit);
        });
        return result;
    }

    private static async Task ReadRows(
        SqlCommand command,
        CancellationToken cancellationToken,
        Action<DbDataReader> map)
    {
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) map(reader);
    }

    private SqlCommand ScopedCommand(
        SqlConnection connection,
        string sql,
        IReadOnlyList<string> schemas) =>
        SqlServerCatalogSql.CreateScopedCommand(
            connection, sql, schemas, settings.CatalogCommandTimeoutSeconds);

    private SqlCommand Command(SqlConnection connection, string sql) => new(sql, connection)
    {
        CommandTimeout = settings.CatalogCommandTimeoutSeconds,
    };

    private static string RequiredText(DbDataReader reader, int ordinal) =>
        NullableText(reader, ordinal)
        ?? throw Failure("MetadataQueryFailed", "读取 SQL Server 目录元数据失败。");

    private static string? NullableText(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static int RequiredInt(DbDataReader reader, int ordinal) =>
        NullableInt(reader, ordinal)
        ?? throw Failure("MetadataQueryFailed", "读取 SQL Server 目录元数据失败。");

    private static int? NullableInt(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : checked(Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture));

    private static bool RequiredBoolean(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? throw Failure("MetadataQueryFailed", "读取 SQL Server 目录元数据失败。")
            : Convert.ToBoolean(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static void EnforceCount(int count, int limit)
    {
        if (count > limit) throw LimitExceeded();
    }

    private static DatabaseDiscoveryProviderException Failure(string code, string summary) =>
        new(code, summary);

    private static DatabaseDiscoveryProviderException LimitExceeded() =>
        Failure("LimitExceeded", "发现结果超过配置的安全限制。");

    private static DatabaseDiscoveryOptions Validate(DatabaseDiscoveryOptions value)
    {
        value.Validate();
        return value;
    }
}

internal interface ISqlServerDiscoveryCatalogReader
{
    Task<SqlServerCapabilityProbe> ReadCapabilitiesAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken);

    Task<SqlServerCatalogSnapshot> ReadCatalogAsync(
        DatabaseDiscoveryConnectionContext connection,
        DatabaseDiscoveryRequest request,
        CancellationToken cancellationToken);
}

internal sealed record SqlServerTargetContext(
    string ServerVersion,
    int ServerMajorVersion,
    string ProviderVersion,
    string DatabaseName,
    string DatabaseCollation,
    string ConnectedPrincipal);

internal sealed record SqlServerCapabilityProbe(
    SqlServerTargetContext Target,
    IReadOnlyList<CanonicalCapability> Capabilities);

internal sealed record SqlServerCatalogSnapshot(
    SqlServerTargetContext Target,
    string ConnectedPrincipal,
    IReadOnlyList<string> VisibleSchemas,
    IReadOnlyList<SqlServerObjectRow> Objects,
    IReadOnlyList<SqlServerColumnRow> Columns,
    IReadOnlyList<SqlServerConstraintColumnRow> Constraints,
    IReadOnlyList<SqlServerIndexPartRow> IndexParts,
    IReadOnlyList<SqlServerSequenceRow> Sequences);

internal sealed record SqlServerObjectRow(
    string SchemaName,
    string Name,
    DatabaseDiscoveryObjectType ObjectType,
    string? Comment);

internal sealed record SqlServerColumnRow(
    string SchemaName,
    string ObjectName,
    string Name,
    int SourceOrdinal,
    string TypeName,
    string TypeNamespace,
    bool IsUserDefined,
    bool IsAssemblyType,
    string BaseTypeName,
    int MaximumLength,
    int NumericPrecision,
    int NumericScale,
    bool IsNullable,
    string? DefaultExpression,
    string? Comment);

internal sealed record SqlServerConstraintColumnRow(
    string SchemaName,
    string ObjectName,
    string Name,
    string ConstraintType,
    int Position,
    string ColumnName,
    string? ReferencedSchemaName,
    string? ReferencedObjectName,
    string? ReferencedColumnName,
    string? UpdateAction,
    string? DeleteAction);

internal sealed record SqlServerIndexPartRow(
    string SchemaName,
    string ObjectName,
    string Name,
    int IndexType,
    string IndexTypeDescription,
    bool IsUnique,
    int Position,
    int KeyOrdinal,
    bool IsIncluded,
    int PartitionOrdinal,
    bool IsDescending,
    string ColumnName,
    string? NativePredicate,
    string? BackingConstraintName,
    bool IsHypothetical);

internal sealed record SqlServerSequenceRow(
    string SchemaName,
    string Name,
    string TypeName,
    string TypeNamespace,
    bool IsUserDefined,
    bool IsAssemblyType,
    string BaseTypeName,
    int NumericPrecision,
    int NumericScale,
    string StartValue,
    string IncrementValue,
    string MinimumValue,
    string MaximumValue,
    int? CacheSize,
    bool IsCyclic);

internal static class SqlServerDiscoveryErrorMapper
{
    public static DatabaseDiscoveryProviderException Map(
        SqlException exception,
        bool connected,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return new DatabaseDiscoveryProviderException(
                "Cancelled",
                "SQL Server 目录读取已取消。",
                AllowlistedVendorCode(exception.Number));
        var code = MapCode(exception.Number, connected);
        return new DatabaseDiscoveryProviderException(
            code,
            Summary(code),
            AllowlistedVendorCode(exception.Number));
    }

    public static DatabaseConnectionFailure MapConnectionFailure(int number, bool connected) =>
        MapCode(number, connected) switch
        {
            "AuthenticationFailed" => DatabaseConnectionFailure.AuthenticationFailed,
            "InsufficientPrivilege" => DatabaseConnectionFailure.InsufficientPrivilege,
            "Timeout" => DatabaseConnectionFailure.Timeout,
            "Cancelled" => DatabaseConnectionFailure.Cancelled,
            _ => DatabaseConnectionFailure.ConnectionFailed,
        };

    public static string MapCode(int number, bool connected) => number switch
    {
        18456 => "AuthenticationFailed",
        229 or 230 or 262 or 297 => "InsufficientPrivilege",
        -2 or 1222 => "Timeout",
        4060 or 53 or 64 or 233 or 258 or 10053 or 10054 or 10060 or 11001 => "ConnectionFailed",
        _ => connected ? "MetadataQueryFailed" : "ConnectionFailed",
    };

    public static string? AllowlistedVendorCode(int number) =>
        number is >= 1 and <= 999999
            ? $"MSSQL-{number.ToString(CultureInfo.InvariantCulture)}"
            : null;

    public static DatabaseDiscoveryProviderException Timeout() =>
        new("Timeout", Summary("Timeout"));

    public static string ConnectionSummary(DatabaseConnectionFailure failure) => failure switch
    {
        DatabaseConnectionFailure.AuthenticationFailed => "SQL Server 用户名或密码验证失败。",
        DatabaseConnectionFailure.InsufficientPrivilege => "SQL Server 账号缺少必要的基础目录可见性。",
        DatabaseConnectionFailure.Timeout => "SQL Server 连接测试超时。",
        DatabaseConnectionFailure.Cancelled => "SQL Server 连接测试已取消。",
        _ => "无法建立 SQL Server 连接。",
    };

    private static string Summary(string code) => code switch
    {
        "AuthenticationFailed" => "SQL Server 用户名或密码验证失败。",
        "InsufficientPrivilege" => "SQL Server 账号缺少必要的目录元数据权限。",
        "Timeout" => "SQL Server 目录读取超时。",
        "ConnectionFailed" => "无法建立 SQL Server 连接。",
        "UnsupportedIndexFamily" => "发现了当前 Core 无法完整表达的 SQL Server 索引类型。",
        _ => "读取 SQL Server 目录元数据失败。",
    };
}

internal static class SqlServerCatalogSql
{
    private const string SchemaFilter = "/*SCHEMA_FILTER*/";

    public const string TargetContext = """
        SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')),
               CONVERT(int, SERVERPROPERTY('ProductMajorVersion')),
               CONVERT(nvarchar(128), DB_NAME()),
               CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation')),
               CONVERT(nvarchar(128), CURRENT_USER)
        """;

    public const string Schemas = """
        SELECT schema_row.name
        FROM (VALUES /*SCHEMA_FILTER*/) AS requested(name)
        JOIN sys.schemas AS schema_row ON schema_row.name = requested.name
        WHERE HAS_PERMS_BY_NAME(schema_row.name, 'SCHEMA', 'VIEW DEFINITION') = 1
        ORDER BY schema_row.name
        """;

    public const string Objects = """
        SELECT schema_row.name,
               object_row.name,
               object_row.type,
               CONVERT(nvarchar(max), description.value)
        FROM sys.objects AS object_row
        JOIN sys.schemas AS schema_row ON schema_row.schema_id = object_row.schema_id
        LEFT JOIN sys.extended_properties AS description
          ON description.class = 1
         AND description.major_id = object_row.object_id
         AND description.minor_id = 0
         AND description.name = N'MS_Description'
        WHERE schema_row.name IN (/*SCHEMA_FILTER*/)
          AND object_row.type IN ('U', 'V')
          AND object_row.is_ms_shipped = 0
        ORDER BY schema_row.name, object_row.name, object_row.type
        """;

    public const string Columns = """
        SELECT schema_row.name,
               object_row.name,
               column_row.name,
               column_row.column_id,
               declared_type.name,
               type_schema.name,
               declared_type.is_user_defined,
               declared_type.is_assembly_type,
               base_type.name,
               column_row.max_length,
               column_row.precision,
               column_row.scale,
               column_row.is_nullable,
               default_row.definition,
               CONVERT(nvarchar(max), description.value)
        FROM sys.objects AS object_row
        JOIN sys.schemas AS schema_row ON schema_row.schema_id = object_row.schema_id
        JOIN sys.columns AS column_row ON column_row.object_id = object_row.object_id
        JOIN sys.types AS declared_type ON declared_type.user_type_id = column_row.user_type_id
        JOIN sys.schemas AS type_schema ON type_schema.schema_id = declared_type.schema_id
        JOIN sys.types AS base_type
          ON base_type.user_type_id = column_row.system_type_id
         AND base_type.user_type_id = base_type.system_type_id
        LEFT JOIN sys.default_constraints AS default_row
          ON default_row.parent_object_id = object_row.object_id
         AND default_row.parent_column_id = column_row.column_id
        LEFT JOIN sys.extended_properties AS description
          ON description.class = 1
         AND description.major_id = object_row.object_id
         AND description.minor_id = column_row.column_id
         AND description.name = N'MS_Description'
        WHERE schema_row.name IN (/*SCHEMA_FILTER*/)
          AND object_row.type IN ('U', 'V')
          AND object_row.is_ms_shipped = 0
        ORDER BY schema_row.name, object_row.name, column_row.column_id
        """;

    public const string Constraints = """
        SELECT schema_row.name,
               object_row.name,
               key_row.name,
               key_row.type,
               index_column.key_ordinal,
               column_row.name,
               CONVERT(nvarchar(128), NULL),
               CONVERT(nvarchar(128), NULL),
               CONVERT(nvarchar(128), NULL),
               CONVERT(nvarchar(60), NULL),
               CONVERT(nvarchar(60), NULL)
        FROM sys.key_constraints AS key_row
        JOIN sys.objects AS object_row ON object_row.object_id = key_row.parent_object_id
        JOIN sys.schemas AS schema_row ON schema_row.schema_id = object_row.schema_id
        JOIN sys.index_columns AS index_column
          ON index_column.object_id = key_row.parent_object_id
         AND index_column.index_id = key_row.unique_index_id
         AND index_column.key_ordinal > 0
        JOIN sys.columns AS column_row
          ON column_row.object_id = index_column.object_id
         AND column_row.column_id = index_column.column_id
        WHERE schema_row.name IN (/*SCHEMA_FILTER*/)
          AND object_row.is_ms_shipped = 0
          AND key_row.type IN ('PK', 'UQ')
        UNION ALL
        SELECT schema_row.name,
               object_row.name,
               foreign_key.name,
               CONVERT(char(2), 'FK'),
               foreign_column.constraint_column_id,
               column_row.name,
               referenced_schema.name,
               referenced_object.name,
               referenced_column.name,
               foreign_key.update_referential_action_desc,
               foreign_key.delete_referential_action_desc
        FROM sys.foreign_keys AS foreign_key
        JOIN sys.objects AS object_row ON object_row.object_id = foreign_key.parent_object_id
        JOIN sys.schemas AS schema_row ON schema_row.schema_id = object_row.schema_id
        JOIN sys.foreign_key_columns AS foreign_column
          ON foreign_column.constraint_object_id = foreign_key.object_id
        JOIN sys.columns AS column_row
          ON column_row.object_id = foreign_column.parent_object_id
         AND column_row.column_id = foreign_column.parent_column_id
        JOIN sys.objects AS referenced_object
          ON referenced_object.object_id = foreign_column.referenced_object_id
        JOIN sys.schemas AS referenced_schema
          ON referenced_schema.schema_id = referenced_object.schema_id
        JOIN sys.columns AS referenced_column
          ON referenced_column.object_id = foreign_column.referenced_object_id
         AND referenced_column.column_id = foreign_column.referenced_column_id
        WHERE schema_row.name IN (/*SCHEMA_FILTER*/)
          AND object_row.is_ms_shipped = 0
        ORDER BY 1, 2, 3, 5
        """;

    public const string IndexParts = """
        SELECT schema_row.name,
               object_row.name,
               index_row.name,
               index_row.type,
               index_row.type_desc,
               index_row.is_unique,
               index_column.index_column_id,
               index_column.key_ordinal,
               index_column.is_included_column,
               index_column.partition_ordinal,
               index_column.is_descending_key,
               column_row.name,
               index_row.filter_definition,
               backing_constraint.name,
               index_row.is_hypothetical
        FROM sys.indexes AS index_row
        JOIN sys.objects AS object_row ON object_row.object_id = index_row.object_id
        JOIN sys.schemas AS schema_row ON schema_row.schema_id = object_row.schema_id
        JOIN sys.index_columns AS index_column
          ON index_column.object_id = index_row.object_id
         AND index_column.index_id = index_row.index_id
        JOIN sys.columns AS column_row
          ON column_row.object_id = index_column.object_id
         AND column_row.column_id = index_column.column_id
        LEFT JOIN sys.key_constraints AS backing_constraint
          ON backing_constraint.parent_object_id = index_row.object_id
         AND backing_constraint.unique_index_id = index_row.index_id
         AND backing_constraint.type IN ('PK', 'UQ')
        WHERE schema_row.name IN (/*SCHEMA_FILTER*/)
          AND object_row.type IN ('U', 'V')
          AND object_row.is_ms_shipped = 0
          AND index_row.index_id > 0
        ORDER BY schema_row.name, object_row.name, index_row.name, index_column.index_column_id
        """;

    public const string Sequences = """
        SELECT schema_row.name,
               sequence_row.name,
               declared_type.name,
               type_schema.name,
               declared_type.is_user_defined,
               declared_type.is_assembly_type,
               base_type.name,
               sequence_row.precision,
               sequence_row.scale,
               CONVERT(nvarchar(128), sequence_row.start_value),
               CONVERT(nvarchar(128), sequence_row.increment),
               CONVERT(nvarchar(128), sequence_row.minimum_value),
               CONVERT(nvarchar(128), sequence_row.maximum_value),
               CASE WHEN sequence_row.is_cached = 1 THEN sequence_row.cache_size ELSE NULL END,
               sequence_row.is_cycling
        FROM sys.sequences AS sequence_row
        JOIN sys.schemas AS schema_row ON schema_row.schema_id = sequence_row.schema_id
        JOIN sys.types AS declared_type ON declared_type.user_type_id = sequence_row.user_type_id
        JOIN sys.schemas AS type_schema ON type_schema.schema_id = declared_type.schema_id
        JOIN sys.types AS base_type
          ON base_type.user_type_id = sequence_row.system_type_id
         AND base_type.user_type_id = base_type.system_type_id
        WHERE schema_row.name IN (/*SCHEMA_FILTER*/)
          AND sequence_row.is_ms_shipped = 0
        ORDER BY schema_row.name, sequence_row.name
        """;

    public static IReadOnlyList<string> RequiredCatalogProbes { get; } =
    [
        "SELECT TOP (0) name FROM sys.schemas",
        "SELECT TOP (0) name FROM sys.tables",
        "SELECT TOP (0) name FROM sys.views",
        "SELECT TOP (0) name FROM sys.columns",
        "SELECT TOP (0) name FROM sys.types",
        "SELECT TOP (0) name FROM sys.default_constraints",
        "SELECT TOP (0) name FROM sys.key_constraints",
        "SELECT TOP (0) name FROM sys.foreign_keys",
        "SELECT TOP (0) constraint_object_id FROM sys.foreign_key_columns",
        "SELECT TOP (0) name FROM sys.indexes",
        "SELECT TOP (0) object_id FROM sys.index_columns",
        "SELECT TOP (0) name FROM sys.sequences",
        "SELECT TOP (0) name FROM sys.extended_properties",
    ];

    public static IReadOnlyList<string> ReviewedQueryInventory { get; } =
    [TargetContext, Schemas, Objects, Columns, Constraints, IndexParts, Sequences, .. RequiredCatalogProbes];

    public static async Task<List<string>> ReadResolvedSchemas(
        SqlConnection connection,
        IReadOnlyList<string> schemas,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = CreateScopedCommand(
            connection, Schemas, schemas, commandTimeoutSeconds, valuesClause: true);
        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture)
                ?? throw new DatabaseDiscoveryProviderException(
                    "MetadataQueryFailed", "读取 SQL Server 目录元数据失败。"));
        }
        return result;
    }

    public static SqlCommand CreateScopedCommand(
        SqlConnection connection,
        string sql,
        IReadOnlyList<string> schemas,
        int commandTimeoutSeconds,
        bool valuesClause = false)
    {
        if (schemas.Count is < 1 or > 1024)
            throw new DatabaseDiscoveryProviderException(
                "MetadataQueryFailed", "读取 SQL Server 目录元数据失败。");
        var placeholders = Enumerable.Range(0, schemas.Count)
            .Select(index => $"@schema{index.ToString(CultureInfo.InvariantCulture)}")
            .ToArray();
        var replacement = valuesClause
            ? string.Join(", ", placeholders.Select(value => $"({value})"))
            : string.Join(", ", placeholders);
        var command = new SqlCommand(
            sql.Replace(SchemaFilter, replacement, StringComparison.Ordinal),
            connection)
        {
            CommandTimeout = commandTimeoutSeconds,
        };
        for (var index = 0; index < schemas.Count; index++)
        {
            command.Parameters.Add(
                new SqlParameter(placeholders[index], SqlDbType.NVarChar, 128)
                {
                    Value = schemas[index],
                });
        }
        return command;
    }
}
