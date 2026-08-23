import { isKnowledgeStatus, type KnowledgeStatus } from '../../../api/contracts/knowledge'

export interface KnowledgeStatusTarget {
  readonly type: 'System' | 'BusinessFunction' | 'DatabaseObject' | 'DatabaseColumn' | 'BusinessRule' | 'Integration' | 'KnowledgeDocument'
  readonly id: number
}

export interface ChangeKnowledgeStatusRequest {
  readonly target: KnowledgeStatusTarget
  readonly targetStatus: KnowledgeStatus
  readonly reason: string | null
  readonly concurrencyToken: string
}

export interface ChangeKnowledgeStatusResponse {
  readonly target: KnowledgeStatusTarget
  readonly previousStatus: KnowledgeStatus
  readonly knowledgeStatus: KnowledgeStatus
  readonly reason: string | null
  readonly changedAt: string
  readonly concurrencyToken: string
}

export interface KnowledgeStatusDialogPayload {
  readonly target: KnowledgeStatusTarget
  readonly title: string
  readonly knowledgeStatus: KnowledgeStatus
  readonly concurrencyToken: string
  readonly evidenceCount: number
  readonly humanConfirmationCount: number
}

type JsonObject = Readonly<Record<string, unknown>>
const targetTypes: readonly KnowledgeStatusTarget['type'][] = [
  'System', 'BusinessFunction', 'DatabaseObject', 'DatabaseColumn', 'BusinessRule', 'Integration', 'KnowledgeDocument',
]

function isObject(value: unknown): value is JsonObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function readStatus(value: unknown, field: string): KnowledgeStatus {
  if (!isKnowledgeStatus(value)) throw new Error(`${field} has an unsupported status`)
  return value
}

function readTarget(value: unknown): KnowledgeStatusTarget {
  if (!isObject(value)
    || typeof value.type !== 'string'
    || !targetTypes.some((type) => type === value.type)
    || typeof value.id !== 'number'
    || !Number.isSafeInteger(value.id)
    || value.id < 1) {
    throw new Error('target is invalid')
  }
  return { type: value.type as KnowledgeStatusTarget['type'], id: value.id }
}

export function isKnowledgeStatusDialogPayload(value: unknown): value is KnowledgeStatusDialogPayload {
  if (!isObject(value) || !isObject(value.target)) return false
  const target = value.target
  return typeof target.type === 'string'
    && targetTypes.some((type) => type === target.type)
    && typeof target.id === 'number'
    && Number.isSafeInteger(target.id)
    && target.id > 0
    && typeof value.title === 'string'
    && isKnowledgeStatus(value.knowledgeStatus)
    && typeof value.concurrencyToken === 'string'
    && typeof value.evidenceCount === 'number'
    && typeof value.humanConfirmationCount === 'number'
}

export function decodeKnowledgeStatusChange(value: unknown): ChangeKnowledgeStatusResponse {
  if (!isObject(value)) throw new Error('knowledgeStatusChange must be an object')
  if (typeof value.changedAt !== 'string' || typeof value.concurrencyToken !== 'string') throw new Error('response metadata is invalid')
  return {
    target: readTarget(value.target),
    previousStatus: readStatus(value.previousStatus, 'previousStatus'),
    knowledgeStatus: readStatus(value.knowledgeStatus, 'knowledgeStatus'),
    reason: value.reason === null ? null : String(value.reason),
    changedAt: value.changedAt,
    concurrencyToken: value.concurrencyToken,
  }
}
