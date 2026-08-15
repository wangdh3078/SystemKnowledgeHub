import { isKnowledgeStatus, type KnowledgeStatus } from '../../../api/contracts/knowledge'
import type { ActorContext } from '../../../app/stores/actor'

export const evidenceTypes = [
  'CodeReference',
  'Sql',
  'DatabaseSample',
  'DatabaseComment',
  'Api',
  'MqMessage',
  'ExistingDocument',
  'HumanConfirmation',
] as const
export type EvidenceType = (typeof evidenceTypes)[number]
export type OrdinaryEvidenceType = Exclude<EvidenceType, 'HumanConfirmation'>
export type EvidenceConfidence = 'High' | 'Medium' | 'Low'

export const evidenceTypeLabels: Readonly<Record<EvidenceType, string>> = {
  CodeReference: '代码引用',
  Sql: 'SQL',
  DatabaseSample: '数据库样本',
  DatabaseComment: '数据库注释',
  Api: 'API',
  MqMessage: 'MQ 消息',
  ExistingDocument: '现有文档',
  HumanConfirmation: '人工确认',
}

export const confidenceLabels: Readonly<Record<EvidenceConfidence, string>> = {
  High: '高',
  Medium: '中',
  Low: '低',
}

export interface EvidenceTarget {
  readonly type: string
  readonly id: number
}

export interface EvidenceSubjectPayload {
  readonly subject: EvidenceTarget
  readonly title: string
  readonly knowledgeStatus: KnowledgeStatus
  readonly subjectDetailKey?: string | null
}

export interface PersonSnapshotInput {
  readonly displayName: string
  readonly roleOrIdentity: string
  readonly occurredAt: string
  readonly team: string | null
  readonly externalUserKey: string | null
  readonly source: string | null
  readonly note: string | null
}

export interface EvidenceDetailResponse {
  readonly id: number
  readonly concurrencyToken: string
  readonly evidenceType: EvidenceType
  readonly subject: EvidenceTarget
  readonly subjectDetailKey: string | null
  readonly sourceTitle: string
  readonly sourceReference: string | null
  readonly sourceLocator: Readonly<Record<string, unknown>> | null
  readonly summary: string | null
  readonly supportReason: string
  readonly confidence: EvidenceConfidence | null
  readonly provider: PersonSnapshotInput
  readonly subjectContext: {
    readonly title: string
    readonly knowledgeStatus: KnowledgeStatus
  }
  readonly availableActions: readonly string[]
}

export interface AddEvidenceRequest {
  readonly evidenceType: OrdinaryEvidenceType
  readonly subject: EvidenceTarget
  readonly subjectDetailKey: string | null
  readonly sourceTitle: string
  readonly sourceReference: string | null
  readonly sourceLocator: Readonly<Record<string, unknown>> | null
  readonly summary: string | null
  readonly supportReason: string
  readonly confidence: EvidenceConfidence | null
  readonly provider: PersonSnapshotInput
}

export interface UpdateEvidenceRequest {
  readonly sourceTitle: string
  readonly sourceReference: string | null
  readonly sourceLocator: Readonly<Record<string, unknown>> | null
  readonly summary: string | null
  readonly supportReason: string
  readonly confidence: EvidenceConfidence | null
  readonly provider: PersonSnapshotInput
  readonly actor: ActorContext
  readonly concurrencyToken: string
}

export interface AddHumanConfirmationRequest {
  readonly subject: EvidenceTarget
  readonly subjectDetailKey: string | null
  readonly confirmationStatement: string
  readonly supportReason: string
  readonly sourceNote: string | null
  readonly confirmer: PersonSnapshotInput
}

export interface AddEvidenceResponse {
  readonly id: number
  readonly evidenceType: EvidenceType
  readonly subject: EvidenceTarget
  readonly subjectDetailKey: string | null
  readonly sourceTitle: string
  readonly subjectKnowledgeStatus: KnowledgeStatus
  readonly knowledgeStatusChanged: false
  readonly concurrencyToken: string
}

type JsonObject = Readonly<Record<string, unknown>>

function isObject(value: unknown): value is JsonObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function readObject(value: unknown, field: string): JsonObject {
  if (!isObject(value)) throw new Error(`${field} must be an object`)
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
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < 1) {
    throw new Error(`${field} must be a safe positive integer`)
  }
  return value
}

function readTarget(value: unknown, field: string): EvidenceTarget {
  const target = readObject(value, field)
  return { type: readString(target.type, `${field}.type`), id: readId(target.id, `${field}.id`) }
}

function readStatus(value: unknown, field: string): KnowledgeStatus {
  if (!isKnowledgeStatus(value)) throw new Error(`${field} has an unsupported status`)
  return value
}

function readEvidenceType(value: unknown, field: string): EvidenceType {
  const type = readString(value, field)
  if (evidenceTypes.some((item) => item === type)) return type as EvidenceType
  throw new Error(`${field} has an unsupported evidence type`)
}

function readConfidence(value: unknown, field: string): EvidenceConfidence | null {
  if (value === null) return null
  const confidence = readString(value, field)
  if (confidence === 'High' || confidence === 'Medium' || confidence === 'Low') return confidence
  throw new Error(`${field} has an unsupported confidence`)
}

function readPerson(value: unknown, field: string): PersonSnapshotInput {
  const person = readObject(value, field)
  return {
    displayName: readString(person.displayName, `${field}.displayName`),
    roleOrIdentity: readString(person.roleOrIdentity, `${field}.roleOrIdentity`),
    occurredAt: readString(person.occurredAt, `${field}.occurredAt`),
    team: readNullableString(person.team, `${field}.team`),
    externalUserKey: readNullableString(person.externalUserKey, `${field}.externalUserKey`),
    source: readNullableString(person.source, `${field}.source`),
    note: readNullableString(person.note, `${field}.note`),
  }
}

export function isEvidenceSubjectPayload(value: unknown): value is EvidenceSubjectPayload {
  if (!isObject(value) || !isObject(value.subject)) return false
  return typeof value.title === 'string'
    && isKnowledgeStatus(value.knowledgeStatus)
    && typeof value.subject.type === 'string'
    && typeof value.subject.id === 'number'
    && Number.isSafeInteger(value.subject.id)
    && value.subject.id > 0
}

export function decodeEvidenceDetail(value: unknown): EvidenceDetailResponse {
  const root = readObject(value, 'evidenceDetail')
  const context = readObject(root.subjectContext, 'subjectContext')
  const sourceLocator = root.sourceLocator === null ? null : readObject(root.sourceLocator, 'sourceLocator')
  if (!Array.isArray(root.availableActions)) throw new Error('availableActions must be an array')
  return {
    id: readId(root.id, 'id'),
    concurrencyToken: readString(root.concurrencyToken, 'concurrencyToken'),
    evidenceType: readEvidenceType(root.evidenceType, 'evidenceType'),
    subject: readTarget(root.subject, 'subject'),
    subjectDetailKey: readNullableString(root.subjectDetailKey, 'subjectDetailKey'),
    sourceTitle: readString(root.sourceTitle, 'sourceTitle'),
    sourceReference: readNullableString(root.sourceReference, 'sourceReference'),
    sourceLocator,
    summary: readNullableString(root.summary, 'summary'),
    supportReason: readString(root.supportReason, 'supportReason'),
    confidence: readConfidence(root.confidence, 'confidence'),
    provider: readPerson(root.provider, 'provider'),
    subjectContext: {
      title: readString(context.title, 'subjectContext.title'),
      knowledgeStatus: readStatus(context.knowledgeStatus, 'subjectContext.knowledgeStatus'),
    },
    availableActions: root.availableActions.map((item, index) => readString(item, `availableActions[${index}]`)),
  }
}

export function decodeAddEvidence(value: unknown): AddEvidenceResponse {
  const root = readObject(value, 'addedEvidence')
  if (root.knowledgeStatusChanged !== false) throw new Error('knowledgeStatusChanged must be false')
  return {
    id: readId(root.id, 'id'),
    evidenceType: readEvidenceType(root.evidenceType, 'evidenceType'),
    subject: readTarget(root.subject, 'subject'),
    subjectDetailKey: readNullableString(root.subjectDetailKey, 'subjectDetailKey'),
    sourceTitle: readString(root.sourceTitle, 'sourceTitle'),
    subjectKnowledgeStatus: readStatus(root.subjectKnowledgeStatus, 'subjectKnowledgeStatus'),
    knowledgeStatusChanged: false,
    concurrencyToken: readString(root.concurrencyToken, 'concurrencyToken'),
  }
}
