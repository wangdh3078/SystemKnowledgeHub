import { isKnowledgeStatus, type KnowledgeStatus } from '../../../api/contracts/knowledge'
import { isSafeApiId } from '../../../api/contracts/id'

export const searchObjectTypes = [
  'System',
  'BusinessFunction',
  'DatabaseObject',
  'DatabaseColumn',
  'BusinessRule',
  'Integration',
  'UnknownItem',
  'KnowledgeDocument',
] as const

export type SearchObjectType = (typeof searchObjectTypes)[number]

export interface SearchKnowledgeRequest {
  readonly query: string
  readonly types?: readonly SearchObjectType[]
  readonly limitPerGroup?: number
}

export interface SearchNavigation {
  readonly routeObjectType: Exclude<SearchObjectType, 'DatabaseColumn'>
  readonly routeObjectId: number
  readonly openDrawer: 'DatabaseColumn' | null
  readonly drawerObjectId: number | null
}

export interface SearchResultItem {
  readonly id: number
  readonly systemContext: string
  readonly title: string
  readonly shortDescription: string
  readonly knowledgeStatus: KnowledgeStatus | null
  readonly unknownItemStatus: UnknownItemStatus | null
  readonly navigation: SearchNavigation
  readonly contentType: string | null
  readonly lifecycleStatus: 'Draft' | 'Published' | 'Archived' | null
  readonly updatedAt: string | null
}

export interface SearchResultGroup {
  readonly objectType: SearchObjectType
  readonly label: string
  readonly items: readonly SearchResultItem[]
}

export interface SearchKnowledgeResponse {
  readonly query: string
  readonly groups: readonly SearchResultGroup[]
  readonly total: number
}

export type UnknownItemStatus = 'Open' | 'Investigating' | 'ConclusionConfirmed' | 'Closed'

type JsonObject = Readonly<Record<string, unknown>>

function readObject(value: unknown, field: string): JsonObject {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    throw new Error(`${field} must be an object`)
  }
  return value as JsonObject
}

function readArray(value: unknown, field: string): readonly unknown[] {
  if (!Array.isArray(value)) throw new Error(`${field} must be an array`)
  return value
}

function readString(value: unknown, field: string): string {
  if (typeof value !== 'string') throw new Error(`${field} must be a string`)
  return value
}

function readId(value: unknown, field: string): number {
  if (typeof value !== 'number' || !isSafeApiId(value)) {
    throw new Error(`${field} must be a safe positive integer`)
  }
  return value
}

function readNullableId(value: unknown, field: string): number | null {
  return value === null ? null : readId(value, field)
}

function readSearchObjectType(value: unknown, field: string): SearchObjectType {
  const objectType = readString(value, field)
  if (searchObjectTypes.includes(objectType as SearchObjectType)) {
    return objectType as SearchObjectType
  }
  throw new Error(`${field} has an unsupported search object type`)
}

function readRouteObjectType(value: unknown, field: string): Exclude<SearchObjectType, 'DatabaseColumn'> {
  const objectType = readSearchObjectType(value, field)
  if (objectType !== 'DatabaseColumn') return objectType
  throw new Error(`${field} cannot be DatabaseColumn`)
}

function readNullableKnowledgeStatus(value: unknown, field: string): KnowledgeStatus | null {
  if (value === null) return null
  if (isKnowledgeStatus(value)) return value
  throw new Error(`${field} has an unsupported knowledge status`)
}

function readNullableUnknownItemStatus(value: unknown, field: string): UnknownItemStatus | null {
  if (value === null) return null
  const status = readString(value, field)
  if (status === 'Open' || status === 'Investigating' || status === 'ConclusionConfirmed' || status === 'Closed') {
    return status
  }
  throw new Error(`${field} has an unsupported unknown item status`)
}

function readNullableDocumentType(value: unknown, field: string): string | null {
  return value === null ? null : readString(value, field)
}

function readNullableLifecycleStatus(value: unknown, field: string): SearchResultItem['lifecycleStatus'] {
  if (value === null) return null
  const status = readString(value, field)
  if (status === 'Draft' || status === 'Published' || status === 'Archived') return status
  throw new Error(`${field} has an unsupported lifecycle status`)
}

function readNullableDateTime(value: unknown, field: string): string | null {
  if (value === null) return null
  const dateTime = readString(value, field)
  if (Number.isNaN(Date.parse(dateTime))) throw new Error(`${field} must be an ISO date-time`)
  return dateTime
}

function readNavigation(value: unknown, field: string): SearchNavigation {
  const navigation = readObject(value, field)
  const openDrawer = navigation.openDrawer === null ? null : readString(navigation.openDrawer, `${field}.openDrawer`)
  if (openDrawer !== null && openDrawer !== 'DatabaseColumn') {
    throw new Error(`${field}.openDrawer has an unsupported drawer`)
  }

  return {
    routeObjectType: readRouteObjectType(navigation.routeObjectType, `${field}.routeObjectType`),
    routeObjectId: readId(navigation.routeObjectId, `${field}.routeObjectId`),
    openDrawer,
    drawerObjectId: readNullableId(navigation.drawerObjectId, `${field}.drawerObjectId`),
  }
}

export function decodeSearchKnowledge(value: unknown): SearchKnowledgeResponse {
  const root = readObject(value, 'searchKnowledge')
  return {
    query: readString(root.query, 'query'),
    groups: readArray(root.groups, 'groups').map((groupValue, groupIndex) => {
      const group = readObject(groupValue, `groups[${groupIndex}]`)
      return {
        objectType: readSearchObjectType(group.objectType, `groups[${groupIndex}].objectType`),
        label: readString(group.label, `groups[${groupIndex}].label`),
        items: readArray(group.items, `groups[${groupIndex}].items`).map((itemValue, itemIndex) => {
          const item = readObject(itemValue, `groups[${groupIndex}].items[${itemIndex}]`)
          return {
            id: readId(item.id, `groups[${groupIndex}].items[${itemIndex}].id`),
            systemContext: readString(item.systemContext, `groups[${groupIndex}].items[${itemIndex}].systemContext`),
            title: readString(item.title, `groups[${groupIndex}].items[${itemIndex}].title`),
            shortDescription: readString(item.shortDescription, `groups[${groupIndex}].items[${itemIndex}].shortDescription`),
            knowledgeStatus: readNullableKnowledgeStatus(item.knowledgeStatus, `groups[${groupIndex}].items[${itemIndex}].knowledgeStatus`),
            unknownItemStatus: readNullableUnknownItemStatus(item.unknownItemStatus, `groups[${groupIndex}].items[${itemIndex}].unknownItemStatus`),
            navigation: readNavigation(item.navigation, `groups[${groupIndex}].items[${itemIndex}].navigation`),
            contentType: readNullableDocumentType(item.contentType, `groups[${groupIndex}].items[${itemIndex}].contentType`),
            lifecycleStatus: readNullableLifecycleStatus(item.lifecycleStatus, `groups[${groupIndex}].items[${itemIndex}].lifecycleStatus`),
            updatedAt: readNullableDateTime(item.updatedAt, `groups[${groupIndex}].items[${itemIndex}].updatedAt`),
          }
        }),
      }
    }),
    total: (() => {
      if (typeof root.total !== 'number' || !Number.isSafeInteger(root.total) || root.total < 0) {
        throw new Error('total must be a safe non-negative integer')
      }
      return root.total
    })(),
  }
}
