import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { DifferenceHistoryItem } from '../api/databaseDiscoveryContracts'
import { discoveryPageStubs } from '../test/discoveryPageTestSupport'
import DiscoveryDifferencesView from './DiscoveryDifferencesView.vue'

const router = vi.hoisted(() => ({ push: vi.fn() }))
const messages = vi.hoisted(() => ({ error: vi.fn() }))
const api = vi.hoisted(() => ({ getRunFilterOptions: vi.fn(), listDifferences: vi.fn() }))
vi.mock('vue-router', () => ({ useRouter: () => router }))
vi.mock('element-plus', () => ({ ElMessage: { error: messages.error } }))
vi.mock('../api/databaseDiscoveryApi', () => api)

const differences: readonly DifferenceHistoryItem[] = [
  {
    id: 51,
    profileId: 1,
    profileName: 'Oracle 只读',
    databaseSourceId: 11,
    databaseSourceName: '核心 Oracle',
    providerType: 'Oracle',
    baseSnapshotId: 40,
    targetSnapshotId: 41,
    createdAt: '2026-08-31T01:00:00Z',
    summaryCounts: { added: 11, changed: 2, missingFromSource: 5, unchanged: 54 },
  },
  {
    id: 52,
    profileId: 2,
    profileName: 'PostgreSQL 只读',
    databaseSourceId: 22,
    databaseSourceName: '分析 PostgreSQL',
    providerType: 'PostgreSql',
    baseSnapshotId: null,
    targetSnapshotId: 42,
    createdAt: '2026-08-31T02:00:00Z',
    summaryCounts: { added: 20, changed: 0, missingFromSource: 0, unchanged: 0 },
  },
]
let wrapper: VueWrapper | undefined

function mountView(): VueWrapper {
  wrapper = mount(DiscoveryDifferencesView, {
    global: {
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
  api.listDifferences.mockResolvedValue({ items: differences, page: 1, pageSize: 20, total: 42 })
})

afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
})

describe('DiscoveryDifferencesView', () => {
  it('shows provider-neutral history with four counts, filters and paging', async () => {
    const view = mountView()
    await flushPromises()

    expect(view.text()).toContain('Oracle 只读')
    expect(view.text()).toContain('PostgreSQL 只读')
    expect(view.text()).toContain('基线 40 → 目标 41')
    expect(view.text()).toContain('11')
    expect(view.text()).toContain('54')
    expect(view.text()).toContain('尚未同步到数据库知识')

    await view.find('[data-option-value="22"]').trigger('click')
    await flushPromises()
    expect(api.listDifferences).toHaveBeenLastCalledWith(
      1,
      20,
      undefined,
      22,
      expect.any(AbortSignal),
    )

    await view.find('[data-pagination-next]').trigger('click')
    await flushPromises()
    expect(api.listDifferences).toHaveBeenLastCalledWith(
      2,
      20,
      undefined,
      22,
      expect.any(AbortSignal),
    )

    expect(view.find('[data-pagination-layout]').attributes('data-pagination-layout')).toBe(
      'total, sizes, prev, pager, next, jumper',
    )
    await view.find('[data-page-size="100"]').trigger('click')
    await flushPromises()
    expect(api.listDifferences).toHaveBeenLastCalledWith(
      1,
      100,
      undefined,
      22,
      expect.any(AbortSignal),
    )
  })

  it('navigates directly to Difference detail', async () => {
    const view = mountView()
    await flushPromises()
    await view
      .findAll('button')
      .find((item) => item.text() === '查看差异')!
      .trigger('click')
    expect(router.push).toHaveBeenCalledWith({
      name: 'database-discovery-difference',
      params: { id: '51' },
    })
  })

  it('treats an empty history as an expected baseline state', async () => {
    api.listDifferences.mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 })
    const view = mountView()
    await flushPromises()
    expect(view.text()).toContain('暂无可审查差异')
    expect(view.text()).toContain('首次发现会建立基线')
    expect(messages.error).not.toHaveBeenCalled()
  })
})
