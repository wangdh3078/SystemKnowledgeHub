import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useActorStore } from '../../../app/stores/actor'
import type { CurrentUserProfile } from '../../users/api/userContracts'
import type { SnapshotHistoryItem } from '../api/databaseDiscoveryContracts'
import { discoveryPageStubs } from '../test/discoveryPageTestSupport'
import DiscoverySnapshotsView from './DiscoverySnapshotsView.vue'

const router = vi.hoisted(() => ({ push: vi.fn() }))
const messages = vi.hoisted(() => ({ error: vi.fn() }))
const api = vi.hoisted(() => ({ getRunFilterOptions: vi.fn(), listSnapshots: vi.fn() }))
vi.mock('vue-router', () => ({ useRouter: () => router }))
vi.mock('element-plus', () => ({ ElMessage: { error: messages.error } }))
vi.mock('../api/databaseDiscoveryApi', () => api)

const user: CurrentUserProfile = {
  id: 1,
  employeeNo: null,
  displayName: '发现管理员',
  email: null,
  departmentOrTeam: null,
  jobTitle: null,
  isActive: true,
  knowledgeRoles: [],
  accessLevel: 'Administrator',
  authenticationMethod: 'local',
  mustChangePassword: false,
}
const counts = {
  schemas: 2,
  objects: 8,
  columns: 30,
  primaryKeys: 7,
  foreignKeys: 3,
  uniqueConstraints: 3,
  indexes: 13,
  sequences: 1,
  foreignKeyReferenceStubs: 1,
}
const snapshots: readonly SnapshotHistoryItem[] = [
  {
    id: 41,
    runId: 31,
    profileId: 1,
    profileName: 'Oracle 只读',
    databaseSourceId: 11,
    databaseSourceName: '核心 Oracle',
    providerType: 'Oracle',
    capturedAt: '2026-08-31T01:00:00Z',
    includedSchemas: ['APP', 'AUDIT'],
    scopeGenerationId: 4,
    baseSnapshotId: 40,
    differenceId: 51,
    counts,
  },
  {
    id: 42,
    runId: 32,
    profileId: 2,
    profileName: 'PostgreSQL 只读',
    databaseSourceId: 22,
    databaseSourceName: '分析 PostgreSQL',
    providerType: 'PostgreSql',
    capturedAt: '2026-08-31T02:00:00Z',
    includedSchemas: ['public'],
    scopeGenerationId: 5,
    baseSnapshotId: null,
    differenceId: 52,
    counts: { ...counts, objects: 4, columns: 18 },
  },
]
let wrapper: VueWrapper | undefined

function mountView(): VueWrapper {
  const pinia = createPinia()
  setActivePinia(pinia)
  const actor = useActorStore()
  actor.currentUser = user
  actor.authStatus = 'authenticated'
  actor.initialized = true
  wrapper = mount(DiscoverySnapshotsView, {
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
  api.getRunFilterOptions.mockResolvedValue({
    profiles: [
      { id: 1, name: 'Oracle 只读' },
      { id: 2, name: 'PostgreSQL 只读' },
    ],
    databaseSources: [
      { id: 11, name: '核心 Oracle' },
      { id: 22, name: '分析 PostgreSQL' },
    ],
  })
  api.listSnapshots.mockResolvedValue({ items: snapshots, page: 1, pageSize: 20, total: 45 })
})

afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
})

describe('DiscoverySnapshotsView', () => {
  it('shows provider-neutral history, filters and server-side paging', async () => {
    const view = mountView()
    await flushPromises()

    expect(view.text()).toContain('Oracle 只读')
    expect(view.text()).toContain('PostgreSQL 只读')
    expect(view.text()).toContain('APP, AUDIT')
    expect(view.text()).toContain('8 对象 / 30 字段')
    expect(view.text()).toContain('基线 40 · 差异 51')
    expect(api.listSnapshots).toHaveBeenCalledWith(
      1,
      20,
      undefined,
      undefined,
      expect.any(AbortSignal),
    )

    await view.find('[data-option-value="2"]').trigger('click')
    await flushPromises()
    expect(api.listSnapshots).toHaveBeenLastCalledWith(1, 20, 2, undefined, expect.any(AbortSignal))

    await view.find('[data-pagination-next]').trigger('click')
    await flushPromises()
    expect(api.listSnapshots).toHaveBeenLastCalledWith(2, 20, 2, undefined, expect.any(AbortSignal))
  })

  it('navigates from history to Snapshot detail with its Difference shortcut', async () => {
    const view = mountView()
    await flushPromises()
    await view
      .findAll('button')
      .find((item) => item.text() === '查看快照')!
      .trigger('click')
    expect(router.push).toHaveBeenCalledWith({
      name: 'database-discovery-snapshot',
      params: { id: '41' },
      query: { differenceId: '51' },
    })
  })

  it('explains the empty state without triggering discovery', async () => {
    api.listSnapshots.mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 })
    const view = mountView()
    await flushPromises()
    expect(view.text()).toContain('暂无发现快照')
    expect(view.text()).toContain('完成一次数据库发现后，可在这里查看发现的数据库结构。')
    expect(view.text()).toContain('前往连接配置')
    expect(view.text()).toContain('前往发现运行')
  })
})
