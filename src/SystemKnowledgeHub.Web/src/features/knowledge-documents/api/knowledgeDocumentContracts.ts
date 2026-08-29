import { isKnowledgeStatus, type KnowledgeStatus } from '../../../api/contracts/knowledge'
import { decodeAttachmentMetadataList, type AttachmentMetadata } from './attachmentContracts'

export const documentTypes = [
  'Requirement',
  'Specification',
  'TestCase',
  'Sop',
  'Troubleshooting',
  'KnowledgeArticle',
  'DesignNote',
] as const
export const documentLifecycleStatuses = ['Draft', 'Published', 'Archived'] as const
export const revisionOrigins = ['Created', 'ContentSave', 'Restore', 'MigrationBaseline'] as const

export type DocumentType = (typeof documentTypes)[number]
export type DocumentLifecycleStatus = (typeof documentLifecycleStatuses)[number]
export type RevisionOrigin = (typeof revisionOrigins)[number]
export type ConfirmationCoverageState =
  | 'NoConfirmation'
  | 'LegacyConfirmationUnknown'
  | 'CurrentRevisionConfirmed'
  | 'ChangedSinceConfirmation'

export const documentTypeLabels: Readonly<Record<DocumentType, string>> = {
  Requirement: '需求',
  Specification: '规格说明',
  TestCase: '测试用例',
  Sop: '操作规程',
  Troubleshooting: '故障排查',
  KnowledgeArticle: '知识文章',
  DesignNote: '设计说明',
}

export const lifecycleLabels: Readonly<Record<DocumentLifecycleStatus, string>> = {
  Draft: '草稿',
  Published: '已发布',
  Archived: '已归档',
}

export interface KnowledgeDocumentListItem {
  readonly id: number
  readonly documentType: DocumentType
  readonly title: string
  readonly summary: string | null
  readonly lifecycleStatus: DocumentLifecycleStatus
  readonly knowledgeStatus: KnowledgeStatus
  readonly createdByDisplayName: string
  readonly updatedByDisplayName: string
  readonly createdAt: string
  readonly updatedAt: string
}

export interface KnowledgeDocumentsListResponse {
  readonly items: readonly KnowledgeDocumentListItem[]
  readonly page: number
  readonly pageSize: number
  readonly total: number
}

export interface KnowledgeDocumentDetail extends KnowledgeDocumentListItem {
  readonly bodyMarkdown: string
  readonly createdByUserId: number
  readonly updatedByUserId: number
  readonly publishedAt: string | null
  readonly archivedAt: string | null
  readonly currentRevisionNumber: number
  readonly latestPublishedRevisionNumber: number | null
  readonly confirmationCoverage: {
    readonly state: ConfirmationCoverageState
    readonly lastConfirmedRevisionNumber: number | null
  }
  readonly concurrencyToken: string
  readonly canDelete: boolean
  readonly attachmentReferences: readonly AttachmentMetadata[]
}

export interface KnowledgeDocumentRevisionListItem {
  readonly id: number
  readonly revisionNumber: number
  readonly revisionOrigin: RevisionOrigin
  readonly lifecycleContext: DocumentLifecycleStatus
  readonly authorUserId: number | null
  readonly authorDisplayName: string | null
  readonly createdAt: string
  readonly changeSummary: string | null
  readonly restoreReason: string | null
  readonly restoredFromRevisionNumber: number | null
  readonly isCurrent: boolean
  readonly isLatestPublished: boolean
}

export interface HistoricalTargetIdentity {
  readonly id: number
  readonly targetType: string
  readonly displayName: string
  readonly isDeleted: boolean
  readonly isNavigable: boolean
}

export interface KnowledgeDocumentRevisionListResponse {
  readonly owner?: HistoricalTargetIdentity
  readonly items: readonly KnowledgeDocumentRevisionListItem[]
  readonly page: number
  readonly pageSize: number
  readonly total: number
}

export interface KnowledgeDocumentRevisionDetail extends KnowledgeDocumentRevisionListItem {
  readonly owner?: HistoricalTargetIdentity
  readonly knowledgeDocumentId: number
  readonly title: string
  readonly summary: string | null
  readonly bodyMarkdown: string
  readonly attachmentReferences: readonly AttachmentMetadata[]
}

export interface KnowledgeDocumentListParameters {
  readonly query?: string
  readonly documentType?: DocumentType
  readonly lifecycleStatus?: DocumentLifecycleStatus
  readonly knowledgeStatus?: KnowledgeStatus
  readonly page: number
  readonly pageSize: number
}

export interface CreateKnowledgeDocumentRequest {
  readonly documentType: DocumentType
  readonly title: string
  readonly summary: string | null
  readonly bodyMarkdown: string
}

export interface UpdateKnowledgeDocumentContentRequest {
  readonly title: string
  readonly summary: string | null
  readonly bodyMarkdown: string
  readonly changeSummary?: string | null
  readonly concurrencyToken: string
}

export interface RestoreKnowledgeDocumentRevisionRequest {
  readonly concurrencyToken: string
  readonly reason: string
}

type JsonObject = Readonly<Record<string, unknown>>

function isJsonObject(value: unknown): value is JsonObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}
function readObject(value: unknown, field: string): JsonObject {
  if (!isJsonObject(value)) throw new Error(`${field} must be an object`)
  return value
}
function readString(value: unknown, field: string): string {
  if (typeof value !== 'string') throw new Error(`${field} must be a string`)
  return value
}
function readNullableString(value: unknown, field: string): string | null {
  return value === null ? null : readString(value, field)
}
function readId(value: unknown, field: string): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < 1)
    throw new Error(`${field} must be a safe ID`)
  return value
}
function readNullableRevisionNumber(value: unknown, field: string): number | null {
  return value === null ? null : readId(value, field)
}
function readBoolean(value: unknown, field: string): boolean {
  if (typeof value !== 'boolean') throw new Error(`${field} must be a boolean`)
  return value
}
function readConfirmationCoverageState(value: unknown, field: string): ConfirmationCoverageState {
  const state = readString(value, field)
  if (
    state === 'NoConfirmation' ||
    state === 'LegacyConfirmationUnknown' ||
    state === 'CurrentRevisionConfirmed' ||
    state === 'ChangedSinceConfirmation'
  )
    return state
  throw new Error(`${field} has an unsupported confirmation coverage state`)
}
function readType(value: unknown, field: string): DocumentType {
  const type = readString(value, field)
  if (documentTypes.includes(type as DocumentType)) return type as DocumentType
  throw new Error(`${field} has an unsupported document type`)
}
function readLifecycle(value: unknown, field: string): DocumentLifecycleStatus {
  const status = readString(value, field)
  if (documentLifecycleStatuses.includes(status as DocumentLifecycleStatus))
    return status as DocumentLifecycleStatus
  throw new Error(`${field} has an unsupported lifecycle`)
}
function readRevisionOrigin(value: unknown, field: string): RevisionOrigin {
  const origin = readString(value, field)
  if (revisionOrigins.includes(origin as RevisionOrigin)) return origin as RevisionOrigin
  throw new Error(`${field} has an unsupported revision origin`)
}
function readStatus(value: unknown, field: string): KnowledgeStatus {
  if (isKnowledgeStatus(value)) return value
  throw new Error(`${field} has an unsupported knowledge status`)
}

function readListItem(value: unknown, index: number): KnowledgeDocumentListItem {
  const item = readObject(value, `items[${index}]`)
  return {
    id: readId(item.id, `items[${index}].id`),
    documentType: readType(item.documentType, `items[${index}].documentType`),
    title: readString(item.title, `items[${index}].title`),
    summary: readNullableString(item.summary, `items[${index}].summary`),
    lifecycleStatus: readLifecycle(item.lifecycleStatus, `items[${index}].lifecycleStatus`),
    knowledgeStatus: readStatus(item.knowledgeStatus, `items[${index}].knowledgeStatus`),
    createdByDisplayName: readString(
      item.createdByDisplayName,
      `items[${index}].createdByDisplayName`,
    ),
    updatedByDisplayName: readString(
      item.updatedByDisplayName,
      `items[${index}].updatedByDisplayName`,
    ),
    createdAt: readString(item.createdAt, `items[${index}].createdAt`),
    updatedAt: readString(item.updatedAt, `items[${index}].updatedAt`),
  }
}

export function decodeKnowledgeDocumentsList(value: unknown): KnowledgeDocumentsListResponse {
  const root = readObject(value, 'knowledgeDocumentsList')
  if (!Array.isArray(root.items)) throw new Error('items must be an array')
  return {
    items: root.items.map(readListItem),
    page: readId(root.page, 'page'),
    pageSize: readId(root.pageSize, 'pageSize'),
    total:
      typeof root.total === 'number' && Number.isSafeInteger(root.total) && root.total >= 0
        ? root.total
        : (() => {
            throw new Error('total must be a non-negative integer')
          })(),
  }
}

export function decodeKnowledgeDocumentDetail(value: unknown): KnowledgeDocumentDetail {
  const root = readObject(value, 'knowledgeDocumentDetail')
  const confirmationCoverage = readObject(root.confirmationCoverage, 'confirmationCoverage')
  return {
    id: readId(root.id, 'id'),
    documentType: readType(root.documentType, 'documentType'),
    title: readString(root.title, 'title'),
    summary: readNullableString(root.summary, 'summary'),
    bodyMarkdown: readString(root.bodyMarkdown, 'bodyMarkdown'),
    lifecycleStatus: readLifecycle(root.lifecycleStatus, 'lifecycleStatus'),
    knowledgeStatus: readStatus(root.knowledgeStatus, 'knowledgeStatus'),
    createdByUserId: readId(root.createdByUserId, 'createdByUserId'),
    createdByDisplayName: readString(root.createdByDisplayName, 'createdByDisplayName'),
    updatedByUserId: readId(root.updatedByUserId, 'updatedByUserId'),
    updatedByDisplayName: readString(root.updatedByDisplayName, 'updatedByDisplayName'),
    createdAt: readString(root.createdAt, 'createdAt'),
    updatedAt: readString(root.updatedAt, 'updatedAt'),
    publishedAt: readNullableString(root.publishedAt, 'publishedAt'),
    archivedAt: readNullableString(root.archivedAt, 'archivedAt'),
    currentRevisionNumber: readId(root.currentRevisionNumber, 'currentRevisionNumber'),
    latestPublishedRevisionNumber: readNullableRevisionNumber(
      root.latestPublishedRevisionNumber,
      'latestPublishedRevisionNumber',
    ),
    confirmationCoverage: {
      state: readConfirmationCoverageState(
        confirmationCoverage.state,
        'confirmationCoverage.state',
      ),
      lastConfirmedRevisionNumber: readNullableRevisionNumber(
        confirmationCoverage.lastConfirmedRevisionNumber,
        'confirmationCoverage.lastConfirmedRevisionNumber',
      ),
    },
    concurrencyToken: readString(root.concurrencyToken, 'concurrencyToken'),
    canDelete: readBoolean(root.canDelete, 'canDelete'),
    attachmentReferences: decodeAttachmentMetadataList(
      root.attachmentReferences,
      'attachmentReferences',
    ),
  }
}

function decodeKnowledgeDocumentRevisionListItem(
  value: unknown,
  field: string,
): KnowledgeDocumentRevisionListItem {
  const item = readObject(value, field)
  return {
    id: readId(item.id, `${field}.id`),
    revisionNumber: readId(item.revisionNumber, `${field}.revisionNumber`),
    revisionOrigin: readRevisionOrigin(item.revisionOrigin, `${field}.revisionOrigin`),
    lifecycleContext: readLifecycle(item.lifecycleContext, `${field}.lifecycleContext`),
    authorUserId: readNullableRevisionNumber(item.authorUserId, `${field}.authorUserId`),
    authorDisplayName: readNullableString(item.authorDisplayName, `${field}.authorDisplayName`),
    createdAt: readString(item.createdAt, `${field}.createdAt`),
    changeSummary: readNullableString(item.changeSummary, `${field}.changeSummary`),
    restoreReason: readNullableString(item.restoreReason, `${field}.restoreReason`),
    restoredFromRevisionNumber: readNullableRevisionNumber(
      item.restoredFromRevisionNumber,
      `${field}.restoredFromRevisionNumber`,
    ),
    isCurrent: readBoolean(item.isCurrent, `${field}.isCurrent`),
    isLatestPublished: readBoolean(item.isLatestPublished, `${field}.isLatestPublished`),
  }
}

function decodeHistoricalTargetIdentity(value: unknown, field: string): HistoricalTargetIdentity {
  const item = readObject(value, field)
  return {
    id: readId(item.id, `${field}.id`),
    targetType: readString(item.targetType, `${field}.targetType`),
    displayName: readString(item.displayName, `${field}.displayName`),
    isDeleted: readBoolean(item.isDeleted, `${field}.isDeleted`),
    isNavigable: readBoolean(item.isNavigable, `${field}.isNavigable`),
  }
}

export function decodeKnowledgeDocumentRevisionList(
  value: unknown,
): KnowledgeDocumentRevisionListResponse {
  const root = readObject(value, 'knowledgeDocumentRevisionList')
  if (!Array.isArray(root.items)) throw new Error('items must be an array')
  if (typeof root.total !== 'number' || !Number.isSafeInteger(root.total) || root.total < 0) {
    throw new Error('total must be a non-negative integer')
  }
  return {
    owner: decodeHistoricalTargetIdentity(root.owner, 'owner'),
    items: root.items.map((item, index) =>
      decodeKnowledgeDocumentRevisionListItem(item, `items[${index}]`),
    ),
    page: readId(root.page, 'page'),
    pageSize: readId(root.pageSize, 'pageSize'),
    total: root.total,
  }
}

export function decodeKnowledgeDocumentRevisionDetail(
  value: unknown,
): KnowledgeDocumentRevisionDetail {
  const root = readObject(value, 'knowledgeDocumentRevisionDetail')
  return {
    ...decodeKnowledgeDocumentRevisionListItem(root, 'revision'),
    owner: decodeHistoricalTargetIdentity(root.owner, 'owner'),
    knowledgeDocumentId: readId(root.knowledgeDocumentId, 'knowledgeDocumentId'),
    title: readString(root.title, 'title'),
    summary: readNullableString(root.summary, 'summary'),
    bodyMarkdown: readString(root.bodyMarkdown, 'bodyMarkdown'),
    attachmentReferences: decodeAttachmentMetadataList(
      root.attachmentReferences,
      'attachmentReferences',
    ),
  }
}
