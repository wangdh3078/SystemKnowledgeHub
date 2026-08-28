import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import DashboardView from './DashboardView.vue'

const routerState = vi.hoisted(() => ({ push: vi.fn() }))
const dashboardState = vi.hoisted(() => ({
  data: {
    scope: { systemName: null },
    knowledgeOverview: {
      systems: 2,
      businessFunctions: 3,
      databaseObjects: 4,
      columns: 5,
      integrations: 0,
      businessRules: 6,
      unknownItems: 7,
    },
    knowledgeProgress: { confirmed: 1, inferred: 1, unknown: 1, openUnknownItems: 7 },
    needsAttention: [],
    recentActivity: [],
  },
}))

vi.mock('vue-router', () => ({ useRouter: () => routerState }))
vi.mock('../composables/useDashboard', () => ({
  useDashboard: () => ({
    data: { __v_isRef: true, value: dashboardState.data },
    loading: { __v_isRef: true, value: false },
    error: { __v_isRef: true, value: null },
    load: vi.fn(),
  }),
}))
vi.mock('../../../app/stores/overlays', () => ({
  useOverlayStore: () => ({ openDialog: vi.fn() }),
}))

describe('DashboardView knowledge overview navigation', () => {
  it('uses real browse destinations and renders metrics without a route as non-interactive statistics', async () => {
    const wrapper = mount(DashboardView, {
      global: {
        stubs: {
          ElButton: { template: '<button><slot /></button>' },
          ElIcon: { template: '<span><slot /></span>' },
          EmptyState: { template: '<div />' },
        },
      },
    })
    const items = wrapper.findAll('.dashboard-overview-item')
    const integrations = items.find(item => item.text().includes('集成关系'))!
    const rules = items.find(item => item.text().includes('业务规则'))!
    const systems = items.find(item => item.text().includes('系统'))!
    const columns = items.find(item => item.text().includes('字段'))!

    expect(integrations.element.tagName).toBe('DIV')
    expect(integrations.text()).toContain('0')
    expect(rules.element.tagName).toBe('DIV')

    await systems.trigger('click')
    expect(routerState.push).toHaveBeenLastCalledWith({ name: 'systems-list' })
    await columns.trigger('click')
    expect(routerState.push).toHaveBeenLastCalledWith({ name: 'database-objects-list' })
    expect(routerState.push).toHaveBeenCalledTimes(2)
  })
})
