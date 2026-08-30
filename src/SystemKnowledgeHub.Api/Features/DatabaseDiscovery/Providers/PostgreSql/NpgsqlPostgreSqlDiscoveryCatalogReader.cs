using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Providers.PostgreSql;

internal sealed class NpgsqlPostgreSqlDiscoveryCatalogReader(
    IOptions<DatabaseDiscoveryOptions> options) : IPostgreSqlDiscoveryCatalogReader
{
    private const int SupportedServerMajorVersion = 18;
    private readonly DatabaseDiscoveryOptions settings = Validate(options.Value);

    internal int ConfiguredConnectionTimeoutSeconds => settings.ConnectionTimeoutSeconds;
    internal int ConfiguredCatalogCommandTimeoutSeconds => settings.CatalogCommandTimeoutSeconds;

    public async Task<PostgreSqlCapabilityProbe> ReadCapabilitiesAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken)
    {
        var connected = false;
        try
        {
            await using var database = CreateConnection(connection);
            await database.OpenAsync(cancellationToken);
            connected = true;
            var target = await ReadTarget(database, cancellationToken);
            ValidateTarget(connection, target);

            IReadOnlyList<CanonicalCapability> capabilities =
            [
                new("SupportsIdentityColumns", DatabaseDiscoveryCapabilityState.Supported, null),
                new("SupportsInvisibleColumns", DatabaseDiscoveryCapabilityState.NotSupported, "PostgreSql18NotSupported"),
                new("SupportsMaterializedViews", DatabaseDiscoveryCapabilityState.Supported, null),
                new("SupportsPartitions", DatabaseDiscoveryCapabilityState.Supported, null),
                new("SupportsSequences", DatabaseDiscoveryCapabilityState.Supported, null),
                new("SupportsSynonyms", DatabaseDiscoveryCapabilityState.NotApplicable, "PostgreSqlNotApplicable"),
                new("SupportsTriggers", DatabaseDiscoveryCapabilityState.Supported, null),
                new("SupportsContainerDatabase", DatabaseDiscoveryCapabilityState.NotApplicable, "PostgreSqlNotApplicable"),
                new("SupportsFullDdl", DatabaseDiscoveryCapabilityState.NotSupported, "CoreScopeExcluded"),
            ];
            return new PostgreSqlCapabilityProbe(target, capabilities);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DatabaseDiscoveryProviderException)
        {
            throw;
        }
        catch (PostgresException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (PostgresException exception)
        {
            throw PostgreSqlDiscoveryErrorMapper.Map(exception, connected, cancellationToken);
        }
        catch (NpgsqlException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (NpgsqlException exception)
        {
            throw PostgreSqlDiscoveryErrorMapper.Map(exception, connected);
        }
        catch (TimeoutException)
        {
            throw PostgreSqlDiscoveryErrorMapper.Timeout();
        }
        catch
        {
            throw Failure(
                connected ? "MetadataQueryFailed" : "ConnectionFailed",
                connected ? "读取 PostgreSQL 目录元数据失败。" : "无法建立 PostgreSQL 连接。");
        }
    }

    public async Task<PostgreSqlCatalogSnapshot> ReadCatalogAsync(
        DatabaseDiscoveryConnectionContext connection,
        DatabaseDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        var connected = false;
        try
        {
            await using var database = CreateConnection(connection);
            await database.OpenAsync(cancellationToken);
            connected = true;
            await using var transaction = await database.BeginTransactionAsync(
                IsolationLevel.RepeatableRead,
                cancellationToken);

            var target = await ReadTarget(database, cancellationToken);
            ValidateTarget(connection, target);
            var schemas = await ReadSchemas(database, request.IncludedSchemas, cancellationToken);
            if (!schemas.ToHashSet(StringComparer.Ordinal).SetEquals(request.IncludedSchemas)
                || schemas.Count != request.IncludedSchemas.Count)
            {
                throw Failure(
                    "InsufficientPrivilege",
                    "PostgreSQL 账号缺少必要的目录元数据权限。");
            }

            var objects = await ReadObjects(
                database,
                request.IncludedSchemas,
                request.Limits.MaximumObjects,
                cancellationToken);
            var columns = await ReadColumns(
                database,
                request.IncludedSchemas,
                request.Limits.MaximumColumns,
                cancellationToken);
            var constraints = await ReadConstraints(
                database,
                request.IncludedSchemas,
                request.Limits.MaximumColumns,
                request.Limits.MaximumConstraintsAndIndexes,
                cancellationToken);
            var constraintCount = constraints
                .Select(item => (item.SchemaName, item.ObjectName, item.Name))
                .Distinct()
                .Count();
            var indexParts = await ReadIndexParts(
                database,
                request.IncludedSchemas,
                request.Limits.MaximumColumns,
                request.Limits.MaximumConstraintsAndIndexes - constraintCount,
                cancellationToken);
            var sequences = await ReadSequences(
                database,
                request.IncludedSchemas,
                request.Limits.MaximumSequences,
                cancellationToken);
            var principal = await ReadConnectedPrincipal(database, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new PostgreSqlCatalogSnapshot(
                target,
                principal,
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
        catch (DatabaseDiscoveryProviderException)
        {
            throw;
        }
        catch (PostgresException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (PostgresException exception)
        {
            throw PostgreSqlDiscoveryErrorMapper.Map(exception, connected, cancellationToken);
        }
        catch (NpgsqlException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (NpgsqlException exception)
        {
            throw PostgreSqlDiscoveryErrorMapper.Map(exception, connected);
        }
        catch (TimeoutException)
        {
            throw PostgreSqlDiscoveryErrorMapper.Timeout();
        }
        catch (OverflowException)
        {
            throw LimitExceeded();
        }
        catch
        {
            throw Failure(
                connected ? "MetadataQueryFailed" : "ConnectionFailed",
                connected ? "读取 PostgreSQL 目录元数据失败。" : "无法建立 PostgreSQL 连接。");
        }
    }

    private NpgsqlConnection CreateConnection(DatabaseDiscoveryConnectionContext connection)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = connection.Host,
            Port = connection.Port,
            Database = connection.DatabaseName,
            Username = connection.Username,
            Password = connection.Password,
            Pooling = false,
            Enlist = false,
            Timeout = settings.ConnectionTimeoutSeconds,
            CommandTimeout = settings.CatalogCommandTimeoutSeconds,
            IncludeErrorDetail = false,
            SearchPath = "pg_catalog",
            ApplicationName = "SystemKnowledgeHub.DatabaseDiscovery",
        };
        return new NpgsqlConnection(builder.ConnectionString);
    }

    private async Task<PostgreSqlTargetContext> ReadTarget(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, PostgreSqlCatalogSql.TargetContext);
        var databaseName = Convert.ToString(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
        if (string.IsNullOrEmpty(databaseName))
            throw Failure("ConnectionFailed", "无法建立 PostgreSQL 连接。");
        return new PostgreSqlTargetContext(
            connection.ServerVersion,
            connection.PostgreSqlVersion.Major,
            $"Npgsql/{typeof(NpgsqlConnection).Assembly.GetName().Version}",
            databaseName);
    }

    private async Task<string> ReadConnectedPrincipal(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, PostgreSqlCatalogSql.ConnectedPrincipal);
        return Convert.ToString(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture)
            ?? throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。");
    }

    private async Task<List<string>> ReadSchemas(
        NpgsqlConnection connection,
        IReadOnlyList<string> schemas,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, PostgreSqlCatalogSql.Schemas, schemas);
        var result = new List<string>();
        await ReadRows(command, cancellationToken, reader => result.Add(RequiredText(reader, 0)));
        return result;
    }

    private async Task<List<PostgreSqlObjectRow>> ReadObjects(
        NpgsqlConnection connection,
        IReadOnlyList<string> schemas,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, PostgreSqlCatalogSql.Objects, schemas);
        var result = new List<PostgreSqlObjectRow>();
        await ReadRows(command, cancellationToken, reader =>
        {
            var schemaName = RequiredText(reader, 0);
            var objectName = RequiredText(reader, 1);
            var objectType = RequiredText(reader, 2) switch
            {
                "r" or "p" => DatabaseDiscoveryObjectType.Table,
                "v" => DatabaseDiscoveryObjectType.View,
                _ => throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。"),
            };
            var comment = NullableText(reader, 3);
            result.Add(new(
                schemaName,
                objectName,
                objectType,
                comment));
            EnforceCount(result.Count, limit);
        });
        return result;
    }

    private async Task<List<PostgreSqlColumnRow>> ReadColumns(
        NpgsqlConnection connection,
        IReadOnlyList<string> schemas,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, PostgreSqlCatalogSql.Columns, schemas);
        var result = new List<PostgreSqlColumnRow>();
        await ReadRows(command, cancellationToken, reader =>
        {
            result.Add(new(
                RequiredText(reader, 0),
                RequiredText(reader, 1),
                RequiredText(reader, 2),
                RequiredInt(reader, 3),
                RequiredText(reader, 4),
                RequiredText(reader, 5),
                RequiredText(reader, 6),
                RequiredBoolean(reader, 7),
                NullableText(reader, 8),
                NullableText(reader, 9),
                RequiredText(reader, 10),
                RequiredText(reader, 11),
                NullableLong(reader, 12),
                RequiredBoolean(reader, 13),
                NullableInt(reader, 14),
                NullableInt(reader, 15)));
            EnforceCount(result.Count, limit);
        });
        return result;
    }

    private async Task<List<PostgreSqlConstraintColumnRow>> ReadConstraints(
        NpgsqlConnection connection,
        IReadOnlyList<string> schemas,
        int rowLimit,
        int constraintLimit,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, PostgreSqlCatalogSql.Constraints, schemas);
        var result = new List<PostgreSqlConstraintColumnRow>();
        var constraintKeys = new HashSet<(string SchemaName, string ObjectName, string Name)>();
        await ReadRows(command, cancellationToken, reader =>
        {
            var row = new PostgreSqlConstraintColumnRow(
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
            constraintKeys.Add((row.SchemaName, row.ObjectName, row.Name));
            EnforceCount(result.Count, rowLimit);
            EnforceCount(constraintKeys.Count, constraintLimit);
        });
        return result;
    }

    private async Task<List<PostgreSqlIndexPartRow>> ReadIndexParts(
        NpgsqlConnection connection,
        IReadOnlyList<string> schemas,
        int rowLimit,
        int indexLimit,
        CancellationToken cancellationToken)
    {
        if (indexLimit < 0) throw LimitExceeded();
        await using var command = ScopedCommand(connection, PostgreSqlCatalogSql.IndexParts, schemas);
        var result = new List<PostgreSqlIndexPartRow>();
        var indexKeys = new HashSet<(string SchemaName, string ObjectName, string Name)>();
        await ReadRows(command, cancellationToken, reader =>
        {
            var row = new PostgreSqlIndexPartRow(
                RequiredText(reader, 0),
                RequiredText(reader, 1),
                RequiredText(reader, 2),
                RequiredText(reader, 3),
                RequiredBoolean(reader, 4),
                RequiredInt(reader, 5),
                RequiredInt(reader, 6),
                NullableText(reader, 7),
                NullableText(reader, 8),
                RequiredBoolean(reader, 9),
                NullableText(reader, 10),
                NullableText(reader, 11),
                RequiredBoolean(reader, 12),
                RequiredBoolean(reader, 13));
            result.Add(row);
            indexKeys.Add((row.SchemaName, row.ObjectName, row.Name));
            EnforceCount(result.Count, rowLimit);
            EnforceCount(indexKeys.Count, indexLimit);
        });
        return result;
    }

    private async Task<List<PostgreSqlSequenceRow>> ReadSequences(
        NpgsqlConnection connection,
        IReadOnlyList<string> schemas,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, PostgreSqlCatalogSql.Sequences, schemas);
        var result = new List<PostgreSqlSequenceRow>();
        await ReadRows(command, cancellationToken, reader =>
        {
            result.Add(new(
                RequiredText(reader, 0),
                RequiredText(reader, 1),
                RequiredText(reader, 2),
                RequiredText(reader, 3),
                RequiredText(reader, 4),
                RequiredText(reader, 5),
                RequiredText(reader, 6),
                RequiredText(reader, 7),
                RequiredText(reader, 8),
                RequiredLong(reader, 9),
                RequiredBoolean(reader, 10)));
            EnforceCount(result.Count, limit);
        });
        return result;
    }

    private static async Task ReadRows(
        NpgsqlCommand command,
        CancellationToken cancellationToken,
        Action<DbDataReader> map)
    {
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) map(reader);
    }

    private NpgsqlCommand ScopedCommand(
        NpgsqlConnection connection,
        string sql,
        IReadOnlyList<string> schemas)
    {
        var command = Command(connection, sql);
        command.Parameters.AddWithValue(
            "schemas",
            NpgsqlDbType.Array | NpgsqlDbType.Text,
            schemas.ToArray());
        return command;
    }

    private NpgsqlCommand Command(NpgsqlConnection connection, string sql) => new(sql, connection)
    {
        CommandTimeout = settings.CatalogCommandTimeoutSeconds,
    };

    private static string RequiredText(DbDataReader reader, int ordinal) =>
        NullableText(reader, ordinal)
        ?? throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。");

    private static string? NullableText(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static int RequiredInt(DbDataReader reader, int ordinal) =>
        NullableInt(reader, ordinal)
        ?? throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。");

    private static int? NullableInt(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : checked(Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture));

    private static long RequiredLong(DbDataReader reader, int ordinal) =>
        NullableLong(reader, ordinal)
        ?? throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。");

    private static long? NullableLong(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : checked(Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture));

    private static bool RequiredBoolean(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。")
            : reader.GetBoolean(ordinal);

    private static void EnforceCount(int count, int limit)
    {
        if (count > limit) throw LimitExceeded();
    }

    private static void ValidateTarget(
        DatabaseDiscoveryConnectionContext connection,
        PostgreSqlTargetContext target)
    {
        if (target.ServerMajorVersion != SupportedServerMajorVersion)
            throw Failure("UnsupportedDatabaseVersion", "仅支持 PostgreSQL 18。");
        if (connection.DatabaseName is null
            || !string.Equals(connection.DatabaseName, target.DatabaseName, StringComparison.Ordinal))
            throw Failure("ConnectionFailed", "连接到的 PostgreSQL 数据库与配置目标不一致。");
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

internal interface IPostgreSqlDiscoveryCatalogReader
{
    Task<PostgreSqlCapabilityProbe> ReadCapabilitiesAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken);

    Task<PostgreSqlCatalogSnapshot> ReadCatalogAsync(
        DatabaseDiscoveryConnectionContext connection,
        DatabaseDiscoveryRequest request,
        CancellationToken cancellationToken);
}

internal sealed record PostgreSqlTargetContext(
    string ServerVersion,
    int ServerMajorVersion,
    string ProviderVersion,
    string DatabaseName);

internal sealed record PostgreSqlCapabilityProbe(
    PostgreSqlTargetContext Target,
    IReadOnlyList<CanonicalCapability> Capabilities);

internal sealed record PostgreSqlCatalogSnapshot(
    PostgreSqlTargetContext Target,
    string ConnectedPrincipal,
    IReadOnlyList<string> VisibleSchemas,
    IReadOnlyList<PostgreSqlObjectRow> Objects,
    IReadOnlyList<PostgreSqlColumnRow> Columns,
    IReadOnlyList<PostgreSqlConstraintColumnRow> Constraints,
    IReadOnlyList<PostgreSqlIndexPartRow> IndexParts,
    IReadOnlyList<PostgreSqlSequenceRow> Sequences);

internal sealed record PostgreSqlObjectRow(
    string SchemaName,
    string Name,
    DatabaseDiscoveryObjectType ObjectType,
    string? Comment);

internal sealed record PostgreSqlColumnRow(
    string SchemaName,
    string ObjectName,
    string Name,
    int SourceOrdinal,
    string TypeName,
    string TypeNamespace,
    string Declaration,
    bool IsNullable,
    string? DefaultExpression,
    string? Comment,
    string BaseTypeName,
    string BaseTypeNamespace,
    long? CharacterLength,
    bool IsUnboundedLength,
    int? NumericPrecision,
    int? NumericScale);

internal sealed record PostgreSqlConstraintColumnRow(
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

internal sealed record PostgreSqlIndexPartRow(
    string SchemaName,
    string ObjectName,
    string Name,
    string IndexKind,
    bool IsUnique,
    int KeyPartCount,
    int Position,
    string? ColumnName,
    string? NativeExpression,
    bool IsDescending,
    string? NativePredicate,
    string? BackingConstraintName,
    bool IsValid,
    bool IsReady);

internal sealed record PostgreSqlSequenceRow(
    string SchemaName,
    string Name,
    string TypeName,
    string TypeNamespace,
    string Declaration,
    string StartValue,
    string IncrementValue,
    string MinimumValue,
    string MaximumValue,
    long CacheSize,
    bool IsCyclic);

internal static class PostgreSqlDiscoveryErrorMapper
{
    public static DatabaseDiscoveryProviderException Map(
        PostgresException exception,
        bool connected,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return new DatabaseDiscoveryProviderException(
                "Cancelled",
                "PostgreSQL 目录读取已取消。",
                AllowlistedVendorCode(exception.SqlState));
        var code = MapCode(exception.SqlState, connected);
        return new DatabaseDiscoveryProviderException(
            code,
            Summary(code),
            AllowlistedVendorCode(exception.SqlState));
    }

    public static DatabaseDiscoveryProviderException Map(NpgsqlException exception, bool connected)
    {
        var timeout = exception.InnerException is TimeoutException;
        var code = timeout ? "Timeout" : connected ? "MetadataQueryFailed" : "ConnectionFailed";
        return new DatabaseDiscoveryProviderException(code, Summary(code));
    }

    public static string MapCode(string sqlState, bool connected)
    {
        if (sqlState is "28000" or "28P01") return "AuthenticationFailed";
        if (sqlState == "42501") return "InsufficientPrivilege";
        if (sqlState == "57014") return "Timeout";
        if (sqlState == "3D000"
            || sqlState.StartsWith("08", StringComparison.Ordinal)
            || sqlState is "57P01" or "57P02" or "57P03")
            return "ConnectionFailed";
        return connected ? "MetadataQueryFailed" : "ConnectionFailed";
    }

    public static string? AllowlistedVendorCode(string? sqlState) =>
        sqlState is { Length: 5 }
        && sqlState.All(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character))
            ? $"SQLSTATE-{sqlState}"
            : null;

    public static DatabaseDiscoveryProviderException Timeout() =>
        new("Timeout", Summary("Timeout"));

    private static string Summary(string code) => code switch
    {
        "AuthenticationFailed" => "PostgreSQL 用户名或密码验证失败。",
        "InsufficientPrivilege" => "PostgreSQL 账号缺少必要的目录元数据权限。",
        "Timeout" => "PostgreSQL 目录读取超时。",
        "ConnectionFailed" => "无法建立 PostgreSQL 连接。",
        _ => "读取 PostgreSQL 目录元数据失败。",
    };
}

internal static class PostgreSqlCatalogSql
{
    public const string TargetContext = "SELECT pg_catalog.current_database()";
    public const string ConnectedPrincipal = "SELECT CURRENT_USER::text";
    public const string Schemas = """
        SELECT namespace.nspname
        FROM pg_catalog.pg_namespace AS namespace
        WHERE namespace.nspname = ANY (@schemas)
          AND pg_catalog.has_schema_privilege(CURRENT_USER, namespace.oid, 'USAGE')
        ORDER BY namespace.nspname
        """;
    public const string Objects = """
        SELECT namespace.nspname,
               relation.relname,
               relation.relkind::text,
               pg_catalog.obj_description(relation.oid, 'pg_class')
        FROM pg_catalog.pg_class AS relation
        JOIN pg_catalog.pg_namespace AS namespace ON namespace.oid = relation.relnamespace
        WHERE namespace.nspname = ANY (@schemas)
          AND relation.relkind IN ('r', 'p', 'v')
        ORDER BY namespace.nspname, relation.relname, relation.relkind
        """;
    public const string Columns = """
        SELECT namespace.nspname,
               relation.relname,
               attribute.attname,
               attribute.attnum,
               data_type.typname,
               type_namespace.nspname,
               pg_catalog.format_type(attribute.atttypid, attribute.atttypmod),
               NOT attribute.attnotnull,
               CASE
                   WHEN attribute.attgenerated = ''
                   THEN pg_catalog.pg_get_expr(attribute_default.adbin, attribute_default.adrelid, false)
                   ELSE NULL
               END,
               pg_catalog.col_description(relation.oid, attribute.attnum),
               base_type.typname,
               base_type_namespace.nspname,
               CASE
                   WHEN base_type.typname IN ('varchar', 'bpchar') AND type_context.effective_typmod >= 4
                       THEN (type_context.effective_typmod - 4)::bigint
                   WHEN base_type.typname IN ('bit', 'varbit') AND type_context.effective_typmod >= 0
                       THEN type_context.effective_typmod::bigint
                   ELSE NULL
               END,
               base_type.typname IN ('text', 'bytea')
                   OR (base_type.typname IN ('varchar', 'varbit') AND type_context.effective_typmod < 0),
               CASE
                   WHEN base_type.typname = 'int2' THEN 16
                   WHEN base_type.typname = 'int4' THEN 32
                   WHEN base_type.typname = 'int8' THEN 64
                   WHEN base_type.typname = 'float4' THEN 24
                   WHEN base_type.typname = 'float8' THEN 53
                   WHEN base_type.typname = 'numeric' AND type_context.effective_typmod >= 4
                       THEN ((type_context.effective_typmod - 4) >> 16) & 65535
                   ELSE NULL
               END,
               CASE
                   WHEN base_type.typname IN ('int2', 'int4', 'int8') THEN 0
                   WHEN base_type.typname = 'numeric' AND type_context.effective_typmod >= 4
                       THEN ((((type_context.effective_typmod - 4) & 2047) # 1024) - 1024)
                   ELSE NULL
               END
        FROM pg_catalog.pg_attribute AS attribute
        JOIN pg_catalog.pg_class AS relation ON relation.oid = attribute.attrelid
        JOIN pg_catalog.pg_namespace AS namespace ON namespace.oid = relation.relnamespace
        JOIN pg_catalog.pg_type AS data_type ON data_type.oid = attribute.atttypid
        JOIN pg_catalog.pg_namespace AS type_namespace ON type_namespace.oid = data_type.typnamespace
        CROSS JOIN LATERAL (
            SELECT CASE WHEN data_type.typbasetype <> 0 THEN data_type.typbasetype ELSE data_type.oid END AS base_type_oid,
                   CASE
                       WHEN attribute.atttypmod >= 0 THEN attribute.atttypmod
                       WHEN data_type.typtypmod >= 0 THEN data_type.typtypmod
                       ELSE -1
                   END AS effective_typmod
        ) AS type_context
        JOIN pg_catalog.pg_type AS base_type ON base_type.oid = type_context.base_type_oid
        JOIN pg_catalog.pg_namespace AS base_type_namespace ON base_type_namespace.oid = base_type.typnamespace
        LEFT JOIN pg_catalog.pg_attrdef AS attribute_default
            ON attribute_default.adrelid = attribute.attrelid
           AND attribute_default.adnum = attribute.attnum
        WHERE namespace.nspname = ANY (@schemas)
          AND relation.relkind IN ('r', 'p', 'v')
          AND attribute.attnum > 0
          AND NOT attribute.attisdropped
        ORDER BY namespace.nspname, relation.relname, attribute.attnum
        """;
    public const string Constraints = """
        SELECT namespace.nspname,
               relation.relname,
               constraint_row.conname,
               constraint_row.contype::text,
               constrained_position.position,
               constrained_attribute.attname,
               CASE
                   WHEN referenced_namespace.oid IS NULL
                     OR pg_catalog.has_schema_privilege(CURRENT_USER, referenced_namespace.oid, 'USAGE')
                   THEN referenced_namespace.nspname
                   ELSE NULL
               END,
               CASE
                   WHEN referenced_namespace.oid IS NULL
                     OR pg_catalog.has_schema_privilege(CURRENT_USER, referenced_namespace.oid, 'USAGE')
                   THEN referenced_relation.relname
                   ELSE NULL
               END,
               CASE
                   WHEN referenced_namespace.oid IS NULL
                     OR pg_catalog.has_schema_privilege(CURRENT_USER, referenced_namespace.oid, 'USAGE')
                   THEN referenced_attribute.attname
                   ELSE NULL
               END,
               CASE WHEN constraint_row.contype = 'f' THEN constraint_row.confupdtype::text ELSE NULL END,
               CASE WHEN constraint_row.contype = 'f' THEN constraint_row.confdeltype::text ELSE NULL END
        FROM pg_catalog.pg_constraint AS constraint_row
        JOIN pg_catalog.pg_class AS relation ON relation.oid = constraint_row.conrelid
        JOIN pg_catalog.pg_namespace AS namespace ON namespace.oid = relation.relnamespace
        CROSS JOIN LATERAL pg_catalog.generate_subscripts(constraint_row.conkey, 1)
            AS constrained_position(position)
        JOIN pg_catalog.pg_attribute AS constrained_attribute
          ON constrained_attribute.attrelid = constraint_row.conrelid
         AND constrained_attribute.attnum = constraint_row.conkey[constrained_position.position]
        LEFT JOIN pg_catalog.pg_class AS referenced_relation
          ON referenced_relation.oid = constraint_row.confrelid
        LEFT JOIN pg_catalog.pg_namespace AS referenced_namespace
          ON referenced_namespace.oid = referenced_relation.relnamespace
        LEFT JOIN pg_catalog.pg_attribute AS referenced_attribute
          ON referenced_attribute.attrelid = constraint_row.confrelid
         AND referenced_attribute.attnum = constraint_row.confkey[constrained_position.position]
        WHERE namespace.nspname = ANY (@schemas)
          AND relation.relkind IN ('r', 'p')
          AND constraint_row.contype IN ('p', 'u', 'f')
        ORDER BY namespace.nspname, relation.relname, constraint_row.conname, constrained_position.position
        """;
    public const string IndexParts = """
        SELECT namespace.nspname,
               relation.relname,
               index_relation.relname,
               access_method.amname,
               index_row.indisunique,
               index_row.indnkeyatts,
               index_position.subscript + 1,
               indexed_attribute.attname,
               CASE
                   WHEN index_row.indkey[index_position.subscript] = 0
                   THEN pg_catalog.pg_get_indexdef(index_row.indexrelid, index_position.subscript + 1, false)
                   ELSE NULL
               END,
               CASE
                   WHEN access_method.amname = 'btree'
                    AND index_position.subscript < index_row.indnkeyatts
                   THEN (index_row.indoption[index_position.subscript] & 1) = 1
                   ELSE FALSE
               END,
               pg_catalog.pg_get_expr(index_row.indpred, index_row.indrelid, false),
               backing_constraint.conname,
               index_row.indisvalid,
               index_row.indisready
        FROM pg_catalog.pg_index AS index_row
        JOIN pg_catalog.pg_class AS index_relation ON index_relation.oid = index_row.indexrelid
        JOIN pg_catalog.pg_class AS relation ON relation.oid = index_row.indrelid
        JOIN pg_catalog.pg_namespace AS namespace ON namespace.oid = relation.relnamespace
        JOIN pg_catalog.pg_am AS access_method ON access_method.oid = index_relation.relam
        CROSS JOIN LATERAL pg_catalog.generate_subscripts(index_row.indkey, 1)
            AS index_position(subscript)
        LEFT JOIN pg_catalog.pg_attribute AS indexed_attribute
          ON indexed_attribute.attrelid = index_row.indrelid
         AND indexed_attribute.attnum = index_row.indkey[index_position.subscript]
        LEFT JOIN pg_catalog.pg_constraint AS backing_constraint
          ON backing_constraint.conrelid = index_row.indrelid
         AND backing_constraint.conindid = index_row.indexrelid
         AND backing_constraint.contype IN ('p', 'u')
        WHERE namespace.nspname = ANY (@schemas)
          AND relation.relkind IN ('r', 'p')
          AND index_row.indislive
        ORDER BY namespace.nspname, relation.relname, index_relation.relname, index_position.subscript
        """;
    public const string Sequences = """
        SELECT namespace.nspname,
               sequence_relation.relname,
               sequence_type.typname,
               type_namespace.nspname,
               pg_catalog.format_type(sequence_row.seqtypid, NULL),
               sequence_row.seqstart::text,
               sequence_row.seqincrement::text,
               sequence_row.seqmin::text,
               sequence_row.seqmax::text,
               sequence_row.seqcache,
               sequence_row.seqcycle
        FROM pg_catalog.pg_sequence AS sequence_row
        JOIN pg_catalog.pg_class AS sequence_relation ON sequence_relation.oid = sequence_row.seqrelid
        JOIN pg_catalog.pg_namespace AS namespace ON namespace.oid = sequence_relation.relnamespace
        JOIN pg_catalog.pg_type AS sequence_type ON sequence_type.oid = sequence_row.seqtypid
        JOIN pg_catalog.pg_namespace AS type_namespace ON type_namespace.oid = sequence_type.typnamespace
        WHERE namespace.nspname = ANY (@schemas)
        ORDER BY namespace.nspname, sequence_relation.relname
        """;

    public static IReadOnlyList<string> ReviewedQueryInventory { get; } =
    [
        TargetContext,
        ConnectedPrincipal,
        Schemas,
        Objects,
        Columns,
        Constraints,
        IndexParts,
        Sequences,
    ];
}
