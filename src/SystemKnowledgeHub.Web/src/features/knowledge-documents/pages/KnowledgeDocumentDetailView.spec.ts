import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ElMessageBox } from 'element-plus'
import { ApiError } from '../../../api/errors/ApiError'
import type { KnowledgeDocumentDetail } from '../api/knowledgeDocumentContracts'
import KnowledgeDocumentDetailView from './KnowledgeDocumentDetailView.vue'
import {
  getKnowledgeDocument,
  getKnowledgeDocumentRevision,
  listKnowledgeDocumentRevisions,
  updateKnowledgeDocumentContent,
} from '../api/knowledgeDocumentsApi'
import { getRelatedKnowledge } from '../../relationships/api/relationshipApi'
import { getEvidenceList } from '../../evidence/api/evidenceApi'

enableAutoUnmount(afterEach)

const actorState = vi.hoisted(() => ({ canEdit: true }))
const overlayState = vi.hoisted(() => ({ openDrawer: vi.fn(), openDialog: vi.fn() }))
const routerState = vi.hoisted(() => ({ push: vi.fn() }))

vi.mock('vue', async (importOriginal) => {
  const actual = await importOriginal<typeof import('vue')>()
  return {
    ...actual,
    defineAsyncComponent: () =>
      actual.defineComponent({
        name: 'KnowledgeDocumentEditor',
        props: { modelValue: { type: String, required: true } },
        emits: ['update:modelValue', 'ready'],
        setup(_props, { emit }) {
          return () =>
            actual.h(
              'button',
              {
                type: 'button',
                onClick: () => emit('update:modelValue', '## 新步骤\n\n1. 已修改'),
              },
              '修改正文',
            )
        },
      }),
  }
})
vi.mock('vue-router', () => ({
  onBeforeRouteLeave: vi.fn(),
  useRoute: () => ({ params: { id: '1' } }),
  useRouter: () => routerState,
}))
vi.mock('element-plus', () => ({
  ElMessage: { success: vi.fn() },
  ElMessageBox: { confirm: vi.fn() },
}))
vi.mock('../api/knowledgeDocumentsApi', () => ({
  getKnowledgeDocument: vi.fn(),
  getKnowledgeDocumentRevision: vi.fn(),
  listKnowledgeDocumentRevisions: vi.fn(),
  updateKnowledgeDocumentContent: vi.fn(),
  updateKnowledgeDocumentLifecycle: vi.fn(),
}))
vi.mock('../../../app/stores/actor', () => ({
  useActorStore: () => ({
    get canEdit() {
      return actorState.canEdit
    },
    refreshCurrentUser: vi.fn(),
  }),
}))
vi.mock('../../../app/stores/overlays', () => ({ useOverlayStore: () => overlayState }))
vi.mock('../../relationships/api/relationshipApi', () => ({
  getRelatedKnowledge: vi.fn().mockResolvedValue([]),
  deleteRelationship: vi.fn(),
}))
vi.mock('../../evidence/api/evidenceApi', () => ({
  getEvidenceList: vi.fn().mockResolvedValue({ items: [] }),
}))
const detail: KnowledgeDocumentDetail = {
  id: 1,
  documentType: 'Sop',
  title: 'Oracle 数据库连接异常处理',
  summary: '原摘要',
  bodyMarkdown: '## 步骤\n\n1. 检查连接',
  lifecycleStatus: 'Draft',
  knowledgeStatus: 'Unknown',
  createdByUserId: 1,
  createdByDisplayName: '编辑者',
  updatedByUserId: 1,
  updatedByDisplayName: '编辑者',
  createdAt: '2026-08-22T12:00:00Z',
  updatedAt: '2026-08-22T12:00:00Z',
  publishedAt: null,
  archivedAt: null,
  currentRevisionNumber: 1,
  latestPublishedRevisionNumber: null,
  confirmationCoverage: { state: 'NoConfirmation', lastConfirmedRevisionNumber: null },
  concurrencyToken: 'token-1',
}

const components = {
  ElButton: {
    props: { disabled: Boolean },
    emits: ['click'],
    template: '<button :disabled="disabled" @click="$emit(\'click\')"><slot /></button>',
  },
  ElTag: { template: '<span><slot /></span>' },
  ElForm: { template: '<form><slot /></form>' },
  ElFormItem: { template: '<div><slot /></div>' },
  ElInput: {
    props: { modelValue: { type: String, required: true } },
    emits: ['update:modelValue'],
    template:
      '<textarea :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
  },
  ElIcon: { template: '<span><slot /></span>' },
  ElPagination: { template: '<div><slot /></div>' },
  KnowledgeStatusBadge: { template: '<span>知识状态</span>' },
}

function mountView() {
  return mount(KnowledgeDocumentDetailView, { global: { components } })
}

function button(wrapper: ReturnType<typeof mountView>, label: string) {
  return wrapper.findAll('button').find((candidate) => candidate.text() === label)
}

describe('KnowledgeDocumentDetailView editing', () => {
  beforeEach(() => {
    actorState.canEdit = true
    vi.mocked(getKnowledgeDocument).mockReset()
    vi.mocked(updateKnowledgeDocumentContent).mockReset()
    vi.mocked(listKnowledgeDocumentRevisions).mockReset()
    vi.mocked(getKnowledgeDocumentRevision).mockReset()
    vi.mocked(ElMessageBox.confirm).mockReset()
    vi.mocked(getRelatedKnowledge).mockReset()
    vi.mocked(getEvidenceList).mockReset()
    vi.mocked(getKnowledgeDocument).mockResolvedValue(detail)
    vi.mocked(getRelatedKnowledge).mockResolvedValue([])
    vi.mocked(getEvidenceList).mockResolvedValue({ items: [] })
    vi.mocked(listKnowledgeDocumentRevisions).mockResolvedValue({
      items: [
        {
          id: 11,
          revisionNumber: 1,
          revisionOrigin: 'Created',
          lifecycleContext: 'Draft',
          authorUserId: 1,
          authorDisplayName: '编辑者',
          createdAt: '2026-08-22T12:00:00Z',
          changeSummary: null,
          restoreReason: null,
          restoredFromRevisionNumber: null,
          isCurrent: true,
          isLatestPublished: false,
        },
      ],
      page: 1,
      pageSize: 20,
      total: 1,
    })
    vi.mocked(getKnowledgeDocumentRevision).mockResolvedValue({
      id: 11,
      knowledgeDocumentId: 1,
      revisionNumber: 1,
      revisionOrigin: 'Created',
      lifecycleContext: 'Draft',
      authorUserId: 1,
      authorDisplayName: '编辑者',
      createdAt: '2026-08-22T12:00:00Z',
      changeSummary: null,
      restoreReason: null,
      restoredFromRevisionNumber: null,
      isCurrent: true,
      isLatestPublished: false,
      title: '历史标题',
      summary: null,
      bodyMarkdown: '## 历史正文',
    })
    overlayState.openDrawer.mockReset()
    overlayState.openDialog.mockReset()
    routerState.push.mockReset()
  })

  it('previews unsaved Markdown and saves title, summary, body and token atomically', async () => {
    const updated = {
      ...detail,
      title: '更新后的 SOP',
      summary: null,
      bodyMarkdown: '## 新步骤\n\n1. 已修改',
      concurrencyToken: 'token-2',
    }
    vi.mocked(updateKnowledgeDocumentContent).mockResolvedValue(updated)
    const wrapper = mountView()
    await flushPromises()
    await button(wrapper, '编辑')?.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('编辑中')
    expect(wrapper.text()).toContain('已保存')
    expect(wrapper.text()).not.toContain('保存后新内容立即成为已发布内容并生成新修订。')

    await wrapper.findAll('textarea')[0].setValue('更新后的 SOP')
    expect(wrapper.text()).toContain('未保存')
    await wrapper.findAll('textarea')[1].setValue('')
    await button(wrapper, '修改正文')?.trigger('click')
    await button(wrapper, '预览')?.trigger('click')
    await flushPromises()
    expect(wrapper.html()).toContain('新步骤')
    expect(wrapper.text()).toContain('预览未保存内容')

    await button(wrapper, '保存')?.trigger('click')
    await flushPromises()
    expect(updateKnowledgeDocumentContent).toHaveBeenCalledWith(1, {
      title: '更新后的 SOP',
      summary: null,
      bodyMarkdown: '## 新步骤\n\n1. 已修改',
      concurrencyToken: 'token-1',
    })
    expect(wrapper.text()).toContain('已保存。')
    expect(wrapper.find('textarea').exists()).toBe(false)
    expect(wrapper.text()).toContain('更新后的 SOP')
  })

  it('shows the in-progress save state while the existing content request is pending', async () => {
    const deferredSave: { complete: ((value: KnowledgeDocumentDetail) => void) | null } = { complete: null }
    vi.mocked(updateKnowledgeDocumentContent).mockImplementation(
      () => new Promise<KnowledgeDocumentDetail>((resolve) => { deferredSave.complete = resolve }),
    )
    const wrapper = mountView()
    await flushPromises()
    await button(wrapper, '编辑')?.trigger('click')
    await wrapper.findAll('textarea')[0].setValue('保存状态验证')
    await button(wrapper, '保存')?.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('正在保存…')
    deferredSave.complete?.({ ...detail, title: '保存状态验证', concurrencyToken: 'token-2' })
    await flushPromises()
    expect(wrapper.text()).toContain('已保存。')
  })

  it('keeps the edit state and local content after a stale conflict', async () => {
    vi.mocked(updateKnowledgeDocumentContent).mockRejectedValue(
      new ApiError(409, {
        code: 'conflict',
        message: 'conflict',
        fieldErrors: null,
        details: null,
      }),
    )
    const wrapper = mountView()
    await flushPromises()
    await button(wrapper, '编辑')?.trigger('click')
    await flushPromises()
    await button(wrapper, '修改正文')?.trigger('click')
    await button(wrapper, '保存')?.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('文档已被其他操作修改。')
    expect(wrapper.text()).toContain('修改正文')
    expect(wrapper.text()).toContain('重新加载')
  })

  it('does not expose editing actions to a Viewer', async () => {
    actorState.canEdit = false
    const wrapper = mountView()
    await flushPromises()

    expect(button(wrapper, '编辑')).toBeUndefined()
    expect(button(wrapper, '发布')).toBeUndefined()
    expect(button(wrapper, '添加关联')).toBeUndefined()
    expect(button(wrapper, '添加证据')).toBeUndefined()
    expect(button(wrapper, '添加人工确认')).toBeUndefined()
    expect(button(wrapper, '修订历史（1）')).toBeDefined()
  })

  it('enters and returns from history mode on the existing detail route for a Viewer', async () => {
    actorState.canEdit = false
    const wrapper = mountView()
    await flushPromises()

    await button(wrapper, '修订历史（1）')?.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('历史标题')
    expect(wrapper.text()).toContain('历史正文')
    expect(wrapper.text()).toContain('返回当前内容')
    expect(routerState.push).not.toHaveBeenCalled()

    await button(wrapper, '返回当前内容')?.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('正文')
    expect(wrapper.text()).toContain('检查连接')
  })

  it('reuses the dirty discard guard before entering history and preserves edits on cancel', async () => {
    const wrapper = mountView()
    await flushPromises()
    await button(wrapper, '编辑')?.trigger('click')
    await wrapper.findAll('textarea')[0].setValue('尚未保存的标题')
    await flushPromises()
    expect(wrapper.text()).toContain('未保存')

    vi.mocked(ElMessageBox.confirm).mockImplementationOnce(() => Promise.reject('cancel'))
    await button(wrapper, '修订历史（1）')?.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('编辑中')
    expect(wrapper.findAll('textarea')[0].element.value).toBe('尚未保存的标题')
    expect(wrapper.text()).not.toContain('返回当前内容')

    vi.mocked(ElMessageBox.confirm).mockImplementationOnce(() => Promise.resolve('confirm' as never))
    await button(wrapper, '修订历史（1）')?.trigger('click')
    await flushPromises()
    expect(ElMessageBox.confirm).toHaveBeenCalledWith(
      '尚有未保存的修改，确认放弃？',
      '放弃编辑',
      expect.objectContaining({ confirmButtonText: '放弃修改', cancelButtonText: '继续编辑' }),
    )
    expect(wrapper.text()).toContain('返回当前内容')
    expect(wrapper.find('textarea').exists()).toBe(false)
  })

  it('loads document Evidence, opens existing drawers with a fixed document target, and keeps progression explicit', async () => {
    vi.mocked(getEvidenceList).mockResolvedValue({
      items: [
        {
          id: 41,
          evidenceType: 'ExistingDocument',
          knowledgeDocumentRevisionNumberSnapshot: null,
          sourceTitle: '已批准范围说明',
          sourceReference: 'REQ-001',
          sourceLocator: { section: 'scope' },
          summary: null,
          supportReason: '明确支持该文档的业务结论。',
          provider: { displayName: '提供者', roleOrIdentity: '业务代表', occurredAt: '2026-08-22T02:30:00Z', team: null, externalUserKey: null, source: null, note: null },
        },
      ],
    })
    const wrapper = mountView()
    await flushPromises()

    expect(getEvidenceList).toHaveBeenCalledWith('KnowledgeDocument', 1)
    expect(wrapper.text()).toContain('证据与人工确认')
    expect(wrapper.text()).toContain('保存后不会自动改变知识状态。')
    await button(wrapper, '添加证据')?.trigger('click')
    expect(overlayState.openDrawer).toHaveBeenCalledWith({
      kind: 'add-evidence',
      id: null,
      mode: 'create',
      payload: {
        subject: { type: 'KnowledgeDocument', id: 1 },
        title: '操作规程 · Oracle 数据库连接异常处理',
        knowledgeStatus: 'Unknown',
        subjectRevisionNumber: 1,
      },
    })
  })

  it('loads document relations once, opens the existing add-relation drawer, and routes to the related object', async () => {
    vi.mocked(getRelatedKnowledge).mockResolvedValue([
      {
        id: 71,
        direction: 'Outgoing',
        relationType: 'AppliesTo',
        related: { type: 'System', id: 12 },
        title: 'MES',
        objectTypeLabel: '系统',
      },
    ])
    const wrapper = mountView()
    await flushPromises()

    expect(getRelatedKnowledge).toHaveBeenCalledWith('KnowledgeDocument', 1)
    expect(wrapper.text()).toContain('指向')
    expect(wrapper.text()).toContain('适用于')
    await button(wrapper, '添加关联')?.trigger('click')
    expect(overlayState.openDrawer).toHaveBeenCalledWith({
      kind: 'add-relationship',
      id: 1,
      mode: 'create',
      payload: {
        source: { type: 'KnowledgeDocument', id: 1 },
        title: 'Oracle 数据库连接异常处理',
        documentType: 'Sop',
      },
    })
    await button(wrapper, '系统 · MES')?.trigger('click')
    expect(routerState.push).toHaveBeenCalledWith({ name: 'system-detail', params: { id: '12' } })
  })

  it('requires explicit confirmation for every dirty Published save and Cancel preserves local edits', async () => {
    const published = {
      ...detail,
      lifecycleStatus: 'Published' as const,
      currentRevisionNumber: 2,
      latestPublishedRevisionNumber: 2,
      publishedAt: '2026-08-23T01:00:00Z',
      concurrencyToken: 'published-token',
    }
    vi.mocked(getKnowledgeDocument).mockResolvedValue(published)
    const wrapper = mountView()
    await flushPromises()
    await button(wrapper, '编辑')?.trigger('click')
    expect(wrapper.text()).toContain('保存后新内容立即成为已发布内容并生成新修订。')
    await wrapper.findAll('textarea')[0].setValue('已发布内容的新标题')

    vi.mocked(ElMessageBox.confirm).mockRejectedValueOnce('cancel')
    await button(wrapper, '保存')?.trigger('click')
    await flushPromises()
    expect(ElMessageBox.confirm).toHaveBeenCalledWith(
      '保存后新内容立即成为已发布内容并生成新修订。',
      '确认保存已发布内容',
      expect.objectContaining({ confirmButtonText: '确认保存并立即发布' }),
    )
    expect(updateKnowledgeDocumentContent).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('编辑中')
    expect(wrapper.findAll('textarea')[0].element.value).toBe('已发布内容的新标题')

    vi.mocked(ElMessageBox.confirm).mockResolvedValueOnce('confirm' as never)
    vi.mocked(updateKnowledgeDocumentContent).mockResolvedValue({
      ...published,
      title: '已发布内容的新标题',
      currentRevisionNumber: 3,
      latestPublishedRevisionNumber: 3,
      publishedAt: '2026-08-23T02:00:00Z',
      concurrencyToken: 'published-next-token',
    })
    await button(wrapper, '保存')?.trigger('click')
    await flushPromises()
    expect(updateKnowledgeDocumentContent).toHaveBeenCalledTimes(1)
    expect(wrapper.text()).toContain('已保存。')
    expect(wrapper.text()).toContain('已发布内容的新标题')
  })

  it('routes Ctrl+S and Cmd+S through the same Published confirmation and ignores a clean editor', async () => {
    const published = {
      ...detail,
      lifecycleStatus: 'Published' as const,
      currentRevisionNumber: 2,
      latestPublishedRevisionNumber: 2,
      publishedAt: '2026-08-23T01:00:00Z',
      concurrencyToken: 'published-token',
    }

    for (const modifier of ['ctrl', 'meta'] as const) {
      vi.mocked(getKnowledgeDocument).mockResolvedValue(published)
      vi.mocked(ElMessageBox.confirm).mockReset()
      vi.mocked(ElMessageBox.confirm).mockResolvedValue('confirm' as never)
      vi.mocked(updateKnowledgeDocumentContent).mockReset()
      vi.mocked(updateKnowledgeDocumentContent).mockResolvedValue({
        ...published,
        title: `${modifier} saved`,
        currentRevisionNumber: 3,
        latestPublishedRevisionNumber: 3,
        concurrencyToken: `${modifier}-next-token`,
      })
      const wrapper = mountView()
      await flushPromises()
      await button(wrapper, '编辑')?.trigger('click')
      await wrapper.findAll('textarea')[0].setValue(`${modifier} saved`)
      window.dispatchEvent(new KeyboardEvent('keydown', {
        key: 's',
        ctrlKey: modifier === 'ctrl',
        metaKey: modifier === 'meta',
      }))
      await flushPromises()
      expect(ElMessageBox.confirm).toHaveBeenCalledTimes(1)
      expect(updateKnowledgeDocumentContent).toHaveBeenCalledTimes(1)
      wrapper.unmount()
    }

    vi.mocked(getKnowledgeDocument).mockResolvedValue(published)
    vi.mocked(ElMessageBox.confirm).mockReset()
    vi.mocked(updateKnowledgeDocumentContent).mockReset()
    const cleanWrapper = mountView()
    await flushPromises()
    await button(cleanWrapper, '编辑')?.trigger('click')
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 's', ctrlKey: true }))
    await flushPromises()
    expect(ElMessageBox.confirm).not.toHaveBeenCalled()
    expect(updateKnowledgeDocumentContent).not.toHaveBeenCalled()
    cleanWrapper.unmount()
  })

  it('renders all four server-projected confirmation coverage states without changing the status badge', async () => {
    const cases: ReadonlyArray<{
      state: KnowledgeDocumentDetail['confirmationCoverage']['state']
      revision: number | null
      text: string | null
    }> = [
      { state: 'NoConfirmation', revision: null, text: null },
      { state: 'LegacyConfirmationUnknown', revision: null, text: '迁移前人工确认无法确定覆盖的修订。' },
      { state: 'CurrentRevisionConfirmed', revision: 1, text: '人工确认覆盖当前修订 1' },
      { state: 'ChangedSinceConfirmation', revision: 1, text: '内容在最近一次确认后已修改' },
    ]

    for (const testCase of cases) {
      vi.mocked(getKnowledgeDocument).mockResolvedValue({
        ...detail,
        confirmationCoverage: {
          state: testCase.state,
          lastConfirmedRevisionNumber: testCase.revision,
        },
      })
      const wrapper = mountView()
      await flushPromises()
      if (testCase.text) expect(wrapper.text()).toContain(testCase.text)
      else expect(wrapper.find('.knowledge-document-confirmation-coverage').exists()).toBe(false)
      expect(wrapper.text()).toContain('知识状态')
      wrapper.unmount()
    }
  })

  it('adopts a successful Restore detail, exits History, and announces the new revision', async () => {
    const wrapper = mountView()
    await flushPromises()
    await button(wrapper, '修订历史（1）')?.trigger('click')
    await flushPromises()
    const restored = {
      ...detail,
      title: '恢复后的标题',
      currentRevisionNumber: 2,
      concurrencyToken: 'restored-token',
      confirmationCoverage: {
        state: 'ChangedSinceConfirmation' as const,
        lastConfirmedRevisionNumber: 1,
      },
    }
    window.dispatchEvent(new CustomEvent('knowledge-document:restored', {
      detail: { document: restored, sourceRevisionNumber: 1 },
    }))
    await flushPromises()

    expect(wrapper.text()).toContain('已从修订 1 恢复，并创建修订 2')
    expect(wrapper.text()).toContain('恢复后的标题')
    expect(wrapper.text()).toContain('内容在最近一次确认后已修改')
    expect(wrapper.text()).not.toContain('返回当前内容')
  })
})
