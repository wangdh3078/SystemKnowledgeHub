import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useOverlayStore } from '../../../app/stores/overlays'
import {
  getKnowledgeDocumentRevision,
  listKnowledgeDocumentRevisions,
} from '../api/knowledgeDocumentsApi'
import type {
  KnowledgeDocumentDetail,
  KnowledgeDocumentRevisionDetail,
  KnowledgeDocumentRevisionListItem,
  KnowledgeDocumentRevisionListResponse,
} from '../api/knowledgeDocumentContracts'
import KnowledgeDocumentRevisionHistory from './KnowledgeDocumentRevisionHistory.vue'
import { formatDateTime } from '../../../app/formatters/dateTime'

vi.mock('../api/knowledgeDocumentsApi', () => ({
  getKnowledgeDocumentRevision: vi.fn(),
  listKnowledgeDocumentRevisions: vi.fn(),
}))

const currentRevision: KnowledgeDocumentRevisionListItem = {
  id: 103,
  revisionNumber: 3,
  revisionOrigin: 'ContentSave',
  lifecycleContext: 'Draft',
  authorUserId: 9,
  authorDisplayName: 'Immutable Author Snapshot',
  createdAt: '2026-08-23T03:00:00Z',
  changeSummary: '补充草稿步骤',
  restoreReason: null,
  restoredFromRevisionNumber: null,
  isCurrent: true,
  isLatestPublished: false,
}
const publishedRestore: KnowledgeDocumentRevisionListItem = {
  id: 102,
  revisionNumber: 2,
  revisionOrigin: 'Restore',
  lifecycleContext: 'Published',
  authorUserId: 9,
  authorDisplayName: 'Immutable Author Snapshot',
  createdAt: '2026-08-23T02:00:00Z',
  changeSummary: null,
  restoreReason: '恢复已验证的初始内容',
  restoredFromRevisionNumber: 1,
  isCurrent: false,
  isLatestPublished: true,
}
const baseline: KnowledgeDocumentRevisionListItem = {
  id: 101,
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
}

const details: Readonly<Record<number, KnowledgeDocumentRevisionDetail>> = {
  3: {
    ...currentRevision,
    knowledgeDocumentId: 7,
    title: '当前草稿标题',
    summary: '当前草稿摘要',
    bodyMarkdown: '# 当前草稿\n\n<script>alert(1)</script>\n\n[危险](javascript:alert(1))',
    attachmentReferences: [],
  },
  2: {
    ...publishedRestore,
    knowledgeDocumentId: 7,
    title: '最近发布标题',
    summary: '发布摘要',
    bodyMarkdown: '## 已发布正文',
    attachmentReferences: [],
  },
  1: {
    ...baseline,
    knowledgeDocumentId: 7,
    title: '迁移标题',
    summary: null,
    bodyMarkdown: '## 迁移正文',
    attachmentReferences: [],
  },
}

const currentDocument: KnowledgeDocumentDetail = {
  id: 7,
  documentType: 'KnowledgeArticle',
  title: '当前草稿标题',
  summary: '当前草稿摘要',
  bodyMarkdown: details[3].bodyMarkdown,
  lifecycleStatus: 'Draft',
  knowledgeStatus: 'Unknown',
  createdByUserId: 9,
  createdByDisplayName: 'Immutable Author Snapshot',
  updatedByUserId: 9,
  updatedByDisplayName: 'Immutable Author Snapshot',
  createdAt: '2026-08-23T01:00:00Z',
  updatedAt: '2026-08-23T03:00:00Z',
  publishedAt: '2026-08-23T02:00:00Z',
  archivedAt: null,
  currentRevisionNumber: 3,
  latestPublishedRevisionNumber: 2,
  confirmationCoverage: { state: 'NoConfirmation', lastConfirmedRevisionNumber: null },
  concurrencyToken: 'opaque-current-token',
  canDelete: true,
  attachmentReferences: [],
}

const components = {
  ElButton: {
    emits: ['click'],
    template: '<button type="button" @click="$emit(\'click\')"><slot /></button>',
  },
  ElIcon: { template: '<span><slot /></span>' },
  ElTag: { template: '<span><slot /></span>' },
  ElPagination: {
    emits: ['current-change'],
    template: '<button type="button" @click="$emit(\'current-change\', 2)">下一页</button>',
  },
  ElSelect: {
    props: { modelValue: Number, id: String },
    emits: ['change'],
    template:
      '<select :id="id" :value="modelValue" @change="$emit(\'change\', +$event.target.value)"><slot /></select>',
  },
  ElOption: {
    props: { label: String, value: Number },
    template: '<option :value="value">{{ label }}</option>',
  },
}

function response(
  items: readonly KnowledgeDocumentRevisionListItem[] = [
    currentRevision,
    publishedRestore,
    baseline,
  ],
  total = items.length,
  page = 1,
): KnowledgeDocumentRevisionListResponse {
  return { items, total, page, pageSize: 20 }
}
function mountHistory() {
  return mount(KnowledgeDocumentRevisionHistory, {
    props: { documentId: currentDocument.id, document: currentDocument, canRestore: true },
    global: { components },
  })
}
function buttonByLabel(wrapper: ReturnType<typeof mountHistory>, label: string) {
  return wrapper.findAll('button').find((item) => item.attributes('aria-label') === label)
}

describe('KnowledgeDocumentRevisionHistory', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(listKnowledgeDocumentRevisions).mockReset()
    vi.mocked(getKnowledgeDocumentRevision).mockReset()
    vi.mocked(listKnowledgeDocumentRevisions).mockResolvedValue(response())
    vi.mocked(getKnowledgeDocumentRevision).mockImplementation((_id, revisionNumber) =>
      Promise.resolve(details[revisionNumber]),
    )
  })

  it('renders newest-first markers, snapshot metadata, safe Markdown and read-only restore compatibility', async () => {
    const wrapper = mountHistory()
    await flushPromises()

    expect(listKnowledgeDocumentRevisions).toHaveBeenCalledWith(7, 1, 20, expect.any(AbortSignal))
    expect(wrapper.text()).toContain('修订历史（3）')
    expect(wrapper.text().indexOf('修订 3')).toBeLessThan(wrapper.text().indexOf('修订 2'))
    expect(wrapper.text()).toContain('当前版本')
    expect(wrapper.text()).toContain('最近发布')
    expect(wrapper.text()).toContain('Immutable Author Snapshot')
    expect(wrapper.text()).toContain('补充草稿步骤')
    expect(wrapper.html()).toContain('&lt;script&gt;alert(1)&lt;/script&gt;')
    expect(wrapper.html()).not.toContain('<script>')
    expect(wrapper.html()).not.toContain('href="javascript:')

    await buttonByLabel(wrapper, '查看修订 2')?.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('历史恢复')
    expect(wrapper.text()).toContain('从修订 1 恢复')
    expect(wrapper.text()).toContain('恢复已验证的初始内容')
    expect(wrapper.text()).toContain('最近发布标题')
    expect(wrapper.findAll('button').some((item) => item.text() === '恢复')).toBe(false)
    expect(wrapper.findAll('button').some((item) => item.text() === '编辑')).toBe(false)

    await buttonByLabel(wrapper, '查看修订 1')?.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('迁移基线')
    expect(wrapper.text()).toContain('历史作者未知')
    expect(wrapper.text()).toContain(`捕获于 ${formatDateTime('2026-08-23T01:00:00Z')}`)
    expect(wrapper.text()).toContain('迁移正文')
  })

  it('keeps deleted-owner revisions readable while showing a tombstone and no restore action', async () => {
    const owner = {
      id: 7,
      targetType: 'KnowledgeDocument',
      displayName: '已删除知识内容',
      isDeleted: true,
      isNavigable: false,
    }
    vi.mocked(listKnowledgeDocumentRevisions).mockResolvedValue({ ...response(), owner })
    const historicalImage = {
      attachmentId: 123,
      kind: 'Image' as const,
      originalFileName: '历史图.png',
      extension: '.png',
      contentType: 'image/png',
      sizeBytes: 24,
      sha256: 'a'.repeat(64),
      previewMode: 'Image' as const,
      canPreview: true,
      canDownload: true,
    }
    vi.mocked(getKnowledgeDocumentRevision).mockImplementation((_id, revisionNumber) =>
      Promise.resolve(
        revisionNumber === 2
          ? {
              ...details[revisionNumber],
              owner,
              bodyMarkdown: '![历史图](attachment:123)',
              attachmentReferences: [historicalImage],
            }
          : { ...details[revisionNumber], owner },
      ),
    )
    const wrapper = mount(KnowledgeDocumentRevisionHistory, {
      props: { documentId: 7, document: null, canRestore: true },
      global: { components },
    })
    await flushPromises()
    await buttonByLabel(wrapper as ReturnType<typeof mountHistory>, '查看修订 2')?.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('已删除知识内容')
    expect(wrapper.text()).toContain('已删除')
    expect(wrapper.text()).toContain('最近发布标题')
    expect(wrapper.get('[data-knowledge-document-attachment-image]').attributes('src')).toBe(
      '/api/knowledge-documents/7/revisions/2/attachments/123/content',
    )
    expect(wrapper.find('a').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('恢复此修订')
  })

  it('renders a legacy BR snapshot safely without mutating its immutable raw body', async () => {
    const rawBody = [
      'A',
      '',
      '<br />',
      '',
      'B',
      '',
      '<script>alert(1)</script>',
      '<img src=x onerror=alert(1)>',
    ].join('\n')
    const legacyRevision = { ...details[3], bodyMarkdown: rawBody }
    vi.mocked(getKnowledgeDocumentRevision).mockResolvedValue(legacyRevision)

    const wrapper = mountHistory()
    await flushPromises()

    expect(wrapper.html()).toContain('<p><br>')
    expect(wrapper.html()).not.toContain('&lt;br /&gt;')
    expect(wrapper.html()).toContain('&lt;script&gt;alert(1)&lt;/script&gt;')
    expect(wrapper.html()).toContain('&lt;img src=x onerror=alert(1)&gt;')
    expect(wrapper.html()).not.toContain('<script>')
    expect(wrapper.html()).not.toContain('<img src=')
    expect(legacyRevision.bodyMarkdown).toBe(rawBody)
  })

  it('passes the exact immutable extension source to the shared historical Markdown view', async () => {
    const extensionSource = [
      '```mermaid',
      'flowchart TD',
      '  A --> B',
      '```',
      '',
      '{color:#e53935|严重告警}',
      '{bg:#fff3b0|请人工确认}',
      '',
      '- [ ] 未完成',
      '- [x] 已完成',
      '',
      '| 字段 | 说明 |',
      '| --- | --- |',
      '| ID | 主键 |',
    ].join('\n')
    const extensionRevision = { ...details[3], bodyMarkdown: extensionSource }
    vi.mocked(getKnowledgeDocumentRevision).mockResolvedValue(extensionRevision)
    const wrapper = mount(KnowledgeDocumentRevisionHistory, {
      props: { documentId: currentDocument.id, document: currentDocument, canRestore: true },
      global: {
        components,
        stubs: {
          KnowledgeDocumentMarkdown: {
            name: 'KnowledgeDocumentMarkdown',
            props: { markdown: { type: String, required: true } },
            template: '<pre data-testid="historical-markdown-source">{{ markdown }}</pre>',
          },
        },
      },
    })

    await flushPromises()

    const sharedMarkdown = wrapper.getComponent({ name: 'KnowledgeDocumentMarkdown' })
    expect(sharedMarkdown.props('markdown')).toBe(extensionSource)
    expect(wrapper.get('[data-testid="historical-markdown-source"]').element.textContent).toBe(
      extensionSource,
    )
    expect(extensionRevision.bodyMarkdown).toBe(extensionSource)
  })

  it('shows list loading and a defensive empty state without inventing a revision', async () => {
    let complete: ((value: KnowledgeDocumentRevisionListResponse) => void) | undefined
    vi.mocked(listKnowledgeDocumentRevisions).mockImplementation(
      () =>
        new Promise((resolve) => {
          complete = resolve
        }),
    )
    const wrapper = mountHistory()
    await flushPromises()
    expect(wrapper.text()).toContain('正在加载修订历史…')

    complete?.(response([], 0))
    await flushPromises()
    expect(wrapper.text()).toContain('无法加载修订历史')
    expect(wrapper.text()).toContain('当前内容不会被伪造为历史快照')
    expect(getKnowledgeDocumentRevision).not.toHaveBeenCalled()
  })

  it('shows a list error using the existing retry UX', async () => {
    vi.mocked(listKnowledgeDocumentRevisions).mockRejectedValue(new Error('network unavailable'))
    const wrapper = mountHistory()
    await flushPromises()

    expect(wrapper.text()).toContain('修订历史加载失败')
    expect(wrapper.text()).toContain('network unavailable')
  })

  it('keeps the loaded list visible when a selected detail fails', async () => {
    vi.mocked(getKnowledgeDocumentRevision).mockRejectedValue(new Error('snapshot 502'))
    const wrapper = mountHistory()
    await flushPromises()

    expect(wrapper.text()).toContain('修订 3')
    expect(wrapper.text()).toContain('修订 2')
    expect(wrapper.text()).toContain('历史快照加载失败')
    expect(wrapper.text()).toContain('snapshot 502')
  })

  it('changes page once, stays in history mode and selects that page first item', async () => {
    vi.mocked(listKnowledgeDocumentRevisions)
      .mockResolvedValueOnce(response([currentRevision], 21, 1))
      .mockResolvedValueOnce(response([baseline], 21, 2))
    const wrapper = mountHistory()
    await flushPromises()

    await wrapper
      .findAll('button')
      .find((item) => item.text() === '下一页')
      ?.trigger('click')
    await flushPromises()

    expect(listKnowledgeDocumentRevisions).toHaveBeenCalledTimes(2)
    expect(listKnowledgeDocumentRevisions).toHaveBeenLastCalledWith(
      7,
      2,
      20,
      expect.any(AbortSignal),
    )
    expect(getKnowledgeDocumentRevision).toHaveBeenLastCalledWith(7, 1, expect.any(AbortSignal))
    expect(wrapper.text()).toContain('修订历史（3）')
    expect(wrapper.text()).toContain('迁移标题')
  })

  it('enters compare for the selected revision and returns to the preserved history state', async () => {
    const wrapper = mountHistory()
    await flushPromises()

    await wrapper
      .findAll('button')
      .find((item) => item.text() === '比较修订')
      ?.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('比较修订')
    expect(wrapper.text()).toContain('从 修订 2 到 修订 3')

    await wrapper
      .findAll('button')
      .find((item) => item.text() === '返回修订历史')
      ?.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('修订历史（3）')
    expect(wrapper.text()).toContain('当前草稿标题')
  })

  it('offers Restore only for an editable Draft historical preview and opens the single dialog host', async () => {
    const wrapper = mountHistory()
    await flushPromises()
    expect(wrapper.findAll('button').some((item) => item.text() === '恢复此修订')).toBe(false)

    await buttonByLabel(wrapper, '查看修订 2')?.trigger('click')
    await flushPromises()
    const restoreButton = wrapper.findAll('button').find((item) => item.text() === '恢复此修订')
    expect(restoreButton).toBeDefined()
    await restoreButton?.trigger('click')
    expect(useOverlayStore().currentDialog).toMatchObject({
      kind: 'restore-knowledge-document-revision',
      id: 7,
      mode: 'edit',
    })

    await wrapper.setProps({ canRestore: false })
    expect(wrapper.findAll('button').some((item) => item.text() === '恢复此修订')).toBe(false)

    await wrapper.setProps({
      canRestore: true,
      document: { ...currentDocument, lifecycleStatus: 'Published' },
    })
    expect(wrapper.findAll('button').some((item) => item.text() === '恢复此修订')).toBe(false)
    expect(wrapper.text()).toContain('请先将文档返回草稿后再恢复历史内容')
  })
})
