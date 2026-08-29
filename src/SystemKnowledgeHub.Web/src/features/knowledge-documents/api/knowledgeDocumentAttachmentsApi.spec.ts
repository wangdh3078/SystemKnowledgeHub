import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiClient } from '../../../api/client/apiClient'
import { UnexpectedResponseError } from '../../../api/errors/ApiError'
import {
  getKnowledgeDocumentAttachmentPreview,
  getKnowledgeDocumentPdfPreview,
} from './knowledgeDocumentAttachmentsApi'

vi.mock('../../../api/client/apiClient', () => ({
  apiClient: {
    get: vi.fn(),
    getBlob: vi.fn(),
    postForm: vi.fn(),
  },
}))

describe('knowledge document attachment preview API', () => {
  beforeEach(() => {
    vi.mocked(apiClient.get).mockReset()
    vi.mocked(apiClient.getBlob).mockReset()
  })

  it('requests current JSON preview through the typed decoder boundary', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ mode: 'Text' } as never)
    const controller = new AbortController()

    await getKnowledgeDocumentAttachmentPreview(7, 53, undefined, undefined, controller.signal)

    expect(apiClient.get).toHaveBeenCalledWith(
      '/knowledge-documents/7/attachments/53/preview',
      expect.objectContaining({ signal: controller.signal, decode: expect.any(Function) }),
    )
  })

  it('requests an exact historical spreadsheet sheet with URL encoding', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ mode: 'Spreadsheet' } as never)

    await getKnowledgeDocumentAttachmentPreview(7, 56, 4, 'MES 数据 & 复核')

    expect(apiClient.get).toHaveBeenCalledWith(
      '/knowledge-documents/7/revisions/4/attachments/56/preview?sheet=MES%20%E6%95%B0%E6%8D%AE%20%26%20%E5%A4%8D%E6%A0%B8',
      expect.objectContaining({ decode: expect.any(Function) }),
    )
  })

  it('requests protected PDF bytes and rejects a mismatched response MIME', async () => {
    const pdf = new Blob(['%PDF-1.7'], { type: 'application/pdf' })
    vi.mocked(apiClient.getBlob).mockResolvedValueOnce(pdf)

    await expect(getKnowledgeDocumentPdfPreview(7, 51, 4)).resolves.toBe(pdf)
    expect(apiClient.getBlob).toHaveBeenCalledWith(
      '/knowledge-documents/7/revisions/4/attachments/51/preview',
      expect.objectContaining({ headers: { Accept: 'application/pdf' } }),
    )

    vi.mocked(apiClient.getBlob).mockResolvedValueOnce(
      new Blob(['not pdf'], { type: 'text/plain' }),
    )
    await expect(getKnowledgeDocumentPdfPreview(7, 51)).rejects.toBeInstanceOf(
      UnexpectedResponseError,
    )
  })

  it('fails before a request when the exact context identifiers are invalid', async () => {
    await expect(getKnowledgeDocumentAttachmentPreview(7, 53, 0)).rejects.toThrow('修订号无效')
    await expect(getKnowledgeDocumentPdfPreview(0, 51)).rejects.toThrow('知识内容或附件 ID 无效')
    expect(apiClient.get).not.toHaveBeenCalled()
    expect(apiClient.getBlob).not.toHaveBeenCalled()
  })
})
