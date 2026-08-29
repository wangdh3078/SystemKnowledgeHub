export const attachmentKinds = ['Image', 'File'] as const
export const attachmentPreviewModes = [
  'Image',
  'Pdf',
  'Text',
  'Markdown',
  'Csv',
  'Spreadsheet',
  'None',
] as const

export type AttachmentKind = (typeof attachmentKinds)[number]
export type AttachmentPreviewMode = (typeof attachmentPreviewModes)[number]

export interface AttachmentMetadata {
  readonly attachmentId: number
  readonly kind: AttachmentKind
  readonly originalFileName: string
  readonly extension: string
  readonly contentType: string
  readonly sizeBytes: number
  readonly sha256: string
  readonly previewMode: AttachmentPreviewMode
  readonly canPreview: boolean
  readonly canDownload: boolean
}

export interface AttachmentTextPreview {
  readonly attachment: AttachmentMetadata
  readonly mode: 'Text' | 'Markdown'
  readonly text: string
  readonly truncated: boolean
  readonly returnedBytes: number
  readonly maximumBytes: number
}

export interface AttachmentCsvPreview {
  readonly attachment: AttachmentMetadata
  readonly mode: 'Csv'
  readonly rows: readonly (readonly string[])[]
  readonly truncated: boolean
  readonly truncationReasons: readonly string[]
  readonly maximumRows: number
  readonly maximumColumns: number
  readonly maximumCharacters: number
}

export interface AttachmentSpreadsheetSheet {
  readonly name: string
  readonly visibility: string
}

export interface AttachmentSpreadsheetRow {
  readonly rowNumber: number
  readonly cells: readonly string[]
}

export interface AttachmentSpreadsheetPreview {
  readonly attachment: AttachmentMetadata
  readonly mode: 'Spreadsheet'
  readonly sheets: readonly AttachmentSpreadsheetSheet[]
  readonly selectedSheet: string
  readonly rows: readonly AttachmentSpreadsheetRow[]
  readonly truncated: boolean
  readonly truncationReasons: readonly string[]
  readonly maximumSheets: number
  readonly maximumRows: number
  readonly maximumColumns: number
}

export type AttachmentJsonPreview =
  AttachmentTextPreview | AttachmentCsvPreview | AttachmentSpreadsheetPreview

export interface AttachmentPreviewContext {
  readonly documentId: number
  readonly revisionNumber?: number
  readonly attachment: AttachmentMetadata
}

type JsonObject = Readonly<Record<string, unknown>>

function readObject(value: unknown, field: string): JsonObject {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    throw new Error(`${field} must be an object`)
  }
  return value as JsonObject
}

function readString(value: unknown, field: string): string {
  if (typeof value !== 'string') throw new Error(`${field} must be a string`)
  return value
}

function readBoolean(value: unknown, field: string): boolean {
  if (typeof value !== 'boolean') throw new Error(`${field} must be a boolean`)
  return value
}

function readPositiveSafeInteger(value: unknown, field: string): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < 1) {
    throw new Error(`${field} must be a safe positive integer`)
  }
  return value
}

function readNonNegativeSafeInteger(value: unknown, field: string): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < 0) {
    throw new Error(`${field} must be a safe non-negative integer`)
  }
  return value
}

function readStringArray(value: unknown, field: string): readonly string[] {
  if (!Array.isArray(value) || !value.every((item) => typeof item === 'string')) {
    throw new Error(`${field} must be a string array`)
  }
  return value
}

function readStringRows(value: unknown, field: string): readonly (readonly string[])[] {
  if (!Array.isArray(value)) throw new Error(`${field} must be an array`)
  return value.map((row, index) => readStringArray(row, `${field}[${index}]`))
}

function readKind(value: unknown, field: string): AttachmentKind {
  const kind = readString(value, field)
  if (attachmentKinds.includes(kind as AttachmentKind)) return kind as AttachmentKind
  throw new Error(`${field} has an unsupported attachment kind`)
}

function readPreviewMode(value: unknown, field: string): AttachmentPreviewMode {
  const mode = readString(value, field)
  if (attachmentPreviewModes.includes(mode as AttachmentPreviewMode)) {
    return mode as AttachmentPreviewMode
  }
  throw new Error(`${field} has an unsupported attachment preview mode`)
}

export function decodeAttachmentMetadata(value: unknown, field = 'attachment'): AttachmentMetadata {
  const item = readObject(value, field)
  const sizeBytes = readPositiveSafeInteger(item.sizeBytes, `${field}.sizeBytes`)
  const sha256 = readString(item.sha256, `${field}.sha256`)
  if (!/^[a-f0-9]{64}$/u.test(sha256)) throw new Error(`${field}.sha256 must be lowercase SHA-256`)

  return {
    attachmentId: readPositiveSafeInteger(item.attachmentId, `${field}.attachmentId`),
    kind: readKind(item.kind, `${field}.kind`),
    originalFileName: readString(item.originalFileName, `${field}.originalFileName`),
    extension: readString(item.extension, `${field}.extension`),
    contentType: readString(item.contentType, `${field}.contentType`),
    sizeBytes,
    sha256,
    previewMode: readPreviewMode(item.previewMode, `${field}.previewMode`),
    canPreview: readBoolean(item.canPreview, `${field}.canPreview`),
    canDownload: readBoolean(item.canDownload, `${field}.canDownload`),
  }
}

export function decodeAttachmentMetadataList(
  value: unknown,
  field = 'attachmentReferences',
): readonly AttachmentMetadata[] {
  if (!Array.isArray(value)) throw new Error(`${field} must be an array`)
  return value.map((item, index) => decodeAttachmentMetadata(item, `${field}[${index}]`))
}

export function decodeAttachmentJsonPreview(value: unknown): AttachmentJsonPreview {
  const root = readObject(value, 'attachmentPreview')
  const mode = readString(root.mode, 'attachmentPreview.mode')
  const attachment = decodeAttachmentMetadata(root.attachment, 'attachmentPreview.attachment')
  const truncated = readBoolean(root.truncated, 'attachmentPreview.truncated')

  if (mode === 'Text' || mode === 'Markdown') {
    return {
      attachment,
      mode,
      text: readString(root.text, 'attachmentPreview.text'),
      truncated,
      returnedBytes: readNonNegativeSafeInteger(
        root.returnedBytes,
        'attachmentPreview.returnedBytes',
      ),
      maximumBytes: readPositiveSafeInteger(root.maximumBytes, 'attachmentPreview.maximumBytes'),
    }
  }

  if (mode === 'Csv') {
    return {
      attachment,
      mode,
      rows: readStringRows(root.rows, 'attachmentPreview.rows'),
      truncated,
      truncationReasons: readStringArray(
        root.truncationReasons,
        'attachmentPreview.truncationReasons',
      ),
      maximumRows: readPositiveSafeInteger(root.maximumRows, 'attachmentPreview.maximumRows'),
      maximumColumns: readPositiveSafeInteger(
        root.maximumColumns,
        'attachmentPreview.maximumColumns',
      ),
      maximumCharacters: readPositiveSafeInteger(
        root.maximumCharacters,
        'attachmentPreview.maximumCharacters',
      ),
    }
  }

  if (mode === 'Spreadsheet') {
    if (!Array.isArray(root.sheets)) throw new Error('attachmentPreview.sheets must be an array')
    if (!Array.isArray(root.rows)) throw new Error('attachmentPreview.rows must be an array')
    return {
      attachment,
      mode,
      sheets: root.sheets.map((item, index) => {
        const sheet = readObject(item, `attachmentPreview.sheets[${index}]`)
        return {
          name: readString(sheet.name, `attachmentPreview.sheets[${index}].name`),
          visibility: readString(sheet.visibility, `attachmentPreview.sheets[${index}].visibility`),
        }
      }),
      selectedSheet: readString(root.selectedSheet, 'attachmentPreview.selectedSheet'),
      rows: root.rows.map((item, index) => {
        const row = readObject(item, `attachmentPreview.rows[${index}]`)
        return {
          rowNumber: readPositiveSafeInteger(
            row.rowNumber,
            `attachmentPreview.rows[${index}].rowNumber`,
          ),
          cells: readStringArray(row.cells, `attachmentPreview.rows[${index}].cells`),
        }
      }),
      truncated,
      truncationReasons: readStringArray(
        root.truncationReasons,
        'attachmentPreview.truncationReasons',
      ),
      maximumSheets: readPositiveSafeInteger(root.maximumSheets, 'attachmentPreview.maximumSheets'),
      maximumRows: readPositiveSafeInteger(root.maximumRows, 'attachmentPreview.maximumRows'),
      maximumColumns: readPositiveSafeInteger(
        root.maximumColumns,
        'attachmentPreview.maximumColumns',
      ),
    }
  }

  throw new Error('attachmentPreview.mode has an unsupported preview mode')
}
