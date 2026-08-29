import {
  attachmentKinds,
  attachmentPreviewModes,
  type AttachmentKind,
  type AttachmentPreviewMode,
} from '../../knowledge-documents/api/attachmentContracts'

export const administratorAttachmentReferenceStatuses = [
  'Referenced',
  'HistoricalOnly',
  'Orphan',
] as const
export const administratorAttachmentStorageStates = ['Ready', 'DeletePending'] as const
export const administratorAttachmentStorageHealthValues = [
  'Ready',
  'Missing',
  'LengthMismatch',
  'Corrupt',
  'DeletePending',
  'Unavailable',
] as const

export type AdministratorAttachmentReferenceStatus =
  (typeof administratorAttachmentReferenceStatuses)[number]
export type AdministratorAttachmentStorageState =
  (typeof administratorAttachmentStorageStates)[number]
export type AdministratorAttachmentStorageHealth =
  (typeof administratorAttachmentStorageHealthValues)[number]
export type AdministratorAttachmentReferenceFilter =
  '' | 'Referenced' | 'Orphan' | 'Current' | 'HistoricalOnly'

export interface AdministratorAttachmentOwner {
  readonly documentId: number
  readonly title: string
  readonly lifecycleStatus: string
  readonly isDeleted: boolean
}

export interface AdministratorAttachmentListItem {
  readonly attachmentId: number
  readonly originalFileName: string
  readonly extension: string
  readonly kind: AttachmentKind
  readonly contentType: string
  readonly sizeBytes: number
  readonly createdByDisplayName: string
  readonly createdAt: string
  readonly owner: AdministratorAttachmentOwner
  readonly referenceCount: number
  readonly currentReferenceCount: number
  readonly historicalReferenceCount: number
  readonly referenceStatus: AdministratorAttachmentReferenceStatus
  readonly storageState: AdministratorAttachmentStorageState
  readonly storageHealth: AdministratorAttachmentStorageHealth
  readonly previewMode: AttachmentPreviewMode
  readonly canPreview: boolean
  readonly sha256: string
}

export interface AdministratorAttachmentListResponse {
  readonly items: readonly AdministratorAttachmentListItem[]
  readonly page: number
  readonly pageSize: number
  readonly total: number
}

export interface AdministratorAttachmentReference {
  readonly revisionNumber: number
  readonly isCurrent: boolean
  readonly createdAt: string
}

export interface AdministratorAttachmentDetail {
  readonly attachmentId: number
  readonly originalFileName: string
  readonly extension: string
  readonly kind: AttachmentKind
  readonly contentType: string
  readonly sizeBytes: number
  readonly sha256: string
  readonly createdAt: string
  readonly createdByUserId: number
  readonly createdByDisplayName: string
  readonly storageState: AdministratorAttachmentStorageState
  readonly storageHealth: AdministratorAttachmentStorageHealth
  readonly previewMode: AttachmentPreviewMode
  readonly canPreview: boolean
  readonly concurrencyToken: string
  readonly owner: AdministratorAttachmentOwner
  readonly referenceCount: number
  readonly currentReferenceCount: number
  readonly historicalReferenceCount: number
  readonly referenceStatus: AdministratorAttachmentReferenceStatus
  readonly references: readonly AdministratorAttachmentReference[]
  readonly referencesTruncated: boolean
}

export interface AdministratorAttachmentStatisticItem {
  readonly attachmentId: number
  readonly originalFileName: string
  readonly kind: AttachmentKind
  readonly sizeBytes: number
  readonly createdAt: string
}

export interface AdministratorAttachmentStatistics {
  readonly totalCount: number
  readonly totalSizeBytes: number
  readonly imageCount: number
  readonly imageSizeBytes: number
  readonly fileCount: number
  readonly fileSizeBytes: number
  readonly orphanCount: number
  readonly orphanSizeBytes: number
  readonly referencedCount: number
  readonly currentReferencedCount: number
  readonly historicalOnlyCount: number
  readonly deletedOwnerCount: number
  readonly readyCount: number
  readonly deletePendingCount: number
  readonly recentWindowDays: number
  readonly recentUploadCount: number
  readonly largestAttachments: readonly AdministratorAttachmentStatisticItem[]
  readonly recentUploads: readonly AdministratorAttachmentStatisticItem[]
}

export interface AdministratorAttachmentIntegrity {
  readonly attachmentId: number
  readonly status: AdministratorAttachmentStorageHealth
  readonly sizeBytes: number
  readonly actualSizeBytes: number | null
  readonly sha256: string
  readonly actualSha256: string | null
  readonly checkedAt: string
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

function readInteger(value: unknown, field: string, minimum = 0): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < minimum) {
    throw new Error(`${field} must be a safe integer`)
  }
  return value
}

function readNullableInteger(value: unknown, field: string): number | null {
  return value === null ? null : readInteger(value, field)
}

function readEnum<TValue extends string>(
  value: unknown,
  field: string,
  values: readonly TValue[],
): TValue {
  const parsed = readString(value, field)
  if (!values.includes(parsed as TValue)) throw new Error(`${field} has an unsupported value`)
  return parsed as TValue
}

function readSha256(value: unknown, field: string): string {
  const hash = readString(value, field)
  if (!/^[a-f0-9]{64}$/u.test(hash)) throw new Error(`${field} must be lowercase SHA-256`)
  return hash
}

function readNullableSha256(value: unknown, field: string): string | null {
  return value === null ? null : readSha256(value, field)
}

function decodeOwner(value: unknown, field: string): AdministratorAttachmentOwner {
  const owner = readObject(value, field)
  return {
    documentId: readInteger(owner.documentId, `${field}.documentId`, 1),
    title: readString(owner.title, `${field}.title`),
    lifecycleStatus: readString(owner.lifecycleStatus, `${field}.lifecycleStatus`),
    isDeleted: readBoolean(owner.isDeleted, `${field}.isDeleted`),
  }
}

function decodeListItem(value: unknown, field: string): AdministratorAttachmentListItem {
  const item = readObject(value, field)
  return {
    attachmentId: readInteger(item.attachmentId, `${field}.attachmentId`, 1),
    originalFileName: readString(item.originalFileName, `${field}.originalFileName`),
    extension: readString(item.extension, `${field}.extension`),
    kind: readEnum(item.kind, `${field}.kind`, attachmentKinds),
    contentType: readString(item.contentType, `${field}.contentType`),
    sizeBytes: readInteger(item.sizeBytes, `${field}.sizeBytes`, 1),
    createdByDisplayName: readString(item.createdByDisplayName, `${field}.createdByDisplayName`),
    createdAt: readString(item.createdAt, `${field}.createdAt`),
    owner: decodeOwner(item.owner, `${field}.owner`),
    referenceCount: readInteger(item.referenceCount, `${field}.referenceCount`),
    currentReferenceCount: readInteger(
      item.currentReferenceCount,
      `${field}.currentReferenceCount`,
    ),
    historicalReferenceCount: readInteger(
      item.historicalReferenceCount,
      `${field}.historicalReferenceCount`,
    ),
    referenceStatus: readEnum(
      item.referenceStatus,
      `${field}.referenceStatus`,
      administratorAttachmentReferenceStatuses,
    ),
    storageState: readEnum(
      item.storageState,
      `${field}.storageState`,
      administratorAttachmentStorageStates,
    ),
    storageHealth: readEnum(
      item.storageHealth,
      `${field}.storageHealth`,
      administratorAttachmentStorageHealthValues,
    ),
    previewMode: readEnum(item.previewMode, `${field}.previewMode`, attachmentPreviewModes),
    canPreview: readBoolean(item.canPreview, `${field}.canPreview`),
    sha256: readSha256(item.sha256, `${field}.sha256`),
  }
}

export function decodeAdministratorAttachmentList(
  value: unknown,
): AdministratorAttachmentListResponse {
  const root = readObject(value, 'administratorAttachmentList')
  if (!Array.isArray(root.items))
    throw new Error('administratorAttachmentList.items must be an array')
  return {
    items: root.items.map((item, index) => decodeListItem(item, `items[${index}]`)),
    page: readInteger(root.page, 'administratorAttachmentList.page', 1),
    pageSize: readInteger(root.pageSize, 'administratorAttachmentList.pageSize', 1),
    total: readInteger(root.total, 'administratorAttachmentList.total'),
  }
}

export function decodeAdministratorAttachmentDetail(value: unknown): AdministratorAttachmentDetail {
  const root = readObject(value, 'administratorAttachmentDetail')
  const base = decodeListItem(value, 'administratorAttachmentDetail')
  if (!Array.isArray(root.references)) {
    throw new Error('administratorAttachmentDetail.references must be an array')
  }
  return {
    ...base,
    createdByUserId: readInteger(root.createdByUserId, 'createdByUserId', 1),
    concurrencyToken: readString(root.concurrencyToken, 'concurrencyToken'),
    references: root.references.map((referenceValue, index) => {
      const reference = readObject(referenceValue, `references[${index}]`)
      return {
        revisionNumber: readInteger(
          reference.revisionNumber,
          `references[${index}].revisionNumber`,
          1,
        ),
        isCurrent: readBoolean(reference.isCurrent, `references[${index}].isCurrent`),
        createdAt: readString(reference.createdAt, `references[${index}].createdAt`),
      }
    }),
    referencesTruncated: readBoolean(root.referencesTruncated, 'referencesTruncated'),
  }
}

function decodeStatisticItem(value: unknown, field: string): AdministratorAttachmentStatisticItem {
  const item = readObject(value, field)
  return {
    attachmentId: readInteger(item.attachmentId, `${field}.attachmentId`, 1),
    originalFileName: readString(item.originalFileName, `${field}.originalFileName`),
    kind: readEnum(item.kind, `${field}.kind`, attachmentKinds),
    sizeBytes: readInteger(item.sizeBytes, `${field}.sizeBytes`, 1),
    createdAt: readString(item.createdAt, `${field}.createdAt`),
  }
}

export function decodeAdministratorAttachmentStatistics(
  value: unknown,
): AdministratorAttachmentStatistics {
  const root = readObject(value, 'administratorAttachmentStatistics')
  if (!Array.isArray(root.largestAttachments) || !Array.isArray(root.recentUploads)) {
    throw new Error('administratorAttachmentStatistics bounded items must be arrays')
  }
  return {
    totalCount: readInteger(root.totalCount, 'totalCount'),
    totalSizeBytes: readInteger(root.totalSizeBytes, 'totalSizeBytes'),
    imageCount: readInteger(root.imageCount, 'imageCount'),
    imageSizeBytes: readInteger(root.imageSizeBytes, 'imageSizeBytes'),
    fileCount: readInteger(root.fileCount, 'fileCount'),
    fileSizeBytes: readInteger(root.fileSizeBytes, 'fileSizeBytes'),
    orphanCount: readInteger(root.orphanCount, 'orphanCount'),
    orphanSizeBytes: readInteger(root.orphanSizeBytes, 'orphanSizeBytes'),
    referencedCount: readInteger(root.referencedCount, 'referencedCount'),
    currentReferencedCount: readInteger(root.currentReferencedCount, 'currentReferencedCount'),
    historicalOnlyCount: readInteger(root.historicalOnlyCount, 'historicalOnlyCount'),
    deletedOwnerCount: readInteger(root.deletedOwnerCount, 'deletedOwnerCount'),
    readyCount: readInteger(root.readyCount, 'readyCount'),
    deletePendingCount: readInteger(root.deletePendingCount, 'deletePendingCount'),
    recentWindowDays: readInteger(root.recentWindowDays, 'recentWindowDays', 1),
    recentUploadCount: readInteger(root.recentUploadCount, 'recentUploadCount'),
    largestAttachments: root.largestAttachments.map((item, index) =>
      decodeStatisticItem(item, `largestAttachments[${index}]`),
    ),
    recentUploads: root.recentUploads.map((item, index) =>
      decodeStatisticItem(item, `recentUploads[${index}]`),
    ),
  }
}

export function decodeAdministratorAttachmentIntegrity(
  value: unknown,
): AdministratorAttachmentIntegrity {
  const root = readObject(value, 'administratorAttachmentIntegrity')
  return {
    attachmentId: readInteger(root.attachmentId, 'attachmentId', 1),
    status: readEnum(root.status, 'status', administratorAttachmentStorageHealthValues),
    sizeBytes: readInteger(root.sizeBytes, 'sizeBytes', 1),
    actualSizeBytes: readNullableInteger(root.actualSizeBytes, 'actualSizeBytes'),
    sha256: readSha256(root.sha256, 'sha256'),
    actualSha256: readNullableSha256(root.actualSha256, 'actualSha256'),
    checkedAt: readString(root.checkedAt, 'checkedAt'),
  }
}
