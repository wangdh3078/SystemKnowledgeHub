import { isKnowledgeStatus, type KnowledgeStatus } from '../../../api/contracts/knowledge'

export interface SystemKnowledgeView {
  readonly systemId: number
  readonly overview: {
    readonly businessFunctionCount: number
    readonly databaseObjectCount: number
    readonly businessRuleCount: number
    readonly integrationCount: number
    readonly documentCount: number
    readonly evidenceCount: number
    readonly openUnknownItemCount: number
  }
  readonly businessFunctions: readonly KnowledgeItem[]
  readonly databaseObjects: readonly KnowledgeItem[]
  readonly businessRules: readonly KnowledgeItem[]
  readonly integrations: readonly KnowledgeIntegration[]
  readonly documents: readonly KnowledgeDocumentItem[]
  readonly relationships: readonly KnowledgeRelationship[]
  readonly evidence: readonly KnowledgeEvidence[]
  readonly unknownItems: readonly KnowledgeUnknownItem[]
}

export interface KnowledgeItem { readonly id: number; readonly title: string; readonly description: string | null; readonly knowledgeStatus: KnowledgeStatus }
export interface KnowledgeIntegration { readonly id: number; readonly name: string; readonly integrationType: string; readonly direction: string; readonly relatedParty: string; readonly knowledgeStatus: KnowledgeStatus }
export interface KnowledgeDocumentItem { readonly id: number; readonly documentType: string; readonly title: string; readonly lifecycleStatus: string; readonly knowledgeStatus: KnowledgeStatus; readonly updatedAt: string; readonly relationTypes: readonly string[] }
export interface KnowledgeRelationship { readonly id: number; readonly direction: string; readonly relationType: string; readonly relatedType: string; readonly relatedId: number; readonly knowledgeStatus: KnowledgeStatus }
export interface KnowledgeEvidence { readonly id: number; readonly evidenceType: string; readonly sourceTitle: string; readonly summary: string | null; readonly providedAt: string }
export interface KnowledgeUnknownItem { readonly id: number; readonly itemCode: string; readonly question: string; readonly priority: string; readonly status: string; readonly updatedAt: string }

type JsonObject = Readonly<Record<string, unknown>>
function object(value: unknown, name: string): JsonObject { if (typeof value !== 'object' || value === null || Array.isArray(value)) throw new Error(`${name} must be an object`); return value as JsonObject }
function text(value: unknown, name: string): string { if (typeof value !== 'string') throw new Error(`${name} must be a string`); return value }
function nullableText(value: unknown, name: string): string | null { return value === null ? null : text(value, name) }
function id(value: unknown, name: string): number { if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < 1) throw new Error(`${name} must be a safe positive integer`); return value }
function count(value: unknown, name: string): number { if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < 0) throw new Error(`${name} must be a non-negative integer`); return value }
function status(value: unknown, name: string): KnowledgeStatus { if (!isKnowledgeStatus(value)) throw new Error(`${name} has an unsupported status`); return value }
function array(value: unknown, name: string): unknown[] { if (!Array.isArray(value)) throw new Error(`${name} must be an array`); return value }

export function decodeSystemKnowledgeView(value: unknown): SystemKnowledgeView {
  const root = object(value, 'systemKnowledgeView')
  const overview = object(root.overview, 'overview')
  const readItem = (value: unknown, name: string): KnowledgeItem => { const item = object(value, name); return { id: id(item.id, `${name}.id`), title: text(item.title, `${name}.title`), description: nullableText(item.description, `${name}.description`), knowledgeStatus: status(item.knowledgeStatus, `${name}.knowledgeStatus`) } }
  return {
    systemId: id(root.systemId, 'systemId'),
    overview: {
      businessFunctionCount: count(overview.businessFunctionCount, 'overview.businessFunctionCount'), databaseObjectCount: count(overview.databaseObjectCount, 'overview.databaseObjectCount'), businessRuleCount: count(overview.businessRuleCount, 'overview.businessRuleCount'), integrationCount: count(overview.integrationCount, 'overview.integrationCount'), documentCount: count(overview.documentCount, 'overview.documentCount'), evidenceCount: count(overview.evidenceCount, 'overview.evidenceCount'), openUnknownItemCount: count(overview.openUnknownItemCount, 'overview.openUnknownItemCount'),
    },
    businessFunctions: array(root.businessFunctions, 'businessFunctions').map((item, index) => readItem(item, `businessFunctions[${index}]`)),
    databaseObjects: array(root.databaseObjects, 'databaseObjects').map((item, index) => readItem(item, `databaseObjects[${index}]`)),
    businessRules: array(root.businessRules, 'businessRules').map((item, index) => readItem(item, `businessRules[${index}]`)),
    integrations: array(root.integrations, 'integrations').map((value, index) => { const item = object(value, `integrations[${index}]`); return { id: id(item.id, `integrations[${index}].id`), name: text(item.name, `integrations[${index}].name`), integrationType: text(item.integrationType, `integrations[${index}].integrationType`), direction: text(item.direction, `integrations[${index}].direction`), relatedParty: text(item.relatedParty, `integrations[${index}].relatedParty`), knowledgeStatus: status(item.knowledgeStatus, `integrations[${index}].knowledgeStatus`) } }),
    documents: array(root.documents, 'documents').map((value, index) => { const item = object(value, `documents[${index}]`); return { id: id(item.id, `documents[${index}].id`), documentType: text(item.documentType, `documents[${index}].documentType`), title: text(item.title, `documents[${index}].title`), lifecycleStatus: text(item.lifecycleStatus, `documents[${index}].lifecycleStatus`), knowledgeStatus: status(item.knowledgeStatus, `documents[${index}].knowledgeStatus`), updatedAt: text(item.updatedAt, `documents[${index}].updatedAt`), relationTypes: array(item.relationTypes, `documents[${index}].relationTypes`).map((entry, entryIndex) => text(entry, `documents[${index}].relationTypes[${entryIndex}]`)) } }),
    relationships: array(root.relationships, 'relationships').map((value, index) => { const item = object(value, `relationships[${index}]`); return { id: id(item.id, `relationships[${index}].id`), direction: text(item.direction, `relationships[${index}].direction`), relationType: text(item.relationType, `relationships[${index}].relationType`), relatedType: text(item.relatedType, `relationships[${index}].relatedType`), relatedId: id(item.relatedId, `relationships[${index}].relatedId`), knowledgeStatus: status(item.knowledgeStatus, `relationships[${index}].knowledgeStatus`) } }),
    evidence: array(root.evidence, 'evidence').map((value, index) => { const item = object(value, `evidence[${index}]`); return { id: id(item.id, `evidence[${index}].id`), evidenceType: text(item.evidenceType, `evidence[${index}].evidenceType`), sourceTitle: text(item.sourceTitle, `evidence[${index}].sourceTitle`), summary: nullableText(item.summary, `evidence[${index}].summary`), providedAt: text(item.providedAt, `evidence[${index}].providedAt`) } }),
    unknownItems: array(root.unknownItems, 'unknownItems').map((value, index) => { const item = object(value, `unknownItems[${index}]`); return { id: id(item.id, `unknownItems[${index}].id`), itemCode: text(item.itemCode, `unknownItems[${index}].itemCode`), question: text(item.question, `unknownItems[${index}].question`), priority: text(item.priority, `unknownItems[${index}].priority`), status: text(item.status, `unknownItems[${index}].status`), updatedAt: text(item.updatedAt, `unknownItems[${index}].updatedAt`) } }),
  }
}
