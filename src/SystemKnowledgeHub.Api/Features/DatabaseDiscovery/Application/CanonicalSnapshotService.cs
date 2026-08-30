using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;

public sealed class CanonicalSnapshotService
{
    public const int CurrentFormatVersion = 1;
    public const int CurrentIdentityAlgorithmVersion = 1;
    private const int MaximumIdentityLength = 2048;
    private const int MaximumNameLength = 512;
    private const int MaximumStructuralTextLength = 32768;

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public CanonicalSnapshotPreparation Prepare(
        CanonicalDatabaseDiscoverySnapshot snapshot,
        DatabaseDiscoveryConnectionContext connection,
        DatabaseDiscoveryLimits limits)
    {
        var validation = Validate(snapshot, connection, limits);
        if (validation is not null)
        {
            return new(null, null, null, null, null, validation.Value.Code, validation.Value.Summary);
        }

        var normalized = Normalize(snapshot);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(normalized, JsonOptions);
        if (bytes.Length > limits.MaximumCanonicalSnapshotBytes)
        {
            return Failure("LimitExceeded", "发现快照超过允许的大小限制。");
        }

        var json = Encoding.UTF8.GetString(bytes);
        var contentHash = Convert.ToHexString(SHA256.HashData(bytes));
        var countsJson = JsonSerializer.Serialize(normalized.Counts, JsonOptions);
        var scopeFingerprint = ComputeScopeFingerprint(normalized, connection.Username);
        return new(normalized, json, contentHash, scopeFingerprint, countsJson, null, null);
    }

    public CanonicalDatabaseDiscoverySnapshot Deserialize(string canonicalJson) =>
        JsonSerializer.Deserialize<CanonicalDatabaseDiscoverySnapshot>(canonicalJson, JsonOptions)
        ?? throw new InvalidOperationException("Stored canonical Snapshot is invalid.");

    public string SerializeEntity<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    public string HashUtf8(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static CanonicalSnapshotPreparation Failure(string code, string summary) =>
        new(null, null, null, null, null, code, summary);

    private static (string Code, string Summary)? Validate(
        CanonicalDatabaseDiscoverySnapshot snapshot,
        DatabaseDiscoveryConnectionContext connection,
        DatabaseDiscoveryLimits limits)
    {
        if (snapshot.FormatVersion != CurrentFormatVersion
            || snapshot.IdentityAlgorithmVersion != CurrentIdentityAlgorithmVersion
            || snapshot.Completeness != DatabaseDiscoveryCompleteness.Complete)
        {
            return ("MetadataQueryFailed", "Provider 返回了不兼容或不完整的发现快照。");
        }
        if (snapshot.ProviderType != connection.ProviderType
            || snapshot.CapturedAt.Offset != TimeSpan.Zero
            || !Required(snapshot.ProviderVersion, 128)
            || !Required(snapshot.DatabaseInfo.Provider, 32)
            || !string.Equals(snapshot.DatabaseInfo.Provider, snapshot.ProviderType.ToString(), StringComparison.Ordinal)
            || !Required(snapshot.DatabaseInfo.ServerVersion, 128)
            || !Required(snapshot.DatabaseInfo.CurrentDatabaseOrService, MaximumNameLength)
            || !Required(snapshot.DatabaseInfo.TargetFingerprint, MaximumIdentityLength))
        {
            return ("MetadataQueryFailed", "Provider 返回了无效的目标或版本信息。");
        }
        if (snapshot.DiscoveryScope.ScopeFormatVersion != 1
            || snapshot.DiscoveryScope.CoreMetadataScopeVersion != 1
            || snapshot.DiscoveryScope.ForeignKeyReferenceClosureVersion != 1
            || !string.Equals(snapshot.DiscoveryScope.IdentifierComparisonMode, "Ordinal", StringComparison.Ordinal)
            || snapshot.DiscoveryScope.IncludedSchemaLogicalIdentities.Count is < 1
            || snapshot.DiscoveryScope.IncludedSchemaLogicalIdentities.Count > limits.MaximumSchemas
            || snapshot.DiscoveryScope.IncludedSchemaLogicalIdentities.Distinct(StringComparer.Ordinal).Count()
                != snapshot.DiscoveryScope.IncludedSchemaLogicalIdentities.Count
            || snapshot.DiscoveryScope.ObjectTypes.Count != 2
            || !snapshot.DiscoveryScope.ObjectTypes.Contains(DatabaseDiscoveryObjectType.Table)
            || !snapshot.DiscoveryScope.ObjectTypes.Contains(DatabaseDiscoveryObjectType.View))
        {
            return ("MetadataQueryFailed", "Provider 返回了无效或不完整的发现范围。");
        }
        if (snapshot.DiscoveryScope.NormalizationOptions.Count > 128
            || snapshot.DiscoveryScope.NormalizationOptions.Any(item =>
                !Required(item.Key, 128) || !Bounded(item.Value, 1024))
            || !Bounded(snapshot.DiscoveryScope.VisibilityFingerprint, MaximumIdentityLength))
        {
            return ("LimitExceeded", "发现范围包含越界的规范化信息。");
        }

        var constraintCount = snapshot.PrimaryKeys.Count + snapshot.ForeignKeys.Count
            + snapshot.UniqueConstraints.Count + snapshot.Indexes.Count;
        if (snapshot.Schemas.Count > limits.MaximumSchemas
            || snapshot.Objects.Count > limits.MaximumObjects
            || snapshot.Columns.Count > limits.MaximumColumns
            || snapshot.ForeignKeyReferenceClosure.Count > limits.MaximumColumns
            || constraintCount > limits.MaximumConstraintsAndIndexes
            || snapshot.Sequences.Count > limits.MaximumSequences)
        {
            return ("LimitExceeded", "发现结果超过配置的对象数量限制。");
        }

        var identities = new HashSet<string>(StringComparer.Ordinal);
        bool AddIdentity(string value) => Required(value, MaximumIdentityLength) && identities.Add(value);

        var schemas = new Dictionary<string, CanonicalSchema>(StringComparer.Ordinal);
        foreach (var item in snapshot.Schemas)
        {
            if (!Required(item.Name, MaximumNameLength) || !AddIdentity(item.LogicalIdentity))
                return ("MetadataQueryFailed", "发现快照包含重复或无效的 Schema identity。");
            schemas[item.LogicalIdentity] = item;
        }
        if (!schemas.Keys.Order(StringComparer.Ordinal)
            .SequenceEqual(snapshot.DiscoveryScope.IncludedSchemaLogicalIdentities.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            return ("MetadataQueryFailed", "发现快照的 Schema 与确认的范围不一致。");
        }
        if (!schemas.Values.Select(item => item.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(connection.IncludedSchemas.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            return ("MetadataQueryFailed", "Provider 返回的 Schema 超出确认的发现范围。");
        }

        var objects = new Dictionary<string, CanonicalDatabaseObject>(StringComparer.Ordinal);
        foreach (var item in snapshot.Objects)
        {
            if (!schemas.TryGetValue(item.SchemaLogicalIdentity, out var schema)
                || !string.Equals(schema.Name, item.SchemaName, StringComparison.Ordinal)
                || !Required(item.Name, MaximumNameLength)
                || !Bounded(item.DatabaseComment, MaximumStructuralTextLength)
                || !Bounded(item.NativeDiagnosticIdentity, MaximumIdentityLength)
                || !AddIdentity(item.LogicalIdentity))
                return ("MetadataQueryFailed", "发现快照包含无效的数据库对象。");
            objects[item.LogicalIdentity] = item;
        }

        var columns = new Dictionary<string, CanonicalColumn>(StringComparer.Ordinal);
        foreach (var item in snapshot.Columns)
        {
            if (!objects.ContainsKey(item.ParentObjectLogicalIdentity)
                || !Required(item.Name, MaximumNameLength)
                || item.SourceOrdinal is <= 0
                || !ValidNativeType(item.NativeDataType)
                || !Bounded(item.DefaultExpression, MaximumStructuralTextLength)
                || !Bounded(item.DatabaseComment, MaximumStructuralTextLength)
                || !AddIdentity(item.LogicalIdentity))
                return ("MetadataQueryFailed", "发现快照包含无效的数据库列。");
            columns[item.LogicalIdentity] = item;
        }

        bool ValidOwnedColumns(string parent, IReadOnlyList<string> columnIds) =>
            columnIds.Count > 0
            && columnIds.Count <= 4096
            && columnIds.Distinct(StringComparer.Ordinal).Count() == columnIds.Count
            && columnIds.All(id => columns.TryGetValue(id, out var column)
                && string.Equals(column.ParentObjectLogicalIdentity, parent, StringComparison.Ordinal));

        foreach (var item in snapshot.PrimaryKeys)
        {
            if (!Required(item.Name, MaximumNameLength) || !objects.ContainsKey(item.ParentObjectLogicalIdentity)
                || !ValidOwnedColumns(item.ParentObjectLogicalIdentity, item.ColumnLogicalIdentities)
                || !AddIdentity(item.LogicalIdentity))
                return ("MetadataQueryFailed", "发现快照包含无效的主键。");
        }
        foreach (var item in snapshot.UniqueConstraints)
        {
            if (!Required(item.Name, MaximumNameLength) || !objects.ContainsKey(item.ParentObjectLogicalIdentity)
                || !ValidOwnedColumns(item.ParentObjectLogicalIdentity, item.ColumnLogicalIdentities)
                || !AddIdentity(item.LogicalIdentity))
                return ("MetadataQueryFailed", "发现快照包含无效的唯一约束。");
        }

        var closureColumns = new Dictionary<string, string>(StringComparer.Ordinal);
        var closureObjects = new Dictionary<string, (string SchemaIdentity, string SchemaName, string ObjectName)>(StringComparer.Ordinal);
        foreach (var item in snapshot.ForeignKeyReferenceClosure)
        {
            if (!item.ReferenceOnly
                || !Required(item.SchemaLogicalIdentity, MaximumIdentityLength)
                || !Required(item.SchemaName, MaximumNameLength)
                || !Required(item.ObjectLogicalIdentity, MaximumIdentityLength)
                || !Required(item.ObjectName, MaximumNameLength)
                || !Required(item.ColumnLogicalIdentity, MaximumIdentityLength)
                || !Required(item.ColumnName, MaximumNameLength)
                || identities.Contains(item.ObjectLogicalIdentity)
                || identities.Contains(item.ColumnLogicalIdentity)
                || !closureColumns.TryAdd(item.ColumnLogicalIdentity, item.ObjectLogicalIdentity))
                return ("MetadataQueryFailed", "发现快照包含无效的外键引用闭包。");
            var objectDescriptor = (item.SchemaLogicalIdentity, item.SchemaName, item.ObjectName);
            if (closureObjects.TryGetValue(item.ObjectLogicalIdentity, out var existing)
                && existing != objectDescriptor)
                return ("MetadataQueryFailed", "发现快照包含不一致的外键引用闭包。");
            closureObjects[item.ObjectLogicalIdentity] = objectDescriptor;
        }
        var usedClosureColumns = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in snapshot.ForeignKeys)
        {
            var referenceIsIncluded = objects.ContainsKey(item.ReferencedObjectLogicalIdentity);
            var referencedColumnsValid = referenceIsIncluded
                ? ValidOwnedColumns(item.ReferencedObjectLogicalIdentity, item.ReferencedColumnLogicalIdentities)
                : closureObjects.ContainsKey(item.ReferencedObjectLogicalIdentity)
                    && item.ReferencedColumnLogicalIdentities.Count > 0
                    && item.ReferencedColumnLogicalIdentities.All(columnIdentity =>
                        closureColumns.TryGetValue(columnIdentity, out var objectIdentity)
                        && objectIdentity == item.ReferencedObjectLogicalIdentity);
            if (!Required(item.Name, MaximumNameLength)
                || !objects.ContainsKey(item.ParentObjectLogicalIdentity)
                || !ValidOwnedColumns(item.ParentObjectLogicalIdentity, item.ColumnLogicalIdentities)
                || item.ColumnLogicalIdentities.Count != item.ReferencedColumnLogicalIdentities.Count
                || !referencedColumnsValid
                || !Bounded(item.UpdateRule, 64)
                || !Bounded(item.DeleteRule, 64)
                || !AddIdentity(item.LogicalIdentity))
                return ("UnresolvedForeignKeyReference", "发现快照包含无法完整解析的外键引用。");
            if (!referenceIsIncluded)
                foreach (var columnIdentity in item.ReferencedColumnLogicalIdentities) usedClosureColumns.Add(columnIdentity);
        }
        if (usedClosureColumns.Count != closureColumns.Count)
            return ("MetadataQueryFailed", "发现快照包含未被外键使用的引用闭包记录。");

        var constraintIds = snapshot.PrimaryKeys.Select(item => item.LogicalIdentity)
            .Concat(snapshot.UniqueConstraints.Select(item => item.LogicalIdentity))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var item in snapshot.Indexes)
        {
            var keyPositions = item.KeyParts.Select(part => part.Position).ToArray();
            var nonKeyPositions = item.NonKeyParts.Select(part => part.Position).ToArray();
            var validKeys = item.KeyParts.Count > 0
                && item.KeyParts.Count <= 4096
                && keyPositions.All(position => position > 0)
                && keyPositions.Distinct().Count() == keyPositions.Length
                && item.KeyParts.All(part =>
                    (part.ColumnLogicalIdentity is null) != (part.NativeExpression is null)
                    && (part.ColumnLogicalIdentity is null
                        ? Bounded(part.NativeExpression, MaximumStructuralTextLength)
                        : columns.TryGetValue(part.ColumnLogicalIdentity, out var column)
                            && column.ParentObjectLogicalIdentity == item.ParentObjectLogicalIdentity));
            var validNonKeys = item.NonKeyParts.Count <= 4096
                && nonKeyPositions.All(position => position > 0)
                && nonKeyPositions.Distinct().Count() == nonKeyPositions.Length
                && item.NonKeyParts.All(part => columns.TryGetValue(part.ColumnLogicalIdentity, out var column)
                    && column.ParentObjectLogicalIdentity == item.ParentObjectLogicalIdentity);
            if (!Required(item.Name, MaximumNameLength)
                || !objects.ContainsKey(item.ParentObjectLogicalIdentity)
                || !Required(item.NativeIndexKind, 128)
                || !validKeys || !validNonKeys
                || !Bounded(item.NativePredicate, MaximumStructuralTextLength)
                || (item.BackingConstraintLogicalIdentity is not null && !constraintIds.Contains(item.BackingConstraintLogicalIdentity))
                || !AddIdentity(item.LogicalIdentity))
                return ("MetadataQueryFailed", "发现快照包含无效的索引。");
        }

        foreach (var item in snapshot.Sequences)
        {
            if (!schemas.ContainsKey(item.SchemaLogicalIdentity)
                || !Required(item.Name, MaximumNameLength)
                || !ValidNativeType(item.NativeDataType)
                || !Bounded(item.IncrementValue, 256)
                || !Bounded(item.MinimumValue, 256)
                || !Bounded(item.MaximumValue, 256)
                || !Bounded(item.StartValue, 256)
                || item.CacheSize is < 0
                || !AddIdentity(item.LogicalIdentity))
                return ("MetadataQueryFailed", "发现快照包含无效的 Sequence。");
        }

        if (snapshot.Capabilities.Count > 128
            || snapshot.Capabilities.Any(item => !Required(item.Name, 128) || !Bounded(item.ReasonCode, 128))
            || snapshot.Capabilities.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != snapshot.Capabilities.Count)
        {
            return ("MetadataQueryFailed", "Provider 返回了无效的能力快照。");
        }

        return null;
    }

    private static CanonicalDatabaseDiscoverySnapshot Normalize(CanonicalDatabaseDiscoverySnapshot snapshot)
    {
        static IReadOnlyList<T> Sort<T>(IEnumerable<T> source, Func<T, string> key) =>
            source.OrderBy(key, StringComparer.Ordinal).ToArray();

        var normalized = snapshot with
        {
            DiscoveryScope = snapshot.DiscoveryScope with
            {
                IncludedSchemaLogicalIdentities = snapshot.DiscoveryScope.IncludedSchemaLogicalIdentities
                    .Order(StringComparer.Ordinal).ToArray(),
                ObjectTypes = snapshot.DiscoveryScope.ObjectTypes.Order().ToArray(),
                NormalizationOptions = new SortedDictionary<string, string>(
                    snapshot.DiscoveryScope.NormalizationOptions.ToDictionary(
                        item => item.Key, item => item.Value, StringComparer.Ordinal),
                    StringComparer.Ordinal),
            },
            Capabilities = Sort(snapshot.Capabilities, item => item.Name),
            Schemas = Sort(snapshot.Schemas, item => item.LogicalIdentity),
            Objects = Sort(snapshot.Objects, item => item.LogicalIdentity),
            Columns = Sort(snapshot.Columns, item => item.LogicalIdentity),
            PrimaryKeys = Sort(snapshot.PrimaryKeys.Select(item => item with
            {
                ColumnLogicalIdentities = item.ColumnLogicalIdentities.ToArray(),
            }), item => item.LogicalIdentity),
            ForeignKeys = Sort(snapshot.ForeignKeys.Select(item => item with
            {
                ColumnLogicalIdentities = item.ColumnLogicalIdentities.ToArray(),
                ReferencedColumnLogicalIdentities = item.ReferencedColumnLogicalIdentities.ToArray(),
            }), item => item.LogicalIdentity),
            UniqueConstraints = Sort(snapshot.UniqueConstraints.Select(item => item with
            {
                ColumnLogicalIdentities = item.ColumnLogicalIdentities.ToArray(),
            }), item => item.LogicalIdentity),
            Indexes = Sort(snapshot.Indexes.Select(item => item with
            {
                KeyParts = item.KeyParts.OrderBy(part => part.Position).ToArray(),
                NonKeyParts = item.NonKeyParts.OrderBy(part => part.Position).ToArray(),
            }), item => item.LogicalIdentity),
            Sequences = Sort(snapshot.Sequences, item => item.LogicalIdentity),
            ForeignKeyReferenceClosure = snapshot.ForeignKeyReferenceClosure
                .OrderBy(item => item.ObjectLogicalIdentity, StringComparer.Ordinal)
                .ThenBy(item => item.ColumnLogicalIdentity, StringComparer.Ordinal)
                .ToArray(),
        };
        return normalized with
        {
            Counts = new CanonicalSnapshotCounts(
                normalized.Schemas.Count,
                normalized.Objects.Count,
                normalized.Columns.Count,
                normalized.PrimaryKeys.Count,
                normalized.ForeignKeys.Count,
                normalized.UniqueConstraints.Count,
                normalized.Indexes.Count,
                normalized.Sequences.Count,
                normalized.ForeignKeyReferenceClosure.Count),
        };
    }

    private static string ComputeScopeFingerprint(
        CanonicalDatabaseDiscoverySnapshot snapshot,
        string connectedPrincipal)
    {
        var scope = new
        {
            snapshot.ProviderType,
            snapshot.DatabaseInfo.TargetFingerprint,
            snapshot.FormatVersion,
            snapshot.IdentityAlgorithmVersion,
            snapshot.DiscoveryScope.ScopeFormatVersion,
            snapshot.DiscoveryScope.CoreMetadataScopeVersion,
            snapshot.DiscoveryScope.IncludedSchemaLogicalIdentities,
            snapshot.DiscoveryScope.ObjectTypes,
            snapshot.DiscoveryScope.ForeignKeyReferenceClosureVersion,
            snapshot.DiscoveryScope.IdentifierComparisonMode,
            snapshot.DiscoveryScope.NormalizationOptions,
            snapshot.DiscoveryScope.VisibilityFingerprint,
            ConnectedPrincipal = connectedPrincipal,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(scope, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static bool ValidNativeType(CanonicalNativeDataType type)
    {
        if (!Required(type.Name, MaximumNameLength)
            || !Bounded(type.Namespace, MaximumNameLength)
            || !Required(type.Declaration, 2048)
            || !Bounded(type.CharacterLengthSemantics, 64)) return false;
        if (type.Length.Kind == DatabaseDiscoveryMeasureKind.Exact)
        {
            if (type.Length.Value is null or < 0 || type.Length.Unit is null) return false;
        }
        else if (type.Length.Value is not null || type.Length.Unit is not null) return false;
        if (!ValidNumeric(type.NumericPrecision, allowNegative: false)
            || !ValidNumeric(type.NumericScale, allowNegative: true)) return false;
        return true;
    }

    private static bool ValidNumeric(CanonicalNumericMeasure measure, bool allowNegative) =>
        measure.Kind == DatabaseDiscoveryMeasureKind.Exact
            ? measure.Value is not null && (allowNegative || measure.Value >= 0)
            : measure.Value is null;

    private static bool Required(string? value, int maximum) =>
        value is { Length: > 0 } && value.Length <= maximum && !value.Any(char.IsControl);

    private static bool Bounded(string? value, int maximum) =>
        value is null || (value.Length <= maximum && !value.Any(character => character == '\0'));

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
