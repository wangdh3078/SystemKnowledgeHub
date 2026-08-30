import { apiClient } from '../../../api/client/apiClient'

export interface AttachmentRuntimeCapabilities {
  readonly allowedImageExtensions: readonly string[]
  readonly allowedFileExtensions: readonly string[]
  readonly maxImageBytes: number
  readonly maxFileBytes: number
  readonly maxStoredAttachmentsPerDocument: number
}

let cachedCapabilities: Promise<AttachmentRuntimeCapabilities> | null = null

export function getAttachmentRuntimeCapabilities(): Promise<AttachmentRuntimeCapabilities> {
  cachedCapabilities ??= apiClient
    .get('/runtime-capabilities/attachments', { decode: decodeAttachmentRuntimeCapabilities })
    .catch((error: unknown) => {
      cachedCapabilities = null
      throw error
    })
  return cachedCapabilities
}

export function clearAttachmentRuntimeCapabilitiesCache(): void {
  cachedCapabilities = null
}

export function decodeAttachmentRuntimeCapabilities(value: unknown): AttachmentRuntimeCapabilities {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    throw new TypeError('attachmentRuntimeCapabilities must be an object')
  }
  const root = value as Record<string, unknown>
  return {
    allowedImageExtensions: readExtensions(root.allowedImageExtensions, 'allowedImageExtensions'),
    allowedFileExtensions: readExtensions(root.allowedFileExtensions, 'allowedFileExtensions'),
    maxImageBytes: readPositiveSafeInteger(root.maxImageBytes, 'maxImageBytes'),
    maxFileBytes: readPositiveSafeInteger(root.maxFileBytes, 'maxFileBytes'),
    maxStoredAttachmentsPerDocument: readPositiveSafeInteger(
      root.maxStoredAttachmentsPerDocument,
      'maxStoredAttachmentsPerDocument',
    ),
  }
}

function readExtensions(value: unknown, field: string): readonly string[] {
  if (!Array.isArray(value)) throw new TypeError(`${field} must be an array`)
  const extensions = value.map((item, index) => {
    if (typeof item !== 'string' || !/^\.[a-z0-9]+$/u.test(item) || item !== item.toLowerCase()) {
      throw new TypeError(`${field}[${index}] must be a canonical extension`)
    }
    return item
  })
  if (new Set(extensions).size !== extensions.length) {
    throw new TypeError(`${field} must not contain duplicates`)
  }
  return extensions
}

function readPositiveSafeInteger(value: unknown, field: string): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value <= 0) {
    throw new TypeError(`${field} must be a positive safe integer`)
  }
  return value
}
