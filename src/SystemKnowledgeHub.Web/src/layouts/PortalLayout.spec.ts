import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import PortalLayout from './PortalLayout.vue'

const { getTree } = vi.hoisted(() => ({ getTree: vi.fn() }))
vi.mock('../features/portal-reading/api/portalReadApi', () => ({
  portalReadApi: { getTree },
}))

afterEach(() => vi.clearAllMocks())

async function mountLayout() {
  getTree.mockResolvedValue({
    items: [
      { nodeId: 1, parentNodeId: null, title: 'MES', nodeKind: 'Folder', pageId: null },
      { nodeId: 2, parentNodeId: 1, title: 'Lot Track In', nodeKind: 'Page', pageId: 9 },
    ],
    total: 2,
  })
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/portal', name: 'portal-home', component: { template: '<div />' } },
      { path: '/portal/pages/:id', name: 'portal-page', component: { template: '<div />' } },
    ],
  })
  await router.push('/portal/pages/9')
  const wrapper = mount(PortalLayout, {
    slots: { default: '<article class="reading-content">正文</article>' },
    global: {
      plugins: [router],
      stubs: {
        ElIcon: { template: '<i><slot /></i>' },
        ArrowLeft: true,
        ArrowRight: true,
        Close: true,
        Menu: true,
      },
    },
  })
  await flushPromises()
  return wrapper
}

describe('PortalLayout', () => {
  it('renders one reading main, minimal header, active tree path, and no Admin/Login UI', async () => {
    const wrapper = await mountLayout()
    expect(getTree).toHaveBeenCalledOnce()
    expect(wrapper.findAll('main')).toHaveLength(1)
    expect(wrapper.get('.portal-header').text()).toBe('系统知识中心')
    expect(wrapper.get('a[aria-current="page"]').text()).toContain('Lot Track In')
    expect(wrapper.text()).not.toMatch(/登录|当前用户|知识门户管理|数据库发现|新增|编辑|发布/u)
  })

  it('supports desktop collapse and an accessible Escape-close narrow overlay', async () => {
    const wrapper = await mountLayout()
    await wrapper.get('button[aria-label="折叠知识目录"]').trigger('click')
    expect(wrapper.get('.portal-layout__body').classes()).toContain('is-tree-collapsed')
    await wrapper.get('button[aria-label="打开知识目录"]').trigger('click')
    expect(wrapper.find('[role="dialog"][aria-label="知识目录"]').exists()).toBe(true)
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await wrapper.vm.$nextTick()
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
  })
})
