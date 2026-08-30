namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

public sealed record CanonicalDatabaseDiscoverySnapshot(
    int FormatVersion,
    DateTimeOffset CapturedAt,
    DatabaseProviderType ProviderType,
    string ProviderVersion,
    CanonicalDatabaseInfo DatabaseInfo,
    CanonicalDiscoveryScope DiscoveryScope,
    int IdentityAlgorithmVersion,
    DatabaseDiscoveryCompleteness Completeness,
    IReadOnlyList<CanonicalCapability> Capabilities,
    IReadOnlyList<CanonicalSchema> Schemas,
    IReadOnlyList<CanonicalDatabaseObject> Objects,
    IReadOnlyList<CanonicalColumn> Columns,
    IReadOnlyList<CanonicalPrimaryKey> PrimaryKeys,
    IReadOnlyList<CanonicalForeignKey> ForeignKeys,
    IReadOnlyList<CanonicalUniqueConstraint> UniqueConstraints,
    IReadOnlyList<CanonicalIndex> Indexes,
    IReadOnlyList<CanonicalSequence> Sequences,
    IReadOnlyList<CanonicalForeignKeyReferenceStub> ForeignKeyReferenceClosure,
    CanonicalSnapshotCounts Counts);

public sealed record CanonicalDatabaseInfo(
    string Provider,
    string ServerVersion,
    string CurrentDatabaseOrService,
    string? CurrentContainer,
    string TargetFingerprint);

public sealed record CanonicalDiscoveryScope(
    int ScopeFormatVersion,
    int CoreMetadataScopeVersion,
    IReadOnlyList<string> IncludedSchemaLogicalIdentities,
    IReadOnlyList<DatabaseDiscoveryObjectType> ObjectTypes,
    int ForeignKeyReferenceClosureVersion,
    string IdentifierComparisonMode,
    IReadOnlyDictionary<string, string> NormalizationOptions,
    string? VisibilityFingerprint);

public sealed record CanonicalCapability(
    string Name,
    DatabaseDiscoveryCapabilityState State,
    string? ReasonCode);

public sealed record CanonicalSchema(string Name, string LogicalIdentity);

public sealed record CanonicalDatabaseObject(
    string SchemaLogicalIdentity,
    string SchemaName,
    string Name,
    DatabaseDiscoveryObjectType ObjectType,
    string? DatabaseComment,
    string LogicalIdentity,
    string? NativeDiagnosticIdentity);

public sealed record CanonicalColumn(
    string ParentObjectLogicalIdentity,
    string Name,
    int? SourceOrdinal,
    CanonicalNativeDataType NativeDataType,
    bool IsNullable,
    string? DefaultExpression,
    bool IsPrimaryKey,
    string? DatabaseComment,
    string LogicalIdentity);

public sealed record CanonicalNativeDataType(
    DatabaseDiscoveryNativeTypeOrigin Origin,
    string Name,
    string? Namespace,
    string Declaration,
    CanonicalLengthMeasure Length,
    string? CharacterLengthSemantics,
    CanonicalNumericMeasure NumericPrecision,
    CanonicalNumericMeasure NumericScale);

public sealed record CanonicalLengthMeasure(
    DatabaseDiscoveryMeasureKind Kind,
    long? Value,
    DatabaseDiscoveryLengthUnit? Unit);

public sealed record CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind Kind, int? Value);

public sealed record CanonicalPrimaryKey(
    string Name,
    string ParentObjectLogicalIdentity,
    IReadOnlyList<string> ColumnLogicalIdentities,
    string LogicalIdentity);

public sealed record CanonicalForeignKey(
    string Name,
    string ParentObjectLogicalIdentity,
    IReadOnlyList<string> ColumnLogicalIdentities,
    string ReferencedObjectLogicalIdentity,
    IReadOnlyList<string> ReferencedColumnLogicalIdentities,
    string? UpdateRule,
    string? DeleteRule,
    string LogicalIdentity);

public sealed record CanonicalForeignKeyReferenceStub(
    string SchemaLogicalIdentity,
    string SchemaName,
    string ObjectLogicalIdentity,
    string ObjectName,
    string ColumnLogicalIdentity,
    string ColumnName,
    bool ReferenceOnly);

public sealed record CanonicalUniqueConstraint(
    string Name,
    string ParentObjectLogicalIdentity,
    IReadOnlyList<string> ColumnLogicalIdentities,
    string LogicalIdentity);

public sealed record CanonicalIndex(
    string Name,
    string ParentObjectLogicalIdentity,
    string NativeIndexKind,
    bool IsUnique,
    IReadOnlyList<CanonicalIndexKeyPart> KeyParts,
    IReadOnlyList<CanonicalIndexNonKeyPart> NonKeyParts,
    string? NativePredicate,
    string? BackingConstraintLogicalIdentity,
    string LogicalIdentity);

public sealed record CanonicalIndexKeyPart(
    int Position,
    string? ColumnLogicalIdentity,
    string? NativeExpression,
    DatabaseDiscoverySortDirection SortDirection);

public sealed record CanonicalIndexNonKeyPart(
    int Position,
    string ColumnLogicalIdentity,
    DatabaseDiscoveryNonKeyPartRole Role);

public sealed record CanonicalSequence(
    string SchemaLogicalIdentity,
    string Name,
    CanonicalNativeDataType NativeDataType,
    string? IncrementValue,
    string? MinimumValue,
    string? MaximumValue,
    int? CacheSize,
    bool? IsCyclic,
    bool? IsOrdered,
    string? StartValue,
    string LogicalIdentity);

public sealed record CanonicalSnapshotCounts(
    int Schemas,
    int Objects,
    int Columns,
    int PrimaryKeys,
    int ForeignKeys,
    int UniqueConstraints,
    int Indexes,
    int Sequences,
    int ForeignKeyReferenceStubs);
