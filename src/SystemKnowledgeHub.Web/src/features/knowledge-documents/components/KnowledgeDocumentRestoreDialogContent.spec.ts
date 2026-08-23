import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../api/errors/ApiError'
import { useOverlayStore } from '../../../app/stores/overlays'
import {
  getKnowledgeDocument,
  restoreKnowledgeDocumentRevision,
} from '../api/knowledgeDocumentsApi'
import type {
  KnowledgeDocumentDetail,
  KnowledgeDocumentRevisionDetail,
} from '../api/knowledgeDocumentContracts'
import KnowledgeDocumentRestoreDialogContent from './KnowledgeDocumentRestoreDialogContent.vue'

vi.mock('../api/knowledgeDocumentsApi', () => ({
  getKnowledgeDocument: vi.fn(),
  restoreKnowledgeDocumentRevision: vi.fn(),
}))

const document: KnowledgeDocumentDetail = {
  id: 7,
  documentType: 'KnowledgeArticle',
  title: '当前标题',
  summary: '当前摘要',
  bodyMarkdown: '当前正文',
  lifecycleStatus: 'Draft',
  knowledgeStatus: 'Confirmed',
  createdByUserId: 9,
  createdByDisplayName: '创建者',
  updatedByUserId: 9,
  updatedByDisplayName: '当前作者',
  createdAt: '2026-08-23T01:00:00Z',
  updatedAt: '2026-08-23T02:00:00Z',
  publishedAt: '2026-08-23T01:30:00Z',
  archivedAt: null,
  currentRevisionNumber: 2,
  latestPublishedRevisionNumber: 2,
  confirmationCoverage: { state: 'ChangedSinceConfirmation', lastConfirmedRevisionNumber: 1 },
  concurrencyToken: 'current-token',
}
const revision: KnowledgeDocumentRevisionDetail = {
  id: 101,
  knowledgeDocumentId: 7,
  revisionNumber: 1,
  revisionOrigin: 'MigrationBaseline',
  lifecycleContext: 'Draft',
  authorUserId: null,
  authorDisplayName: null,
  createdAt: '2026-08-23T01:00:00Z',
  changeSummary: null,
  restoreReason: null,
  restoredFromRevisionNumber: null,
  isCurrent: false,
  isLatestPublished: false,
  title: '历史标题',
  summary: '历史摘要',
  bodyMarkdown: '历史正文',
}

const components = {
  ElButton: {
    props: { disabled: Boolean, loading: Boolean },
    emits: ['click'],
    template: '<button type="button" :disabled="disabled" @click="$emit(\'click\')"><slot /></button>',
  },
  ElInput: {
    props: { modelValue: String },
    emits: ['update:modelValue', 'input'],
    template: '<textarea :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value); $emit(\'input\', $event.target.value)" />',
  },
}

function openDialog(currentDocument: KnowledgeDocumentDetail = document): void {
  useOverlayStore().openDialog({
    kind: 'restore-knowledge-document-revision',
    id: currentDocument.id,
    mode: 'edit',
    payload: { document: currentDocument, revision },
  })
}

function mountDialog() {
  return mount(KnowledgeDocumentRestoreDialogContent, { global: { components } })
}

function button(wrapper: ReturnType<typeof mountDialog>, label: string) {
  return wrapper.findAll('button').find((item) => item.text() === label)
}

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((resolvePromise) => { resolve = resolvePromise })
  return { promise, resolve }
}

describe('KnowledgeDocumentRestoreDialogContent', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(getKnowledgeDocument).mockReset()
    vi.mocked(restoreKnowledgeDocumentRevision).mockReset()
    openDialog()
  })

  it('shows the immutable preview and submits one exact trimmed request before announcing success', async () => {
    const pending = deferred<KnowledgeDocumentDetail>()
    vi.mocked(restoreKnowledgeDocumentRevision).mockReturnValue(pending.promise)
    const restored = { ...document, currentRevisionNumber: 3, concurrencyToken: 'next-token' }
    const restoredListener = vi.fn()
    window.addEventListener('knowledge-document:restored', restoredListener)
    const wrapper = mountDialog()

    expect(wrapper.text()).toContain('恢复修订 1')
    expect(wrapper.text()).toContain('迁移基线')
    expect(wrapper.text()).toContain('历史作者未知')
    expect(wrapper.text()).toContain('历史标题')
    expect(wrapper.text()).toContain('恢复不会删除后续修订')
    expect(wrapper.text()).toContain('系统会把该历史内容复制为新的当前版本，并创建新的修订')

    await wrapper.get('textarea').setValue('  恢复被误删的处理步骤  ')
    await button(wrapper, '恢复并创建新修订')?.trigger('click')
    await button(wrapper, '恢复并创建新修订')?.trigger('click')
    expect(restoreKnowledgeDocumentRevision).toHaveBeenCalledTimes(1)
    expect(restoreKnowledgeDocumentRevision).toHaveBeenCalledWith(7, 1, {
      concurrencyToken: 'current-token',
      reason: '恢复被误删的处理步骤',
    })

    pending.resolve(restored)
    await flushPromises()
    expect(useOverlayStore().currentDialog).toBeNull()
    expect(restoredListener).toHaveBeenCalledTimes(1)
    expect((restoredListener.mock.calls[0]?.[0] as CustomEvent).detail).toEqual({
      document: restored,
      sourceRevisionNumber: 1,
    })
    window.removeEventListener('knowledge-document:restored', restoredListener)
  })

  it('validates 5–500 trimmed characters and Cancel performs no request', async () => {
    const wrapper = mountDialog()
    expect(button(wrapper, '恢复并创建新修订')?.attributes('disabled')).toBeDefined()
    await wrapper.get('textarea').setValue('abcd')
    expect(wrapper.text()).toContain('恢复原因至少需要 5 个字符')
    await wrapper.get('textarea').setValue('有效原因五')
    expect(button(wrapper, '恢复并创建新修订')?.attributes('disabled')).toBeUndefined()
    await button(wrapper, '取消')?.trigger('click')
    expect(restoreKnowledgeDocumentRevision).not.toHaveBeenCalled()
    expect(useOverlayStore().currentDialog).toBeNull()
  })

  it('preserves the reason on conflict, reloads the current token, and requires a second confirmation', async () => {
    vi.mocked(restoreKnowledgeDocumentRevision)
      .mockRejectedValueOnce(new ApiError(409, {
        code: 'conflict',
        message: 'stale',
        fieldErrors: null,
        details: null,
      }))
      .mockResolvedValueOnce({ ...document, currentRevisionNumber: 5, concurrencyToken: 'restored-token' })
    const latest = {
      ...document,
      title: '并发后的当前标题',
      currentRevisionNumber: 4,
      concurrencyToken: 'latest-token',
    }
    vi.mocked(getKnowledgeDocument).mockResolvedValue(latest)
    const wrapper = mountDialog()
    await wrapper.get('textarea').setValue('保留用户填写的恢复原因')
    await button(wrapper, '恢复并创建新修订')?.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('当前文档已被其他操作修改，请重新加载最新内容后再重试恢复')
    expect(wrapper.get('textarea').element.value).toBe('保留用户填写的恢复原因')
    await button(wrapper, '重新加载最新内容')?.trigger('click')
    await flushPromises()
    expect(getKnowledgeDocument).toHaveBeenCalledWith(7)
    expect(wrapper.text()).toContain('已重新加载当前修订 4，请再次明确确认恢复')
    expect(wrapper.get('textarea').element.value).toBe('保留用户填写的恢复原因')

    await button(wrapper, '恢复并创建新修订')?.trigger('click')
    await flushPromises()
    expect(restoreKnowledgeDocumentRevision).toHaveBeenLastCalledWith(7, 1, {
      concurrencyToken: 'latest-token',
      reason: '保留用户填写的恢复原因',
    })
  })

  it('refreshes stale state for invalid-state and business-rule errors while retaining user context', async () => {
    const cases = [
      {
        status: 409,
        code: 'invalid_state' as const,
        latest: { ...document, lifecycleStatus: 'Published' as const, concurrencyToken: 'published-token' },
        expected: '当前文档已不处于草稿状态，无法恢复',
      },
      {
        status: 422,
        code: 'business_rule_violation' as const,
        latest: {
          ...document,
          title: revision.title,
          summary: revision.summary,
          bodyMarkdown: revision.bodyMarkdown,
          currentRevisionNumber: 3,
          concurrencyToken: 'identical-token',
        },
        expected: '所选历史修订内容与当前版本相同',
      },
    ]

    for (const testCase of cases) {
      setActivePinia(createPinia())
      openDialog()
      vi.mocked(getKnowledgeDocument).mockResolvedValueOnce(testCase.latest)
      vi.mocked(restoreKnowledgeDocumentRevision).mockRejectedValueOnce(new ApiError(
        testCase.status,
        {
          code: testCase.code,
          message: testCase.expected,
          fieldErrors: null,
          details: null,
        },
      ))
      const wrapper = mountDialog()
      await wrapper.get('textarea').setValue('发生错误也要保留原因')
      await button(wrapper, '恢复并创建新修订')?.trigger('click')
      await flushPromises()
      expect(wrapper.text()).toContain(testCase.expected)
      expect(wrapper.get('textarea').element.value).toBe('发生错误也要保留原因')
      expect(getKnowledgeDocument).toHaveBeenCalledWith(7)
      wrapper.unmount()
    }

    setActivePinia(createPinia())
    openDialog()
    vi.mocked(restoreKnowledgeDocumentRevision).mockRejectedValueOnce(new Error('network unavailable'))
    const wrapper = mountDialog()
    await wrapper.get('textarea').setValue('网络失败保留恢复原因')
    await button(wrapper, '恢复并创建新修订')?.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('network unavailable')
    expect(wrapper.get('textarea').element.value).toBe('网络失败保留恢复原因')
  })
})
