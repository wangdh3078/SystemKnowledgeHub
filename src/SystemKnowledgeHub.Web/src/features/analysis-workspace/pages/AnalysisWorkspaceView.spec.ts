import { defineComponent, h, reactive, ref } from 'vue'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createMemoryHistory, createRouter, RouterView } from 'vue-router'
import ElementPlus, { ElMessageBox, ElSelect, ElTree } from 'element-plus'
import AnalysisWorkspaceView from './AnalysisWorkspaceView.vue'
import { routes } from '../../../app/router/routes'
import { navigationItems } from '../../../app/router/navigation'
import { ApiError } from '../../../api/errors/ApiError'
import * as api from '../api/analysisWorkspaceApi'
import type { AnalysisNode, AnalysisTree } from '../api/analysisWorkspaceContracts'
import ExistingDocumentPicker from '../components/ExistingDocumentPicker.vue'
import { getKnowledgeDocuments } from '../../knowledge-documents/api/knowledgeDocumentsApi'
vi.mock('../../knowledge-documents/api/knowledgeDocumentsApi', () => ({
  getKnowledgeDocuments: vi.fn(),
}))

enableAutoUnmount(afterEach)
const state = vi.hoisted(() => ({
  canEdit: true,
  isAdministrator: false,
  allowLeave: true,
  requestLeave: vi.fn(),
  mounted: vi.fn(),
}))
const overlays = reactive({
  currentDialog: null as { kind: string } | null,
  openDialog(value: { kind: string }) {
    this.currentDialog = value
  },
  closeDialog() {
    this.currentDialog = null
  },
})
vi.mock('../../../app/stores/actor', () => ({ useActorStore: () => state }))
vi.mock('../../../app/stores/overlays', () => ({ useOverlayStore: () => overlays }))
vi.mock('element-plus', async (original) => ({
  ...(await original<typeof import('element-plus')>()),
  ElMessageBox: { confirm: vi.fn() },
  ElMessage: { success: vi.fn() },
}))
vi.mock('../api/analysisWorkspaceApi', () => ({
  getAnalysisTree: vi.fn(),
  createAnalysisFolder: vi.fn(),
  createAnalysisDocument: vi.fn(),
  addAnalysisPlacement: vi.fn(),
  renameAnalysisFolder: vi.fn(),
  moveAnalysisNode: vi.fn(),
  reorderAnalysisChildren: vi.fn(),
  removeAnalysisPlacement: vi.fn(),
  deleteAnalysisFolder: vi.fn(),
}))
vi.mock('../../knowledge-documents/components/KnowledgeDocumentDetailPanel.vue', () => ({
  default: defineComponent({
    name: 'KnowledgeDocumentDetailPanel',
    props: { documentId: { type: Number, required: true }, autoEdit: Boolean, embedded: Boolean },
    emits: ['updated', 'unavailable'],
    setup(props, { expose, emit }) {
      const buffer = ref('')
      state.mounted()
      expose({
        requestLeave: () => {
          state.requestLeave()
          return Promise.resolve(state.allowLeave)
        },
        finishEdit: () => {
          buffer.value = ''
        },
      })
      return () =>
        h('section', { class: 'document-panel' }, [
          h('span', `文档 ${props.documentId} ${props.autoEdit ? '自动编辑' : '阅读'}`),
          h('input', {
            'aria-label': '测试正文',
            value: buffer.value,
            onInput: (event: Event) => {
              buffer.value = (event.target as HTMLInputElement).value
            },
          }),
          h(
            'button',
            {
              onClick: () =>
                emit('updated', {
                  id: props.documentId,
                  title: '保存后标题',
                  documentType: 'DesignNote',
                  lifecycleStatus: 'Draft',
                }),
            },
            '模拟保存完成',
          ),
          h('button', { onClick: () => emit('unavailable', props.documentId) }, '模拟知识删除完成'),
        ])
    },
  }),
}))
const token = `a1.${'A'.repeat(64)}`
const nextToken = `a1.${'B'.repeat(64)}`
const folder: AnalysisNode = {
  id: 10,
  parentId: null,
  nodeType: 'Folder',
  title: 'MES 分析',
  knowledgeDocumentId: null,
  documentType: null,
  lifecycleStatus: null,
  availability: 'Available',
  concurrencyToken: 'node-10',
  sortOrder: 0,
  createdAt: '2026-09-08T00:00:00Z',
  updatedAt: '2026-09-08T00:00:00Z',
}
const docA: AnalysisNode = {
  ...folder,
  id: 20,
  parentId: 10,
  nodeType: 'Document',
  title: '分析 A',
  knowledgeDocumentId: 101,
  documentType: 'DesignNote',
  lifecycleStatus: 'Draft',
}
const docB: AnalysisNode = {
  ...docA,
  id: 30,
  title: '分析 B',
  knowledgeDocumentId: 202,
  sortOrder: 1,
  concurrencyToken: 'node-30',
}
const tree: AnalysisTree = { items: [folder, docA, docB], treeConcurrencyToken: token }
beforeEach(() => {
  vi.clearAllMocks()
  state.canEdit = true
  state.isAdministrator = false
  vi.mocked(getKnowledgeDocuments).mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 })
  state.allowLeave = true
  overlays.currentDialog = null
  document.body.innerHTML = '<div id="dialog-feature-content"></div>'
  vi.mocked(api.getAnalysisTree).mockReset().mockResolvedValue(tree)
  vi.mocked(ElMessageBox.confirm)
    .mockReset()
    .mockResolvedValue(undefined as never)
})
afterEach(() => {
  document.body.innerHTML = ''
})
async function setup(path = '/analysis') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      ...routes.filter(
        (route) => route.name === 'analysis-workspace' || route.name === 'analysis-workspace-node',
      ),
      { path: '/outside', component: { template: '<p>外部页面</p>' } },
      { path: '/portal-management', component: { template: '<p>门户管理</p>' } },
    ],
  })
  await router.push(path)
  await router.isReady()
  const wrapper = mount(RouterView, {
    attachTo: document.body,
    global: { plugins: [router, ElementPlus] },
  })
  await flushPromises()
  return { wrapper, router }
}
type Wrapper = Awaited<ReturnType<typeof setup>>['wrapper']
function button(wrapper: Wrapper, label: string) {
  const value = wrapper.findAll('button').find((item) => item.text() === label)
  if (!value) throw new Error(`缺少按钮 ${label}`)
  return value
}
function dialogButton(label: string): HTMLButtonElement {
  const value = [...document.querySelectorAll<HTMLButtonElement>('.analysis-dialog button')].find(
    (item) => item.textContent === label,
  )
  if (!value) throw new Error(`缺少对话框按钮 ${label}`)
  return value
}
async function setTitle(value: string) {
  const input = document.querySelector<HTMLInputElement>('.analysis-dialog input.el-input__inner')!
  input.value = value
  input.dispatchEvent(new Event('input', { bubbles: true }))
  await flushPromises()
}
describe('Analysis authoring workspace', () => {
  it('uses both authenticated routes and a Viewer-visible menu between knowledge and unknown items', async () => {
    const entry = routes.find((route) => route.name === 'analysis-workspace')!
    const selected = routes.find((route) => route.name === 'analysis-workspace-node')!
    expect(entry.component).toBe(AnalysisWorkspaceView)
    expect(selected.component).toBe(entry.component)
    expect(selected.meta).toMatchObject({
      layout: 'app-shell',
      navigationKey: 'analysis',
      hasContextRail: false,
    })
    const index = navigationItems.findIndex((item) => item.key === 'analysis')
    expect(navigationItems.slice(index - 1, index + 2).map((item) => item.key)).toEqual([
      'knowledge-documents',
      'analysis',
      'unknown-items',
    ])
    expect(navigationItems[index]?.minimumAccessLevel).toBeUndefined()
    vi.mocked(api.getAnalysisTree).mockResolvedValue({ items: [], treeConcurrencyToken: token })
    const { wrapper } = await setup()
    expect(wrapper.text()).toContain('暂无分析内容')
    expect(wrapper.text()).toContain('从左侧目录选择一个文档开始查看')
  })
  it('shows folders and resolves canonical document IDs, with Viewer organization controls absent', async () => {
    state.canEdit = false
    const { wrapper, router } = await setup('/analysis/nodes/10')
    expect(wrapper.text()).toContain('包含 2 个直接子项')
    expect(wrapper.findAll('button').map((item) => item.text())).not.toContain('新建目录')
    await router.push('/analysis/nodes/20')
    await flushPromises()
    expect(wrapper.find('.document-panel').text()).toContain('文档 101')
    expect(wrapper.find('[aria-label="目录组织操作"]').exists()).toBe(false)
  })
  it('creates a folder, adopts returned tree without GET and selects it', async () => {
    const created = { ...folder, id: 40, parentId: 10, title: '子目录', sortOrder: 2 }
    vi.mocked(api.createAnalysisFolder).mockResolvedValue({
      items: [...tree.items, created],
      treeConcurrencyToken: nextToken,
      node: created,
    })
    const { wrapper, router } = await setup('/analysis/nodes/10')
    await button(wrapper, '新建目录').trigger('click')
    await setTitle('子目录')
    dialogButton('确认').click()
    await flushPromises()
    expect(api.createAnalysisFolder).toHaveBeenCalledWith({
      parentId: 10,
      title: '子目录',
      treeConcurrencyToken: token,
    })
    expect(router.currentRoute.value.params.nodeId).toBe('40')
    expect(api.getAnalysisTree).toHaveBeenCalledTimes(1)
  })
  it('creates one atomic DesignNote under the selected document parent and enters the shared editor', async () => {
    const created = { ...docA, id: 40, knowledgeDocumentId: 303, sortOrder: 2, title: '新分析' }
    vi.mocked(api.createAnalysisDocument).mockResolvedValue({
      items: [...tree.items, created],
      treeConcurrencyToken: nextToken,
      node: created,
    })
    const { wrapper, router } = await setup('/analysis/nodes/20')
    await button(wrapper, '新建分析文档').trigger('click')
    await setTitle('新分析')
    dialogButton('确认').click()
    await flushPromises()
    expect(api.createAnalysisDocument).toHaveBeenCalledExactlyOnceWith({
      parentId: 10,
      title: '新分析',
      documentType: 'DesignNote',
      treeConcurrencyToken: token,
    })
    expect(router.currentRoute.value.params.nodeId).toBe('40')
    expect(wrapper.find('.document-panel').text()).toContain('文档 303 自动编辑')
    expect(api.getAnalysisTree).toHaveBeenCalledTimes(1)
    await router.push('/analysis/nodes/20')
    await router.push('/analysis/nodes/40')
    await flushPromises()
    expect(wrapper.find('.document-panel').text()).toContain('文档 303 阅读')
  })
  it('renames only Folder and excludes itself/descendants from move destinations', async () => {
    const child = { ...folder, id: 40, parentId: 10, sortOrder: 2, title: '子目录' }
    vi.mocked(api.getAnalysisTree).mockResolvedValue({ ...tree, items: [...tree.items, child] })
    vi.mocked(api.renameAnalysisFolder).mockResolvedValue({
      ...tree,
      node: { ...folder, title: 'MES 新名' },
      items: [{ ...folder, title: 'MES 新名' }, docA, docB, child],
      treeConcurrencyToken: nextToken,
    })
    const { wrapper } = await setup('/analysis/nodes/10')
    await button(wrapper, '重命名目录').trigger('click')
    await setTitle('MES 新名')
    dialogButton('确认').click()
    await flushPromises()
    expect(api.renameAnalysisFolder).toHaveBeenCalledWith(10, {
      title: 'MES 新名',
      treeConcurrencyToken: token,
      nodeConcurrencyToken: 'node-10',
    })
    await button(wrapper, '移动').trigger('click')
    await flushPromises()
    expect(document.querySelectorAll('.analysis-dialog .el-select').length).toBe(1)
    expect(
      wrapper
        .findAllComponents(ElSelect)
        .flatMap((item) => item.findAllComponents({ name: 'ElOption' })).length,
    ).toBe(1)
  })
  it('reorders the complete sibling set and moves to append using post-removal sibling count', async () => {
    vi.mocked(api.reorderAnalysisChildren).mockResolvedValue({
      ...tree,
      node: null,
      treeConcurrencyToken: nextToken,
      items: [folder, { ...docA, sortOrder: 1 }, { ...docB, sortOrder: 0 }],
    })
    vi.mocked(api.moveAnalysisNode).mockResolvedValue({ ...tree, node: docA })
    const { wrapper } = await setup('/analysis/nodes/20')
    expect(button(wrapper, '上移').attributes('disabled')).toBeDefined()
    await button(wrapper, '下移').trigger('click')
    await flushPromises()
    expect(api.reorderAnalysisChildren).toHaveBeenCalledWith({
      parentId: 10,
      treeConcurrencyToken: token,
      items: [
        { id: 30, nodeConcurrencyToken: 'node-30' },
        { id: 20, nodeConcurrencyToken: 'node-10' },
      ],
    })
    expect(button(wrapper, '下移').attributes('disabled')).toBeDefined()
    await button(wrapper, '移动').trigger('click')
    await flushPromises()
    dialogButton('确认').click()
    await flushPromises()
    expect(api.moveAnalysisNode).toHaveBeenCalledWith(20, {
      nodeConcurrencyToken: 'node-10',
      treeConcurrencyToken: nextToken,
      targetParentId: 10,
      targetPosition: 1,
    })
  })
  it('guards A→B, A→Folder/root, leaving, and browser back before committing route/selection', async () => {
    const { wrapper, router } = await setup('/analysis/nodes/20')
    await wrapper.get('input[aria-label="测试正文"]').setValue('保留草稿')
    state.allowLeave = false
    await wrapper
      .findAll('.analysis-tree-row')
      .find((row) => row.text().includes('分析 B'))!
      .trigger('click')
    await flushPromises()
    expect(wrapper.findComponent(ElTree).vm.getCurrentKey()).toBe(20)
    for (const path of ['/analysis/nodes/30', '/analysis/nodes/10', '/analysis', '/outside']) {
      await router.push(path)
      expect(router.currentRoute.value.params.nodeId).toBe('20')
      expect(wrapper.get<HTMLInputElement>('input[aria-label="测试正文"]').element.value).toBe(
        '保留草稿',
      )
    }
    state.allowLeave = true
    await router.push('/analysis/nodes/30')
    await flushPromises()
    state.allowLeave = false
    router.back()
    await flushPromises()
    expect(router.currentRoute.value.params.nodeId).toBe('30')
    state.allowLeave = true
    router.back()
    await flushPromises()
    expect(router.currentRoute.value.params.nodeId).toBe('20')
  })
  it('guards removal and removes only placement before returning to its parent', async () => {
    vi.mocked(api.removeAnalysisPlacement).mockResolvedValue({
      items: [folder, { ...docB, sortOrder: 0 }],
      treeConcurrencyToken: nextToken,
      node: null,
    })
    const { wrapper, router } = await setup('/analysis/nodes/20')
    state.allowLeave = false
    await button(wrapper, '从分析目录移除').trigger('click')
    await flushPromises()
    expect(api.removeAnalysisPlacement).not.toHaveBeenCalled()
    expect(ElMessageBox.confirm).not.toHaveBeenCalled()
    state.allowLeave = true
    await button(wrapper, '从分析目录移除').trigger('click')
    await flushPromises()
    expect(ElMessageBox.confirm).toHaveBeenCalledWith(
      '确认从分析目录移除？知识文档本身不会被删除。',
      '从分析目录移除',
      expect.any(Object),
    )
    expect(api.removeAnalysisPlacement).toHaveBeenCalledWith(20, {
      nodeConcurrencyToken: 'node-10',
      treeConcurrencyToken: token,
    })
    expect(router.currentRoute.value.params.nodeId).toBe('10')
  })
  it('disables nonempty Folder deletion and deletes an empty Folder', async () => {
    const { wrapper } = await setup('/analysis/nodes/10')
    expect(button(wrapper, '删除空目录').attributes('disabled')).toBeDefined()
    vi.mocked(api.getAnalysisTree).mockResolvedValue({
      items: [folder],
      treeConcurrencyToken: nextToken,
    })
    vi.mocked(api.deleteAnalysisFolder).mockResolvedValue({
      items: [],
      treeConcurrencyToken: token,
      node: null,
    })
    await button(wrapper, '刷新').trigger('click')
    await flushPromises()
    await button(wrapper, '删除空目录').trigger('click')
    await flushPromises()
    expect(api.deleteAnalysisFolder).toHaveBeenCalledWith(10, {
      nodeConcurrencyToken: 'node-10',
      treeConcurrencyToken: nextToken,
    })
    expect(wrapper.text()).toContain('暂无分析内容')
  })
  it('refreshes 409 without replay/remount/buffer reset and updates title without changing tree token', async () => {
    vi.mocked(api.reorderAnalysisChildren).mockRejectedValue(
      new ApiError(409, { code: 'conflict', message: 'stale', fieldErrors: null, details: null }),
    )
    vi.mocked(api.getAnalysisTree)
      .mockResolvedValueOnce(tree)
      .mockResolvedValue({ ...tree, treeConcurrencyToken: nextToken })
    const { wrapper } = await setup('/analysis/nodes/20')
    await wrapper.get('input[aria-label="测试正文"]').setValue('保留草稿')
    await button(wrapper, '下移').trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('分析目录已被其他操作修改，已刷新最新目录。')
    expect(wrapper.get<HTMLInputElement>('input[aria-label="测试正文"]').element.value).toBe(
      '保留草稿',
    )
    expect(state.mounted).toHaveBeenCalledTimes(1)
    expect(api.reorderAnalysisChildren).toHaveBeenCalledTimes(1)
    await button(wrapper, '模拟保存完成').trigger('click')
    expect(wrapper.find('.analysis-tree').text()).toContain('保存后标题')
    await button(wrapper, '下移').trigger('click')
    expect(api.reorderAnalysisChildren).toHaveBeenLastCalledWith(
      expect.objectContaining({ treeConcurrencyToken: nextToken }),
    )
  })
  it('fails closed on tree load failure while retaining the already-loaded document', async () => {
    const { wrapper } = await setup('/analysis/nodes/20')
    vi.mocked(api.getAnalysisTree).mockRejectedValue(new Error('目录读取失败'))
    await button(wrapper, '刷新').trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('目录读取失败')
    expect(button(wrapper, '移动').attributes('disabled')).toBeDefined()
    expect(wrapper.find('.document-panel').text()).toContain('文档 101')
  })
  it('does not mount/read unavailable content and leaves Archived placement organization available', async () => {
    vi.mocked(api.getAnalysisTree).mockResolvedValue({
      ...tree,
      items: [
        folder,
        {
          ...docA,
          availability: 'Unavailable',
          title: '文档不可用',
          documentType: null,
          lifecycleStatus: null,
        },
        { ...docB, lifecycleStatus: 'Archived' },
      ],
    })
    const { wrapper, router } = await setup('/analysis/nodes/20')
    expect(wrapper.text()).toContain('该知识文档当前已不可读取')
    expect(state.mounted).not.toHaveBeenCalled()
    expect(wrapper.text()).not.toContain('分析 A')
    expect(button(wrapper, '移动').attributes('disabled')).toBeUndefined()
    await router.push('/analysis/nodes/30')
    await flushPromises()
    expect(wrapper.text()).toContain('已归档')
    expect(button(wrapper, '从分析目录移除').attributes('disabled')).toBeUndefined()
    await button(wrapper, '模拟知识删除完成').trigger('click')
    expect(wrapper.find('.document-panel').exists()).toBe(false)
    expect(router.currentRoute.value.params.nodeId).toBe('30')
  })
  it('preserves create input on 409, refreshes once and requires an explicit new submission', async () => {
    vi.mocked(api.createAnalysisFolder).mockRejectedValueOnce(
      new ApiError(409, { code: 'conflict', message: 'stale', fieldErrors: null, details: null }),
    )
    const { wrapper } = await setup('/analysis/nodes/10')
    await button(wrapper, '新建目录').trigger('click')
    await setTitle('未提交目录名称')
    dialogButton('确认').click()
    await flushPromises()
    expect(
      document.querySelector<HTMLInputElement>('.analysis-dialog input.el-input__inner')!.value,
    ).toBe('未提交目录名称')
    expect(document.querySelector('.analysis-dialog')!.textContent).toContain('已刷新最新目录')
    expect(api.createAnalysisFolder).toHaveBeenCalledTimes(1)
    expect(api.getAnalysisTree).toHaveBeenCalledTimes(2)
  })
  it('hides newly unavailable content on tree refresh without destroying a dirty buffer or its leave guard', async () => {
    const { wrapper, router } = await setup('/analysis/nodes/20')
    await wrapper.get('input[aria-label="测试正文"]').setValue('保留尚未丢弃的缓冲区')
    vi.mocked(api.getAnalysisTree).mockResolvedValue({
      ...tree,
      items: [
        folder,
        {
          ...docA,
          availability: 'Unavailable',
          title: '文档不可用',
          documentType: null,
          lifecycleStatus: null,
        },
        docB,
      ],
    })
    await button(wrapper, '刷新').trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('该知识文档当前已不可读取')
    expect(wrapper.find('.document-panel').isVisible()).toBe(false)
    expect(wrapper.get<HTMLInputElement>('input[aria-label="测试正文"]').element.value).toBe(
      '保留尚未丢弃的缓冲区',
    )
    expect(state.mounted).toHaveBeenCalledTimes(1)
    state.allowLeave = false
    await router.push('/analysis/nodes/30')
    expect(router.currentRoute.value.params.nodeId).toBe('20')
  })
})

describe('B03 placement, title filter and handoff', () => {
  it('filters literal titles with ancestors, without changing dirty selection or remounting', async () => {
    vi.mocked(api.getAnalysisTree).mockResolvedValue({
      ...tree,
      items: [folder, { ...docA, title: 'Lot %/_ Track' }, docB],
    })
    const { wrapper, router } = await setup('/analysis/nodes/20')
    await wrapper.get('[aria-label="测试正文"]').setValue('未保存')
    const filter = wrapper.get('input[aria-label="筛选目录和文档标题"]')
    const mounted = state.mounted.mock.calls.length
    await filter.setValue('  LOT %/_  ')
    expect(wrapper.findAll('.analysis-tree-row').map((node) => node.text())).toEqual([
      'MES 分析',
      'Lot %/_ Track设计说明 · 草稿',
    ])
    expect(button(wrapper, '下移').attributes('disabled')).toBeDefined()
    await button(wrapper, '下移').trigger('click')
    expect(api.reorderAnalysisChildren).not.toHaveBeenCalled()
    await filter.setValue('MES')
    expect(wrapper.findAll('.analysis-tree-row')).toHaveLength(1)
    expect(wrapper.text()).toContain('当前选中项已被筛选隐藏')
    await filter.setValue('没有匹配')
    expect(wrapper.text()).toContain('未找到匹配的目录或文档')
    expect(router.currentRoute.value.params.nodeId).toBe('20')
    expect((wrapper.get('[aria-label="测试正文"]').element as HTMLInputElement).value).toBe(
      '未保存',
    )
    expect(state.requestLeave).not.toHaveBeenCalled()
    expect(state.mounted).toHaveBeenCalledTimes(mounted)
    await filter.setValue('   ')
    expect(wrapper.findAll('.analysis-tree-row')).toHaveLength(3)
  })
  it('never filters unavailable nodes by stale titles', async () => {
    vi.mocked(api.getAnalysisTree).mockResolvedValue({
      ...tree,
      items: [
        folder,
        {
          ...docA,
          availability: 'Unavailable',
          title: '删除前标题',
          documentType: null,
          lifecycleStatus: null,
        },
      ],
    })
    const { wrapper } = await setup()
    const filter = wrapper.get('input[aria-label="筛选目录和文档标题"]')
    await filter.setValue('删除前标题')
    expect(wrapper.findAll('.analysis-tree-row')).toHaveLength(0)
    await filter.setValue('文档不可用')
    expect(wrapper.findAll('.analysis-tree-row')).toHaveLength(2)
    expect(wrapper.text()).not.toContain('删除前标题')
  })
  it.each(['/analysis', '/analysis/nodes/10', '/analysis/nodes/20'])(
    'places existing documents in context and opens read mode: %s',
    async (path) => {
      const { wrapper, router } = await setup(path)
      const parentId = path === '/analysis' ? null : 10
      const placed = {
        ...docA,
        id: 40,
        knowledgeDocumentId: 303,
        parentId,
        sortOrder: parentId === null ? 1 : 2,
      }
      vi.mocked(api.addAnalysisPlacement).mockResolvedValue({
        ...tree,
        items: [...tree.items, placed],
        treeConcurrencyToken: nextToken,
        node: placed,
      })
      await button(wrapper, '加入已有文档').trigger('click')
      await flushPromises()
      wrapper.findComponent(ExistingDocumentPicker).vm.$emit('select', 303)
      await flushPromises()
      expect(api.addAnalysisPlacement).toHaveBeenCalledWith({
        parentId,
        knowledgeDocumentId: 303,
        treeConcurrencyToken: token,
      })
      expect(api.getAnalysisTree).toHaveBeenCalledTimes(1)
      expect(router.currentRoute.value.params.nodeId).toBe('40')
      expect(wrapper.text()).toContain('文档 303 阅读')
      expect(api.createAnalysisDocument).not.toHaveBeenCalled()
    },
  )
  it.each([409, 422])('keeps placement input and reports %s without replay', async (status) => {
    vi.mocked(api.addAnalysisPlacement).mockRejectedValue(
      new ApiError(status, {
        code: 'validation_error',
        message: '该文档已在分析目录中。',
        fieldErrors: null,
        details: null,
      }),
    )
    const { wrapper } = await setup('/analysis/nodes/10')
    await button(wrapper, '加入已有文档').trigger('click')
    await flushPromises()
    wrapper.findComponent(ExistingDocumentPicker).vm.$emit('select', 303)
    await flushPromises()
    expect(api.addAnalysisPlacement).toHaveBeenCalledTimes(1)
    expect(api.getAnalysisTree).toHaveBeenCalledTimes(status === 409 ? 2 : 1)
    expect(document.querySelector('.analysis-dialog')?.textContent).toContain(
      status === 409 ? '已刷新最新目录' : '该文档已在分析目录中。',
    )
  })
  it('rejects duplicate submission and locates the full-tree placement', async () => {
    const { wrapper, router } = await setup()
    await button(wrapper, '加入已有文档').trigger('click')
    await flushPromises()
    wrapper.findComponent(ExistingDocumentPicker).vm.$emit('select', 101)
    await flushPromises()
    expect(api.addAnalysisPlacement).not.toHaveBeenCalled()
    wrapper.findComponent(ExistingDocumentPicker).vm.$emit('locate', docA)
    await flushPromises()
    expect(router.currentRoute.value.params.nodeId).toBe('20')
  })
  it.each(['Draft', 'Archived', 'Unavailable', 'Editor', 'Viewer'])(
    'does not hand off %s',
    async (kind) => {
      state.isAdministrator = !['Editor', 'Viewer'].includes(kind)
      state.canEdit = kind !== 'Viewer'
      const node = {
        ...docA,
        lifecycleStatus:
          kind === 'Archived'
            ? ('Archived' as const)
            : kind === 'Draft'
              ? ('Draft' as const)
              : ('Published' as const),
        availability: kind === 'Unavailable' ? ('Unavailable' as const) : ('Available' as const),
      }
      vi.mocked(api.getAnalysisTree).mockResolvedValue({ ...tree, items: [folder, node] })
      const { wrapper } = await setup('/analysis/nodes/20')
      expect(wrapper.text()).not.toContain('在知识门户管理中使用')
      if (kind === 'Draft') expect(wrapper.text()).toContain('请先发布知识文档。')
      if (kind === 'Viewer') expect(wrapper.text()).not.toContain('加入已有文档')
    },
  )
  it('guards dirty handoff then navigates using canonical document identity', async () => {
    state.isAdministrator = true
    vi.mocked(api.getAnalysisTree).mockResolvedValue({
      ...tree,
      items: [folder, { ...docA, lifecycleStatus: 'Published' }],
    })
    const { wrapper, router } = await setup('/analysis/nodes/20')
    await wrapper.get('[aria-label="测试正文"]').setValue('未保存')
    state.allowLeave = false
    await button(wrapper, '在知识门户管理中使用').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.params.nodeId).toBe('20')
    expect((wrapper.get('[aria-label="测试正文"]').element as HTMLInputElement).value).toBe(
      '未保存',
    )
    state.allowLeave = true
    await button(wrapper, '在知识门户管理中使用').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.fullPath).toBe(
      '/portal-management?targetType=KnowledgeDocument&targetId=101',
    )
  })
})
