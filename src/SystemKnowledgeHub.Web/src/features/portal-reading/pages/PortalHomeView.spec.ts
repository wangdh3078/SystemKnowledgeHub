import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import PortalHomeView from './PortalHomeView.vue'

const { getHome } = vi.hoisted(() => ({ getHome: vi.fn() }))
vi.mock('../api/portalReadApi', () => ({ portalReadApi: { getHome } }))
afterEach(() => vi.clearAllMocks())

async function mountHome() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/portal', name: 'portal-home', component: PortalHomeView },
      { path: '/portal/pages/:id', name: 'portal-page', component: { template: '<div />' } },
    ],
  })
  await router.push('/portal')
  const wrapper = mount(PortalHomeView, {
    global: { plugins: [router], stubs: { ElIcon: { template: '<i><slot /></i>' } } },
  })
  await flushPromises()
  return wrapper
}

describe('PortalHomeView', () => {
  it('renders top-level categories and recent published pages as reading navigation', async () => {
    getHome.mockResolvedValue({
      portalName: '系统知识中心',
      categories: [{ nodeId: 1, title: 'MES', nodeKind: 'Folder', pageId: null }],
      recentPages: [
        {
          id: 9,
          title: 'Lot Track In',
          primaryTarget: { type: 'BusinessFunction', id: 2, title: 'Lot Track In' },
          breadcrumb: [{ nodeId: 1, title: 'MES' }],
          publishedAt: '2026-09-04T00:00:00Z',
        },
      ],
    })
    const wrapper = await mountHome()
    expect(wrapper.text()).toContain('浏览已发布的系统、业务、数据库和知识文档。')
    expect(wrapper.text()).toContain('MES')
    expect(wrapper.text()).toContain('Lot Track In')
    expect(wrapper.get('.portal-recent-list a').attributes('href')).toBe('/portal/pages/9')
    expect(wrapper.text()).not.toMatch(/管理|登录|用户/u)
  })

  it('renders the exact empty state without an authoring call to action', async () => {
    getHome.mockResolvedValue({ portalName: '系统知识中心', categories: [], recentPages: [] })
    const wrapper = await mountHome()
    expect(wrapper.findAll('.portal-empty')).toHaveLength(2)
    expect(wrapper.text()).toContain('暂无已发布知识')
    expect(wrapper.find('button').exists()).toBe(false)
  })

  it('uses the login-free retryable error copy', async () => {
    getHome.mockRejectedValue(new Error('offline'))
    const wrapper = await mountHome()
    expect(wrapper.text()).toContain('知识暂时无法加载，请稍后重试。')
    expect(wrapper.text()).not.toContain('登录')
  })
})
