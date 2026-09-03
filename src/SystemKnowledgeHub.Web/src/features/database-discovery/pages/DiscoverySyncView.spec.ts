import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import type { AccessLevel, CurrentUserProfile } from '../../users/api/userContracts'
import type {
  ReconciliationCandidate,
  ReconciliationChild,
  ReconciliationObjectChildrenPage,
  ReconciliationObjectGroup,
  ReconciliationObjectGroupPage,
  SyncPlan,
  SyncSelection,
} from '../api/databaseDiscoverySyncContracts'
import { discoveryPageStubs } from '../test/discoveryPageTestSupport'
import DiscoverySyncView from './DiscoverySyncView.vue'

const messages = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), confirm: vi.fn() }))
const discoveryApi = vi.hoisted(() => ({ getRunFilterOptions: vi.fn() }))
const syncApi = vi.hoisted(() => ({
  queryReconciliationObjectGroups: vi.fn(),
  queryReconciliationObjectChildren: vi.fn(),
  setWholeObjectSelection: vi.fn(),
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

const candidate = (
  action: ReconciliationCandidate['suggestedAction'],
  logicalIdentity: string,
  childName: string | null,
  status: ReconciliationCandidate['status'] = 'Applicable',
  blockCode: string | null = null,
): ReconciliationCandidate => ({
  key: `${action ?? 'none'}:${logicalIdentity}`,
  category: status === 'Applicable' ? 'New' : status === 'Conflict' ? 'Conflict' : 'Unsupported',
  entityKind: childName === null ? 'DatabaseObject' : 'Column',
  status,
  suggestedAction: action,
  blockCode,
  schemaLogicalIdentity: 'schema-1',
  logicalIdentity,
  parentLogicalIdentity: childName === null ? null : 'obj-a',
  schemaName: 'APP',
  objectName: 'CUSTOMERS',
  childName,
  targetId: null,
  targetConcurrencyToken: null,
  summary: status === 'Applicable' ? '可加入同步计划。' : '当前项不可选择。',
})

const objectAction = candidate('CreateDatabaseObject', 'obj-a', null)
const columnActions = [
  candidate('CreateDatabaseColumn', 'col-a-1', 'ID'),
  candidate('CreateDatabaseColumn', 'col-a-2', 'CODE'),
  candidate('CreateDatabaseColumn', 'col-a-3', 'DISPLAY_NAME'),
]
const requiredParent: SyncSelection = {
  actionType: 'CreateDatabaseObject',
  logicalIdentity: 'obj-a',
  targetId: null,
}
const selectionFor = (item: ReconciliationCandidate): SyncSelection => ({
  actionType: item.suggestedAction!,
  logicalIdentity: item.logicalIdentity,
  targetId: item.targetId,
})
const selectionKey = (item: SyncSelection): string =>
  `${item.actionType}\u001f${item.logicalIdentity}\u001f${item.targetId ?? ''}`
const selectedCountFor = (
  candidates: readonly ReconciliationCandidate[],
  selected: readonly SyncSelection[],
): number => {
  const keys = new Set(selected.map(selectionKey))
  return candidates.map(selectionFor).filter((item) => keys.has(selectionKey(item))).length
}

const baseGroups: readonly ReconciliationObjectGroup[] = [
  {
    key: 'object:obj-a',
    schemaLogicalIdentity: 'schema-1',
    objectLogicalIdentity: 'obj-a',
    schemaName: 'APP',
    objectName: 'CUSTOMERS',
    objectType: 'Table',
    targetId: null,
    status: 'Applicable',
    objectCandidates: [objectAction],
    requiredParentAction: requiredParent,
    totalColumnCount: 3,
    selectableColumnCount: 3,
    totalChildCount: 4,
    selectableCount: 4,
    selectedCount: 0,
    conflictCount: 0,
    unsupportedCount: 1,
    noActionCount: 0,
    summary: '部分可处理，共 4 个类型化操作可加入计划。',
  },
  {
    key: 'object:obj-b',
    schemaLogicalIdentity: 'schema-1',
    objectLogicalIdentity: 'obj-b',
    schemaName: 'APP',
    objectName: 'ORDERS',
    objectType: 'Table',
    targetId: 71,
    status: 'Applicable',
    objectCandidates: [
      {
        ...candidate(null, 'obj-b', null, 'NoAction'),
        objectName: 'ORDERS',
        targetId: 71,
      },
    ],
    requiredParentAction: null,
    totalColumnCount: 3,
    selectableColumnCount: 1,
    totalChildCount: 3,
    selectableCount: 1,
    selectedCount: 0,
    conflictCount: 1,
    unsupportedCount: 0,
    noActionCount: 1,
    summary: '部分可处理，共 1 个类型化操作可加入计划。',
  },
  {
    key: 'object:obj-c',
    schemaLogicalIdentity: 'schema-1',
    objectLogicalIdentity: 'obj-c',
    schemaName: 'APP',
    objectName: 'AUDIT_LOG',
    objectType: 'Table',
    targetId: 72,
    status: 'Conflict',
    objectCandidates: [
      {
        ...candidate(null, 'obj-c', null, 'Conflict', 'UnsupportedIdentifierCollision'),
        objectName: 'AUDIT_LOG',
        targetId: 72,
      },
    ],
    requiredParentAction: null,
    totalColumnCount: 1,
    selectableColumnCount: 0,
    totalChildCount: 2,
    selectableCount: 0,
    selectedCount: 0,
    conflictCount: 1,
    unsupportedCount: 1,
    noActionCount: 0,
    summary: '当前对象只有冲突项，不能加入同步计划。',
  },
]

const groupPage = (selected: readonly SyncSelection[] = []): ReconciliationObjectGroupPage => ({
  profileId: 1,
  profileName: 'Oracle 只读',
  databaseSourceId: 11,
  databaseSourceName: '核心 Oracle',
  providerType: 'Oracle',
  targetSnapshotId: 41,
  targetDifferenceId: 51,
  scopeGenerationId: 7,
  identityAlgorithmVersion: 1,
  maximumSyncPlanActions: 2000,
  ungroupedReviewOnlyCount: 1,
  page: 1,
  pageSize: 50,
  total: 3,
  items: baseGroups.map((group) => {
    const candidates =
      group.objectLogicalIdentity === 'obj-a'
        ? [objectAction, ...columnActions]
        : group.objectCandidates.filter((item) => item.suggestedAction !== null)
    return { ...group, selectedCount: selectedCountFor(candidates, selected) }
  }),
})

const child = (
  item: ReconciliationCandidate,
  selected: readonly SyncSelection[],
): ReconciliationChild => ({
  key: `Column:${item.logicalIdentity}`,
  entityKind: 'Column',
  logicalIdentity: item.logicalIdentity,
  name: item.childName,
  status: item.status,
  candidates: [item],
  selectableCount: item.suggestedAction === null ? 0 : 1,
  selectedCount: item.suggestedAction === null ? 0 : selectedCountFor([item], selected),
  blockCodes: item.blockCode ? [item.blockCode] : [],
  summary: item.summary,
})
const childPage = (
  page: number,
  selected: readonly SyncSelection[] = [],
): ReconciliationObjectChildrenPage => {
  const pageItems = page === 1 ? columnActions.slice(0, 2) : columnActions.slice(2)
  return {
    profileId: 1,
    targetSnapshotId: 41,
    objectLogicalIdentity: 'obj-a',
    items: pageItems.map((item) => child(item, selected)),
    page,
    pageSize: 50,
    total: 51,
  }
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
  actions: [requiredParent],
  preview: {
    planId: 9,
    targetSnapshotId: 41,
    scopeGenerationId: 7,
    previewHash: 'a'.repeat(64),
    counts: { createObjects: 1, createColumns: 3 },
    warnings: ['索引仅供审查'],
    actions: [
      {
        actionType: 'CreateDatabaseObject',
        entityKind: 'DatabaseObject',
        schemaLogicalIdentity: 'schema-1',
        logicalIdentity: 'obj-a',
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
        objectSchemaName: 'APP',
        objectName: 'CUSTOMERS',
        objectType: 'Table',
        objectDatabaseComment: null,
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
        Teleport: true,
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
  syncApi.queryReconciliationObjectGroups.mockImplementation(
    (_profileId, _snapshotId, _category, _search, _page, _pageSize, selected) =>
      Promise.resolve(groupPage(selected)),
  )
  syncApi.queryReconciliationObjectChildren.mockImplementation(
    (_profileId, _snapshotId, _identity, _category, _search, page, _pageSize, selected) =>
      Promise.resolve(childPage(page, selected)),
  )
  syncApi.setWholeObjectSelection.mockImplementation(
    (_profileId, _snapshotId, identity, checked, current: readonly SyncSelection[]) => {
      const next = new Map(current.map((item) => [selectionKey(item), item]))
      if (identity === 'obj-a') {
        for (const item of [requiredParent, ...columnActions.map(selectionFor)]) {
          if (checked) next.set(selectionKey(item), item)
          else next.delete(selectionKey(item))
        }
      }
      const actions = [...next.values()]
      return Promise.resolve({
        actions,
        selectedCount: actions.length,
        maximumSyncPlanActions: 2000,
        objectSelectableCount: 4,
        objectSelectedCount: checked ? 4 : 0,
      })
    },
  )
  syncApi.listSyncPlans.mockResolvedValue({ items: [plan], page: 1, pageSize: 20, total: 1 })
  syncApi.createSyncPlan.mockImplementation((_profileId, _snapshotId, actions) =>
    Promise.resolve({ ...plan, actions }),
  )
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
      createdColumns: 3,
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

describe('DiscoverySyncView object-group reconciliation', () => {
  it('keeps Viewer read-only while allowing lazy object expansion', async () => {
    const view = mountAt('Viewer')
    await flushPromises()
    expect(view.text()).toContain('APP.CUSTOMERS')
    expect(view.text()).toContain('3/3 可处理')
    expect(view.find('[role="treegrid"]').exists()).toBe(true)
    expect(view.find('.discovery-object-groups__header').text()).toContain('冲突/仅审查')
    expect(view.find('.discovery-object-group__action').attributes('title')).toBe(
      baseGroups[0]!.summary,
    )
    expect(view.find('.discovery-object-group__summary').exists()).toBe(false)
    expect(view.text()).not.toContain('生成计划并预览')
    expect(view.find('input[aria-label^="选择对象 APP.CUSTOMERS"]').exists()).toBe(false)
    expect(syncApi.queryReconciliationObjectChildren).not.toHaveBeenCalled()
    expect(syncApi.queryReconciliationObjectGroups).toHaveBeenCalledWith(
      1,
      null,
      '',
      '',
      1,
      50,
      [],
      expect.any(AbortSignal),
    )
    expect(syncApi.listSyncPlans).toHaveBeenCalledWith(1, 20, 1, expect.any(AbortSignal))

    await view.find('button[aria-label="展开 APP.CUSTOMERS 字段"]').trigger('click')
    await flushPromises()
    expect(syncApi.queryReconciliationObjectChildren).toHaveBeenCalledWith(
      1,
      41,
      'obj-a',
      '',
      '',
      1,
      50,
      [],
      expect.any(AbortSignal),
    )
    expect(view.text()).toContain('ID')
    expect(view.find('input[aria-label="选择 ID 的可处理操作"]').exists()).toBe(false)
    expect(syncApi.setWholeObjectSelection).not.toHaveBeenCalled()
  })

  it('renders mixed and disabled groups without selecting conflict or review-only items', async () => {
    const view = mountAt('Editor')
    await flushPromises()
    expect(view.text()).toContain('APP.ORDERS')
    expect(view.text()).toContain('部分可处理')
    expect(view.text()).toContain('冲突 1 · 仅审查 0')
    expect(
      view.find('input[aria-label^="选择对象 APP.AUDIT_LOG"]').attributes('disabled'),
    ).toBeDefined()
  })

  it('expands and collapses children lazily while retaining selection state', async () => {
    const view = mountAt('Editor')
    await flushPromises()
    await view.find('button[aria-label="展开 APP.CUSTOMERS 字段"]').trigger('click')
    await flushPromises()
    await view.find('input[aria-label="选择 ID 的可处理操作"]').setValue(true)
    await flushPromises()
    expect(view.text()).toContain('1 对象 · 1 字段 · 2 个操作')
    expect(
      view.find('input[aria-label^="选择对象 APP.CUSTOMERS"]').attributes('data-indeterminate'),
    ).toBe('true')

    await view.find('button[aria-label="收起 APP.CUSTOMERS 字段"]').trigger('click')
    await view.find('button[aria-label="展开 APP.CUSTOMERS 字段"]').trigger('click')
    await flushPromises()
    expect(view.find('input[aria-label="选择 ID 的可处理操作"]').element).toHaveProperty(
      'checked',
      true,
    )
  })

  it('whole-object selection includes the required parent and unloaded child typed actions', async () => {
    const view = mountAt('Editor')
    await flushPromises()
    await view.find('input[aria-label^="选择对象 APP.CUSTOMERS"]').setValue(true)
    await flushPromises()

    expect(syncApi.setWholeObjectSelection).toHaveBeenCalledWith(1, 41, 'obj-a', true, [])
    expect(view.text()).toContain('1 对象 · 3 字段 · 4 个操作')
    expect(view.find('input[aria-label^="选择对象 APP.CUSTOMERS"]').element).toHaveProperty(
      'checked',
      true,
    )
    await view
      .findAll('button')
      .find((item) => item.text() === '生成计划并预览')!
      .trigger('click')
    await flushPromises()
    const actions = syncApi.createSyncPlan.mock.calls[0][2] as readonly SyncSelection[]
    expect(actions).toHaveLength(4)
    expect(actions).toContainEqual(requiredParent)
    expect(actions).toContainEqual(selectionFor(columnActions[2]!))
    expect(useOverlayStore().currentDialog?.kind).toBe('database-discovery-sync-plan')
    expect(useOverlayStore().currentDrawer).toBeNull()
  })

  it('changes the parent to indeterminate after deselecting one child', async () => {
    const view = mountAt('Editor')
    await flushPromises()
    await view.find('input[aria-label^="选择对象 APP.CUSTOMERS"]').setValue(true)
    await flushPromises()
    await view.find('button[aria-label="展开 APP.CUSTOMERS 字段"]').trigger('click')
    await flushPromises()
    await view.find('input[aria-label="选择 ID 的可处理操作"]').setValue(false)
    await flushPromises()
    const parent = view.find('input[aria-label^="选择对象 APP.CUSTOMERS"]')
    expect(parent.attributes('data-indeterminate')).toBe('true')
    expect(view.text()).toContain('1 对象 · 2 字段 · 3 个操作')
  })

  it('keeps selections when paging unloaded children', async () => {
    const view = mountAt('Editor')
    await flushPromises()
    await view.find('button[aria-label="展开 APP.CUSTOMERS 字段"]').trigger('click')
    await flushPromises()
    await view.find('input[aria-label="选择 ID 的可处理操作"]').setValue(true)
    await flushPromises()
    const childPager = view
      .findAll('[data-pagination-next]')
      .find((item) => item.element.closest('.discovery-object-children'))
    await childPager!.trigger('click')
    await flushPromises()
    expect(view.text()).toContain('DISPLAY_NAME')
    expect(view.text()).toContain('1 对象 · 1 字段 · 2 个操作')
    expect(syncApi.queryReconciliationObjectChildren).toHaveBeenLastCalledWith(
      1,
      41,
      'obj-a',
      '',
      '',
      2,
      50,
      expect.arrayContaining([requiredParent, selectionFor(columnActions[0]!)]),
      expect.any(AbortSignal),
    )
  })

  it('uses server page sizes for groups, children, and plan history without losing selections or search', async () => {
    const view = mountAt('Editor')
    await flushPromises()
    await view.find('button[aria-label="展开 APP.CUSTOMERS 字段"]').trigger('click')
    await flushPromises()
    await view.find('input[aria-label="选择 ID 的可处理操作"]').setValue(true)
    await flushPromises()

    await view.find('input[placeholder="搜索 schema / 对象 / 字段"]').setValue('CUSTOMERS')
    await view
      .findAll('button')
      .find((item) => item.text() === '查询')!
      .trigger('click')
    await flushPromises()

    const groupPageSize = view
      .findAll('[data-page-size="100"]')
      .find(
        (item) =>
          item.element.closest('.discovery-table-section') &&
          !item.element.closest('.discovery-object-children'),
      )
    await groupPageSize!.trigger('click')
    await flushPromises()
    expect(syncApi.queryReconciliationObjectGroups).toHaveBeenLastCalledWith(
      1,
      41,
      '',
      'CUSTOMERS',
      1,
      100,
      expect.arrayContaining([requiredParent, selectionFor(columnActions[0]!)]),
      expect.any(AbortSignal),
    )
    expect(view.text()).toContain('1 对象 · 1 字段 · 2 个操作')

    await view.find('button[aria-label="展开 APP.CUSTOMERS 字段"]').trigger('click')
    await flushPromises()
    const childPageSize = view
      .findAll('[data-page-size="100"]')
      .find((item) => item.element.closest('.discovery-object-children'))
    expect(
      view
        .findAll('[data-page-size="200"]')
        .filter((item) => item.element.closest('.discovery-table-section')),
    ).toHaveLength(2)
    await childPageSize!.trigger('click')
    await flushPromises()
    expect(syncApi.queryReconciliationObjectChildren).toHaveBeenLastCalledWith(
      1,
      41,
      'obj-a',
      '',
      'CUSTOMERS',
      1,
      100,
      expect.arrayContaining([requiredParent, selectionFor(columnActions[0]!)]),
      expect.any(AbortSignal),
    )

    const planPageSize = view
      .findAll('[data-page-size="50"]')
      .find((item) => item.element.closest('.discovery-panel'))
    await planPageSize!.trigger('click')
    await flushPromises()
    expect(syncApi.listSyncPlans).toHaveBeenLastCalledWith(1, 50, 1, expect.any(AbortSignal))
    expect(view.text()).toContain('1 对象 · 1 字段 · 2 个操作')
  })

  it('rejects a child selection that would exceed the action cap without truncation', async () => {
    syncApi.queryReconciliationObjectGroups.mockImplementation(
      (_profileId, _snapshotId, _category, _search, _page, _pageSize, selected) =>
        Promise.resolve({ ...groupPage(selected), maximumSyncPlanActions: 1 }),
    )
    const view = mountAt('Editor')
    await flushPromises()
    const maximumPageSize = view
      .findAll('[data-page-size="200"]')
      .find((item) => !item.element.closest('.discovery-object-children'))
    await maximumPageSize!.trigger('click')
    await flushPromises()
    await view.find('button[aria-label="展开 APP.CUSTOMERS 字段"]').trigger('click')
    await flushPromises()
    await view.find('input[aria-label="选择 ID 的可处理操作"]').setValue(true)
    await flushPromises()
    expect(messages.error).toHaveBeenCalledWith(
      '该选择将超过单个同步计划允许的最大操作数，请减少选择范围。',
    )
    expect(view.text()).toContain('0 对象 · 0 字段 · 0 个操作')
  })

  it.each<AccessLevel>(['Editor', 'Administrator'])(
    'allows %s to use the existing preview and explicit confirmation flow',
    async (accessLevel) => {
      const view = mountAt(accessLevel)
      await flushPromises()
      await view
        .findAll('button')
        .find((item) => item.text() === '查看计划')!
        .trigger('click')
      expect(useOverlayStore().currentDialog?.kind).toBe('database-discovery-sync-plan')
      expect(useOverlayStore().currentDrawer).toBeNull()
      expect(view.text()).toContain('预览校验值')
      expect(view.text()).toContain('确认当前预览')
      const confirmation = view
        .findAll('label')
        .find((item) => item.text().includes('我已核对预览内容'))!
      await confirmation.find('input').setValue(true)
      await view
        .findAll('button')
        .find((item) => item.text() === '确认当前预览')!
        .trigger('click')
      await flushPromises()
      expect(syncApi.confirmSyncPlan).toHaveBeenCalledOnce()
    },
  )
})
