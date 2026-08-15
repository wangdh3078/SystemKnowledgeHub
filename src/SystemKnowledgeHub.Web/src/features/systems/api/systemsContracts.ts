import { isKnowledgeStatus, type KnowledgeStatus } from '../../../api/contracts/knowledge'
import type { ActorContext } from '../../../app/stores/actor'

export const systemLifecycles = [
  'Planned',
  'InDevelopment',
  'Running',
  'Maintaining',
  'Legacy',
  'Retired',
] as const

export type SystemLifecycle = (typeof systemLifecycles)[number]
export type SystemsSort =
  | 'name:asc'
  | 'name:desc'
  | 'updatedAt:asc'
  | 'updatedAt:desc'
  | 'knowledgeStatus:asc'
  | 'knowledgeStatus:desc'

export const systemLifecycleLabels: Readonly<Record<SystemLifecycle, string>> = {
  Planned: '规划中',
  InDevelopment: '开发中',
  Running: '运行中',
  Maintaining: '维护中',
  Legacy: '遗留',
  Retired: '已退役',
}

export interface SystemSummary {
  readonly id: number
  readonly name: string
  readonly displayName: string
  readonly systemType: string
  readonly purpose: string | null
  readonly technologies: readonly string[]
  readonly functionCount: number
  readonly databaseObjectCount: number
  readonly openUnknownCount: number
  readonly lifecycle: SystemLifecycle
  readonly knowledgeStatus: KnowledgeStatus
  readonly updatedAt: string
}

export interface SystemsListResponse {
  readonly items: readonly SystemSummary[]
  readonly page: number
  readonly pageSize: number
  readonly total: number
}

export interface SystemsListParameters {
  readonly search?: string
  readonly lifecycle?: SystemLifecycle
  readonly technology?: string
  readonly knowledgeStatus?: KnowledgeStatus
  readonly sort: SystemsSort
  readonly page: number
  readonly pageSize: number
}

export interface CreateSystemRequest {
  readonly name: string
  readonly displayName: string
  readonly systemType: string
  readonly lifecycle: SystemLifecycle
  readonly purpose?: string | null
  readonly actor: ActorContext
}

export interface CreateSystemResponse {
  readonly id: number
  readonly name: string
  readonly displayName: string
  readonly lifecycle: SystemLifecycle
  readonly knowledgeStatus: KnowledgeStatus
  readonly concurrencyToken: string
}

export interface SystemRepository {
  readonly name: string | null
  readonly url: string | null
}

export interface SystemDeployment {
  readonly environment: string
  readonly description: string
}

export interface SystemDetailOverview {
  readonly name: string
  readonly displayName: string
  readonly systemType: string
  readonly lifecycle: SystemLifecycle
  readonly purpose: string | null
  readonly mainUsers: readonly string[]
  readonly technologies: readonly string[]
  readonly repository: SystemRepository
  readonly deployment: readonly SystemDeployment[]
  readonly notes: string | null
  readonly knowledgeStatus: KnowledgeStatus
}

export interface SystemKnowledgeSummary {
  readonly confirmed: number
  readonly inferred: number
  readonly unknown: number
  readonly openUnknownItems: number
}

export interface SystemBusinessFunctionSummary {
  readonly id: number
  readonly name: string
  readonly purpose: string | null
  readonly knowledgeStatus: KnowledgeStatus
  readonly unknownCount: number
}

export interface SystemDatabaseObjectSummary {
  readonly id: number
  readonly qualifiedName: string
  readonly objectType: 'Table' | 'View'
  readonly knowledgeStatus: KnowledgeStatus
  readonly unknownCount: number
}

export interface SystemIntegrationSummary {
  readonly id: number
  readonly name: string
  readonly integrationType: string
  readonly relatedSystem: string
  readonly knowledgeStatus: KnowledgeStatus
}

export interface SystemUnknownItemSummary {
  readonly id: number
  readonly itemCode: string
  readonly question: string
  readonly priority: 'High' | 'Medium' | 'Low'
  readonly status: 'Open' | 'Investigating' | 'ConclusionConfirmed' | 'Closed'
}

export interface SystemContextRail {
  readonly relatedSystems: readonly { readonly id: number; readonly name: string }[]
  readonly integrationCount: number
  readonly mainDatabase: { readonly id: number; readonly name: string } | null
  readonly highPriorityUnknownCount: number
  readonly knowledgeGaps: readonly string[]
}

export interface SystemDetailResponse {
  readonly id: number
  readonly concurrencyToken: string
  readonly overview: SystemDetailOverview
  readonly knowledgeSummary: SystemKnowledgeSummary
  readonly businessFunctions: readonly SystemBusinessFunctionSummary[]
  readonly databaseObjects: readonly SystemDatabaseObjectSummary[]
  readonly integrations: readonly SystemIntegrationSummary[]
  readonly unknownItems: readonly SystemUnknownItemSummary[]
  readonly contextRail: SystemContextRail
  readonly availableActions: readonly string[]
}

export interface UpdateSystemOverviewRequest {
  readonly displayName: string
  readonly systemType: string
  readonly purpose: string | null
  readonly mainUsers: readonly string[]
  readonly repository: SystemRepository
  readonly deployment: readonly SystemDeployment[]
  readonly mainProjects: readonly string[]
  readonly mainEntryPoints: readonly string[]
  readonly notes: string | null
  readonly actor: ActorContext
  readonly concurrencyToken: string
}

export interface UpdateSystemOverviewResponse {
  readonly id: number
  readonly overview: {
    readonly displayName: string
    readonly purpose: string | null
    readonly notes: string | null
  }
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

function readString(value: unknown, field: string): string {
  if (typeof value !== 'string') throw new Error(`${field} must be a string`)
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

function readLifecycle(value: unknown, field: string): SystemLifecycle {
  const lifecycle = readString(value, field)
  if (
    lifecycle === 'Planned' ||
    lifecycle === 'InDevelopment' ||
    lifecycle === 'Running' ||
    lifecycle === 'Maintaining' ||
    lifecycle === 'Legacy' ||
    lifecycle === 'Retired'
  ) {
    return lifecycle
  }
  throw new Error(`${field} has an unsupported lifecycle`)
}

function readStatus(value: unknown, field: string): KnowledgeStatus {
  if (isKnowledgeStatus(value)) return value
  throw new Error(`${field} has an unsupported status`)
}

function readStringArray(value: unknown, field: string): string[] {
  if (!Array.isArray(value)) throw new Error(`${field} must be an array`)
  return value.map((item, index) => readString(item, `${field}[${index}]`))
}

function readRepository(value: unknown, field: string): SystemRepository {
  const repository = readObject(value, field)
  return {
    name: readNullableString(repository.name, `${field}.name`),
    url: readNullableString(repository.url, `${field}.url`),
  }
}

function readDeployment(value: unknown, field: string): SystemDeployment[] {
  if (!Array.isArray(value)) throw new Error(`${field} must be an array`)
  return value.map((item, index) => {
    const deployment = readObject(item, `${field}[${index}]`)
    return {
      environment: readString(deployment.environment, `${field}[${index}].environment`),
      description: readString(deployment.description, `${field}[${index}].description`),
    }
  })
}

function readDatabaseObjectType(value: unknown, field: string): 'Table' | 'View' {
  const objectType = readString(value, field)
  if (objectType === 'Table' || objectType === 'View') return objectType
  throw new Error(`${field} has an unsupported object type`)
}

function readPriority(value: unknown, field: string): 'High' | 'Medium' | 'Low' {
  const priority = readString(value, field)
  if (priority === 'High' || priority === 'Medium' || priority === 'Low') return priority
  throw new Error(`${field} has an unsupported priority`)
}

function readUnknownItemStatus(
  value: unknown,
  field: string,
): 'Open' | 'Investigating' | 'ConclusionConfirmed' | 'Closed' {
  const status = readString(value, field)
  if (
    status === 'Open' || status === 'Investigating' || status === 'ConclusionConfirmed' || status === 'Closed'
  ) return status
  throw new Error(`${field} has an unsupported unknown item status`)
}

function readSystemSummary(value: unknown, index: number): SystemSummary {
  const item = readObject(value, `items[${index}]`)
  if (!Array.isArray(item.technologies)) throw new Error(`items[${index}].technologies must be an array`)
  return {
    id: readInteger(item.id, `items[${index}].id`, 1),
    name: readString(item.name, `items[${index}].name`),
    displayName: readString(item.displayName, `items[${index}].displayName`),
    systemType: readString(item.systemType, `items[${index}].systemType`),
    purpose: readNullableString(item.purpose, `items[${index}].purpose`),
    technologies: item.technologies.map((technology, technologyIndex) =>
      readString(technology, `items[${index}].technologies[${technologyIndex}]`),
    ),
    functionCount: readInteger(item.functionCount, `items[${index}].functionCount`),
    databaseObjectCount: readInteger(item.databaseObjectCount, `items[${index}].databaseObjectCount`),
    openUnknownCount: readInteger(item.openUnknownCount, `items[${index}].openUnknownCount`),
    lifecycle: readLifecycle(item.lifecycle, `items[${index}].lifecycle`),
    knowledgeStatus: readStatus(item.knowledgeStatus, `items[${index}].knowledgeStatus`),
    updatedAt: readString(item.updatedAt, `items[${index}].updatedAt`),
  }
}

export function decodeSystemsList(value: unknown): SystemsListResponse {
  const root = readObject(value, 'systemsList')
  if (!Array.isArray(root.items)) throw new Error('items must be an array')
  return {
    items: root.items.map(readSystemSummary),
    page: readInteger(root.page, 'page', 1),
    pageSize: readInteger(root.pageSize, 'pageSize', 1),
    total: readInteger(root.total, 'total'),
  }
}

export function decodeCreateSystem(value: unknown): CreateSystemResponse {
  const root = readObject(value, 'createdSystem')
  return {
    id: readInteger(root.id, 'id', 1),
    name: readString(root.name, 'name'),
    displayName: readString(root.displayName, 'displayName'),
    lifecycle: readLifecycle(root.lifecycle, 'lifecycle'),
    knowledgeStatus: readStatus(root.knowledgeStatus, 'knowledgeStatus'),
    concurrencyToken: readString(root.concurrencyToken, 'concurrencyToken'),
  }
}

export function decodeSystemDetail(value: unknown): SystemDetailResponse {
  const root = readObject(value, 'systemDetail')
  const overview = readObject(root.overview, 'overview')
  const knowledgeSummary = readObject(root.knowledgeSummary, 'knowledgeSummary')
  const contextRail = readObject(root.contextRail, 'contextRail')
  if (!Array.isArray(root.businessFunctions)) throw new Error('businessFunctions must be an array')
  if (!Array.isArray(root.databaseObjects)) throw new Error('databaseObjects must be an array')
  if (!Array.isArray(root.integrations)) throw new Error('integrations must be an array')
  if (!Array.isArray(root.unknownItems)) throw new Error('unknownItems must be an array')
  if (!Array.isArray(contextRail.relatedSystems)) throw new Error('contextRail.relatedSystems must be an array')

  return {
    id: readInteger(root.id, 'id', 1),
    concurrencyToken: readString(root.concurrencyToken, 'concurrencyToken'),
    overview: {
      name: readString(overview.name, 'overview.name'),
      displayName: readString(overview.displayName, 'overview.displayName'),
      systemType: readString(overview.systemType, 'overview.systemType'),
      lifecycle: readLifecycle(overview.lifecycle, 'overview.lifecycle'),
      purpose: readNullableString(overview.purpose, 'overview.purpose'),
      mainUsers: readStringArray(overview.mainUsers, 'overview.mainUsers'),
      technologies: readStringArray(overview.technologies, 'overview.technologies'),
      repository: readRepository(overview.repository, 'overview.repository'),
      deployment: readDeployment(overview.deployment, 'overview.deployment'),
      notes: readNullableString(overview.notes, 'overview.notes'),
      knowledgeStatus: readStatus(overview.knowledgeStatus, 'overview.knowledgeStatus'),
    },
    knowledgeSummary: {
      confirmed: readInteger(knowledgeSummary.confirmed, 'knowledgeSummary.confirmed'),
      inferred: readInteger(knowledgeSummary.inferred, 'knowledgeSummary.inferred'),
      unknown: readInteger(knowledgeSummary.unknown, 'knowledgeSummary.unknown'),
      openUnknownItems: readInteger(knowledgeSummary.openUnknownItems, 'knowledgeSummary.openUnknownItems'),
    },
    businessFunctions: root.businessFunctions.map((item, index) => {
      const value = readObject(item, `businessFunctions[${index}]`)
      return {
        id: readInteger(value.id, `businessFunctions[${index}].id`, 1),
        name: readString(value.name, `businessFunctions[${index}].name`),
        purpose: readNullableString(value.purpose, `businessFunctions[${index}].purpose`),
        knowledgeStatus: readStatus(value.knowledgeStatus, `businessFunctions[${index}].knowledgeStatus`),
        unknownCount: readInteger(value.unknownCount, `businessFunctions[${index}].unknownCount`),
      }
    }),
    databaseObjects: root.databaseObjects.map((item, index) => {
      const value = readObject(item, `databaseObjects[${index}]`)
      return {
        id: readInteger(value.id, `databaseObjects[${index}].id`, 1),
        qualifiedName: readString(value.qualifiedName, `databaseObjects[${index}].qualifiedName`),
        objectType: readDatabaseObjectType(value.objectType, `databaseObjects[${index}].objectType`),
        knowledgeStatus: readStatus(value.knowledgeStatus, `databaseObjects[${index}].knowledgeStatus`),
        unknownCount: readInteger(value.unknownCount, `databaseObjects[${index}].unknownCount`),
      }
    }),
    integrations: root.integrations.map((item, index) => {
      const value = readObject(item, `integrations[${index}]`)
      return {
        id: readInteger(value.id, `integrations[${index}].id`, 1),
        name: readString(value.name, `integrations[${index}].name`),
        integrationType: readString(value.integrationType, `integrations[${index}].integrationType`),
        relatedSystem: readString(value.relatedSystem, `integrations[${index}].relatedSystem`),
        knowledgeStatus: readStatus(value.knowledgeStatus, `integrations[${index}].knowledgeStatus`),
      }
    }),
    unknownItems: root.unknownItems.map((item, index) => {
      const value = readObject(item, `unknownItems[${index}]`)
      return {
        id: readInteger(value.id, `unknownItems[${index}].id`, 1),
        itemCode: readString(value.itemCode, `unknownItems[${index}].itemCode`),
        question: readString(value.question, `unknownItems[${index}].question`),
        priority: readPriority(value.priority, `unknownItems[${index}].priority`),
        status: readUnknownItemStatus(value.status, `unknownItems[${index}].status`),
      }
    }),
    contextRail: {
      relatedSystems: contextRail.relatedSystems.map((item, index) => {
        const value = readObject(item, `contextRail.relatedSystems[${index}]`)
        return {
          id: readInteger(value.id, `contextRail.relatedSystems[${index}].id`, 1),
          name: readString(value.name, `contextRail.relatedSystems[${index}].name`),
        }
      }),
      integrationCount: readInteger(contextRail.integrationCount, 'contextRail.integrationCount'),
      mainDatabase: contextRail.mainDatabase === null
        ? null
        : (() => {
            const value = readObject(contextRail.mainDatabase, 'contextRail.mainDatabase')
            return {
              id: readInteger(value.id, 'contextRail.mainDatabase.id', 1),
              name: readString(value.name, 'contextRail.mainDatabase.name'),
            }
          })(),
      highPriorityUnknownCount: readInteger(
        contextRail.highPriorityUnknownCount,
        'contextRail.highPriorityUnknownCount',
      ),
      knowledgeGaps: readStringArray(contextRail.knowledgeGaps, 'contextRail.knowledgeGaps'),
    },
    availableActions: readStringArray(root.availableActions, 'availableActions'),
  }
}

export function decodeUpdateSystemOverview(value: unknown): UpdateSystemOverviewResponse {
  const root = readObject(value, 'updatedSystemOverview')
  const overview = readObject(root.overview, 'overview')
  return {
    id: readInteger(root.id, 'id', 1),
    overview: {
      displayName: readString(overview.displayName, 'overview.displayName'),
      purpose: readNullableString(overview.purpose, 'overview.purpose'),
      notes: readNullableString(overview.notes, 'overview.notes'),
    },
    concurrencyToken: readString(root.concurrencyToken, 'concurrencyToken'),
  }
}
