import { isKnowledgeStatus, type KnowledgeStatus } from '../../../api/contracts/knowledge'
import type { ActorContext } from '../../../app/stores/actor'

export const rewriteStatuses = ['Keep', 'Change', 'Remove', 'Unknown'] as const
export type RewriteStatus = (typeof rewriteStatuses)[number]

export const rewriteStatusLabels: Readonly<Record<RewriteStatus, string>> = {
  Keep: '保留',
  Change: '调整',
  Remove: '移除',
  Unknown: '待确认',
}

export const functionTypeLabels: Readonly<Record<string, string>> = {
  Query: '页面查询',
  ServiceQuery: '服务查询',
  BusinessOperation: '业务操作',
  IntegrationTask: '集成任务',
  Batch: '批处理',
}

export type BusinessFunctionsSort =
  | 'name:asc'
  | 'name:desc'
  | 'updatedAt:asc'
  | 'updatedAt:desc'
  | 'knowledgeStatus:asc'
  | 'knowledgeStatus:desc'

export interface BusinessFunctionsListParameters {
  readonly systemId?: number
  readonly search?: string
  readonly functionType?: string
  readonly rewriteStatus?: RewriteStatus
  readonly knowledgeStatus?: KnowledgeStatus
  readonly hasUnknownItems?: boolean
  readonly sort: BusinessFunctionsSort
  readonly page: number
  readonly pageSize: number
}

export interface SystemReference {
  readonly id: number
  readonly name: string
}

export interface BusinessFunctionSummary {
  readonly id: number
  readonly name: string
  readonly system: SystemReference
  readonly functionType: string
  readonly purpose: string | null
  readonly relatedDataCount: number
  readonly ruleCount: number
  readonly unknownCount: number
  readonly rewriteStatus: RewriteStatus
  readonly knowledgeStatus: KnowledgeStatus
  readonly updatedAt: string
}

export interface BusinessFunctionsListResponse {
  readonly items: readonly BusinessFunctionSummary[]
  readonly page: number
  readonly pageSize: number
  readonly total: number
}

export interface BusinessFunctionDetailResponse {
  readonly id: number
  readonly system: SystemReference
  readonly concurrencyToken: string
  readonly header: {
    readonly name: string
    readonly functionType: string
    readonly rewriteStatus: RewriteStatus
    readonly knowledgeStatus: KnowledgeStatus
  }
  readonly overview: {
    readonly purpose: string | null
    readonly caller: string | null
    readonly input: string | null
    readonly output: string | null
  }
  readonly businessProcess: readonly {
    readonly order: number
    readonly name: string
    readonly description: string | null
  }[]
  readonly relatedData: readonly {
    readonly relationshipId: number
    readonly target: { readonly type: string; readonly id: number }
    readonly name: string
    readonly relationType: string
    readonly evidenceCount: number
  }[]
  readonly businessRules: readonly {
    readonly relationshipId: number
    readonly id: number
    readonly name: string
    readonly knowledgeStatus: KnowledgeStatus
    readonly evidenceCount: number
  }[]
  readonly integrations: readonly {
    readonly relationshipId: number
    readonly id: number
    readonly name: string
    readonly relationType: string
  }[]
  readonly evidence: readonly {
    readonly id: number
    readonly evidenceType: string
    readonly sourceTitle: string
  }[]
  readonly unknownItems: readonly {
    readonly id: number
    readonly question: string
    readonly status: 'Open' | 'Investigating' | 'ConclusionConfirmed' | 'Closed'
  }[]
  readonly contextRail: {
    readonly callers: readonly string[]
    readonly adjacentFunctions: readonly string[]
    readonly integrationCount: number
    readonly openUnknownCount: number
  }
  readonly canDelete: boolean
  readonly availableActions: readonly string[]
}

export interface CreateBusinessFunctionRequest {
  readonly systemId: number
  readonly name: string
  readonly displayName?: string | null
  readonly functionType: string
  readonly purpose?: string | null
  readonly rewriteStatus: RewriteStatus
  readonly actor: ActorContext
}

export interface CreateBusinessFunctionResponse {
  readonly id: number
  readonly system: SystemReference
  readonly name: string
  readonly rewriteStatus: RewriteStatus
  readonly knowledgeStatus: KnowledgeStatus
  readonly concurrencyToken: string
}

export interface UpdateBusinessFunctionOverviewRequest {
  readonly name: string
  readonly displayName: string | null
  readonly functionType: string
  readonly purpose: string | null
  readonly caller: string | null
  readonly input: string | null
  readonly output: string | null
  readonly rewriteStatus: RewriteStatus
  readonly actor: ActorContext
  readonly concurrencyToken: string
}

export interface UpdateBusinessFunctionOverviewResponse {
  readonly overview: {
    readonly name: string
    readonly displayName: string | null
    readonly functionType: string
    readonly purpose: string | null
    readonly caller: string | null
    readonly input: string | null
    readonly output: string | null
    readonly rewriteStatus: RewriteStatus
  }
  readonly concurrencyToken: string
}

export interface BusinessProcessStepInput {
  readonly order: number
  readonly name: string
  readonly description: string | null
}

export interface ReplaceBusinessProcessStepsRequest {
  readonly steps: readonly BusinessProcessStepInput[]
  readonly actor: ActorContext
  readonly concurrencyToken: string
}

export interface ReplaceBusinessProcessStepsResponse {
  readonly steps: readonly BusinessProcessStepInput[]
  readonly concurrencyToken: string
}

type JsonObject = Readonly<Record<string, unknown>>

function isJsonObject(value: unknown): value is JsonObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function readObject(value: unknown, field: string): JsonObject {
  if (!isJsonObject(value)) {
    throw new Error(`${field} must be an object`)
  }
  return value
}

function readArray(value: unknown, field: string): readonly unknown[] {
  if (!Array.isArray(value)) throw new Error(`${field} must be an array`)
  return value
}

function readString(value: unknown, field: string): string {
  if (typeof value !== 'string') throw new Error(`${field} must be a string`)
  return value
}

function readBoolean(value: unknown, field: string): boolean {
  if (typeof value !== 'boolean') throw new TypeError(`${field} 必须是布尔值。`)
  return value
}

function readNullableString(value: unknown, field: string): string | null {
  return value === null ? null : readString(value, field)
}

function readInteger(value: unknown, field: string, minimum = 0): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < minimum) {
    throw new Error(`${field} must be a safe integer`)
  }
  return value
}

function readKnowledgeStatus(value: unknown, field: string): KnowledgeStatus {
  if (isKnowledgeStatus(value)) return value
  throw new Error(`${field} has an unsupported knowledge status`)
}

function readRewriteStatus(value: unknown, field: string): RewriteStatus {
  const status = readString(value, field)
  if (status === 'Keep' || status === 'Change' || status === 'Remove' || status === 'Unknown') return status
  throw new Error(`${field} has an unsupported rewrite status`)
}

function readSystemReference(value: unknown, field: string): SystemReference {
  const system = readObject(value, field)
  return {
    id: readInteger(system.id, `${field}.id`, 1),
    name: readString(system.name, `${field}.name`),
  }
}

function readUnknownItemStatus(value: unknown, field: string): 'Open' | 'Investigating' | 'ConclusionConfirmed' | 'Closed' {
  const status = readString(value, field)
  if (status === 'Open' || status === 'Investigating' || status === 'ConclusionConfirmed' || status === 'Closed') return status
  throw new Error(`${field} has an unsupported status`)
}

export function decodeBusinessFunctionsList(value: unknown): BusinessFunctionsListResponse {
  const root = readObject(value, 'businessFunctionsList')
  return {
    items: readArray(root.items, 'items').map((value, index) => {
      const item = readObject(value, `items[${index}]`)
      return {
        id: readInteger(item.id, `items[${index}].id`, 1),
        name: readString(item.name, `items[${index}].name`),
        system: readSystemReference(item.system, `items[${index}].system`),
        functionType: readString(item.functionType, `items[${index}].functionType`),
        purpose: readNullableString(item.purpose, `items[${index}].purpose`),
        relatedDataCount: readInteger(item.relatedDataCount, `items[${index}].relatedDataCount`),
        ruleCount: readInteger(item.ruleCount, `items[${index}].ruleCount`),
        unknownCount: readInteger(item.unknownCount, `items[${index}].unknownCount`),
        rewriteStatus: readRewriteStatus(item.rewriteStatus, `items[${index}].rewriteStatus`),
        knowledgeStatus: readKnowledgeStatus(item.knowledgeStatus, `items[${index}].knowledgeStatus`),
        updatedAt: readString(item.updatedAt, `items[${index}].updatedAt`),
      }
    }),
    page: readInteger(root.page, 'page', 1),
    pageSize: readInteger(root.pageSize, 'pageSize', 1),
    total: readInteger(root.total, 'total'),
  }
}

export function decodeBusinessFunctionDetail(value: unknown): BusinessFunctionDetailResponse {
  const root = readObject(value, 'businessFunctionDetail')
  const header = readObject(root.header, 'header')
  const overview = readObject(root.overview, 'overview')
  const contextRail = readObject(root.contextRail, 'contextRail')

  return {
    id: readInteger(root.id, 'id', 1),
    system: readSystemReference(root.system, 'system'),
    concurrencyToken: readString(root.concurrencyToken, 'concurrencyToken'),
    header: {
      name: readString(header.name, 'header.name'),
      functionType: readString(header.functionType, 'header.functionType'),
      rewriteStatus: readRewriteStatus(header.rewriteStatus, 'header.rewriteStatus'),
      knowledgeStatus: readKnowledgeStatus(header.knowledgeStatus, 'header.knowledgeStatus'),
    },
    overview: {
      purpose: readNullableString(overview.purpose, 'overview.purpose'),
      caller: readNullableString(overview.caller, 'overview.caller'),
      input: readNullableString(overview.input, 'overview.input'),
      output: readNullableString(overview.output, 'overview.output'),
    },
    businessProcess: readArray(root.businessProcess, 'businessProcess').map((value, index) => {
      const step = readObject(value, `businessProcess[${index}]`)
      return {
        order: readInteger(step.order, `businessProcess[${index}].order`, 1),
        name: readString(step.name, `businessProcess[${index}].name`),
        description: readNullableString(step.description, `businessProcess[${index}].description`),
      }
    }),
    relatedData: readArray(root.relatedData, 'relatedData').map((value, index) => {
      const item = readObject(value, `relatedData[${index}]`)
      const target = readObject(item.target, `relatedData[${index}].target`)
      return {
        relationshipId: readInteger(item.relationshipId, `relatedData[${index}].relationshipId`, 1),
        target: {
          type: readString(target.type, `relatedData[${index}].target.type`),
          id: readInteger(target.id, `relatedData[${index}].target.id`, 1),
        },
        name: readString(item.name, `relatedData[${index}].name`),
        relationType: readString(item.relationType, `relatedData[${index}].relationType`),
        evidenceCount: readInteger(item.evidenceCount, `relatedData[${index}].evidenceCount`),
      }
    }),
    businessRules: readArray(root.businessRules, 'businessRules').map((value, index) => {
      const item = readObject(value, `businessRules[${index}]`)
      return {
        relationshipId: readInteger(item.relationshipId, `businessRules[${index}].relationshipId`, 1),
        id: readInteger(item.id, `businessRules[${index}].id`, 1),
        name: readString(item.name, `businessRules[${index}].name`),
        knowledgeStatus: readKnowledgeStatus(item.knowledgeStatus, `businessRules[${index}].knowledgeStatus`),
        evidenceCount: readInteger(item.evidenceCount, `businessRules[${index}].evidenceCount`),
      }
    }),
    integrations: readArray(root.integrations, 'integrations').map((value, index) => {
      const item = readObject(value, `integrations[${index}]`)
      return {
        relationshipId: readInteger(item.relationshipId, `integrations[${index}].relationshipId`, 1),
        id: readInteger(item.id, `integrations[${index}].id`, 1),
        name: readString(item.name, `integrations[${index}].name`),
        relationType: readString(item.relationType, `integrations[${index}].relationType`),
      }
    }),
    evidence: readArray(root.evidence, 'evidence').map((value, index) => {
      const item = readObject(value, `evidence[${index}]`)
      return {
        id: readInteger(item.id, `evidence[${index}].id`, 1),
        evidenceType: readString(item.evidenceType, `evidence[${index}].evidenceType`),
        sourceTitle: readString(item.sourceTitle, `evidence[${index}].sourceTitle`),
      }
    }),
    unknownItems: readArray(root.unknownItems, 'unknownItems').map((value, index) => {
      const item = readObject(value, `unknownItems[${index}]`)
      return {
        id: readInteger(item.id, `unknownItems[${index}].id`, 1),
        question: readString(item.question, `unknownItems[${index}].question`),
        status: readUnknownItemStatus(item.status, `unknownItems[${index}].status`),
      }
    }),
    contextRail: {
      callers: readArray(contextRail.callers, 'contextRail.callers').map((value, index) => readString(value, `contextRail.callers[${index}]`)),
      adjacentFunctions: readArray(contextRail.adjacentFunctions, 'contextRail.adjacentFunctions').map((value, index) => readString(value, `contextRail.adjacentFunctions[${index}]`)),
      integrationCount: readInteger(contextRail.integrationCount, 'contextRail.integrationCount'),
      openUnknownCount: readInteger(contextRail.openUnknownCount, 'contextRail.openUnknownCount'),
    },
    canDelete: readBoolean(root.canDelete, 'canDelete'),
    availableActions: readArray(root.availableActions, 'availableActions').map((value, index) => readString(value, `availableActions[${index}]`)),
  }
}

export function decodeCreateBusinessFunction(value: unknown): CreateBusinessFunctionResponse {
  const root = readObject(value, 'createdBusinessFunction')
  return {
    id: readInteger(root.id, 'id', 1),
    system: readSystemReference(root.system, 'system'),
    name: readString(root.name, 'name'),
    rewriteStatus: readRewriteStatus(root.rewriteStatus, 'rewriteStatus'),
    knowledgeStatus: readKnowledgeStatus(root.knowledgeStatus, 'knowledgeStatus'),
    concurrencyToken: readString(root.concurrencyToken, 'concurrencyToken'),
  }
}

export function decodeUpdateBusinessFunctionOverview(
  value: unknown,
): UpdateBusinessFunctionOverviewResponse {
  const root = readObject(value, 'updatedBusinessFunctionOverview')
  const overview = readObject(root.overview, 'overview')
  return {
    overview: {
      name: readString(overview.name, 'overview.name'),
      displayName: readNullableString(overview.displayName, 'overview.displayName'),
      functionType: readString(overview.functionType, 'overview.functionType'),
      purpose: readNullableString(overview.purpose, 'overview.purpose'),
      caller: readNullableString(overview.caller, 'overview.caller'),
      input: readNullableString(overview.input, 'overview.input'),
      output: readNullableString(overview.output, 'overview.output'),
      rewriteStatus: readRewriteStatus(overview.rewriteStatus, 'overview.rewriteStatus'),
    },
    concurrencyToken: readString(root.concurrencyToken, 'concurrencyToken'),
  }
}

function readBusinessProcessSteps(value: unknown, field: string): BusinessProcessStepInput[] {
  return readArray(value, field).map((item, index) => {
    const step = readObject(item, `${field}[${index}]`)
    return {
      order: readInteger(step.order, `${field}[${index}].order`, 1),
      name: readString(step.name, `${field}[${index}].name`),
      description: readNullableString(step.description, `${field}[${index}].description`),
    }
  })
}

export function decodeReplaceBusinessProcessSteps(
  value: unknown,
): ReplaceBusinessProcessStepsResponse {
  const root = readObject(value, 'replacedBusinessProcessSteps')
  return {
    steps: readBusinessProcessSteps(root.steps, 'steps'),
    concurrencyToken: readString(root.concurrencyToken, 'concurrencyToken'),
  }
}
