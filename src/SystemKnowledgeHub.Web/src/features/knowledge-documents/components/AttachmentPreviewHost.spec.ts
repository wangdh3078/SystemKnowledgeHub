import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { ApiErrorCode } from '../../../api/contracts/errors'
import { ApiError, NetworkRequestError } from '../../../api/errors/ApiError'
import { useOverlayStore } from '../../../app/stores/overlays'
import type {
  AttachmentJsonPreview,
  AttachmentMetadata,
  AttachmentPreviewContext,
  AttachmentTextPreview,
} from '../api/attachmentContracts'
import {
  getKnowledgeDocumentAttachmentPreview,
  getKnowledgeDocumentPdfPreview,
} from '../api/knowledgeDocumentAttachmentsApi'
import AttachmentPreviewHost from './AttachmentPreviewHost.vue'

vi.mock('../api/knowledgeDocumentAttachmentsApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/knowledgeDocumentAttachmentsApi')>()
  return {
    ...actual,
    getKnowledgeDocumentAttachmentPreview: vi.fn(),
    getKnowledgeDocumentPdfPreview: vi.fn(),
  }
})

function attachment(
  attachmentId: number,
  previewMode: AttachmentMetadata['previewMode'],
  overrides: Partial<AttachmentMetadata> = {},
): AttachmentMetadata {
  const extensionByMode: Partial<Record<AttachmentMetadata['previewMode'], string>> = {
    Pdf: '.pdf',
    Text: '.txt',
    Markdown: '.md',
    Csv: '.csv',
    Spreadsheet: '.xlsx',
  }
  const extension = extensionByMode[previewMode] ?? '.bin'
  return {
    attachmentId,
    kind: 'File',
    originalFileName: `attachment-${attachmentId}${extension}`,
    extension,
    contentType: 'application/octet-stream',
    sizeBytes: 2048,
    sha256: attachmentId.toString(16).padStart(64, '0'),
    previewMode,
    canPreview: true,
    canDownload: true,
    ...overrides,
  }
}

function context(
  previewMode: AttachmentMetadata['previewMode'],
  overrides: Partial<AttachmentPreviewContext> = {},
): AttachmentPreviewContext {
  return {
    documentId: 7,
    attachment: attachment(51, previewMode),
    ...overrides,
  }
}

function textPreview(
  mode: 'Text' | 'Markdown',
  metadata = attachment(51, mode),
  text = 'plain text',
): AttachmentTextPreview {
  return {
    attachment: metadata,
    mode,
    text,
    truncated: false,
    returnedBytes: text.length,
    maximumBytes: 4096,
  }
}

function spreadsheetPreview(
  selectedSheet: string,
  metadata = attachment(51, 'Spreadsheet'),
): AttachmentJsonPreview {
  return {
    attachment: metadata,
    mode: 'Spreadsheet',
    sheets: [
      { name: 'Data', visibility: 'Visible' },
      { name: 'Archive', visibility: 'Hidden' },
    ],
    selectedSheet,
    rows: [{ rowNumber: 2, cells: [selectedSheet, '42'] }],
    truncated: selectedSheet === 'Archive',
    truncationReasons: selectedSheet === 'Archive' ? ['Rows'] : [],
    maximumSheets: 8,
    maximumRows: 200,
    maximumColumns: 50,
  }
}

const components = {
  ElButton: {
    props: { disabled: Boolean },
    emits: ['click'],
    template:
      '<button type="button" :disabled="disabled" @click="$emit(\'click\')"><slot /></button>',
  },
}

function mountPreview(previewContext: AttachmentPreviewContext) {
  const pinia = createPinia()
  setActivePinia(pinia)
  const overlayStore = useOverlayStore()
  overlayStore.openDialog({
    kind: 'attachment-preview',
    id: previewContext.attachment.attachmentId,
    mode: 'read',
    payload: previewContext,
  })
  return {
    overlayStore,
    wrapper: mount(AttachmentPreviewHost, {
      global: { plugins: [pinia], components },
    }),
  }
}

function apiError(status: number, code: ApiErrorCode, message: string) {
  return new ApiError(status, { code, message, fieldErrors: null, details: null })
}

describe('AttachmentPreviewHost', () => {
  beforeEach(() => {
    vi.mocked(getKnowledgeDocumentAttachmentPreview).mockReset()
    vi.mocked(getKnowledgeDocumentPdfPreview).mockReset()
    Object.defineProperties(URL, {
      createObjectURL: {
        configurable: true,
        value: vi.fn(() => 'blob:protected-pdf'),
      },
      revokeObjectURL: {
        configurable: true,
        value: vi.fn(),
      },
    })
  })

  it('loads exact protected PDF bytes, exposes download fallback, and revokes the object URL', async () => {
    vi.mocked(getKnowledgeDocumentPdfPreview).mockResolvedValue(
      new Blob(['%PDF-1.7'], { type: 'application/pdf' }),
    )
    const previewContext = context('Pdf', {
      revisionNumber: 4,
      attachment: attachment(51, 'Pdf', { originalFileName: '规范.pdf' }),
    })
    const { wrapper } = mountPreview(previewContext)
    expect(wrapper.get('[role="status"]').text()).toContain('正在加载')

    await flushPromises()

    expect(getKnowledgeDocumentPdfPreview).toHaveBeenCalledWith(7, 51, 4, expect.any(AbortSignal))
    expect(wrapper.text()).toContain('历史修订 4')
    expect(wrapper.get('iframe').attributes('src')).toBe('blob:protected-pdf')
    expect(wrapper.text()).not.toContain('正在初始化 PDF 阅读器')
    expect(wrapper.get('[aria-label="下载原文件 规范.pdf"]').attributes('href')).toBe(
      '/api/knowledge-documents/7/revisions/4/attachments/51/download',
    )
    await wrapper.get('iframe').trigger('error')
    expect(wrapper.get('[role="alert"]').text()).toContain('浏览器无法显示该 PDF')
    wrapper.unmount()
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:protected-pdf')
  })

  it('renders text and truncation feedback as inert preformatted content', async () => {
    const metadata = attachment(52, 'Text', { originalFileName: 'runtime.json' })
    vi.mocked(getKnowledgeDocumentAttachmentPreview).mockResolvedValue({
      ...textPreview('Text', metadata, '<script>alert(1)</script>\n{"ok":true}'),
      truncated: true,
      returnedBytes: 32,
      maximumBytes: 32,
    })
    const { wrapper } = mountPreview(context('Text', { attachment: metadata }))
    await flushPromises()

    expect(getKnowledgeDocumentAttachmentPreview).toHaveBeenCalledWith(
      7,
      52,
      undefined,
      undefined,
      expect.any(AbortSignal),
    )
    expect(wrapper.get('pre').text()).toContain('<script>alert(1)</script>')
    expect(wrapper.find('script').exists()).toBe(false)
    expect(wrapper.text()).toContain('内容已截断')
  })

  it('reuses the safe Markdown renderer without activating embedded markup', async () => {
    const metadata = attachment(53, 'Markdown')
    vi.mocked(getKnowledgeDocumentAttachmentPreview).mockResolvedValue(
      textPreview('Markdown', metadata, '# 标题\n\n<script>alert(1)</script>'),
    )
    const { wrapper } = mountPreview(context('Markdown', { attachment: metadata }))
    await flushPromises()

    expect(wrapper.get('h1').text()).toBe('标题')
    expect(wrapper.find('script').exists()).toBe(false)
    expect(wrapper.text()).toContain('<script>alert(1)</script>')
  })

  it('renders bounded CSV values as text and reports truncation', async () => {
    const metadata = attachment(54, 'Csv')
    vi.mocked(getKnowledgeDocumentAttachmentPreview).mockResolvedValue({
      attachment: metadata,
      mode: 'Csv',
      rows: [
        ['设备', '值'],
        ['PLC-01', '=HYPERLINK("https://example.invalid")'],
        ['<script>alert(1)</script>', '42'],
      ],
      truncated: true,
      truncationReasons: ['Rows', 'Columns'],
      maximumRows: 3,
      maximumColumns: 2,
      maximumCharacters: 1024,
    })
    const { wrapper } = mountPreview(context('Csv', { attachment: metadata }))
    await flushPromises()

    expect(wrapper.findAll('tbody tr')).toHaveLength(3)
    expect(wrapper.text()).toContain('=HYPERLINK("https://example.invalid")')
    expect(wrapper.find('script').exists()).toBe(false)
    expect(wrapper.text()).toContain('行数、列数')
  })

  it('switches XLSX sheets through the exact endpoint and shows cached display values only', async () => {
    const metadata = attachment(55, 'Spreadsheet')
    vi.mocked(getKnowledgeDocumentAttachmentPreview)
      .mockResolvedValueOnce(spreadsheetPreview('Data', metadata))
      .mockResolvedValueOnce(spreadsheetPreview('Archive', metadata))
    const { wrapper } = mountPreview(context('Spreadsheet', { attachment: metadata }))
    await flushPromises()

    expect(wrapper.get('caption').text()).toBe('Data 工作表')
    await wrapper.get('select').setValue('Archive')
    await flushPromises()

    expect(getKnowledgeDocumentAttachmentPreview).toHaveBeenLastCalledWith(
      7,
      55,
      undefined,
      'Archive',
      expect.any(AbortSignal),
    )
    expect(wrapper.get('caption').text()).toBe('Archive 工作表')
    expect(wrapper.text()).toContain('工作表预览已截断')
    expect(wrapper.text()).toContain('42')
  })

  it('cancels an obsolete context request and ignores its late result', async () => {
    let resolveOld!: (value: AttachmentJsonPreview) => void
    const oldMetadata = attachment(61, 'Text', { originalFileName: 'old.log' })
    const newMetadata = attachment(62, 'Text', { originalFileName: 'new.log' })
    vi.mocked(getKnowledgeDocumentAttachmentPreview)
      .mockReturnValueOnce(
        new Promise((resolve) => {
          resolveOld = resolve
        }),
      )
      .mockResolvedValueOnce(textPreview('Text', newMetadata, 'new content'))
    const { overlayStore, wrapper } = mountPreview(context('Text', { attachment: oldMetadata }))
    await flushPromises()
    const firstSignal = vi.mocked(getKnowledgeDocumentAttachmentPreview).mock.calls[0]?.[4]

    overlayStore.openDialog({
      kind: 'attachment-preview',
      id: 62,
      mode: 'read',
      payload: context('Text', { attachment: newMetadata }),
    })
    await flushPromises()
    resolveOld(textPreview('Text', oldMetadata, 'obsolete content'))
    await flushPromises()

    expect(firstSignal?.aborted).toBe(true)
    expect(wrapper.text()).toContain('new content')
    expect(wrapper.text()).not.toContain('obsolete content')
  })

  it.each([
    [404, 'not_found', 'missing', '当前文档或修订中不存在该附件'],
    [422, 'preview_not_supported', 'unsupported', '不支持在线预览'],
    [422, 'preview_limit_exceeded', 'limit', '超过安全预览限制'],
    [503, 'attachment_unavailable', 'unavailable', '附件内容暂不可用'],
  ] as const)(
    'shows a clear fallback for HTTP %s preview failures',
    async (status, code, message, copy) => {
      vi.mocked(getKnowledgeDocumentAttachmentPreview).mockRejectedValue(
        apiError(status, code, message),
      )
      const metadata = attachment(71, 'Text', { originalFileName: 'failed.log' })
      const { wrapper } = mountPreview(context('Text', { attachment: metadata }))
      await flushPromises()

      expect(wrapper.get('[role="alert"]').text()).toContain(copy)
      expect(wrapper.get('[role="alert"] a').attributes('href')).toBe(
        '/api/knowledge-documents/7/attachments/71/download',
      )
    },
  )

  it('keeps the overlay usable on a network failure and closes through the shared host', async () => {
    vi.mocked(getKnowledgeDocumentAttachmentPreview).mockRejectedValue(new NetworkRequestError())
    const { overlayStore, wrapper } = mountPreview(context('Text'))
    await flushPromises()

    expect(wrapper.get('[role="alert"]').text()).toContain('无法连接服务器')
    await wrapper.get('[aria-label="关闭附件预览"]').trigger('click')
    expect(overlayStore.currentDialog).toBeNull()
  })
})
