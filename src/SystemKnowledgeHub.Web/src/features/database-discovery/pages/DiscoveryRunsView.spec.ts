import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useActorStore } from '../../../app/stores/actor'
import type { AccessLevel, CurrentUserProfile } from '../../users/api/userContracts'
import type { DiscoveryRun, Page } from '../api/databaseDiscoveryContracts'
import { discoveryPageStubs } from '../test/discoveryPageTestSupport'
import DiscoveryRunsView from './DiscoveryRunsView.vue'

const router = vi.hoisted(() => ({ push: vi.fn() }))
const route = vi.hoisted(() => ({ query: { runId: '11' } as Record<string, string> }))
const messages = vi.hoisted(() => ({
  confirm: vi.fn(),
  error: vi.fn(),
  success: vi.fn(),
}))
const api = vi.hoisted(() => ({
  cancelRun: vi.fn(),
  getRunFilterOptions: vi.fn(),
  listRuns: vi.fn(),
}))

vi.mock('vue-router', () => ({
  useRoute: () => route,
  useRouter: () => router,
}))
vi.mock('element-plus', () => ({
  ElMessage: { error: messages.error, success: messages.success },
  ElMessageBox: { confirm: messages.confirm },
}))
vi.mock('../api/databaseDiscoveryApi', () => api)

const user: CurrentUserProfile = {
  id: 1,
  employeeNo: null,
  displayName: '发现审查人',
  email: null,
  departmentOrTeam: null,
  jobTitle: null,
  isActive: true,
  knowledgeRoles: [],
  accessLevel: 'Viewer',
  authenticationMethod: 'local',
  mustChangePassword: false,
}

const run = (status: DiscoveryRun['status']): DiscoveryRun => ({
  id: 11,
  profileId: 3,
  databaseSourceId: 5,
  databaseSourceName: '订单数据库',
  profileName: '订单库只读',
  providerType: 'PostgreSql',
  status,
  baseSnapshotId: 7,
  snapshotId: status === 'Succeeded' ? 8 : null,
  differenceId: status === 'Succeeded' ? 9 : null,
  scopeGenerationId: 2,
  queuedAt: '2026-08-30T00:00:00Z',
  startedAt: '2026-08-30T00:00:01Z',
  completedAt: status === 'Succeeded' ? '2026-08-30T00:00:02Z' : null,
  cancellationRequestedAt: null,
  providerVersion: '18.0',
  objectCounts: null,
  errorCode: null,
  errorSummary: null,
  concurrencyToken: `run-${status}`,
})

const page = (item: DiscoveryRun, total = 1): Page<DiscoveryRun> => ({
  items: [item],
  page: 1,
  pageSize: 20,
  total,
})

function mountFor(accessLevel: AccessLevel) {
  const pinia = createPinia()
  setActivePinia(pinia)
  const actor = useActorStore()
  actor.currentUser = { ...user, accessLevel }
  actor.authStatus = 'authenticated'
  actor.initialized = true
  return mount(DiscoveryRunsView, {
    global: { plugins: [pinia], stubs: discoveryPageStubs },
  })
}

describe('DiscoveryRunsView', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.clearAllMocks()
    route.query = { runId: '11' }
    api.getRunFilterOptions.mockResolvedValue({ profiles: [], databaseSources: [] })
    messages.confirm.mockResolvedValue(undefined)
    api.cancelRun.mockResolvedValue(run('Cancelled'))
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('polls while a run is active, stops at a terminal state, and never offers Viewer cancellation', async () => {
    api.listRuns
      .mockResolvedValueOnce(page(run('Running')))
      .mockResolvedValueOnce(page(run('Succeeded')))

    const wrapper = mountFor('Viewer')
    await flushPromises()

    expect(api.getRunFilterOptions).toHaveBeenCalledOnce()
    expect(api.listRuns).toHaveBeenCalledOnce()
    expect(wrapper.text()).toContain('运行中')
    expect(wrapper.findAll('button').some((button) => button.text() === '取消')).toBe(false)

    await vi.advanceTimersByTimeAsync(2500)
    await flushPromises()

    expect(api.listRuns).toHaveBeenCalledTimes(2)
    expect(wrapper.text()).toContain('成功')

    await vi.advanceTimersByTimeAsync(5000)
    await flushPromises()
    expect(api.listRuns).toHaveBeenCalledTimes(2)

    wrapper.unmount()
  })

  it('aborts the in-flight request and clears polling when unmounted', async () => {
    api.listRuns.mockResolvedValue(page(run('Running')))
    const wrapper = mountFor('Administrator')
    await flushPromises()

    const signal = api.listRuns.mock.calls[0]?.[4] as AbortSignal
    expect(signal.aborted).toBe(false)
    expect(wrapper.findAll('button').some((button) => button.text() === '取消')).toBe(true)

    wrapper.unmount()
    expect(signal.aborted).toBe(true)

    await vi.advanceTimersByTimeAsync(5000)
    expect(api.listRuns).toHaveBeenCalledOnce()
  })

  it('lets an Administrator confirm cancellation through the dedicated API', async () => {
    const activeRun = run('Running')
    api.listRuns.mockResolvedValue(page(activeRun))
    const wrapper = mountFor('Administrator')
    await flushPromises()

    const cancelButton = wrapper.findAll('button').find((item) => item.text() === '取消')
    expect(cancelButton).toBeDefined()
    await cancelButton!.trigger('click')
    await flushPromises()

    expect(messages.confirm).toHaveBeenCalledWith(
      '确认取消该发现任务？运行取消后不会产生成功快照或差异。',
      '取消发现',
      { type: 'warning' },
    )
    expect(api.cancelRun).toHaveBeenCalledWith(activeRun)
    expect(messages.success).toHaveBeenCalledWith('取消请求已提交。')

    wrapper.unmount()
  })

  it('keeps snapshot and difference navigation in their dedicated tabs', async () => {
    api.listRuns.mockResolvedValue(page(run('Succeeded')))
    const wrapper = mountFor('Viewer')
    await flushPromises()

    const snapshotButton = wrapper.findAll('button').find((item) => item.text() === '查看快照')
    const differenceButton = wrapper.findAll('button').find((item) => item.text() === '查看差异')
    expect(snapshotButton).toBeUndefined()
    expect(differenceButton).toBeUndefined()
    expect(router.push).not.toHaveBeenCalled()

    wrapper.unmount()
  })

  it('uses the selected server page size and resets the page to one', async () => {
    api.listRuns.mockResolvedValue(page(run('Succeeded'), 101))
    const wrapper = mountFor('Viewer')
    await flushPromises()

    expect(api.listRuns).toHaveBeenCalledWith(1, 20, undefined, undefined, expect.any(AbortSignal))
    expect(wrapper.find('[data-pagination-layout]').attributes('data-pagination-layout')).toBe(
      'total, sizes, prev, pager, next, jumper',
    )
    await wrapper.find('[data-page-size="50"]').trigger('click')
    await flushPromises()
    expect(api.listRuns).toHaveBeenLastCalledWith(
      1,
      50,
      undefined,
      undefined,
      expect.any(AbortSignal),
    )

    wrapper.unmount()
  })
})
