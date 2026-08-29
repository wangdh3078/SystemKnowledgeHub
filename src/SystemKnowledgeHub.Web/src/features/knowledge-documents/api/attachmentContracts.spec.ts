import { describe, expect, it } from 'vitest'
import { decodeAttachmentMetadata } from './attachmentContracts'
import {
  knowledgeDocumentAttachmentDownloadUrl,
  knowledgeDocumentImageContentUrl,
} from './knowledgeDocumentAttachmentsApi'

function imageMetadata(overrides: Readonly<Record<string, unknown>> = {}) {
  return {
    attachmentId: 123,
    kind: 'Image',
    originalFileName: 'MES 状态.png',
    extension: '.png',
    contentType: 'image/png',
    sizeBytes: 24,
    sha256: 'a'.repeat(64),
    previewMode: 'Image',
    canPreview: true,
    canDownload: false,
    ...overrides,
  }
}

describe('attachment contracts', () => {
  it('decodes the B01 upload response without accepting storage details', () => {
    const metadata = decodeAttachmentMetadata({
      ...imageMetadata(),
      storageKey: 'objects/not-public.bin',
    })

    expect(metadata).toEqual(imageMetadata())
    expect(metadata).not.toHaveProperty('storageKey')
  })

  it('rejects unsupported kind, preview mode, unsafe IDs, and malformed hashes', () => {
    expect(() => decodeAttachmentMetadata(imageMetadata({ kind: 'Svg' }))).toThrow()
    expect(() => decodeAttachmentMetadata(imageMetadata({ previewMode: 'Thumbnail' }))).toThrow()
    expect(() =>
      decodeAttachmentMetadata(imageMetadata({ attachmentId: Number.MAX_SAFE_INTEGER + 1 })),
    ).toThrow()
    expect(() => decodeAttachmentMetadata(imageMetadata({ sha256: 'ABC' }))).toThrow()
  })

  it('builds only exact current or historical protected content routes', () => {
    expect(knowledgeDocumentImageContentUrl(7, 123)).toBe(
      '/api/knowledge-documents/7/attachments/123/content',
    )
    expect(knowledgeDocumentImageContentUrl(7, 123, 4)).toBe(
      '/api/knowledge-documents/7/revisions/4/attachments/123/content',
    )
    expect(() => knowledgeDocumentImageContentUrl(7, 0)).toThrow('附件 ID 无效')
  })

  it('builds only exact current or historical protected download routes', () => {
    expect(knowledgeDocumentAttachmentDownloadUrl(7, 456)).toBe(
      '/api/knowledge-documents/7/attachments/456/download',
    )
    expect(knowledgeDocumentAttachmentDownloadUrl(7, 456, 9)).toBe(
      '/api/knowledge-documents/7/revisions/9/attachments/456/download',
    )
    expect(() => knowledgeDocumentAttachmentDownloadUrl(7, 456, 0)).toThrow('修订号无效')
  })
})
