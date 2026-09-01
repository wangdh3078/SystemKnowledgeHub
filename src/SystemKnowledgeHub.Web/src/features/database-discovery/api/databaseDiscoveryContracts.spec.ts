import { describe, expect, it } from 'vitest'
import {
  decodeDifference,
  decodeDifferenceHistory,
  decodeDifferenceEntries,
  databaseProviderDefaultPort,
  databaseProviderEngine,
  databaseProviderLabel,
  decodeObjectReview,
  decodeObjects,
  decodeProfile,
  decodeRun,
  decodeSequences,
  decodeSnapshotSummary,
  decodeSnapshotHistory,
} from './databaseDiscoveryContracts'

const counts = () => ({
  schemas: 1,
  objects: 2,
  columns: 4,
  primaryKeys: 2,
  foreignKeys: 1,
  uniqueConstraints: 1,
  indexes: 1,
  sequences: 1,
  foreignKeyReferenceStubs: 0,
})

const validProfile = () => ({
  id: 1,
  databaseSourceId: 2,
  databaseSourceName: '应用数据库',
  name: 'PG',
  providerType: 'PostgreSql',
  host: 'db.internal',
  port: 5432,
  databaseName: 'app',
  serviceName: null,
  username: 'reader',
  includedSchemas: ['public'],
  isEnabled: true,
  connectionStatus: 'Succeeded',
  hasSecret: true,
  lastConnectionTestAt: null,
  lastConnectionTestErrorCode: null,
  lastConnectionTestSummary: null,
  lastDiscoveryAt: null,
  lastSuccessfulDiscoveryAt: null,
  concurrencyToken: 'profile-token',
  password: 'decoder-secret-canary',
  protectedPayload: 'ciphertext-canary',
  secretReference: 'secret-reference-canary',
})

const validRun = () => ({
  id: 11,
  profileId: 1,
  databaseSourceId: 2,
  databaseSourceName: '应用数据库',
  profileName: 'PG',
  providerType: 'PostgreSql',
  status: 'Succeeded',
  baseSnapshotId: 8,
  snapshotId: 9,
  differenceId: 10,
  scopeGenerationId: 7,
  queuedAt: '2026-08-30T00:00:00Z',
  startedAt: '2026-08-30T00:00:01Z',
  completedAt: '2026-08-30T00:00:02Z',
  cancellationRequestedAt: null,
  providerVersion: '18.0',
  objectCounts: counts(),
  errorCode: null,
  errorSummary: null,
  concurrencyToken: 'run-token',
  host: 'must-not-cross-run-read-model',
  username: 'must-not-cross-run-read-model',
  connectionString: 'must-not-cross-run-read-model',
  secretReference: 'must-not-cross-run-read-model',
})

const validSnapshotSummary = () => ({
  id: 9,
  runId: 11,
  profileId: 1,
  capturedAt: '2026-08-30T00:00:02Z',
  providerType: 'PostgreSql',
  providerVersion: '18.0',
  currentDatabaseOrService: 'app',
  currentContainer: null,
  formatVersion: 1,
  identityAlgorithmVersion: 1,
  scopeGenerationId: 7,
  scopeFingerprint: 'scope-fingerprint',
  completeness: 'Complete',
  contentSha256: 'a'.repeat(64),
  includedSchemas: ['public'],
  capabilities: [
    { name: 'SupportsSequences', state: 'Supported', reasonCode: null },
    { name: 'SupportsInvisibleColumns', state: 'NotSupported', reasonCode: 'ProviderScope' },
  ],
  counts: counts(),
  content: { mustNotBeProjected: true },
  host: 'must-not-cross-snapshot-read-model',
  username: 'must-not-cross-snapshot-read-model',
  secretReference: 'must-not-cross-snapshot-read-model',
})

const validDifferenceSummary = () => ({
  id: 10,
  profileId: 1,
  baseSnapshotId: 8,
  targetSnapshotId: 9,
  scopeGenerationId: 7,
  algorithmVersion: 1,
  createdAt: '2026-08-30T00:00:03Z',
  summaryCounts: { added: 1, changed: 1, missingFromSource: 1, unchanged: 1 },
  contentSha256: 'b'.repeat(64),
  host: 'must-not-cross-difference-read-model',
  username: 'must-not-cross-difference-read-model',
  secretReference: 'must-not-cross-difference-read-model',
})

const validSnapshotHistory = () => ({
  items: [
    {
      id: 9,
      runId: 11,
      profileId: 1,
      profileName: 'PG',
      databaseSourceId: 2,
      databaseSourceName: '应用数据库',
      providerType: 'PostgreSql',
      capturedAt: '2026-08-30T00:00:02Z',
      includedSchemas: ['public'],
      scopeGenerationId: 7,
      baseSnapshotId: 8,
      differenceId: 10,
      counts: counts(),
      canonicalContentJson: 'must-not-cross-history-read-model',
    },
  ],
  page: 1,
  pageSize: 20,
  total: 1,
})

const validDifferenceHistory = () => ({
  items: [
    {
      id: 10,
      profileId: 1,
      profileName: 'PG',
      databaseSourceId: 2,
      databaseSourceName: '应用数据库',
      providerType: 'PostgreSql',
      baseSnapshotId: 8,
      targetSnapshotId: 9,
      createdAt: '2026-08-30T00:00:03Z',
      summaryCounts: { added: 1, changed: 1, missingFromSource: 1, unchanged: 1 },
      canonicalContentJson: 'must-not-cross-history-read-model',
    },
  ],
  page: 1,
  pageSize: 20,
  total: 1,
})

const validObjectPage = () => ({
  items: [
    {
      logicalIdentity: 'Object:public:CUSTOMERS',
      schemaName: 'public',
      name: 'CUSTOMERS',
      objectType: 'Table',
      databaseComment: 'Customer master',
      columnCount: 2,
      constraintCount: 2,
      indexCount: 1,
    },
  ],
  page: 1,
  pageSize: 50,
  total: 1,
})

const validDifferenceEntryPage = () => ({
  items: [
    {
      id: 21,
      entityKind: 'Column',
      logicalIdentity: 'Column:Object:public:CUSTOMERS:NAME',
      parentLogicalIdentity: 'Object:public:CUSTOMERS',
      displayName: 'public.CUSTOMERS.NAME',
      state: 'Changed',
      schemaName: 'public',
      objectName: 'CUSTOMERS',
      childName: 'NAME',
      changes: [
        { field: 'nativeDataType', before: 'varchar(100)', after: 'varchar(200)' },
        { field: 'isNullable', before: false, after: true },
        { field: 'sourceOrdinal', before: 1, after: null },
      ],
    },
  ],
  page: 1,
  pageSize: 50,
  total: 1,
})

const validSequencePage = () => ({
  items: [
    {
      schemaName: 'public',
      name: 'CUSTOMERS_SEQ',
      nativeDataType: 'bigint',
      incrementValue: '1',
      minimumValue: '1',
      maximumValue: null,
      cacheSize: 20,
      isCyclic: false,
      isOrdered: false,
      startValue: '1',
    },
  ],
  page: 1,
  pageSize: 50,
  total: 1,
})

const validObjectReview = () => ({
  object: {
    schemaName: 'public',
    name: 'CUSTOMERS',
    objectType: 'Table',
    databaseComment: 'Customer master',
    logicalIdentity: 'DatabaseObject:public:CUSTOMERS',
  },
  columns: {
    items: [
      {
        name: 'ID',
        sourceOrdinal: 1,
        nativeDataType: { declaration: 'bigint' },
        isNullable: false,
        defaultExpression: null,
        databaseComment: null,
      },
    ],
    page: 1,
    pageSize: 50,
    total: 1,
  },
  constraints: {
    items: [
      {
        entityKind: 'PrimaryKey',
        name: 'PK_CUSTOMERS',
        columnNames: ['ID'],
        referencedObjectName: null,
        updateRule: null,
        deleteRule: null,
      },
    ],
    page: 1,
    pageSize: 50,
    total: 1,
  },
  indexes: {
    items: [
      {
        name: 'IX_CUSTOMERS_ID',
        nativeIndexKind: 'btree',
        isUnique: true,
        keyParts: ['ID'],
        nonKeyParts: [],
        nativePredicate: null,
      },
    ],
    page: 1,
    pageSize: 50,
    total: 1,
  },
})

describe('database discovery provider-neutral decoders', () => {
  it.each([
    ['Oracle', 'Oracle', 'Oracle', 1521],
    ['PostgreSql', 'PostgreSQL', 'PostgreSQL', 5432],
    ['SqlServer', 'SQL Server', 'SQL Server', 1433],
  ] as const)(
    'accepts and labels the %s provider without changing the wire value',
    (providerType, label, engine, defaultPort) => {
      expect(decodeProfile({ ...validProfile(), providerType }).providerType).toBe(providerType)
      expect(databaseProviderLabel(providerType)).toBe(label)
      expect(databaseProviderEngine(providerType)).toBe(engine)
      expect(databaseProviderDefaultPort(providerType)).toBe(defaultPort)
    },
  )

  it('decodes complete legal profile, run, snapshot, object, and difference fixtures', () => {
    const profile = decodeProfile(validProfile())
    const run = decodeRun(validRun())
    const summary = decodeSnapshotSummary(validSnapshotSummary())
    const snapshotHistory = decodeSnapshotHistory(validSnapshotHistory())
    const objects = decodeObjects(validObjectPage())
    const sequences = decodeSequences(validSequencePage())
    const review = decodeObjectReview(validObjectReview())
    const difference = decodeDifference(validDifferenceSummary())
    const differenceHistory = decodeDifferenceHistory(validDifferenceHistory())
    const entries = decodeDifferenceEntries(validDifferenceEntryPage())

    expect(profile).toMatchObject({
      databaseSourceName: '应用数据库',
      providerType: 'PostgreSql',
      connectionStatus: 'Succeeded',
    })
    expect(profile).not.toHaveProperty('password')
    expect(profile).not.toHaveProperty('protectedPayload')
    expect(profile).not.toHaveProperty('secretReference')
    expect(run).toMatchObject({ status: 'Succeeded', snapshotId: 9, differenceId: 10 })
    expect(run).not.toHaveProperty('host')
    expect(run).not.toHaveProperty('username')
    expect(run).not.toHaveProperty('connectionString')
    expect(run).not.toHaveProperty('secretReference')
    expect(summary).toMatchObject({ completeness: 'Complete', counts: { objects: 2 } })
    expect(summary.capabilities.map((item) => item.state)).toEqual(['Supported', 'NotSupported'])
    expect(summary).not.toHaveProperty('content')
    expect(summary).not.toHaveProperty('host')
    expect(summary).not.toHaveProperty('username')
    expect(summary).not.toHaveProperty('secretReference')
    expect(snapshotHistory.items[0]).toMatchObject({
      providerType: 'PostgreSql',
      differenceId: 10,
      counts: { objects: 2, columns: 4 },
    })
    expect(snapshotHistory.items[0]).not.toHaveProperty('canonicalContentJson')
    expect(objects.items[0]).toMatchObject({ objectType: 'Table', name: 'CUSTOMERS' })
    expect(sequences.items[0]).toMatchObject({ name: 'CUSTOMERS_SEQ', incrementValue: '1' })
    expect(review.object).toMatchObject({ objectType: 'Table', name: 'CUSTOMERS' })
    expect(review.constraints.items[0]).toMatchObject({
      entityKind: 'PrimaryKey',
      name: 'PK_CUSTOMERS',
    })
    expect(difference).toMatchObject({ targetSnapshotId: 9, algorithmVersion: 1 })
    expect(difference).not.toHaveProperty('host')
    expect(difference).not.toHaveProperty('username')
    expect(difference).not.toHaveProperty('secretReference')
    expect(differenceHistory.items[0]).toMatchObject({
      providerType: 'PostgreSql',
      targetSnapshotId: 9,
      summaryCounts: { added: 1, changed: 1, missingFromSource: 1, unchanged: 1 },
    })
    expect(differenceHistory.items[0]).not.toHaveProperty('canonicalContentJson')
    expect(entries.items[0]).toMatchObject({
      entityKind: 'Column',
      state: 'Changed',
      schemaName: 'public',
      objectName: 'CUSTOMERS',
      childName: 'NAME',
    })
    expect(entries.items[0]).not.toHaveProperty('before')
    expect(entries.items[0]).not.toHaveProperty('after')
    expect(entries.items[0].changes).toEqual([
      { field: 'nativeDataType', before: 'varchar(100)', after: 'varchar(200)' },
      { field: 'isNullable', before: false, after: true },
      { field: 'sourceOrdinal', before: 1, after: null },
    ])
  })

  it.each([
    ['provider type', () => decodeProfile({ ...validProfile(), providerType: 'MySql' })],
    [
      'connection status',
      () => decodeProfile({ ...validProfile(), connectionStatus: 'PartiallySucceeded' }),
    ],
    ['run status', () => decodeRun({ ...validRun(), status: 'Paused' })],
    [
      'snapshot completeness',
      () => decodeSnapshotSummary({ ...validSnapshotSummary(), completeness: 'Partial' }),
    ],
    [
      'capability state',
      () =>
        decodeSnapshotSummary({
          ...validSnapshotSummary(),
          capabilities: [
            { ...validSnapshotSummary().capabilities[0], state: 'PartiallySupported' },
          ],
        }),
    ],
    [
      'object type',
      () =>
        decodeObjects({
          ...validObjectPage(),
          items: [{ ...validObjectPage().items[0], objectType: 'MaterializedView' }],
        }),
    ],
    [
      'difference entity kind',
      () =>
        decodeDifferenceEntries({
          ...validDifferenceEntryPage(),
          items: [{ ...validDifferenceEntryPage().items[0], entityKind: 'Object' }],
        }),
    ],
    [
      'constraint entity kind',
      () =>
        decodeObjectReview({
          ...validObjectReview(),
          constraints: {
            ...validObjectReview().constraints,
            items: [{ ...validObjectReview().constraints.items[0], entityKind: 'Index' }],
          },
        }),
    ],
    [
      'difference state',
      () =>
        decodeDifferenceEntries({
          ...validDifferenceEntryPage(),
          items: [{ ...validDifferenceEntryPage().items[0], state: 'Renamed' }],
        }),
    ],
  ])('fails closed when one otherwise-valid %s value leaves its closed set', (_name, decode) => {
    expect(decode).toThrow(/unsupported/)
  })

  it.each([
    ['page zero', { page: 0 }],
    ['page size above the bounded contract', { pageSize: 101 }],
    ['negative total', { total: -1 }],
  ])('fails closed for %s pagination metadata', (_name, mutation) => {
    expect(() => decodeObjects({ ...validObjectPage(), ...mutation })).toThrow()
  })

  it.each([
    [
      'object before value',
      { field: 'nativeDataType', before: { declaration: 'text' }, after: 'text' },
    ],
    ['array after value', { field: 'columnNames', before: 'ID', after: ['ID', 'TenantID'] }],
    ['missing before value', { field: 'nativeDataType', after: 'text' }],
    ['missing after value', { field: 'nativeDataType', before: 'varchar(100)' }],
  ])('fails closed for %s in a field change', (_name, invalidChange) => {
    const fixture = validDifferenceEntryPage()
    expect(() =>
      decodeDifferenceEntries({
        ...fixture,
        items: [{ ...fixture.items[0], changes: [invalidChange] }],
      }),
    ).toThrow(/scalar or null/)
  })
})
