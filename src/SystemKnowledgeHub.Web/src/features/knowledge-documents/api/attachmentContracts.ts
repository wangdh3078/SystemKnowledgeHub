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
