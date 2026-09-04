import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../api/errors/ApiError'
import PortalPageView from './PortalPageView.vue'

const { getPage } = vi.hoisted(() => ({ getPage: vi.fn() }))
vi.mock('../api/portalReadApi', () => ({ portalReadApi: { getPage } }))
afterEach(() => vi.clearAllMocks())

async function mountPage(path = '/portal/pages/9', settle = true) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/portal', name: 'portal-home', component: { template: '<div />' } },
      { path: '/portal/pages/:id', name: 'portal-page', component: PortalPageView },
    ],
  })
  await router.push(path)
  const wrapper = mount(PortalPageView, {
    global: {
      plugins: [router],
      stubs: {
        PortalSectionRenderer: {
          props: ['section'],
          template: '<section class="section-stub">{{ section.heading }}</section>',
        },
      },
    },
  })
  if (settle) await flushPromises()
  return wrapper
}

describe('PortalPageView', () => {
  it('shows a lightweight skeleton while the published page is loading', async () => {
    getPage.mockReturnValue(new Promise(() => undefined))
    const wrapper = await mountPage('/portal/pages/9', false)
    expect(wrapper.get('.portal-loading').attributes('aria-live')).toBe('polite')
    expect(wrapper.findAll('.portal-skeleton')).toHaveLength(4)
    expect(wrapper.text()).not.toContain('登录')
    wrapper.unmount()
  })

  it('renders canonical breadcrumb, title, target type, ordered sections, and document title', async () => {
    getPage.mockResolvedValue({
      id: 9,
      title: 'Lot Track In',
      primaryTarget: { type: 'BusinessFunction', id: 2, title: 'Lot Track In' },
      breadcrumb: [
        { nodeId: 1, title: 'MES' },
        { nodeId: 2, title: '生产管理' },
      ],
      sections: [
        {
          id: 3,
          heading: '业务概览',
          sourceKind: 'PrimaryTarget',
          projectionKind: 'StructuredOverview',
          content: { kind: 'BusinessFunctionOverview' },
        },
        {
          id: 4,
          heading: '业务说明',
          sourceKind: 'ExplicitReference',
          projectionKind: 'KnowledgeDocumentBody',
          content: { kind: 'KnowledgeDocumentBody' },
        },
      ],
    })
    const wrapper = await mountPage()
    expect(wrapper.get('.portal-breadcrumb').text()).toContain('MES/生产管理/Lot Track In')
    expect(wrapper.get('h1').text()).toBe('Lot Track In')
    expect(wrapper.text()).toContain('业务功能')
    expect(wrapper.findAll('.section-stub').map((item) => item.text())).toEqual([
      '业务概览',
      '业务说明',
    ])
    expect(document.title).toBe('Lot Track In · 系统知识中心')
  })

  it('shows the sanitized Portal 404 for missing and invalid IDs without login or permission text', async () => {
    getPage.mockRejectedValue(
      new ApiError(404, {
        code: 'not_found',
        message: '未找到指定页面。',
        fieldErrors: null,
        details: null,
      }),
    )
    const wrapper = await mountPage()
    expect(wrapper.text()).toContain('页面未找到')
    expect(wrapper.text()).toContain('该知识可能尚未发布、已取消发布，或地址不正确。')
    expect(wrapper.text()).not.toMatch(/登录|权限|Draft|Archived|deleted/u)
    expect(document.title).toBe('页面未找到 · 系统知识中心')
  })

  it('shows retryable failure without redirecting to the Admin login experience', async () => {
    getPage.mockRejectedValue(
      new ApiError(401, {
        code: 'unauthenticated',
        message: 'unauthenticated',
        fieldErrors: null,
        details: null,
      }),
    )
    const wrapper = await mountPage()
    expect(wrapper.text()).toContain('知识暂时无法加载，请稍后重试。')
    expect(wrapper.text()).not.toContain('登录')
  })
})
