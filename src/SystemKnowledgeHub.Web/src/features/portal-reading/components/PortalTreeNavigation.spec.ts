import { mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { describe, expect, it } from 'vitest'
import PortalTreeNavigation from './PortalTreeNavigation.vue'

const items = [
  { nodeId: 1, parentNodeId: null, title: 'MES', nodeKind: 'Folder' as const, pageId: null },
  { nodeId: 2, parentNodeId: 1, title: '生产管理', nodeKind: 'Folder' as const, pageId: null },
  { nodeId: 3, parentNodeId: 2, title: 'Lot Track In', nodeKind: 'Page' as const, pageId: 9 },
]

describe('PortalTreeNavigation', () => {
  it('uses semantic lists, expands folders, and navigates pages with active state', async () => {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: '/portal/pages/:id', name: 'portal-page', component: { template: '<div />' } },
      ],
    })
    await router.push('/portal/pages/9')
    const wrapper = mount(PortalTreeNavigation, {
      props: { items, expandedNodeIds: new Set([1, 2]), activePageId: 9 },
      global: { plugins: [router], stubs: { ElIcon: { template: '<i><slot /></i>' } } },
    })

    expect(wrapper.find('nav[aria-label="知识目录"]').exists()).toBe(true)
    expect(wrapper.findAll('ul')).toHaveLength(3)
    expect(wrapper.get('a[aria-current="page"]').text()).toContain('Lot Track In')
    await wrapper.get('button[aria-expanded="true"]').trigger('click')
    expect(wrapper.emitted('toggle')?.[0]).toEqual([1])
  })

  it('shows the published-empty reading state without management guidance', () => {
    const wrapper = mount(PortalTreeNavigation, {
      props: { items: [], expandedNodeIds: new Set<number>(), activePageId: null },
    })
    expect(wrapper.text()).toBe('暂无已发布知识')
    expect(wrapper.text()).not.toContain('管理')
  })
})
