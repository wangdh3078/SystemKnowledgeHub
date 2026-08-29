import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { KnowledgeDocumentRevisionDetail } from '../api/knowledgeDocumentContracts'
import { getKnowledgeDocumentRevision } from '../api/knowledgeDocumentsApi'
import RevisionCompareView from './RevisionCompareView.vue'

vi.mock('../api/knowledgeDocumentsApi', () => ({
  getKnowledgeDocumentRevision: vi.fn(),
}))

function revision(
  revisionNumber: number,
  overrides: Partial<KnowledgeDocumentRevisionDetail> = {},
): KnowledgeDocumentRevisionDetail {
  return {
    id: 100 + revisionNumber,
    knowledgeDocumentId: 7,
    revisionNumber,
    revisionOrigin: revisionNumber === 1 ? 'MigrationBaseline' : 'ContentSave',
    lifecycleContext: 'Draft',
    authorUserId: revisionNumber === 1 ? null : 9,
    authorDisplayName: revisionNumber === 1 ? null : 'Immutable Author Snapshot',
    createdAt: `2026-08-23T0${revisionNumber}:00:00Z`,
    changeSummary: revisionNumber === 1 ? null : `修订说明 ${revisionNumber}`,
    restoreReason: null,
    restoredFromRevisionNumber: null,
    isCurrent: revisionNumber === 5,
    isLatestPublished: revisionNumber === 4,
    title: revisionNumber < 5 ? 'Oracle Listener' : 'Oracle Listener Runbook',
    summary: revisionNumber < 3 ? null : `摘要 ${revisionNumber}`,
    bodyMarkdown: `# Oracle Listener\n\n检查监听服务。\n\n修订 ${revisionNumber}`,
    attachmentReferences: [],
    ...overrides,
  }
}

const details: Readonly<Record<number, KnowledgeDocumentRevisionDetail>> = {
  1: revision(1, {
    bodyMarkdown: '# Oracle Listener\n\n检查监听服务。',
  }),
  2: revision(2, {
    bodyMarkdown: '# Oracle Listener\n\n检查监听服务。\n\n- 检查端口\n- 检查进程',
  }),
  3: revision(3, {
    summary: '监听故障排查摘要',
    bodyMarkdown: '# Oracle Listener\n\n检查监听服务。\n\n- 检查端口\n- 检查进程',
  }),
  4: revision(4, {
    revisionOrigin: 'Restore',
    restoredFromRevisionNumber: 2,
    restoreReason: '恢复已验证的监听步骤',
    summary: '监听故障排查摘要',
    bodyMarkdown:
      '# Oracle Listener\n\n检查监听服务。\n\n```sql\nSELECT status FROM v$instance;\n```\n\n<img src=x onerror=alert(1)>',
  }),
  5: revision(5, {
    summary: '监听与数据库状态排查',
    bodyMarkdown:
      '# Oracle Listener\n\n检查监听与数据库服务。\n\n```sql\nSELECT status FROM v$instance;\n```\n\n| 项目 | 状态 |\n| --- | --- |\n| Listener | OK |\n\n<script>alert(1)</script>\n[j](javascript:alert(1))',
  }),
}

const components = {
  ElButton: {
    props: { disabled: Boolean },
    emits: ['click'],
    template:
      '<button type="button" :disabled="disabled" @click="$emit(\'click\')"><slot /></button>',
  },
  ElIcon: { template: '<span><slot /></span>' },
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

function mountCompare(
  initialToRevisionNumber = 5,
  initialSnapshot: KnowledgeDocumentRevisionDetail | null = details[initialToRevisionNumber],
) {
  return mount(RevisionCompareView, {
    props: {
      documentId: 7,
      revisionCount: 5,
      initialToRevisionNumber,
      initialSnapshot,
    },
    global: { components },
  })
}

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason?: unknown) => void
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise
    reject = rejectPromise
  })
  return { promise, resolve, reject }
}

describe('RevisionCompareView', () => {
  beforeEach(() => {
    vi.mocked(getKnowledgeDocumentRevision).mockReset()
    vi.mocked(getKnowledgeDocumentRevision).mockImplementation((_documentId, revisionNumber) =>
      Promise.resolve(details[revisionNumber]),
    )
  })

  it('defaults to previous→selected and renders metadata, field/body diff, markers and XSS as text', async () => {
    const wrapper = mountCompare()
    await flushPromises()

    expect(getKnowledgeDocumentRevision).toHaveBeenCalledTimes(1)
    expect(getKnowledgeDocumentRevision).toHaveBeenCalledWith(7, 4, expect.any(AbortSignal))
    expect(wrapper.text()).toContain('从 修订 4 到 修订 5')
    expect(wrapper.text()).toContain('标题变化')
    expect(wrapper.text()).toContain('摘要变化')
    expect(wrapper.text()).toContain('正文变化')
    expect(wrapper.text()).toContain('+ 新增')
    expect(wrapper.text()).toContain('- 删除')
    expect(wrapper.text()).toContain('未变化')
    expect(wrapper.text()).toContain('当前版本')
    expect(wrapper.text()).toContain('最近发布')
    expect(wrapper.text()).toContain('历史恢复')
    expect(wrapper.text()).toContain('从修订 2 恢复')
    expect(wrapper.text()).toContain('恢复已验证的监听步骤')
    expect(wrapper.text()).toContain('<script>alert(1)</script>')
    expect(wrapper.text()).toContain('<img src=x onerror=alert(1)>')
    expect(wrapper.text()).toContain('[j](javascript:alert(1))')
    expect(wrapper.find('script').exists()).toBe(false)
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.find('a[href^="javascript:"]').exists()).toBe(false)
    expect(wrapper.findAll('button').some((button) => button.text().includes('恢复'))).toBe(false)
    expect(wrapper.findAll('button').some((button) => button.text() === '编辑')).toBe(false)
  })

  it('compares Markdown extensions as immutable raw source without rendering or canonicalizing them', async () => {
    const rawFrom = [
      '```mermaid',
      'flowchart LR',
      '  A[开始] --> B[结束]',
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
    const rawTo = rawFrom.replace('#e53935', '#E53935')
    const fromSnapshot = revision(1, { bodyMarkdown: rawFrom })
    const toSnapshot = revision(2, { bodyMarkdown: rawTo })
    vi.mocked(getKnowledgeDocumentRevision).mockResolvedValue(fromSnapshot)

    const wrapper = mountCompare(2, toSnapshot)
    await flushPromises()

    const diffLines = wrapper
      .findAll('.knowledge-document-compare__line code')
      .map((line) => line.element.textContent ?? '')
    expect(diffLines).toContain('```mermaid')
    expect(diffLines).toContain('  A[开始] --> B[结束]')
    expect(diffLines).toContain('{color:#e53935|严重告警}')
    expect(diffLines).toContain('{color:#E53935|严重告警}')
    expect(diffLines).toContain('{bg:#fff3b0|请人工确认}')
    expect(diffLines).toContain('- [ ] 未完成')
    expect(diffLines).toContain('| 字段 | 说明 |')
    expect(wrapper.find('.knowledge-document-mermaid').exists()).toBe(false)
    expect(wrapper.find('.knowledge-document-text-color').exists()).toBe(false)
    expect(wrapper.find('.knowledge-document-background-color').exists()).toBe(false)
    expect(wrapper.find('input[type="checkbox"]').exists()).toBe(false)
    expect(wrapper.find('table').exists()).toBe(false)
    expect(fromSnapshot.bodyMarkdown).toBe(rawFrom)
    expect(toSnapshot.bodyMarkdown).toBe(rawTo)
  })

  it('renders deterministic attachment set changes beside the raw Markdown diff', async () => {
    const file = (attachmentId: number, originalFileName: string) => ({
      attachmentId,
      kind: 'File' as const,
      originalFileName,
      extension: '.pdf',
      contentType: 'application/pdf',
      sizeBytes: 1024,
      sha256: 'e'.repeat(64),
      previewMode: 'Pdf' as const,
      canPreview: true,
      canDownload: true,
    })
    const from = revision(4, {
      attachmentReferences: [file(20, '旧规范.pdf'), file(9, '保留.pdf')],
    })
    const to = revision(5, { attachmentReferences: [file(11, '新规范.pdf'), file(9, '保留.pdf')] })
    vi.mocked(getKnowledgeDocumentRevision).mockResolvedValue(from)

    const wrapper = mountCompare(5, to)
    await flushPromises()

    expect(wrapper.text()).toContain('附件集合变化')
    expect(wrapper.text()).toContain('按 Attachment ID + Kind 比较，不比较二进制内容')
    expect(wrapper.text()).toContain('新增（1）')
    expect(wrapper.text()).toContain('普通附件 #11 · 新规范.pdf')
    expect(wrapper.text()).toContain('移除（1）')
    expect(wrapper.text()).toContain('普通附件 #20 · 旧规范.pdf')
    expect(wrapper.text()).toContain('未变化（1）')
    expect(wrapper.text()).toContain('普通附件 #9 · 保留.pdf')
  })

  it('handles Revision 1 without fetching or comparing itself', async () => {
    const wrapper = mountCompare(1, details[1])
    await flushPromises()

    expect(wrapper.text()).toContain('这是最早的修订，没有更早版本可比较')
    expect(getKnowledgeDocumentRevision).not.toHaveBeenCalled()
  })

  it('supports manual pairs, baseline metadata, reverse normalization and same-pair handling', async () => {
    const wrapper = mountCompare()
    await flushPromises()

    await wrapper.get('#compare-from-revision').setValue('1')
    await flushPromises()
    expect(wrapper.text()).toContain('从 修订 1 到 修订 5')
    expect(wrapper.text()).toContain('迁移基线')
    expect(wrapper.text()).toContain('历史作者未知')
    expect(wrapper.text()).toContain('捕获于')

    await wrapper.get('#compare-to-revision').setValue('2')
    await flushPromises()
    expect(wrapper.text()).toContain('从 修订 1 到 修订 2')

    await wrapper.get('#compare-from-revision').setValue('4')
    await flushPromises()
    expect(wrapper.text()).toContain('从 修订 2 到 修订 4')
    expect(wrapper.text()).toContain('已按较早到较新修订调整比较方向')

    const callsBeforeSamePair = vi.mocked(getKnowledgeDocumentRevision).mock.calls.length
    await wrapper.get('#compare-to-revision').setValue('2')
    await flushPromises()
    expect(wrapper.text()).toContain('两个修订相同，没有可比较的变化')
    expect(getKnowledgeDocumentRevision).toHaveBeenCalledTimes(callsBeforeSamePair)
  })

  it('shows a complete loading state and existing error UX', async () => {
    const pending = deferred<KnowledgeDocumentRevisionDetail>()
    vi.mocked(getKnowledgeDocumentRevision).mockReturnValue(pending.promise)
    const wrapper = mountCompare()
    await flushPromises()
    expect(wrapper.text()).toContain('正在加载两个修订快照…')
    expect(wrapper.text()).not.toContain('标题变化')

    pending.reject(new Error('snapshot 502'))
    await flushPromises()
    expect(wrapper.text()).toContain('修订比较加载失败')
    expect(wrapper.text()).toContain('snapshot 502')
  })

  it('ignores a stale pair response after rapid selector switching', async () => {
    const stale = deferred<KnowledgeDocumentRevisionDetail>()
    let revisionFourCalls = 0
    vi.mocked(getKnowledgeDocumentRevision).mockImplementation((_documentId, revisionNumber) => {
      if (revisionNumber === 4 && revisionFourCalls++ === 0) return stale.promise
      return Promise.resolve(details[revisionNumber])
    })
    const wrapper = mountCompare()
    await flushPromises()

    await wrapper.get('#compare-to-revision').setValue('2')
    await flushPromises()
    expect(wrapper.text()).toContain('从 修订 2 到 修订 4')
    expect(wrapper.text()).toContain('恢复已验证的监听步骤')

    stale.resolve(revision(4, { title: 'STALE RESPONSE MUST NOT WIN' }))
    await flushPromises()
    expect(wrapper.text()).not.toContain('STALE RESPONSE MUST NOT WIN')
    expect(wrapper.text()).toContain('从 修订 2 到 修订 4')
  })

  it('shows the oversized guard without a partial result', async () => {
    const oversizedFrom = revision(1, { title: 'a'.repeat(1_002_500) })
    const oversizedTo = revision(2, { title: 'b'.repeat(1_002_501) })
    vi.mocked(getKnowledgeDocumentRevision).mockResolvedValue(oversizedFrom)
    const wrapper = mountCompare(2, oversizedTo)
    await flushPromises()

    expect(wrapper.text()).toContain('该版本组合超出比较限制，未生成差异结果')
    expect(wrapper.text()).toContain('请返回修订历史分别查看修订内容')
    expect(wrapper.find('.knowledge-document-compare__result').exists()).toBe(false)
  })
})
