import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type {
  DifferenceEntry,
  DifferenceState,
  DifferenceSummary,
  Page,
} from '../api/databaseDiscoveryContracts'
import { discoveryPageStubs } from '../test/discoveryPageTestSupport'
import DiscoveryDifferenceView from './DiscoveryDifferenceView.vue'

const router = vi.hoisted(() => ({ push: vi.fn() }))
const route = vi.hoisted(() => ({ params: { id: '77' } }))
const messages = vi.hoisted(() => ({ error: vi.fn(), info: vi.fn() }))
const api = vi.hoisted(() => ({
  getDifference: vi.fn(),
  getDifferenceEntries: vi.fn(),
}))

vi.mock('vue-router', () => ({
  useRoute: () => route,
  useRouter: () => router,
}))
vi.mock('element-plus', () => ({
  ElMessage: { error: messages.error, info: messages.info },
}))
vi.mock('../api/databaseDiscoveryApi', () => api)

const summary: DifferenceSummary = {
  id: 77,
  profileId: 3,
  baseSnapshotId: 41,
  targetSnapshotId: 42,
  scopeGenerationId: 2,
  algorithmVersion: 1,
  createdAt: '2026-08-30T00:00:00Z',
  summaryCounts: { added: 1, changed: 1, missingFromSource: 1, unchanged: 0 },
  contentSha256: 'b'.repeat(64),
}
const changedEntry: DifferenceEntry = {
  id: 701,
  entityKind: 'Column',
  logicalIdentity: 'Column:DatabaseObject:public:CUSTOMERS:NAME',
  parentLogicalIdentity: 'DatabaseObject:public:CUSTOMERS',
  displayName: 'public.CUSTOMERS.NAME',
  state: 'Changed',
  schemaName: 'public',
  objectName: 'CUSTOMERS',
  childName: 'NAME',
  changes: [{ field: 'nativeDataType', before: 'varchar(100)', after: 'varchar(200)' }],
}
const missingEntry: DifferenceEntry = {
  id: 702,
  entityKind: 'DatabaseObject',
  logicalIdentity: 'DatabaseObject:public:OLD_CUSTOMERS',
  parentLogicalIdentity: 'Schema:public',
  displayName: 'public.OLD_CUSTOMERS',
  state: 'MissingFromSource',
  schemaName: 'public',
  objectName: 'OLD_CUSTOMERS',
  childName: null,
  changes: [],
}
const page = (items: readonly DifferenceEntry[]): Page<DifferenceEntry> => ({
  items,
  page: 1,
  pageSize: 50,
  total: items.length,
})

function mountView() {
  const pinia = createPinia()
  setActivePinia(pinia)
  return mount(DiscoveryDifferenceView, {
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
}

describe('DiscoveryDifferenceView', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    api.getDifference.mockResolvedValue(summary)
    api.getDifferenceEntries.mockImplementation(async (_id: number, state: DifferenceState) =>
      state === 'MissingFromSource' ? page([missingEntry]) : page([changedEntry]),
    )
  })

  it('defaults to Changed, switches states, renders field values, and stays review-only', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(api.getDifference).toHaveBeenCalledWith(77, expect.any(AbortSignal))
    expect(api.getDifferenceEntries).toHaveBeenCalledWith(
      77,
      'Changed',
      1,
      20,
      '',
      '',
      '',
      expect.any(AbortSignal),
    )
    expect(wrapper.find('.discovery-panel h2').text()).toBe('已变化')

    await wrapper.find('input[type="checkbox"]').setValue(true)
    await flushPromises()
    expect(wrapper.text()).toContain('已选择 1 项')
    const planButton = wrapper
      .findAll('button')
      .find((button) => button.text() === '下一步：手工同步计划')
    expect(planButton).toBeDefined()
    await planButton!.trigger('click')
    expect(messages.info).toHaveBeenCalledWith(
      '已保留当前审查选择；手工同步计划将在后续任务提供，本页不会应用任何变更。',
    )
    expect(api.getDifference).toHaveBeenCalledOnce()
    expect(api.getDifferenceEntries).toHaveBeenCalledOnce()

    const reviewButton = wrapper.findAll('button').find((button) => button.text() === '查看前后值')
    expect(reviewButton).toBeDefined()
    await reviewButton!.trigger('click')
    await flushPromises()
    const drawer = wrapper.find('.discovery-drawer')
    expect(drawer.text()).toContain('数据类型')
    expect(drawer.text()).toContain('varchar(100)')
    expect(drawer.text()).toContain('varchar(200)')
    expect(drawer.text()).not.toContain('nativeDataType')
    expect(drawer.text()).not.toContain('pg_catalog')
    expect(drawer.find('pre, code').exists()).toBe(false)
    expect(drawer.text()).not.toMatch(/[{}]/)

    const stateButtons = wrapper.findAll('.discovery-diff-cards button')
    expect(stateButtons).toHaveLength(4)
    expect(stateButtons.map((button) => button.find('small').text())).toEqual([
      '新增',
      '已变化',
      '来源中未发现',
      '未变化',
    ])
    await stateButtons[2]!.trigger('click')
    await flushPromises()

    expect(api.getDifferenceEntries).toHaveBeenLastCalledWith(
      77,
      'MissingFromSource',
      1,
      20,
      '',
      '',
      '',
      expect.any(AbortSignal),
    )
    expect(wrapper.text()).toContain('来源中未发现')
    expect(wrapper.text()).toContain('OLD_CUSTOMERS')
    expect(
      wrapper
        .findAll('button')
        .some((button) => /^(?:Apply|应用|应用变更|立即同步|执行同步)$/i.test(button.text())),
    ).toBe(false)
    expect(wrapper.text()).toContain('本页面不会应用或同步任何变更')

    wrapper.unmount()
  })

  it('falls back to Unchanged when it is the only populated state', async () => {
    api.getDifference.mockResolvedValue({
      ...summary,
      summaryCounts: { added: 0, changed: 0, missingFromSource: 0, unchanged: 3 },
    })
    api.getDifferenceEntries.mockResolvedValue(page([]))

    const wrapper = mountView()
    await flushPromises()

    expect(api.getDifferenceEntries).toHaveBeenCalledWith(
      77,
      'Unchanged',
      1,
      20,
      '',
      '',
      '',
      expect.any(AbortSignal),
    )
    expect(wrapper.find('.discovery-panel h2').text()).toBe('未变化')
    expect(wrapper.find('.discovery-diff-cards [aria-pressed="true"]').text()).toContain('未变化')

    wrapper.unmount()
  })
})
