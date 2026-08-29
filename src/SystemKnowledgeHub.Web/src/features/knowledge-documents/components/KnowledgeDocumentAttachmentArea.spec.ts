import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ElMessageBox } from 'element-plus'
import { ApiError } from '../../../api/errors/ApiError'
import type { AttachmentMetadata } from '../api/attachmentContracts'
import { uploadKnowledgeDocumentAttachment } from '../api/knowledgeDocumentAttachmentsApi'
import KnowledgeDocumentAttachmentArea from './KnowledgeDocumentAttachmentArea.vue'

vi.mock('element-plus', () => ({ ElMessageBox: { confirm: vi.fn() } }))
vi.mock('../api/knowledgeDocumentAttachmentsApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/knowledgeDocumentAttachmentsApi')>()
  return { ...actual, uploadKnowledgeDocumentAttachment: vi.fn() }
})

function attachment(
  attachmentId: number,
  name: string,
  overrides: Partial<AttachmentMetadata> = {},
): AttachmentMetadata {
  const extension = `.${name.split('.').at(-1)?.toLowerCase() ?? 'pdf'}`
  return {
    attachmentId,
    kind: 'File',
    originalFileName: name,
    extension,
    contentType: extension === '.zip' ? 'application/zip' : 'application/pdf',
    sizeBytes: 2_621_440,
    sha256: attachmentId.toString(16).padStart(64, '0'),
    previewMode: extension === '.zip' ? 'None' : 'Pdf',
    canPreview: extension !== '.zip',
    canDownload: false,
    ...overrides,
  }
}

const components = {
  ElButton: {
    props: { disabled: Boolean, loading: Boolean },
    emits: ['click'],
    template:
      '<button type="button" :disabled="disabled" :data-loading="loading" @click="$emit(\'click\')"><slot /></button>',
  },
}

function mountArea(
  attachments: readonly AttachmentMetadata[] = [],
  options: { editable?: boolean; revisionNumber?: number } = {},
) {
  return mount(KnowledgeDocumentAttachmentArea, {
    props: { documentId: 7, attachments, ...options },
    global: { components },
  })
}

async function selectFiles(wrapper: ReturnType<typeof mountArea>, files: readonly File[]) {
  const input = wrapper.get('input[type="file"]')
  Object.defineProperty(input.element, 'files', { configurable: true, value: files })
  await input.trigger('change')
  await flushPromises()
}

function apiError(status: number, message: string) {
  return new ApiError(status, {
    code: status === 413 ? 'payload_too_large' : 'unsupported_media_type',
    message,
    fieldErrors: { file: [message] },
    details: null,
  })
}

describe('KnowledgeDocumentAttachmentArea', () => {
  beforeEach(() => {
    vi.mocked(uploadKnowledgeDocumentAttachment).mockReset()
    vi.mocked(ElMessageBox.confirm).mockReset()
  })

  it('uploads one ordinary file and emits the complete desired set without adding images', async () => {
    const existing = attachment(10, '已有规范.pdf', { canDownload: true })
    const uploaded = attachment(11, '新增日志.txt', {
      extension: '.txt',
      contentType: 'text/plain',
      previewMode: 'Text',
    })
    vi.mocked(uploadKnowledgeDocumentAttachment).mockResolvedValue(uploaded)
    const wrapper = mountArea(
      [
        existing,
        attachment(99, '图片.png', {
          kind: 'Image',
          extension: '.png',
          contentType: 'image/png',
          previewMode: 'Image',
        }),
      ],
      { editable: true },
    )

    await selectFiles(wrapper, [new File(['log'], '新增日志.txt', { type: 'text/plain' })])

    expect(uploadKnowledgeDocumentAttachment).toHaveBeenCalledWith(7, expect.any(File))
    expect(wrapper.emitted('update:attachments')?.at(-1)?.[0]).toEqual([existing, uploaded])
    expect(wrapper.text()).toContain('已添加 1 个附件到待保存集合')
  })

  it('uploads multiple files sequentially, preserves successes on partial failure, and reports oversize', async () => {
    const pdf = attachment(21, 'MES接口规范.pdf')
    const zip = attachment(23, 'Source.zip')
    vi.mocked(uploadKnowledgeDocumentAttachment)
      .mockResolvedValueOnce(pdf)
      .mockRejectedValueOnce(apiError(413, '附件超过配置的大小限制。'))
      .mockResolvedValueOnce(zip)
    const wrapper = mountArea([], { editable: true })

    await selectFiles(wrapper, [
      new File(['pdf'], 'MES接口规范.pdf', { type: 'application/pdf' }),
      new File(['xlsx'], 'Equipment.xlsx'),
      new File(['zip'], 'Source.zip', { type: 'application/x-zip-compressed' }),
    ])

    expect(
      vi.mocked(uploadKnowledgeDocumentAttachment).mock.calls.map((call) => call[1].name),
    ).toEqual(['MES接口规范.pdf', 'Equipment.xlsx', 'Source.zip'])
    expect(vi.mocked(uploadKnowledgeDocumentAttachment).mock.calls[2][1].type).toBe(
      'application/zip',
    )
    expect(
      Array.from(
        new Uint8Array(
          await vi.mocked(uploadKnowledgeDocumentAttachment).mock.calls[2][1].arrayBuffer(),
        ),
      ),
    ).toEqual(Array.from(new TextEncoder().encode('zip')))
    expect(wrapper.emitted('update:attachments')?.at(-1)?.[0]).toEqual([pdf, zip])
    expect(wrapper.text()).toContain('Equipment.xlsx：文件超过服务器允许的大小限制。')
    expect(wrapper.text()).toContain('已添加 2 个附件到待保存集合')
  })

  it('rejects an unsupported final extension before upload and keeps the existing set', async () => {
    const existing = attachment(31, '保留.zip')
    const wrapper = mountArea([existing], { editable: true })

    await selectFiles(wrapper, [new File(['exe'], '危险.pdf.exe')])

    expect(uploadKnowledgeDocumentAttachment).not.toHaveBeenCalled()
    expect(wrapper.emitted('update:attachments')).toBeUndefined()
    expect(wrapper.text()).toContain('危险.pdf.exe：文件扩展名不在普通附件允许列表中。')
    expect(wrapper.text()).toContain('保留.zip')
  })

  it('prevents a second batch and duplicate IDs while an upload is pending', async () => {
    let resolveUpload!: (value: AttachmentMetadata) => void
    vi.mocked(uploadKnowledgeDocumentAttachment).mockReturnValue(
      new Promise((resolve) => {
        resolveUpload = resolve
      }),
    )
    const wrapper = mountArea([], { editable: true })
    const first = new File(['one'], 'one.pdf', { type: 'application/pdf' })
    const second = new File(['two'], 'two.pdf', { type: 'application/pdf' })

    const input = wrapper.get('input[type="file"]')
    Object.defineProperty(input.element, 'files', { configurable: true, value: [first, second] })
    await input.trigger('change')
    await flushPromises()
    expect(wrapper.text()).toContain('正在上传 1/2：one.pdf')
    expect(wrapper.get('[aria-label="添加普通附件"]').attributes('disabled')).toBeDefined()

    Object.defineProperty(input.element, 'files', { configurable: true, value: [first] })
    await input.trigger('change')
    expect(uploadKnowledgeDocumentAttachment).toHaveBeenCalledTimes(1)

    resolveUpload(attachment(41, 'one.pdf'))
    await flushPromises()
    vi.mocked(uploadKnowledgeDocumentAttachment).mockResolvedValueOnce(attachment(41, 'two.pdf'))
    await flushPromises()
    expect(wrapper.emitted('update:attachments')?.at(-1)?.[0]).toEqual([attachment(41, 'one.pdf')])
  })

  it('shows preview capability metadata and exact current/historical secure download routes', () => {
    const pdf = attachment(51, '超长名称-MES-接口-规范-2026-最终确认版本.pdf', {
      canDownload: true,
    })
    const zip = attachment(52, 'Source.zip', { canDownload: true })
    const current = mountArea([pdf, zip])

    expect(current.text()).toContain('PDF · 2.5 MB')
    expect(current.text()).toContain('支持PDF预览（预览功能将在下一阶段提供）')
    expect(current.text()).toContain('仅支持下载')
    expect(current.get('[aria-label^="下载附件 超长名称"]').attributes('href')).toBe(
      '/api/knowledge-documents/7/attachments/51/download',
    )
    expect(current.find('input[type="file"]').exists()).toBe(false)
    expect(current.findAll('button').some((button) => button.text() === '移除')).toBe(false)

    const historical = mountArea([pdf], { revisionNumber: 4 })
    expect(historical.get('a').attributes('href')).toBe(
      '/api/knowledge-documents/7/revisions/4/attachments/51/download',
    )
  })

  it('confirms reference-only removal and renders an accessible empty state', async () => {
    const pdf = attachment(61, '待移除.pdf', { canDownload: true })
    vi.mocked(ElMessageBox.confirm).mockResolvedValue('confirm' as never)
    const wrapper = mountArea([pdf], { editable: true })

    await wrapper.get('[aria-label="移除附件 待移除.pdf"]').trigger('click')
    await flushPromises()

    expect(ElMessageBox.confirm).toHaveBeenCalledWith(
      expect.stringContaining('历史修订和文件本身仍会保留'),
      '移除附件引用',
      expect.objectContaining({ confirmButtonText: '移除' }),
    )
    expect(wrapper.emitted('update:attachments')?.at(-1)?.[0]).toEqual([])
    expect(mountArea().text()).toContain('暂无附件')
  })
})
