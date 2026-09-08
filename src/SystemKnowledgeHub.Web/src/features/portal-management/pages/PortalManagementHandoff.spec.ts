import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createMemoryHistory, createRouter, RouterView } from 'vue-router'
import ElementPlus from 'element-plus'
import PortalManagementView from './PortalManagementView.vue'
import { apiClient } from '../../../api/client/apiClient'
import { getKnowledgeDocument } from '../../knowledge-documents/api/knowledgeDocumentsApi'
import type { KnowledgeDocumentDetail } from '../../knowledge-documents/api/knowledgeDocumentContracts'
vi.mock('../../../api/client/apiClient', () => ({
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn(), deleteWithBody: vi.fn() },
}))
vi.mock('../../knowledge-documents/api/knowledgeDocumentsApi', () => ({
  getKnowledgeDocument: vi.fn(),
}))
enableAutoUnmount(afterEach)
const target = {
  type: 'KnowledgeDocument',
  id: 101,
  title: '当前已发布标题',
  context: null,
  status: '可发布',
  documentType: 'KnowledgeArticle',
  lifecycle: 'Published',
}
let candidates: (typeof target)[]
beforeEach(() => {
  vi.clearAllMocks()
  candidates = [target]
  vi.mocked(getKnowledgeDocument).mockResolvedValue({
    id: 101,
    title: target.title,
    lifecycleStatus: 'Published',
  } as KnowledgeDocumentDetail)
  vi.mocked(apiClient.get).mockImplementation(async (url) => {
    if (url.includes('/targets?'))
      return { items: candidates, page: 1, pageSize: 100, total: candidates.length }
    if (url.includes('/tree')) return { items: [], total: 0 }
    if (url.includes('/pages?')) return { items: [], page: 1, pageSize: 20, total: 0 }
    throw new Error(`Unexpected GET ${url}`)
  })
})
afterEach(() => (document.body.innerHTML = ''))
async function setup(query = '') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/portal-management', component: PortalManagementView }],
  })
  await router.push('/portal-management' + query)
  const wrapper = mount(RouterView, {
    attachTo: document.body,
    global: { plugins: [router, ElementPlus] },
  })
  await flushPromises()
  return { wrapper, router }
}
function noWrites() {
  expect(apiClient.post).not.toHaveBeenCalled()
  expect(apiClient.put).not.toHaveBeenCalled()
  expect(apiClient.deleteWithBody).not.toHaveBeenCalled()
}
describe('Portal first-entry KnowledgeDocument handoff', () => {
  it('continues existing server pages until the exact ID is resolved', async () => {
    vi.mocked(apiClient.get).mockImplementation(async (url) => {
      if (url.includes('/targets?')) {
        const second = new URL(url, 'http://localhost').searchParams.get('page') === '2'
        return {
          items: [second ? target : { ...target, id: 99 }],
          page: second ? 2 : 1,
          pageSize: 100,
          total: 101,
        }
      }
      if (url.includes('/tree')) return { items: [], total: 0 }
      return { items: [], page: 1, pageSize: 20, total: 0 }
    })
    await setup('?targetType=KnowledgeDocument&targetId=101')
    expect(
      vi.mocked(apiClient.get).mock.calls.filter(([url]) => url.includes('/targets?')),
    ).toHaveLength(2)
    expect(document.body.textContent).toContain('知识文档 · 当前已发布标题')
    noWrites()
  })
  it('does not overwrite a new-page form opened while resolution is pending', async () => {
    let complete!: (document: KnowledgeDocumentDetail) => void
    vi.mocked(getKnowledgeDocument).mockReturnValue(
      new Promise((resolve) => {
        complete = resolve
      }),
    )
    const { wrapper } = await setup('?targetType=KnowledgeDocument&targetId=101')
    await wrapper
      .findAll('button')
      .find((button) => button.text() === '新建页面')!
      .trigger('click')
    await flushPromises()
    const input = document.querySelector<HTMLInputElement>('.el-dialog input')!
    input.value = '用户未保存页面'
    input.dispatchEvent(new Event('input', { bubbles: true }))
    complete({
      id: 101,
      title: target.title,
      lifecycleStatus: 'Published',
    } as KnowledgeDocumentDetail)
    await flushPromises()
    expect(input.value).toBe('用户未保存页面')
    expect(document.body.textContent).not.toContain('知识文档 · 当前已发布标题')
    noWrites()
  })
  it('re-resolves Published target into an unsaved new-page form without any write', async () => {
    const { router } = await setup(
      '?targetType=KnowledgeDocument&targetId=101&title=伪造&lifecycle=Published',
    )
    expect(getKnowledgeDocument).toHaveBeenCalledWith(101, expect.any(AbortSignal))
    expect(apiClient.get).toHaveBeenCalledWith(
      expect.stringContaining('/admin/portal/targets?'),
      expect.anything(),
    )
    expect(document.body.textContent).toContain('知识文档 · 当前已发布标题')
    expect(document.body.textContent).not.toContain('伪造')
    const title = document.querySelector<HTMLInputElement>('.el-dialog input')!
    title.value = '未保存页面'
    title.dispatchEvent(new Event('input', { bubbles: true }))
    await router.push('?targetType=KnowledgeDocument&targetId=202')
    await flushPromises()
    expect(title.value).toBe('未保存页面')
    expect(getKnowledgeDocument).toHaveBeenCalledTimes(1)
    noWrites()
  })
  it.each(['0', '-1', '1.5', '9007199254740992', 'abc', '101&targetId=102'])(
    'rejects unsafe query ID %s',
    async (id) => {
      const { wrapper } = await setup('?targetType=KnowledgeDocument&targetId=' + id)
      expect(wrapper.text()).toContain('无法使用该知识文档作为门户目标')
      expect(getKnowledgeDocument).not.toHaveBeenCalled()
      noWrites()
    },
  )
  it.each(['Draft', 'Archived'])('does not accept canonical %s', async (lifecycleStatus) => {
    vi.mocked(getKnowledgeDocument).mockResolvedValue({
      id: 101,
      title: '不能发布',
      lifecycleStatus,
    } as KnowledgeDocumentDetail)
    const { wrapper } = await setup('?targetType=KnowledgeDocument&targetId=101')
    expect(wrapper.text()).toContain('无法使用该知识文档作为门户目标')
    expect(vi.mocked(apiClient.get).mock.calls.some(([url]) => url.includes('/targets?'))).toBe(
      false,
    )
    noWrites()
  })
  it.each(['deleted', 'forbidden', 'missing'])('rejects %s current target', async () => {
    vi.mocked(getKnowledgeDocument).mockRejectedValue(new Error('不可读取'))
    const { wrapper } = await setup('?targetType=KnowledgeDocument&targetId=101')
    expect(wrapper.text()).toContain('无法使用该知识文档作为门户目标')
    noWrites()
  })
  it.each(['Draft', 'Archived', 'missing'])(
    'rechecks Portal target state %s',
    async (lifecycle) => {
      candidates = lifecycle === 'missing' ? [] : [{ ...target, lifecycle }]
      const { wrapper } = await setup('?targetType=KnowledgeDocument&targetId=101')
      expect(wrapper.text()).toContain('无法使用该知识文档作为门户目标')
      noWrites()
    },
  )
  it.each(['', '?targetType=System&targetId=101'])(
    'preserves normal management entry %s',
    async (query) => {
      const { wrapper } = await setup(query)
      expect(wrapper.text()).toContain('选择或新建 Portal 页面')
      expect(wrapper.text()).not.toContain('无法使用该知识文档作为门户目标')
      expect(getKnowledgeDocument).not.toHaveBeenCalled()
      expect(document.querySelector('.el-dialog')).toBeNull()
      noWrites()
    },
  )
})
