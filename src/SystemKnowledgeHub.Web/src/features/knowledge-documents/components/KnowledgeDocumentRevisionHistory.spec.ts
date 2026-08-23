import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  getKnowledgeDocumentRevision,
  listKnowledgeDocumentRevisions,
} from '../api/knowledgeDocumentsApi'
import type {
  KnowledgeDocumentRevisionDetail,
  KnowledgeDocumentRevisionListItem,
  KnowledgeDocumentRevisionListResponse,
} from '../api/knowledgeDocumentContracts'
import KnowledgeDocumentRevisionHistory from './KnowledgeDocumentRevisionHistory.vue'

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
  },
  2: {
    ...publishedRestore,
    knowledgeDocumentId: 7,
    title: '最近发布标题',
    summary: '发布摘要',
    bodyMarkdown: '## 已发布正文',
  },
  1: {
    ...baseline,
    knowledgeDocumentId: 7,
    title: '迁移标题',
    summary: null,
    bodyMarkdown: '## 迁移正文',
  },
}

const components = {
  ElButton: {
    emits: ['click'],
    template: '<button type="button" @click="$emit(\'click\')"><slot /></button>',
  },
  ElIcon: { template: '<span><slot /></span>' },
  ElPagination: {
    emits: ['current-change'],
    template: '<button type="button" @click="$emit(\'current-change\', 2)">下一页</button>',
  },
  ElSelect: {
    props: { modelValue: Number, id: String },
    emits: ['change'],
    template: '<select :id="id" :value="modelValue" @change="$emit(\'change\', +$event.target.value)"><slot /></select>',
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
    props: { documentId: 7, currentRevisionNumber: 3 },
    global: { components },
  })
}
function buttonByLabel(wrapper: ReturnType<typeof mountHistory>, label: string) {
  return wrapper.findAll('button').find((item) => item.attributes('aria-label') === label)
}

describe('KnowledgeDocumentRevisionHistory', () => {
  beforeEach(() => {
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
    expect(wrapper.text()).toContain('捕获于 2026-08-23 01:00')
    expect(wrapper.text()).toContain('迁移正文')
  })

  it('shows list loading and a defensive empty state without inventing a revision', async () => {
    let complete: ((value: KnowledgeDocumentRevisionListResponse) => void) | undefined
    vi.mocked(listKnowledgeDocumentRevisions).mockImplementation(
      () => new Promise((resolve) => { complete = resolve }),
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

    await wrapper.findAll('button').find((item) => item.text() === '下一页')?.trigger('click')
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

    await wrapper.findAll('button').find((item) => item.text() === '比较修订')?.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('比较修订')
    expect(wrapper.text()).toContain('从 修订 2 到 修订 3')

    await wrapper.findAll('button').find((item) => item.text() === '返回修订历史')?.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('修订历史（3）')
    expect(wrapper.text()).toContain('当前草稿标题')
  })
})
