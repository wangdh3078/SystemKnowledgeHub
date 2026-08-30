using System.Data;
using System.Data.Common;
using System.Globalization;
using Oracle.ManagedDataAccess.Client;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Providers.Oracle;

internal sealed class OracleManagedDiscoveryCatalogReader : IOracleDiscoveryCatalogReader
{
    private const int ConnectionTimeoutSeconds = 15;
    private const int CommandTimeoutSeconds = 60;
    private const int ReferenceBatchSize = 250;

    public async Task<OracleCapabilityProbe> ReadCapabilitiesAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken)
    {
        var connected = false;
        try
        {
            await using var oracle = CreateConnection(connection);
            await oracle.OpenAsync(cancellationToken);
            connected = true;
            var target = await ReadTarget(oracle, cancellationToken);
            OracleDiscoveryRules.ValidateTarget(connection, target);

            var capabilities = new List<CanonicalCapability>
            {
                await Probe(oracle, "SupportsIdentityColumns", OracleCatalogSql.IdentityCapability, cancellationToken),
                await Probe(oracle, "SupportsInvisibleColumns", OracleCatalogSql.InvisibleCapability, cancellationToken),
                await Probe(oracle, "SupportsMaterializedViews", OracleCatalogSql.MaterializedViewCapability, cancellationToken),
                await Probe(oracle, "SupportsPartitions", OracleCatalogSql.PartitionCapability, cancellationToken),
                await Probe(oracle, "SupportsSequences", OracleCatalogSql.SequenceCapability, cancellationToken),
                await Probe(oracle, "SupportsSynonyms", OracleCatalogSql.SynonymCapability, cancellationToken),
                await Probe(oracle, "SupportsTriggers", OracleCatalogSql.TriggerCapability, cancellationToken),
                new("SupportsContainerDatabase",
                    target.ContainerName is null
                        ? DatabaseDiscoveryCapabilityState.NotApplicable
                        : DatabaseDiscoveryCapabilityState.Supported,
                    target.ContainerName is null ? "NonContainerDatabase" : null),
                new("SupportsFullDdl", DatabaseDiscoveryCapabilityState.NotSupported, "CoreScopeExcluded"),
            };
            return new OracleCapabilityProbe(target, capabilities);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DatabaseDiscoveryProviderException)
        {
            throw;
        }
        catch (OracleException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OracleException exception)
        {
            throw OracleDiscoveryErrorMapper.Map(exception, connected, cancellationToken);
        }
        catch
        {
            throw new DatabaseDiscoveryProviderException(
                connected ? "MetadataQueryFailed" : "ConnectionFailed",
                connected ? "读取 Oracle 目录元数据失败。" : "无法建立 Oracle 连接。");
        }
    }

    public async Task<OracleCatalogSnapshot> ReadCatalogAsync(
        DatabaseDiscoveryConnectionContext connection,
        DatabaseDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        var connected = false;
        try
        {
            await using var oracle = CreateConnection(connection);
            await oracle.OpenAsync(cancellationToken);
            connected = true;
            var target = await ReadTarget(oracle, cancellationToken);
            OracleDiscoveryRules.ValidateTarget(connection, target);

            var schemas = await ReadSchemas(oracle, request.IncludedSchemas, cancellationToken);
            if (!schemas.ToHashSet(StringComparer.Ordinal).SetEquals(request.IncludedSchemas))
                throw new DatabaseDiscoveryProviderException(
                    "InsufficientPrivilege", "Oracle 账号缺少必要的目录元数据权限。");

            var tables = await ReadObjects(oracle, OracleCatalogSql.Tables, request.IncludedSchemas, request.Limits.MaximumObjects, cancellationToken);
            var views = await ReadObjects(oracle, OracleCatalogSql.Views, request.IncludedSchemas, request.Limits.MaximumObjects, cancellationToken);
            if (tables.Count + views.Count > request.Limits.MaximumObjects) LimitExceeded();
            var columns = await ReadColumns(oracle, request.IncludedSchemas, request.Limits.MaximumColumns, cancellationToken);
            var objectComments = await ReadObjectComments(oracle, request.IncludedSchemas, request.Limits.MaximumObjects, cancellationToken);
            var columnComments = await ReadColumnComments(oracle, request.IncludedSchemas, request.Limits.MaximumColumns, cancellationToken);
            var constraints = await ReadConstraints(
                oracle, OracleCatalogSql.Constraints, request.IncludedSchemas,
                request.Limits.MaximumConstraintsAndIndexes, cancellationToken);
            var constraintColumns = await ReadConstraintColumns(
                oracle, OracleCatalogSql.ConstraintColumns, request.IncludedSchemas,
                request.Limits.MaximumColumns, cancellationToken);

            var knownConstraints = constraints.Select(item => (item.Owner, item.Name)).ToHashSet();
            var references = constraints
                .Where(item => item.ConstraintType == "R"
                    && item.ReferencedOwner is not null
                    && item.ReferencedConstraintName is not null
                    && !knownConstraints.Contains((item.ReferencedOwner, item.ReferencedConstraintName)))
                .Select(item => (Owner: item.ReferencedOwner!, Name: item.ReferencedConstraintName!))
                .Distinct()
                .ToArray();
            foreach (var batch in references.Chunk(ReferenceBatchSize))
            {
                var referencedConstraints = await ReadReferencedConstraints(
                    oracle, batch, request.Limits.MaximumConstraintsAndIndexes - constraints.Count, cancellationToken);
                constraints.AddRange(referencedConstraints);
                var referencedColumns = await ReadReferencedConstraintColumns(
                    oracle, batch, request.Limits.MaximumColumns - constraintColumns.Count, cancellationToken);
                constraintColumns.AddRange(referencedColumns);
            }

            var indexes = await ReadIndexes(
                oracle, request.IncludedSchemas,
                request.Limits.MaximumConstraintsAndIndexes - constraints.Count, cancellationToken);
            var indexColumns = await ReadIndexColumns(
                oracle, request.IncludedSchemas, request.Limits.MaximumColumns, cancellationToken);
            var indexExpressions = await ReadIndexExpressions(
                oracle, request.IncludedSchemas, request.Limits.MaximumColumns, cancellationToken);
            var sequences = await ReadSequences(
                oracle, request.IncludedSchemas, request.Limits.MaximumSequences, cancellationToken);

            return new OracleCatalogSnapshot(
                target,
                await ReadConnectedPrincipal(oracle, cancellationToken),
                schemas,
                tables,
                views,
                columns,
                objectComments,
                columnComments,
                constraints,
                constraintColumns,
                indexes,
                indexColumns,
                indexExpressions,
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
        catch (OracleException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OracleException exception)
        {
            throw OracleDiscoveryErrorMapper.Map(exception, connected, cancellationToken);
        }
        catch (OverflowException)
        {
            LimitExceeded();
            throw;
        }
        catch
        {
            throw new DatabaseDiscoveryProviderException(
                connected ? "MetadataQueryFailed" : "ConnectionFailed",
                connected ? "读取 Oracle 目录元数据失败。" : "无法建立 Oracle 连接。");
        }
    }

    private static OracleConnection CreateConnection(DatabaseDiscoveryConnectionContext connection)
    {
        var host = connection.Host.Contains(':', StringComparison.Ordinal)
            ? $"[{connection.Host.Trim('[', ']')}]"
            : connection.Host;
        var builder = new OracleConnectionStringBuilder
        {
            DataSource = $"{host}:{connection.Port}/{connection.ServiceName}",
            UserID = connection.Username,
            Password = connection.Password,
            Pooling = false,
            ConnectionTimeout = ConnectionTimeoutSeconds,
        };
        return new OracleConnection(builder.ConnectionString);
    }

    private static async Task<OracleTargetContext> ReadTarget(
        OracleConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, OracleCatalogSql.TargetContext);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new DatabaseDiscoveryProviderException("ConnectionFailed", "无法建立 Oracle 连接。");
        return new OracleTargetContext(
            connection.ServerVersion,
            $"Oracle.ManagedDataAccess.Core/{typeof(OracleConnection).Assembly.GetName().Version}",
            RequiredText(reader, 0),
            NullableText(reader, 1),
            NullableText(reader, 2));
    }

    private static async Task<string> ReadConnectedPrincipal(
        OracleConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, OracleCatalogSql.ConnectedPrincipal);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture)
            ?? throw new DatabaseDiscoveryProviderException("MetadataQueryFailed", "读取 Oracle 目录元数据失败。");
    }

    private static async Task<CanonicalCapability> Probe(
        OracleConnection connection,
        string name,
        string sql,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = Command(connection, sql);
            await command.ExecuteScalarAsync(cancellationToken);
            return new CanonicalCapability(name, DatabaseDiscoveryCapabilityState.Supported, null);
        }
        catch (OracleException exception) when (exception.Number is 942 or 1031)
        {
            return new CanonicalCapability(name, DatabaseDiscoveryCapabilityState.Unavailable,
                OracleDiscoveryErrorMapper.AllowlistedVendorCode(exception.Number) ?? "InsufficientPrivilege");
        }
        catch (OracleException exception) when (exception.Number is 904)
        {
            return new CanonicalCapability(name, DatabaseDiscoveryCapabilityState.NotSupported, "Oracle19CapabilityUnavailable");
        }
    }

    private static async Task<List<string>> ReadSchemas(
        OracleConnection connection,
        IReadOnlyList<string> schemas,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, OracleCatalogSql.Schemas, schemas);
        var result = new List<string>();
        await ReadRows(command, cancellationToken, reader => result.Add(RequiredText(reader, 0)));
        return result;
    }

    private static async Task<List<OracleObjectRow>> ReadObjects(
        OracleConnection connection,
        string sql,
        IReadOnlyList<string> schemas,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, sql, schemas);
        var result = new List<OracleObjectRow>();
        await ReadRows(command, cancellationToken, reader =>
        {
            result.Add(new(RequiredText(reader, 0), RequiredText(reader, 1)));
            EnforceCount(result.Count, limit);
        });
        return result;
    }

    private static async Task<List<OracleColumnRow>> ReadColumns(
        OracleConnection connection,
        IReadOnlyList<string> schemas,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, OracleCatalogSql.Columns, schemas);
        var result = new List<OracleColumnRow>();
        await ReadRows(command, cancellationToken, reader =>
        {
            result.Add(new(
                RequiredText(reader, 0), RequiredText(reader, 1), RequiredText(reader, 2), NullableInt(reader, 3),
                RequiredText(reader, 4), NullableText(reader, 5), NullableLong(reader, 6), NullableLong(reader, 7),
                NullableText(reader, 8), NullableInt(reader, 9), NullableInt(reader, 10), RequiredText(reader, 11),
                NullableText(reader, 12)));
            EnforceCount(result.Count, limit);
        });
        return result;
    }

    private static async Task<List<OracleObjectCommentRow>> ReadObjectComments(
        OracleConnection connection,
        IReadOnlyList<string> schemas,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, OracleCatalogSql.ObjectComments, schemas);
        var result = new List<OracleObjectCommentRow>();
        await ReadRows(command, cancellationToken, reader =>
        {
            result.Add(new(RequiredText(reader, 0), RequiredText(reader, 1), RequiredText(reader, 2), NullableText(reader, 3)));
            EnforceCount(result.Count, limit);
        });
        return result;
    }

    private static async Task<List<OracleColumnCommentRow>> ReadColumnComments(
        OracleConnection connection,
        IReadOnlyList<string> schemas,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, OracleCatalogSql.ColumnComments, schemas);
        var result = new List<OracleColumnCommentRow>();
        await ReadRows(command, cancellationToken, reader =>
        {
            result.Add(new(RequiredText(reader, 0), RequiredText(reader, 1), RequiredText(reader, 2), NullableText(reader, 3)));
            EnforceCount(result.Count, limit);
        });
        return result;
    }

    private static async Task<List<OracleConstraintRow>> ReadConstraints(
        OracleConnection connection,
        string sql,
        IReadOnlyList<string> schemas,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, sql, schemas);
        return await ReadConstraintRows(command, limit, cancellationToken);
    }

    private static async Task<List<OracleConstraintColumnRow>> ReadConstraintColumns(
        OracleConnection connection,
        string sql,
        IReadOnlyList<string> schemas,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, sql, schemas);
        return await ReadConstraintColumnRows(command, limit, cancellationToken);
    }

    private static async Task<List<OracleConstraintRow>> ReadReferencedConstraints(
        OracleConnection connection,
        IReadOnlyList<(string Owner, string Name)> references,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = ReferencedCommand(connection, OracleCatalogSql.ReferencedConstraints, references);
        return await ReadConstraintRows(command, limit, cancellationToken);
    }

    private static async Task<List<OracleConstraintColumnRow>> ReadReferencedConstraintColumns(
        OracleConnection connection,
        IReadOnlyList<(string Owner, string Name)> references,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = ReferencedCommand(connection, OracleCatalogSql.ReferencedConstraintColumns, references);
        return await ReadConstraintColumnRows(command, limit, cancellationToken);
    }

    private static async Task<List<OracleConstraintRow>> ReadConstraintRows(
        OracleCommand command,
        int limit,
        CancellationToken cancellationToken)
    {
        var result = new List<OracleConstraintRow>();
        await ReadRows(command, cancellationToken, reader =>
        {
            result.Add(new(
                RequiredText(reader, 0), RequiredText(reader, 1), RequiredText(reader, 2), RequiredText(reader, 3),
                NullableText(reader, 4), NullableText(reader, 5), NullableText(reader, 6), NullableText(reader, 7), NullableText(reader, 8)));
            EnforceCount(result.Count, limit);
        });
        return result;
    }

    private static async Task<List<OracleConstraintColumnRow>> ReadConstraintColumnRows(
        OracleCommand command,
        int limit,
        CancellationToken cancellationToken)
    {
        var result = new List<OracleConstraintColumnRow>();
        await ReadRows(command, cancellationToken, reader =>
        {
            result.Add(new(
                RequiredText(reader, 0), RequiredText(reader, 1), RequiredText(reader, 2), RequiredText(reader, 3), RequiredInt(reader, 4)));
            EnforceCount(result.Count, limit);
        });
        return result;
    }

    private static async Task<List<OracleIndexRow>> ReadIndexes(
        OracleConnection connection,
        IReadOnlyList<string> schemas,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, OracleCatalogSql.Indexes, schemas);
        var result = new List<OracleIndexRow>();
        await ReadRows(command, cancellationToken, reader =>
        {
            result.Add(new(
                RequiredText(reader, 0), RequiredText(reader, 1), RequiredText(reader, 2), RequiredText(reader, 3),
                RequiredText(reader, 4), RequiredText(reader, 5)));
            EnforceCount(result.Count, limit);
        });
        return result;
    }

    private static async Task<List<OracleIndexColumnRow>> ReadIndexColumns(
        OracleConnection connection,
        IReadOnlyList<string> schemas,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, OracleCatalogSql.IndexColumns, schemas);
        var result = new List<OracleIndexColumnRow>();
        await ReadRows(command, cancellationToken, reader =>
        {
            result.Add(new(
                RequiredText(reader, 0), RequiredText(reader, 1), RequiredText(reader, 2), RequiredText(reader, 3),
                RequiredText(reader, 4), RequiredInt(reader, 5), RequiredText(reader, 6)));
            EnforceCount(result.Count, limit);
        });
        return result;
    }

    private static async Task<List<OracleIndexExpressionRow>> ReadIndexExpressions(
        OracleConnection connection,
        IReadOnlyList<string> schemas,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, OracleCatalogSql.IndexExpressions, schemas);
        var result = new List<OracleIndexExpressionRow>();
        await ReadRows(command, cancellationToken, reader =>
        {
            result.Add(new(
                RequiredText(reader, 0), RequiredText(reader, 1), RequiredText(reader, 2), RequiredText(reader, 3),
                RequiredText(reader, 4), RequiredInt(reader, 5)));
            EnforceCount(result.Count, limit);
        });
        return result;
    }

    private static async Task<List<OracleSequenceRow>> ReadSequences(
        OracleConnection connection,
        IReadOnlyList<string> schemas,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = ScopedCommand(connection, OracleCatalogSql.Sequences, schemas);
        var result = new List<OracleSequenceRow>();
        await ReadRows(command, cancellationToken, reader =>
        {
            result.Add(new(
                RequiredText(reader, 0), RequiredText(reader, 1), InvariantText(reader, 2), InvariantText(reader, 3),
                InvariantText(reader, 4), RequiredText(reader, 5), RequiredText(reader, 6), NullableLong(reader, 7)));
            EnforceCount(result.Count, limit);
        });
        return result;
    }

    private static async Task ReadRows(
        OracleCommand command,
        CancellationToken cancellationToken,
        Action<DbDataReader> map)
    {
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) map(reader);
    }

    private static OracleCommand ScopedCommand(
        OracleConnection connection,
        string sql,
        IReadOnlyList<string> schemas)
    {
        var command = Command(connection, string.Format(CultureInfo.InvariantCulture, sql, BindList(schemas.Count, "schema")));
        for (var index = 0; index < schemas.Count; index++)
            command.Parameters.Add($"schema{index}", OracleDbType.Varchar2, schemas[index], ParameterDirection.Input);
        return command;
    }

    private static OracleCommand ReferencedCommand(
        OracleConnection connection,
        string sql,
        IReadOnlyList<(string Owner, string Name)> references)
    {
        var predicates = references.Select((_, index) => $"(OWNER = :refOwner{index} AND CONSTRAINT_NAME = :refName{index})");
        var command = Command(connection, string.Format(CultureInfo.InvariantCulture, sql, string.Join(" OR ", predicates)));
        for (var index = 0; index < references.Count; index++)
        {
            command.Parameters.Add($"refOwner{index}", OracleDbType.Varchar2, references[index].Owner, ParameterDirection.Input);
            command.Parameters.Add($"refName{index}", OracleDbType.Varchar2, references[index].Name, ParameterDirection.Input);
        }
        return command;
    }

    private static OracleCommand Command(OracleConnection connection, string sql) => new(sql, connection)
    {
        BindByName = true,
        CommandTimeout = CommandTimeoutSeconds,
    };

    private static string BindList(int count, string prefix) =>
        string.Join(',', Enumerable.Range(0, count).Select(index => $":{prefix}{index}"));

    private static string RequiredText(DbDataReader reader, int ordinal) =>
        NullableText(reader, ordinal)
        ?? throw new DatabaseDiscoveryProviderException("MetadataQueryFailed", "读取 Oracle 目录元数据失败。");

    private static string? NullableText(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static string InvariantText(DbDataReader reader, int ordinal)
    {
        var value = reader.GetValue(ordinal);
        return value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty
            : value.ToString() ?? string.Empty;
    }

    private static int RequiredInt(DbDataReader reader, int ordinal) =>
        NullableInt(reader, ordinal)
        ?? throw new DatabaseDiscoveryProviderException("MetadataQueryFailed", "读取 Oracle 目录元数据失败。");

    private static int? NullableInt(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : checked(Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture));

    private static long? NullableLong(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : checked(Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture));

    private static void EnforceCount(int count, int limit)
    {
        if (count > limit) LimitExceeded();
    }

    private static void LimitExceeded() => throw new DatabaseDiscoveryProviderException(
        "LimitExceeded", "发现结果超过配置的安全限制。");
}

internal static class OracleDiscoveryErrorMapper
{
    public static DatabaseDiscoveryProviderException Map(
        OracleException exception,
        bool connected,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return new DatabaseDiscoveryProviderException("Cancelled", "Oracle 目录读取已取消。", AllowlistedVendorCode(exception.Number));
        var code = MapCode(exception.Number, connected);
        var summary = code switch
        {
            "AuthenticationFailed" => "Oracle 用户名或密码验证失败。",
            "InsufficientPrivilege" => "Oracle 账号缺少必要的目录元数据权限。",
            "Timeout" => "Oracle 目录读取超时。",
            "ConnectionFailed" => "无法建立 Oracle 连接。",
            _ => "读取 Oracle 目录元数据失败。",
        };
        return new DatabaseDiscoveryProviderException(code, summary, AllowlistedVendorCode(exception.Number));
    }

    public static string MapCode(int number, bool connected) => number switch
    {
        1005 or 1017 => "AuthenticationFailed",
        942 or 1031 => "InsufficientPrivilege",
        1013 or 12170 or 12535 => "Timeout",
        _ => connected ? "MetadataQueryFailed" : "ConnectionFailed",
    };

    public static string? AllowlistedVendorCode(int number) =>
        number is >= 1 and <= 99999
            ? $"ORA-{number.ToString("D5", CultureInfo.InvariantCulture)}"
            : null;
}

internal static class OracleCatalogSql
{
    public const string TargetContext = "SELECT SYS_CONTEXT('USERENV','SERVICE_NAME'), SYS_CONTEXT('USERENV','CON_NAME'), SYS_CONTEXT('USERENV','DB_UNIQUE_NAME') FROM DUAL";
    public const string ConnectedPrincipal = "SELECT SYS_CONTEXT('USERENV','SESSION_USER') FROM DUAL";
    public const string Schemas = "SELECT USERNAME FROM ALL_USERS WHERE USERNAME IN ({0}) ORDER BY USERNAME";
    public const string Tables = "SELECT OWNER, TABLE_NAME FROM ALL_TABLES WHERE OWNER IN ({0}) ORDER BY OWNER, TABLE_NAME";
    public const string Views = "SELECT OWNER, VIEW_NAME FROM ALL_VIEWS WHERE OWNER IN ({0}) ORDER BY OWNER, VIEW_NAME";
    public const string Columns = "SELECT C.OWNER, C.TABLE_NAME, C.COLUMN_NAME, C.COLUMN_ID, C.DATA_TYPE, C.DATA_TYPE_OWNER, C.DATA_LENGTH, C.CHAR_LENGTH, C.CHAR_USED, C.DATA_PRECISION, C.DATA_SCALE, C.NULLABLE, C.DATA_DEFAULT FROM ALL_TAB_COLUMNS C WHERE C.OWNER IN ({0}) AND (EXISTS (SELECT 1 FROM ALL_TABLES T WHERE T.OWNER = C.OWNER AND T.TABLE_NAME = C.TABLE_NAME) OR EXISTS (SELECT 1 FROM ALL_VIEWS V WHERE V.OWNER = C.OWNER AND V.VIEW_NAME = C.TABLE_NAME)) ORDER BY C.OWNER, C.TABLE_NAME, C.COLUMN_ID NULLS LAST, C.COLUMN_NAME";
    public const string ObjectComments = "SELECT OWNER, TABLE_NAME, TABLE_TYPE, COMMENTS FROM ALL_TAB_COMMENTS WHERE OWNER IN ({0}) AND TABLE_TYPE IN ('TABLE','VIEW') ORDER BY OWNER, TABLE_NAME, TABLE_TYPE";
    public const string ColumnComments = "SELECT OWNER, TABLE_NAME, COLUMN_NAME, COMMENTS FROM ALL_COL_COMMENTS WHERE OWNER IN ({0}) ORDER BY OWNER, TABLE_NAME, COLUMN_NAME";
    public const string Constraints = "SELECT OWNER, CONSTRAINT_NAME, CONSTRAINT_TYPE, TABLE_NAME, R_OWNER, R_CONSTRAINT_NAME, DELETE_RULE, INDEX_OWNER, INDEX_NAME FROM ALL_CONSTRAINTS WHERE OWNER IN ({0}) AND CONSTRAINT_TYPE IN ('P','R','U') ORDER BY OWNER, CONSTRAINT_NAME";
    public const string ConstraintColumns = "SELECT CC.OWNER, CC.CONSTRAINT_NAME, CC.TABLE_NAME, CC.COLUMN_NAME, CC.POSITION FROM ALL_CONS_COLUMNS CC WHERE CC.OWNER IN ({0}) AND EXISTS (SELECT 1 FROM ALL_CONSTRAINTS C WHERE C.OWNER = CC.OWNER AND C.CONSTRAINT_NAME = CC.CONSTRAINT_NAME AND C.CONSTRAINT_TYPE IN ('P','R','U')) ORDER BY CC.OWNER, CC.CONSTRAINT_NAME, CC.POSITION";
    public const string ReferencedConstraints = "SELECT OWNER, CONSTRAINT_NAME, CONSTRAINT_TYPE, TABLE_NAME, R_OWNER, R_CONSTRAINT_NAME, DELETE_RULE, INDEX_OWNER, INDEX_NAME FROM ALL_CONSTRAINTS WHERE ({0}) AND CONSTRAINT_TYPE IN ('P','U') ORDER BY OWNER, CONSTRAINT_NAME";
    public const string ReferencedConstraintColumns = "SELECT OWNER, CONSTRAINT_NAME, TABLE_NAME, COLUMN_NAME, POSITION FROM ALL_CONS_COLUMNS WHERE ({0}) ORDER BY OWNER, CONSTRAINT_NAME, POSITION";
    public const string Indexes = "SELECT OWNER, INDEX_NAME, TABLE_OWNER, TABLE_NAME, INDEX_TYPE, UNIQUENESS FROM ALL_INDEXES WHERE TABLE_OWNER IN ({0}) ORDER BY OWNER, INDEX_NAME";
    public const string IndexColumns = "SELECT INDEX_OWNER, INDEX_NAME, TABLE_OWNER, TABLE_NAME, COLUMN_NAME, COLUMN_POSITION, DESCEND FROM ALL_IND_COLUMNS WHERE TABLE_OWNER IN ({0}) ORDER BY INDEX_OWNER, INDEX_NAME, COLUMN_POSITION";
    public const string IndexExpressions = "SELECT INDEX_OWNER, INDEX_NAME, TABLE_OWNER, TABLE_NAME, COLUMN_EXPRESSION, COLUMN_POSITION FROM ALL_IND_EXPRESSIONS WHERE TABLE_OWNER IN ({0}) ORDER BY INDEX_OWNER, INDEX_NAME, COLUMN_POSITION";
    public const string Sequences = "SELECT SEQUENCE_OWNER, SEQUENCE_NAME, MIN_VALUE, MAX_VALUE, INCREMENT_BY, CYCLE_FLAG, ORDER_FLAG, CACHE_SIZE FROM ALL_SEQUENCES WHERE SEQUENCE_OWNER IN ({0}) ORDER BY SEQUENCE_OWNER, SEQUENCE_NAME";

    public const string IdentityCapability = "SELECT 1 FROM ALL_TAB_IDENTITY_COLS WHERE 1 = 0";
    public const string InvisibleCapability = "SELECT 1 FROM ALL_TAB_COLS WHERE 1 = 0";
    public const string MaterializedViewCapability = "SELECT 1 FROM ALL_MVIEWS WHERE 1 = 0";
    public const string PartitionCapability = "SELECT 1 FROM ALL_TAB_PARTITIONS WHERE 1 = 0";
    public const string SequenceCapability = "SELECT 1 FROM ALL_SEQUENCES WHERE 1 = 0";
    public const string SynonymCapability = "SELECT 1 FROM ALL_SYNONYMS WHERE 1 = 0";
    public const string TriggerCapability = "SELECT 1 FROM ALL_TRIGGERS WHERE 1 = 0";

    public static IReadOnlyList<string> ReviewedQueryInventory { get; } =
    [
        TargetContext, ConnectedPrincipal, Schemas, Tables, Views, Columns, ObjectComments, ColumnComments,
        Constraints, ConstraintColumns, ReferencedConstraints, ReferencedConstraintColumns, Indexes, IndexColumns,
        IndexExpressions, Sequences, IdentityCapability, InvisibleCapability, MaterializedViewCapability,
        PartitionCapability, SequenceCapability, SynonymCapability, TriggerCapability,
    ];
}
