import { isSafeApiId } from '../../../api/contracts/id'

export type DatabaseProviderType = 'Oracle' | 'PostgreSql'
export type DiscoveryRunStatus = 'Queued' | 'Running' | 'Succeeded' | 'Failed' | 'Cancelled'
export type DifferenceState = 'Added' | 'Changed' | 'MissingFromSource' | 'Unchanged'
export type DiscoveryEntityKind =
  | 'Schema'
  | 'DatabaseObject'
  | 'Column'
  | 'PrimaryKey'
  | 'ForeignKey'
  | 'UniqueConstraint'
  | 'Index'
  | 'Sequence'
export type DiscoveryObjectType = 'Table' | 'View'
export type CapabilityState = 'Supported' | 'NotSupported' | 'Unavailable' | 'NotApplicable'
export type SnapshotConstraintKind = Extract<
  DiscoveryEntityKind,
  'PrimaryKey' | 'ForeignKey' | 'UniqueConstraint'
>

export interface ConnectionProfile {
  readonly id: number
  readonly databaseSourceId: number
  readonly databaseSourceName: string
  readonly name: string
  readonly providerType: DatabaseProviderType
  readonly host: string
  readonly port: number
  readonly databaseName: string | null
  readonly serviceName: string | null
  readonly username: string
  readonly includedSchemas: readonly string[]
  readonly isEnabled: boolean
  readonly connectionStatus: 'Unknown' | 'Succeeded' | 'Failed'
  readonly hasSecret: boolean
  readonly lastConnectionTestAt: string | null
  readonly lastConnectionTestErrorCode: string | null
  readonly lastConnectionTestSummary: string | null
  readonly lastDiscoveryAt: string | null
  readonly lastSuccessfulDiscoveryAt: string | null
  readonly concurrencyToken: string
}
export interface SourceOption {
  readonly id: number
  readonly name: string
  readonly engine: string
  readonly systemName: string
  readonly hasConnectionProfile: boolean
}
export interface ConnectionTestResult {
  readonly profileId: number
  readonly succeeded: boolean
  readonly summary: string
  readonly providerVersion: string | null
  readonly databaseName: string | null
  readonly serviceName: string | null
  readonly containerName: string | null
  readonly concurrencyToken: string
}
export interface DiscoveryCounts {
  readonly schemas: number
  readonly objects: number
  readonly columns: number
  readonly primaryKeys: number
  readonly foreignKeys: number
  readonly uniqueConstraints: number
  readonly indexes: number
  readonly sequences: number
  readonly foreignKeyReferenceStubs: number
}
export interface DiscoveryRun {
  readonly id: number
  readonly profileId: number
  readonly databaseSourceId: number
  readonly databaseSourceName: string
  readonly profileName: string
  readonly providerType: DatabaseProviderType
  readonly status: DiscoveryRunStatus
  readonly baseSnapshotId: number | null
  readonly snapshotId: number | null
  readonly differenceId: number | null
  readonly scopeGenerationId: number | null
  readonly queuedAt: string
  readonly startedAt: string | null
  readonly completedAt: string | null
  readonly cancellationRequestedAt: string | null
  readonly providerVersion: string | null
  readonly objectCounts: DiscoveryCounts | null
  readonly errorCode: string | null
  readonly errorSummary: string | null
  readonly concurrencyToken: string
}
export interface Page<T> {
  readonly items: readonly T[]
  readonly page: number
  readonly pageSize: number
  readonly total: number
}
export interface RunFilterOption {
  readonly id: number
  readonly name: string
}
export interface RunFilterOptions {
  readonly profiles: readonly RunFilterOption[]
  readonly databaseSources: readonly RunFilterOption[]
}
export interface SnapshotSummary {
  readonly id: number
  readonly runId: number
  readonly profileId: number
  readonly capturedAt: string
  readonly providerType: DatabaseProviderType
  readonly providerVersion: string
  readonly currentDatabaseOrService: string
  readonly currentContainer: string | null
  readonly formatVersion: number
  readonly identityAlgorithmVersion: number
  readonly scopeGenerationId: number
  readonly scopeFingerprint: string
  readonly completeness: 'Complete'
  readonly contentSha256: string
  readonly includedSchemas: readonly string[]
  readonly capabilities: readonly {
    readonly name: string
    readonly state: CapabilityState
    readonly reasonCode: string | null
  }[]
  readonly counts: DiscoveryCounts
}
export interface SnapshotSchema {
  readonly name: string
  readonly logicalIdentity: string
  readonly objectCount: number
  readonly sequenceCount: number
}
export interface SnapshotObject {
  readonly logicalIdentity: string
  readonly schemaName: string
  readonly name: string
  readonly objectType: DiscoveryObjectType
  readonly databaseComment: string | null
  readonly columnCount: number
  readonly constraintCount: number
  readonly indexCount: number
}
export interface SnapshotObjectHeaderData {
  readonly schemaName: string
  readonly name: string
  readonly objectType: DiscoveryObjectType
  readonly databaseComment: string | null
  readonly logicalIdentity: string
}
export interface SnapshotColumn {
  readonly name: string
  readonly sourceOrdinal: number | null
  readonly nativeDataType: { readonly declaration: string }
  readonly isNullable: boolean
  readonly defaultExpression: string | null
  readonly databaseComment: string | null
}
export interface SnapshotIndex {
  readonly name: string
  readonly nativeIndexKind: string
  readonly isUnique: boolean
  readonly keyParts: readonly string[]
  readonly nonKeyParts: readonly string[]
  readonly nativePredicate: string | null
}
export interface SnapshotSequence {
  readonly schemaName: string
  readonly name: string
  readonly nativeDataType: string
  readonly incrementValue: string | null
  readonly minimumValue: string | null
  readonly maximumValue: string | null
  readonly cacheSize: number | null
  readonly isCyclic: boolean | null
  readonly isOrdered: boolean | null
  readonly startValue: string | null
}
export interface SnapshotObjectHeader {
  readonly object: SnapshotObjectHeaderData
}
export interface SnapshotConstraint {
  readonly entityKind: SnapshotConstraintKind
  readonly name: string
  readonly columnNames: readonly string[]
  readonly referencedObjectName: string | null
  readonly updateRule: string | null
  readonly deleteRule: string | null
}
export interface SnapshotObjectReview {
  readonly object: SnapshotObjectHeaderData
  readonly columns: Page<SnapshotColumn>
  readonly constraints: Page<SnapshotConstraint>
  readonly indexes: Page<SnapshotIndex>
}
export interface DifferenceSummary {
  readonly id: number
  readonly profileId: number
  readonly baseSnapshotId: number | null
  readonly targetSnapshotId: number
  readonly scopeGenerationId: number
  readonly algorithmVersion: number
  readonly createdAt: string
  readonly summaryCounts: {
    readonly added: number
    readonly changed: number
    readonly missingFromSource: number
    readonly unchanged: number
  }
  readonly contentSha256: string
}
export type DifferenceScalarValue = string | number | boolean | null
export interface DifferenceFieldChange {
  readonly field: string
  readonly before: DifferenceScalarValue
  readonly after: DifferenceScalarValue
}
export interface DifferenceEntry {
  readonly id: number | null
  readonly entityKind: DiscoveryEntityKind
  readonly logicalIdentity: string
  readonly parentLogicalIdentity: string | null
  readonly displayName: string
  readonly state: DifferenceState
  readonly schemaName: string | null
  readonly objectName: string | null
  readonly childName: string | null
  readonly changes: readonly DifferenceFieldChange[]
}

function object(value: unknown, field: string): Record<string, unknown> {
  if (typeof value !== 'object' || value === null || Array.isArray(value))
    throw new Error(`${field} must be an object`)
  return value as Record<string, unknown>
}
function string(value: unknown, field: string): string {
  if (typeof value !== 'string') throw new Error(`${field} must be a string`)
  return value
}
function nullableString(value: unknown, field: string): string | null {
  return value === null ? null : string(value, field)
}
function boolean(value: unknown, field: string): boolean {
  if (typeof value !== 'boolean') throw new Error(`${field} must be a boolean`)
  return value
}
function scalar(value: unknown, field: string): DifferenceScalarValue {
  if (
    value === null ||
    typeof value === 'string' ||
    typeof value === 'boolean' ||
    (typeof value === 'number' && Number.isFinite(value))
  )
    return value
  throw new Error(`${field} must be a scalar or null`)
}
function integer(value: unknown, field: string, min = 0, max = Number.MAX_SAFE_INTEGER): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < min || value > max)
    throw new Error(`${field} must be an integer`)
  return value
}
function id(value: unknown, field: string): number {
  const result = integer(value, field, 1)
  if (!isSafeApiId(result)) throw new Error(`${field} must be a safe id`)
  return result
}
function nullableId(value: unknown, field: string): number | null {
  return value === null ? null : id(value, field)
}
function nullableInteger(value: unknown, field: string, min = 0): number | null {
  return value === null ? null : integer(value, field, min)
}
function nullableBoolean(value: unknown, field: string): boolean | null {
  return value === null ? null : boolean(value, field)
}
function strings(value: unknown, field: string): readonly string[] {
  if (!Array.isArray(value)) throw new Error(`${field} must be an array`)
  return value.map((item, index) => string(item, `${field}[${index}]`))
}
function provider(value: unknown, field: string): DatabaseProviderType {
  if (value === 'Oracle' || value === 'PostgreSql') return value
  throw new Error(`${field} is unsupported`)
}
function connectionStatus(value: unknown, field: string): ConnectionProfile['connectionStatus'] {
  if (value === 'Unknown' || value === 'Succeeded' || value === 'Failed') return value
  throw new Error(`${field} is unsupported`)
}
function objectType(value: unknown, field: string): DiscoveryObjectType {
  if (value === 'Table' || value === 'View') return value
  throw new Error(`${field} is unsupported`)
}
function entityKind(value: unknown, field: string): DiscoveryEntityKind {
  if (
    value === 'Schema' ||
    value === 'DatabaseObject' ||
    value === 'Column' ||
    value === 'PrimaryKey' ||
    value === 'ForeignKey' ||
    value === 'UniqueConstraint' ||
    value === 'Index' ||
    value === 'Sequence'
  )
    return value
  throw new Error(`${field} is unsupported`)
}
function constraintKind(value: unknown, field: string): SnapshotConstraintKind {
  if (value === 'PrimaryKey' || value === 'ForeignKey' || value === 'UniqueConstraint') return value
  throw new Error(`${field} is unsupported`)
}
function capabilityState(value: unknown, field: string): CapabilityState {
  if (
    value === 'Supported' ||
    value === 'NotSupported' ||
    value === 'Unavailable' ||
    value === 'NotApplicable'
  )
    return value
  throw new Error(`${field} is unsupported`)
}
function status(value: unknown, field: string): DiscoveryRunStatus {
  if (
    value === 'Queued' ||
    value === 'Running' ||
    value === 'Succeeded' ||
    value === 'Failed' ||
    value === 'Cancelled'
  )
    return value
  throw new Error(`${field} is unsupported`)
}
function counts(value: unknown, field: string): DiscoveryCounts {
  const root = object(value, field)
  return {
    schemas: integer(root.schemas, `${field}.schemas`),
    objects: integer(root.objects, `${field}.objects`),
    columns: integer(root.columns, `${field}.columns`),
    primaryKeys: integer(root.primaryKeys, `${field}.primaryKeys`),
    foreignKeys: integer(root.foreignKeys, `${field}.foreignKeys`),
    uniqueConstraints: integer(root.uniqueConstraints, `${field}.uniqueConstraints`),
    indexes: integer(root.indexes, `${field}.indexes`),
    sequences: integer(root.sequences, `${field}.sequences`),
    foreignKeyReferenceStubs: integer(
      root.foreignKeyReferenceStubs,
      `${field}.foreignKeyReferenceStubs`,
    ),
  }
}

export function decodeProfile(value: unknown): ConnectionProfile {
  const r = object(value, 'profile')
  return {
    id: id(r.id, 'profile.id'),
    databaseSourceId: id(r.databaseSourceId, 'profile.databaseSourceId'),
    databaseSourceName: string(r.databaseSourceName, 'profile.databaseSourceName'),
    name: string(r.name, 'profile.name'),
    providerType: provider(r.providerType, 'profile.providerType'),
    host: string(r.host, 'profile.host'),
    port: integer(r.port, 'profile.port', 1),
    databaseName: nullableString(r.databaseName, 'profile.databaseName'),
    serviceName: nullableString(r.serviceName, 'profile.serviceName'),
    username: string(r.username, 'profile.username'),
    includedSchemas: strings(r.includedSchemas, 'profile.includedSchemas'),
    isEnabled: boolean(r.isEnabled, 'profile.isEnabled'),
    connectionStatus: connectionStatus(r.connectionStatus, 'profile.connectionStatus'),
    hasSecret: boolean(r.hasSecret, 'profile.hasSecret'),
    lastConnectionTestAt: nullableString(r.lastConnectionTestAt, 'profile.lastConnectionTestAt'),
    lastConnectionTestErrorCode: nullableString(
      r.lastConnectionTestErrorCode,
      'profile.lastConnectionTestErrorCode',
    ),
    lastConnectionTestSummary: nullableString(
      r.lastConnectionTestSummary,
      'profile.lastConnectionTestSummary',
    ),
    lastDiscoveryAt: nullableString(r.lastDiscoveryAt, 'profile.lastDiscoveryAt'),
    lastSuccessfulDiscoveryAt: nullableString(
      r.lastSuccessfulDiscoveryAt,
      'profile.lastSuccessfulDiscoveryAt',
    ),
    concurrencyToken: string(r.concurrencyToken, 'profile.concurrencyToken'),
  }
}
export function decodeProfiles(value: unknown): readonly ConnectionProfile[] {
  if (!Array.isArray(value)) throw new Error('profiles must be an array')
  return value.map(decodeProfile)
}
export function decodeSourceOptions(value: unknown): readonly SourceOption[] {
  if (!Array.isArray(value)) throw new Error('sources must be an array')
  return value.map((item) => {
    const r = object(item, 'source')
    return {
      id: id(r.id, 'source.id'),
      name: string(r.name, 'source.name'),
      engine: string(r.engine, 'source.engine'),
      systemName: string(r.systemName, 'source.systemName'),
      hasConnectionProfile: boolean(r.hasConnectionProfile, 'source.hasConnectionProfile'),
    }
  })
}
export function decodeConnectionTest(value: unknown): ConnectionTestResult {
  const r = object(value, 'connectionTest')
  return {
    profileId: id(r.profileId, 'connectionTest.profileId'),
    succeeded: boolean(r.succeeded, 'connectionTest.succeeded'),
    summary: string(r.summary, 'connectionTest.summary'),
    providerVersion: nullableString(r.providerVersion, 'connectionTest.providerVersion'),
    databaseName: nullableString(r.databaseName, 'connectionTest.databaseName'),
    serviceName: nullableString(r.serviceName, 'connectionTest.serviceName'),
    containerName: nullableString(r.containerName, 'connectionTest.containerName'),
    concurrencyToken: string(r.concurrencyToken, 'connectionTest.concurrencyToken'),
  }
}
export function decodeRun(value: unknown): DiscoveryRun {
  const r = object(value, 'run')
  return {
    id: id(r.id, 'run.id'),
    profileId: id(r.profileId, 'run.profileId'),
    databaseSourceId: id(r.databaseSourceId, 'run.databaseSourceId'),
    databaseSourceName: string(r.databaseSourceName, 'run.databaseSourceName'),
    profileName: string(r.profileName, 'run.profileName'),
    providerType: provider(r.providerType, 'run.providerType'),
    status: status(r.status, 'run.status'),
    baseSnapshotId: nullableId(r.baseSnapshotId, 'run.baseSnapshotId'),
    snapshotId: nullableId(r.snapshotId, 'run.snapshotId'),
    differenceId: nullableId(r.differenceId, 'run.differenceId'),
    scopeGenerationId: nullableId(r.scopeGenerationId, 'run.scopeGenerationId'),
    queuedAt: string(r.queuedAt, 'run.queuedAt'),
    startedAt: nullableString(r.startedAt, 'run.startedAt'),
    completedAt: nullableString(r.completedAt, 'run.completedAt'),
    cancellationRequestedAt: nullableString(
      r.cancellationRequestedAt,
      'run.cancellationRequestedAt',
    ),
    providerVersion: nullableString(r.providerVersion, 'run.providerVersion'),
    objectCounts: r.objectCounts === null ? null : counts(r.objectCounts, 'run.objectCounts'),
    errorCode: nullableString(r.errorCode, 'run.errorCode'),
    errorSummary: nullableString(r.errorSummary, 'run.errorSummary'),
    concurrencyToken: string(r.concurrencyToken, 'run.concurrencyToken'),
  }
}
function page<T>(value: unknown, decode: (item: unknown) => T, field: string): Page<T> {
  const r = object(value, field)
  if (!Array.isArray(r.items)) throw new Error(`${field}.items must be an array`)
  return {
    items: r.items.map(decode),
    page: integer(r.page, `${field}.page`, 1),
    pageSize: integer(r.pageSize, `${field}.pageSize`, 1, 100),
    total: integer(r.total, `${field}.total`),
  }
}
export const decodeRuns = (value: unknown): Page<DiscoveryRun> => page(value, decodeRun, 'runs')
export function decodeRunFilterOptions(value: unknown): RunFilterOptions {
  const r = object(value, 'runFilterOptions')
  const options = (items: unknown, field: string): readonly RunFilterOption[] => {
    if (!Array.isArray(items)) throw new Error(`${field} must be an array`)
    return items.map((item) => {
      const value = object(item, field)
      return { id: id(value.id, `${field}.id`), name: string(value.name, `${field}.name`) }
    })
  }
  return {
    profiles: options(r.profiles, 'runFilterOptions.profiles'),
    databaseSources: options(r.databaseSources, 'runFilterOptions.databaseSources'),
  }
}
export function decodeSnapshotSummary(value: unknown): SnapshotSummary {
  const r = object(value, 'snapshot')
  if (!Array.isArray(r.capabilities)) throw new Error('snapshot.capabilities must be an array')
  return {
    id: id(r.id, 'snapshot.id'),
    runId: id(r.runId, 'snapshot.runId'),
    profileId: id(r.profileId, 'snapshot.profileId'),
    capturedAt: string(r.capturedAt, 'snapshot.capturedAt'),
    providerType: provider(r.providerType, 'snapshot.providerType'),
    providerVersion: string(r.providerVersion, 'snapshot.providerVersion'),
    currentDatabaseOrService: string(
      r.currentDatabaseOrService,
      'snapshot.currentDatabaseOrService',
    ),
    currentContainer: nullableString(r.currentContainer, 'snapshot.currentContainer'),
    formatVersion: integer(r.formatVersion, 'snapshot.formatVersion', 1),
    identityAlgorithmVersion: integer(
      r.identityAlgorithmVersion,
      'snapshot.identityAlgorithmVersion',
      1,
    ),
    scopeGenerationId: id(r.scopeGenerationId, 'snapshot.scopeGenerationId'),
    scopeFingerprint: string(r.scopeFingerprint, 'snapshot.scopeFingerprint'),
    completeness:
      r.completeness === 'Complete'
        ? 'Complete'
        : (() => {
            throw new Error('snapshot.completeness is unsupported')
          })(),
    contentSha256: string(r.contentSha256, 'snapshot.contentSha256'),
    includedSchemas: strings(r.includedSchemas, 'snapshot.includedSchemas'),
    capabilities: r.capabilities.map((item) => {
      const c = object(item, 'capability')
      return {
        name: string(c.name, 'capability.name'),
        state: capabilityState(c.state, 'capability.state'),
        reasonCode: nullableString(c.reasonCode, 'capability.reasonCode'),
      }
    }),
    counts: counts(r.counts, 'snapshot.counts'),
  }
}
export const decodeSchemas = (value: unknown): Page<SnapshotSchema> =>
  page(
    value,
    (item) => {
      const r = object(item, 'schema')
      return {
        name: string(r.name, 'schema.name'),
        logicalIdentity: string(r.logicalIdentity, 'schema.logicalIdentity'),
        objectCount: integer(r.objectCount, 'schema.objectCount'),
        sequenceCount: integer(r.sequenceCount, 'schema.sequenceCount'),
      }
    },
    'schemas',
  )
export const decodeObjects = (value: unknown): Page<SnapshotObject> =>
  page(
    value,
    (item) => {
      const r = object(item, 'object')
      return {
        logicalIdentity: string(r.logicalIdentity, 'object.logicalIdentity'),
        schemaName: string(r.schemaName, 'object.schemaName'),
        name: string(r.name, 'object.name'),
        objectType: objectType(r.objectType, 'object.objectType'),
        databaseComment: nullableString(r.databaseComment, 'object.databaseComment'),
        columnCount: integer(r.columnCount, 'object.columnCount'),
        constraintCount: integer(r.constraintCount, 'object.constraintCount'),
        indexCount: integer(r.indexCount, 'object.indexCount'),
      }
    },
    'objects',
  )
export const decodeSequences = (value: unknown): Page<SnapshotSequence> =>
  page(
    value,
    (item) => {
      const r = object(item, 'sequence')
      return {
        schemaName: string(r.schemaName, 'sequence.schemaName'),
        name: string(r.name, 'sequence.name'),
        nativeDataType: string(r.nativeDataType, 'sequence.nativeDataType'),
        incrementValue: nullableString(r.incrementValue, 'sequence.incrementValue'),
        minimumValue: nullableString(r.minimumValue, 'sequence.minimumValue'),
        maximumValue: nullableString(r.maximumValue, 'sequence.maximumValue'),
        cacheSize: nullableInteger(r.cacheSize, 'sequence.cacheSize'),
        isCyclic: nullableBoolean(r.isCyclic, 'sequence.isCyclic'),
        isOrdered: nullableBoolean(r.isOrdered, 'sequence.isOrdered'),
        startValue: nullableString(r.startValue, 'sequence.startValue'),
      }
    },
    'sequences',
  )
function decodeObjectHeaderData(value: unknown, field: string): SnapshotObjectHeaderData {
  const r = object(value, field)
  return {
    schemaName: string(r.schemaName, `${field}.schemaName`),
    name: string(r.name, `${field}.name`),
    objectType: objectType(r.objectType, `${field}.objectType`),
    databaseComment: nullableString(r.databaseComment, `${field}.databaseComment`),
    logicalIdentity: string(r.logicalIdentity, `${field}.logicalIdentity`),
  }
}
function decodeColumn(value: unknown): SnapshotColumn {
  const r = object(value, 'column')
  const dataType = object(r.nativeDataType, 'column.nativeDataType')
  return {
    name: string(r.name, 'column.name'),
    sourceOrdinal: nullableInteger(r.sourceOrdinal, 'column.sourceOrdinal'),
    nativeDataType: {
      declaration: string(dataType.declaration, 'column.nativeDataType.declaration'),
    },
    isNullable: boolean(r.isNullable, 'column.isNullable'),
    defaultExpression: nullableString(r.defaultExpression, 'column.defaultExpression'),
    databaseComment: nullableString(r.databaseComment, 'column.databaseComment'),
  }
}
function decodeIndex(value: unknown): SnapshotIndex {
  const r = object(value, 'index')
  return {
    name: string(r.name, 'index.name'),
    nativeIndexKind: string(r.nativeIndexKind, 'index.nativeIndexKind'),
    isUnique: boolean(r.isUnique, 'index.isUnique'),
    keyParts: strings(r.keyParts, 'index.keyParts'),
    nonKeyParts: strings(r.nonKeyParts, 'index.nonKeyParts'),
    nativePredicate: nullableString(r.nativePredicate, 'index.nativePredicate'),
  }
}
export function decodeObjectHeader(value: unknown): SnapshotObjectHeader {
  const r = object(value, 'objectHeader')
  return { object: decodeObjectHeaderData(r.object, 'objectHeader.object') }
}
export const decodeRecordPage = (value: unknown): Page<Record<string, unknown>> =>
  page(value, (item) => object(item, 'record'), 'records')
export const decodeConstraintPage = (value: unknown): Page<SnapshotConstraint> =>
  page(
    value,
    (item) => {
      const r = object(item, 'constraint')
      return {
        entityKind: constraintKind(r.entityKind, 'constraint.entityKind'),
        name: string(r.name, 'constraint.name'),
        columnNames: strings(r.columnNames, 'constraint.columnNames'),
        referencedObjectName: nullableString(
          r.referencedObjectName,
          'constraint.referencedObjectName',
        ),
        updateRule: nullableString(r.updateRule, 'constraint.updateRule'),
        deleteRule: nullableString(r.deleteRule, 'constraint.deleteRule'),
      }
    },
    'constraints',
  )
export function decodeObjectReview(value: unknown): SnapshotObjectReview {
  const r = object(value, 'objectReview')
  return {
    object: decodeObjectHeaderData(r.object, 'objectReview.object'),
    columns: page(r.columns, decodeColumn, 'columns'),
    constraints: decodeConstraintPage(r.constraints),
    indexes: page(r.indexes, decodeIndex, 'indexes'),
  }
}
export function decodeDifference(value: unknown): DifferenceSummary {
  const r = object(value, 'difference')
  const c = object(r.summaryCounts, 'difference.summaryCounts')
  return {
    id: id(r.id, 'difference.id'),
    profileId: id(r.profileId, 'difference.profileId'),
    baseSnapshotId: nullableId(r.baseSnapshotId, 'difference.baseSnapshotId'),
    targetSnapshotId: id(r.targetSnapshotId, 'difference.targetSnapshotId'),
    scopeGenerationId: id(r.scopeGenerationId, 'difference.scopeGenerationId'),
    algorithmVersion: integer(r.algorithmVersion, 'difference.algorithmVersion', 1),
    createdAt: string(r.createdAt, 'difference.createdAt'),
    summaryCounts: {
      added: integer(c.added, 'counts.added'),
      changed: integer(c.changed, 'counts.changed'),
      missingFromSource: integer(c.missingFromSource, 'counts.missingFromSource'),
      unchanged: integer(c.unchanged, 'counts.unchanged'),
    },
    contentSha256: string(r.contentSha256, 'difference.contentSha256'),
  }
}
export const decodeDifferenceEntries = (value: unknown): Page<DifferenceEntry> =>
  page(
    value,
    (item) => {
      const r = object(item, 'entry')
      const state = r.state
      if (
        state !== 'Added' &&
        state !== 'Changed' &&
        state !== 'MissingFromSource' &&
        state !== 'Unchanged'
      )
        throw new Error('entry.state unsupported')
      if (!Array.isArray(r.changes)) throw new Error('entry.changes must be an array')
      return {
        id: r.id === null ? null : id(r.id, 'entry.id'),
        entityKind: entityKind(r.entityKind, 'entry.entityKind'),
        logicalIdentity: string(r.logicalIdentity, 'entry.logicalIdentity'),
        parentLogicalIdentity: nullableString(
          r.parentLogicalIdentity,
          'entry.parentLogicalIdentity',
        ),
        displayName: string(r.displayName, 'entry.displayName'),
        state,
        schemaName: nullableString(r.schemaName, 'entry.schemaName'),
        objectName: nullableString(r.objectName, 'entry.objectName'),
        childName: nullableString(r.childName, 'entry.childName'),
        changes: r.changes.map((change) => {
          const c = object(change, 'entry.change')
          return {
            field: string(c.field, 'entry.change.field'),
            before: scalar(c.before, 'entry.change.before'),
            after: scalar(c.after, 'entry.change.after'),
          }
        }),
      }
    },
    'entries',
  )
