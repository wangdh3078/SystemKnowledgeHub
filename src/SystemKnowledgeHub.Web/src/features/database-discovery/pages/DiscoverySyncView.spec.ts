import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useActorStore } from '../../../app/stores/actor'
import type { AccessLevel, CurrentUserProfile } from '../../users/api/userContracts'
import type { ReconciliationPage, SyncPlan } from '../api/databaseDiscoverySyncContracts'
import { discoveryPageStubs } from '../test/discoveryPageTestSupport'
import DiscoverySyncView from './DiscoverySyncView.vue'

const messages = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), confirm: vi.fn() }))
const discoveryApi = vi.hoisted(() => ({ getRunFilterOptions: vi.fn() }))
const syncApi = vi.hoisted(() => ({
  getReconciliation: vi.fn(),
  listSyncPlans: vi.fn(),
  createSyncPlan: vi.fn(),
  previewSyncPlan: vi.fn(),
  confirmSyncPlan: vi.fn(),
  applySyncPlan: vi.fn(),
}))
vi.mock('element-plus', () => ({
  ElMessage: { success: messages.success, error: messages.error },
  ElMessageBox: { confirm: messages.confirm },
}))
vi.mock('../api/databaseDiscoveryApi', () => discoveryApi)
vi.mock('../api/databaseDiscoverySyncApi', () => syncApi)

const user: CurrentUserProfile = {
  id: 1,
  employeeNo: null,
  displayName: '同步审查人',
  email: null,
  departmentOrTeam: null,
  jobTitle: null,
  isActive: true,
  knowledgeRoles: [],
  accessLevel: 'Viewer',
  authenticationMethod: 'local',
  mustChangePassword: false,
}
const reconciliation: ReconciliationPage = {
  profileId: 1,
  profileName: 'Oracle 只读',
  databaseSourceId: 11,
  databaseSourceName: '核心 Oracle',
  providerType: 'Oracle',
  targetSnapshotId: 41,
  targetDifferenceId: 51,
  scopeGenerationId: 7,
  identityAlgorithmVersion: 1,
  page: 1,
  pageSize: 50,
  total: 2,
  items: [
    {
      key: 'CreateDatabaseObject:obj-1',
      category: 'New',
      entityKind: 'DatabaseObject',
      status: 'Applicable',
      suggestedAction: 'CreateDatabaseObject',
      blockCode: null,
      schemaLogicalIdentity: 'schema-1',
      logicalIdentity: 'obj-1',
      parentLogicalIdentity: null,
      schemaName: 'APP',
      objectName: 'CUSTOMERS',
      childName: null,
      targetId: null,
      targetConcurrencyToken: null,
      summary: '可创建新的数据库知识对象。',
    },
    {
      key: 'none:index-1',
      category: 'Unsupported',
      entityKind: 'Index',
      status: 'Unsupported',
      suggestedAction: null,
      blockCode: 'ReviewOnlyStructure',
      schemaLogicalIdentity: 'schema-1',
      logicalIdentity: 'index-1',
      parentLogicalIdentity: 'obj-1',
      schemaName: 'APP',
      objectName: 'CUSTOMERS',
      childName: 'IX_CUSTOMERS',
      targetId: null,
      targetConcurrencyToken: null,
      summary: '当前结构仅供审查。',
    },
  ],
}
const plan: SyncPlan = {
  id: 9,
  profileId: 1,
  profileName: 'Oracle 只读',
  databaseSourceId: 11,
  databaseSourceName: '核心 Oracle',
  profileConfigurationRevision: 1,
  baseSnapshotId: null,
  targetSnapshotId: 41,
  targetDifferenceId: 51,
  scopeGenerationId: 7,
  identityAlgorithmVersion: 1,
  status: 'Draft',
  actions: [{ actionType: 'CreateDatabaseObject', logicalIdentity: 'obj-1', targetId: null }],
  preview: {
    planId: 9,
    targetSnapshotId: 41,
    scopeGenerationId: 7,
    previewHash: 'a'.repeat(64),
    counts: { createObjects: 1 },
    warnings: ['索引仅供审查'],
    actions: [
      {
        actionType: 'CreateDatabaseObject',
        entityKind: 'DatabaseObject',
        schemaLogicalIdentity: 'schema-1',
        logicalIdentity: 'obj-1',
        parentLogicalIdentity: null,
        targetId: null,
        before: null,
        after: {
          schemaName: 'APP',
          name: 'CUSTOMERS',
          objectType: 'Table',
          databaseComment: null,
          primaryKeyColumns: ['ID'],
          ordinalPosition: null,
          dataType: null,
          isNullable: null,
          defaultValue: null,
        },
        summary: '创建数据库对象',
      },
    ],
  },
  confirmedPreviewHash: null,
  createdAt: '2026-09-01T00:00:00Z',
  updatedAt: '2026-09-01T00:00:00Z',
  confirmedAt: null,
  appliedAt: null,
  result: null,
  concurrencyToken: 'v1_token',
}
let wrapper: VueWrapper | undefined

function mountAt(accessLevel: AccessLevel): VueWrapper {
  const pinia = createPinia()
  setActivePinia(pinia)
  const actor = useActorStore()
  actor.currentUser = { ...user, accessLevel }
  actor.authStatus = 'authenticated'
  actor.initialized = true
  wrapper = mount(DiscoverySyncView, {
    global: {
      plugins: [pinia],
      stubs: {
        ...discoveryPageStubs,
        EmptyState: {
          props: ['title', 'description'],
          template: '<section>{{ title }} {{ description }}</section>',
        },
        ErrorState: { props: ['title', 'message'], template: '<p>{{ title }} {{ message }}</p>' },
        LoadingState: { props: ['message'], template: '<p>{{ message }}</p>' },
      },
    },
  })
  return wrapper
}

beforeEach(() => {
  vi.clearAllMocks()
  discoveryApi.getRunFilterOptions.mockResolvedValue({
    profiles: [{ id: 1, name: 'Oracle 只读' }],
    databaseSources: [{ id: 11, name: '核心 Oracle' }],
  })
  syncApi.getReconciliation.mockResolvedValue(reconciliation)
  syncApi.listSyncPlans.mockResolvedValue({ items: [plan], page: 1, pageSize: 20, total: 1 })
  syncApi.createSyncPlan.mockResolvedValue(plan)
  syncApi.previewSyncPlan.mockResolvedValue(plan)
  syncApi.confirmSyncPlan.mockResolvedValue({
    ...plan,
    status: 'Ready',
    confirmedPreviewHash: plan.preview!.previewHash,
  })
  syncApi.applySyncPlan.mockResolvedValue({
    ...plan,
    status: 'Applied',
    appliedAt: '2026-09-01T00:02:00Z',
    result: {
      createdObjects: 1,
      linkedObjects: 0,
      createdColumns: 2,
      linkedColumns: 0,
      updatedObjects: 0,
      updatedColumns: 0,
      markedMissing: 0,
      clearedMissing: 0,
      appliedAt: '2026-09-01T00:02:00Z',
      appliedByDisplayName: '同步审查人',
    },
  })
})
afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
})

describe('DiscoverySyncView', () => {
  it('keeps Viewer read-only while exposing reconciliation and plan review', async () => {
    const view = mountAt('Viewer')
    await flushPromises()
    expect(view.text()).toContain('手工同步')
    expect(view.text()).toContain('APP.CUSTOMERS')
    expect(view.text()).toContain('仅审查')
    expect(view.text()).not.toContain('生成计划并预览')
    await view
      .findAll('button')
      .find((item) => item.text() === '查看计划')!
      .trigger('click')
    expect(view.text()).toContain('PreviewHash')
    expect(view.text()).not.toContain('确认当前预览')
    expect(view.text()).not.toContain('应用已确认计划')
  })

  it.each<AccessLevel>(['Editor', 'Administrator'])(
    'allows %s to select and run the explicit preview/confirm flow',
    async (accessLevel) => {
      const view = mountAt(accessLevel)
      await flushPromises()
      expect(view.text()).toContain('生成计划并预览')
      expect(view.text()).not.toContain('测试连接')
      expect(view.text()).not.toContain('开始发现')
      await view
        .findAll('button')
        .find((item) => item.text() === '查看计划')!
        .trigger('click')
      expect(view.text()).toContain('我已核对当前 PreviewHash')
      expect(view.text()).toContain('确认当前预览')
      await view.find('input[type="checkbox"]').setValue(true)
      await view
        .findAll('button')
        .find((item) => item.text() === '确认当前预览')!
        .trigger('click')
      await flushPromises()
      expect(syncApi.confirmSyncPlan).toHaveBeenCalledOnce()
    },
  )

  it('supports server-side category filters and renders applied result navigation', async () => {
    const applied = await syncApi.applySyncPlan(plan)
    syncApi.listSyncPlans.mockResolvedValue({ items: [applied], page: 1, pageSize: 20, total: 1 })
    const view = mountAt('Editor')
    await flushPromises()
    await view.find('[data-option-value="Unsupported"]').trigger('click')
    await flushPromises()
    expect(syncApi.getReconciliation).toHaveBeenLastCalledWith(
      1,
      'Unsupported',
      '',
      1,
      expect.any(AbortSignal),
    )
    await view
      .findAll('button')
      .find((item) => item.text() === '查看计划')!
      .trigger('click')
    expect(view.text()).toContain('创建对象')
    expect(view.text()).toContain('创建字段')
    expect(view.find('a[href="/database-objects?databaseSourceId=11"]').text()).toBe(
      '查看数据库对象',
    )
  })

  it('shows superseded plans as non-applicable history without write controls', async () => {
    syncApi.listSyncPlans.mockResolvedValueOnce({
      items: [{ ...plan, status: 'Superseded' }],
      page: 1,
      pageSize: 20,
      total: 1,
    })
    const view = mountAt('Editor')
    await flushPromises()
    await view
      .findAll('button')
      .find((item) => item.text() === '查看计划')!
      .trigger('click')
    expect(view.text()).toContain('Superseded')
    expect(view.text()).not.toContain('确认当前预览')
    expect(view.text()).not.toContain('应用已确认计划')
  })
})
