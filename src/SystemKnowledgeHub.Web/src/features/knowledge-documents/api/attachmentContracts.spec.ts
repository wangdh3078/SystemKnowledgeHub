import { describe, expect, it } from 'vitest'
import { decodeAttachmentJsonPreview, decodeAttachmentMetadata } from './attachmentContracts'
import {
  knowledgeDocumentAttachmentDownloadUrl,
  knowledgeDocumentAttachmentPreviewPath,
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

  it('builds exact protected preview routes and safely encodes sheet names', () => {
    expect(knowledgeDocumentAttachmentPreviewPath(7, 456)).toBe(
      '/knowledge-documents/7/attachments/456/preview',
    )
    expect(knowledgeDocumentAttachmentPreviewPath(7, 456, 9, 'MES 数据 & 复核')).toBe(
      '/knowledge-documents/7/revisions/9/attachments/456/preview?sheet=MES%20%E6%95%B0%E6%8D%AE%20%26%20%E5%A4%8D%E6%A0%B8',
    )
    expect(() => knowledgeDocumentAttachmentPreviewPath(7, 456, 0)).toThrow('修订号无效')
  })

  it.each(['Text', 'Markdown'] as const)(
    'decodes %s preview text without interpreting inert markup',
    (mode) => {
      const preview = decodeAttachmentJsonPreview({
        attachment: imageMetadata({ kind: 'File', previewMode: mode }),
        mode,
        text: '<script>alert(1)</script>\n=1+1',
        truncated: true,
        returnedBytes: 31,
        maximumBytes: 32,
      })

      expect(preview.mode).toBe(mode)
      if (preview.mode === 'Text' || preview.mode === 'Markdown') {
        expect(preview.text).toBe('<script>alert(1)</script>\n=1+1')
        expect(preview.truncated).toBe(true)
      }
    },
  )

  it('decodes bounded CSV rows as display strings', () => {
    const preview = decodeAttachmentJsonPreview({
      attachment: imageMetadata({ kind: 'File', previewMode: 'Csv' }),
      mode: 'Csv',
      rows: [
        ['设备', '公式'],
        ['PLC-01', '=HYPERLINK("https://example.invalid")'],
      ],
      truncated: true,
      truncationReasons: ['Rows'],
      maximumRows: 2,
      maximumColumns: 4,
      maximumCharacters: 1024,
    })

    expect(preview.mode).toBe('Csv')
    if (preview.mode === 'Csv') {
      expect(preview.rows[1]?.[1]).toBe('=HYPERLINK("https://example.invalid")')
      expect(preview.truncationReasons).toEqual(['Rows'])
    }
  })

  it('decodes spreadsheet sheet metadata and cached display values only', () => {
    const preview = decodeAttachmentJsonPreview({
      attachment: imageMetadata({ kind: 'File', previewMode: 'Spreadsheet' }),
      mode: 'Spreadsheet',
      sheets: [
        { name: 'Data', visibility: 'Visible' },
        { name: 'Archive', visibility: 'Hidden' },
      ],
      selectedSheet: 'Data',
      rows: [{ rowNumber: 2, cells: ['PLC-01', '42'] }],
      truncated: false,
      truncationReasons: [],
      maximumSheets: 8,
      maximumRows: 200,
      maximumColumns: 50,
    })

    expect(preview.mode).toBe('Spreadsheet')
    if (preview.mode === 'Spreadsheet') {
      expect(preview.sheets.map((sheet) => sheet.name)).toEqual(['Data', 'Archive'])
      expect(preview.rows).toEqual([{ rowNumber: 2, cells: ['PLC-01', '42'] }])
    }
  })

  it('rejects unsupported preview modes and malformed bounded structures', () => {
    const base = {
      attachment: imageMetadata({ kind: 'File', previewMode: 'Text' }),
      mode: 'Text',
      text: 'log',
      truncated: false,
      returnedBytes: 3,
      maximumBytes: 32,
    }
    expect(() => decodeAttachmentJsonPreview({ ...base, mode: 'Pdf' })).toThrow(
      'unsupported preview mode',
    )
    expect(() =>
      decodeAttachmentJsonPreview({ ...base, mode: 'Csv', rows: [['ok'], [7]] }),
    ).toThrow('string array')
  })
})
