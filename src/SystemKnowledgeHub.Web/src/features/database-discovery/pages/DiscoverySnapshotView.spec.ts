import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type {
  Page,
  SnapshotObject,
  SnapshotObjectReview,
  SnapshotSchema,
  SnapshotSequence,
  SnapshotSummary,
} from '../api/databaseDiscoveryContracts'
import { discoveryPageStubs } from '../test/discoveryPageTestSupport'
import DiscoverySnapshotView from './DiscoverySnapshotView.vue'

const router = vi.hoisted(() => ({ push: vi.fn() }))
const route = vi.hoisted(() => ({
  params: { id: '42' },
  query: { differenceId: '77' } as Record<string, string>,
}))
const messages = vi.hoisted(() => ({ error: vi.fn() }))
const api = vi.hoisted(() => ({
  getSnapshotObjectReview: vi.fn(),
  getSnapshotObjects: vi.fn(),
  getSnapshotSchemas: vi.fn(),
  getSnapshotSequences: vi.fn(),
  getSnapshotSummary: vi.fn(),
}))

vi.mock('vue-router', () => ({
  useRoute: () => route,
  useRouter: () => router,
}))
vi.mock('element-plus', () => ({ ElMessage: { error: messages.error } }))
vi.mock('../api/databaseDiscoveryApi', () => api)

const counts = {
  schemas: 1,
  objects: 1,
  columns: 2,
  primaryKeys: 1,
  foreignKeys: 0,
  uniqueConstraints: 0,
  indexes: 1,
  sequences: 1,
  foreignKeyReferenceStubs: 0,
}
const summary: SnapshotSummary = {
  id: 42,
  runId: 11,
  profileId: 3,
  capturedAt: '2026-08-30T00:00:00Z',
  providerType: 'PostgreSql',
  providerVersion: '18.0',
  currentDatabaseOrService: 'orders',
  currentContainer: null,
  formatVersion: 1,
  identityAlgorithmVersion: 1,
  scopeGenerationId: 2,
  scopeFingerprint: 'scope-fingerprint',
  completeness: 'Complete',
  contentSha256: 'a'.repeat(64),
  includedSchemas: ['SalesOps'],
  capabilities: [
    { name: 'SupportsSequences', state: 'Supported', reasonCode: null },
    { name: 'SupportsInvisibleColumns', state: 'NotSupported', reasonCode: 'ProviderScope' },
  ],
  counts,
}
const schema: SnapshotSchema = {
  name: 'SalesOps',
  logicalIdentity: 'Schema:SalesOps',
  objectCount: 1,
  sequenceCount: 1,
}
const object: SnapshotObject = {
  logicalIdentity: 'DatabaseObject:SalesOps:CustomerProfile',
  schemaName: 'SalesOps',
  name: 'CustomerProfile',
  objectType: 'Table',
  databaseComment: '客户主数据',
  columnCount: 2,
  constraintCount: 1,
  indexCount: 1,
}
const review: SnapshotObjectReview = {
  object: {
    schemaName: 'SalesOps',
    name: 'CustomerProfile',
    objectType: 'Table',
    databaseComment: '客户主数据',
    logicalIdentity: object.logicalIdentity,
  },
  columns: {
    items: [
      {
        sourceOrdinal: 1,
        name: 'customerID',
        nativeDataType: { declaration: 'varchar(200)' },
        isNullable: false,
        defaultExpression: null,
        databaseComment: '客户名称',
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
        name: 'Pk_CustomerProfile',
        columnNames: ['customerID'],
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
        name: 'Ix_CustomerProfile_customerID',
        nativeIndexKind: 'btree',
        isUnique: false,
        keyParts: ['customerID'],
        nonKeyParts: [],
        nativePredicate: null,
      },
    ],
    page: 1,
    pageSize: 50,
    total: 1,
  },
}
const sequence: SnapshotSequence = {
  schemaName: 'SalesOps',
  name: 'CustomerSequence',
  nativeDataType: 'bigint',
  incrementValue: '1',
  minimumValue: '1',
  maximumValue: null,
  cacheSize: 20,
  isCyclic: false,
  isOrdered: false,
  startValue: '1',
}

const page = <T>(items: readonly T[]): Page<T> => ({
  items,
  page: 1,
  pageSize: 50,
  total: items.length,
})

describe('DiscoverySnapshotView', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    api.getSnapshotSummary.mockResolvedValue(summary)
    api.getSnapshotSchemas.mockResolvedValue(page([schema]))
    api.getSnapshotObjects.mockResolvedValue(page([object]))
    api.getSnapshotSequences.mockResolvedValue(page([sequence]))
    api.getSnapshotObjectReview.mockResolvedValue(review)
  })

  it('loads all bounded snapshot surfaces, exposes capability warnings, and fetches detail on demand', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const wrapper = mount(DiscoverySnapshotView, {
      global: {
        plugins: [pinia],
        stubs: {
          ...discoveryPageStubs,
          Teleport: true,
          EmptyState: { props: ['title'], template: '<p>{{ title }}</p>' },
          ErrorState: { props: ['title', 'message'], template: '<p>{{ title }} {{ message }}</p>' },
          LoadingState: { props: ['message'], template: '<p>{{ message }}</p>' },
        },
      },
    })
    await flushPromises()

    expect(api.getSnapshotSummary).toHaveBeenCalledWith(42, expect.any(AbortSignal))
    expect(api.getSnapshotSchemas).toHaveBeenCalledWith(42, 1, '', expect.any(AbortSignal))
    expect(api.getSnapshotObjects).toHaveBeenCalledWith(42, 1, '', '', '', expect.any(AbortSignal))
    expect(api.getSnapshotSequences).toHaveBeenCalledWith(42, 1, '', '', expect.any(AbortSignal))
    expect(api.getSnapshotObjectReview).not.toHaveBeenCalled()

    expect(wrapper.text()).toContain('可见性提示')
    expect(wrapper.text()).toContain('SupportsSequences · 支持')
    expect(wrapper.text()).toContain('SupportsInvisibleColumns · 不支持（ProviderScope）')
    expect(wrapper.text()).toContain('SalesOps')
    expect(wrapper.text()).toContain('CustomerProfile')
    expect(wrapper.text()).toContain('CustomerSequence')

    const detailButton = wrapper.findAll('button').find((button) => button.text() === '查看结构')
    expect(detailButton).toBeDefined()
    await detailButton!.trigger('click')
    await flushPromises()

    expect(api.getSnapshotObjectReview).toHaveBeenCalledWith(
      42,
      object.logicalIdentity,
      1,
      1,
      1,
      expect.any(AbortSignal),
    )
    expect(wrapper.text()).toContain('SalesOps.CustomerProfile')
    expect(wrapper.text()).toContain('customerID')
    expect(wrapper.text()).toContain('varchar(200)')
    expect(wrapper.text()).toContain('Pk_CustomerProfile')
    expect(wrapper.text()).toContain('Ix_CustomerProfile_customerID')
    expect(
      [
        api.getSnapshotSummary,
        api.getSnapshotSchemas,
        api.getSnapshotObjects,
        api.getSnapshotSequences,
        api.getSnapshotObjectReview,
      ].every((call) => call.mock.calls.length === 1),
    ).toBe(true)

    wrapper.unmount()
  })
})
