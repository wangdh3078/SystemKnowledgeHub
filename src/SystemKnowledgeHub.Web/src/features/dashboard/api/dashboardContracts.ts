import { isSafeApiId } from '../../../api/contracts/id'

export type DashboardObjectType =
  | 'System'
  | 'BusinessFunction'
  | 'DatabaseObject'
  | 'DatabaseColumn'
  | 'BusinessRule'
  | 'Integration'
  | 'UnknownItem'

export interface DashboardScope {
  readonly systemId: number | null
  readonly systemName: string | null
}

export interface DashboardKnowledgeOverview {
  readonly systems: number
  readonly businessFunctions: number
  readonly databaseObjects: number
  readonly columns: number
  readonly integrations: number
  readonly businessRules: number
  readonly unknownItems: number
}

export interface DashboardKnowledgeProgress {
  readonly confirmed: number
  readonly inferred: number
  readonly unknown: number
  readonly openUnknownItems: number
}

export interface DashboardNeedsAttention {
  readonly kind: string
  readonly count: number
  readonly label: string
}

export interface DashboardRecentActivity {
  readonly objectType: DashboardObjectType
  readonly objectId: number
  readonly title: string
  readonly updatedAt: string
}

export interface DashboardResponse {
  readonly scope: DashboardScope
  readonly knowledgeOverview: DashboardKnowledgeOverview
  readonly knowledgeProgress: DashboardKnowledgeProgress
  readonly needsAttention: readonly DashboardNeedsAttention[]
  readonly recentActivity: readonly DashboardRecentActivity[]
}

type JsonObject = Readonly<Record<string, unknown>>

function readObject(value: unknown, field: string): JsonObject {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    throw new Error(`${field} 必须是对象。`)
  }
  return value as JsonObject
}

function readArray(value: unknown, field: string): readonly unknown[] {
  if (!Array.isArray(value)) throw new Error(`${field} 必须是数组。`)
  return value
}

function readString(value: unknown, field: string): string {
  if (typeof value !== 'string') throw new Error(`${field} 必须是字符串。`)
  return value
}

function readNullableString(value: unknown, field: string): string | null {
  return value === null ? null : readString(value, field)
}

function readCount(value: unknown, field: string): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < 0) {
    throw new Error(`${field} 必须是非负安全整数。`)
  }
  return value
}

function readNullableId(value: unknown, field: string): number | null {
  if (value === null) return null
  return readId(value, field)
}

function readId(value: unknown, field: string): number {
  if (typeof value !== 'number' || !isSafeApiId(value)) {
    throw new Error(`${field} 必须是安全正整数。`)
  }
  return value
}

function readDashboardObjectType(value: unknown, field: string): DashboardObjectType {
  const objectType = readString(value, field)
  const allowed: readonly DashboardObjectType[] = [
    'System',
    'BusinessFunction',
    'DatabaseObject',
    'DatabaseColumn',
    'BusinessRule',
    'Integration',
    'UnknownItem',
  ]
  if (allowed.includes(objectType as DashboardObjectType)) return objectType as DashboardObjectType
  throw new Error(`${field} 不是支持的知识对象类型。`)
}

function readIsoTimestamp(value: unknown, field: string): string {
  const timestamp = readString(value, field)
  if (Number.isNaN(Date.parse(timestamp))) throw new Error(`${field} 必须是有效时间。`)
  return timestamp
}

function readOverview(value: unknown): DashboardKnowledgeOverview {
  const overview = readObject(value, 'knowledgeOverview')
  return {
    systems: readCount(overview.systems, 'knowledgeOverview.systems'),
    businessFunctions: readCount(overview.businessFunctions, 'knowledgeOverview.businessFunctions'),
    databaseObjects: readCount(overview.databaseObjects, 'knowledgeOverview.databaseObjects'),
    columns: readCount(overview.columns, 'knowledgeOverview.columns'),
    integrations: readCount(overview.integrations, 'knowledgeOverview.integrations'),
    businessRules: readCount(overview.businessRules, 'knowledgeOverview.businessRules'),
    unknownItems: readCount(overview.unknownItems, 'knowledgeOverview.unknownItems'),
  }
}

export function decodeDashboard(value: unknown): DashboardResponse {
  const root = readObject(value, 'dashboard')
  const scope = readObject(root.scope, 'scope')
  const progress = readObject(root.knowledgeProgress, 'knowledgeProgress')

  return {
    scope: {
      systemId: readNullableId(scope.systemId, 'scope.systemId'),
      systemName: readNullableString(scope.systemName, 'scope.systemName'),
    },
    knowledgeOverview: readOverview(root.knowledgeOverview),
    knowledgeProgress: {
      confirmed: readCount(progress.confirmed, 'knowledgeProgress.confirmed'),
      inferred: readCount(progress.inferred, 'knowledgeProgress.inferred'),
      unknown: readCount(progress.unknown, 'knowledgeProgress.unknown'),
      openUnknownItems: readCount(progress.openUnknownItems, 'knowledgeProgress.openUnknownItems'),
    },
    needsAttention: readArray(root.needsAttention, 'needsAttention').map((item, index) => {
      const attention = readObject(item, `needsAttention[${index}]`)
      return {
        kind: readString(attention.kind, `needsAttention[${index}].kind`),
        count: readCount(attention.count, `needsAttention[${index}].count`),
        label: readString(attention.label, `needsAttention[${index}].label`),
      }
    }),
    recentActivity: readArray(root.recentActivity, 'recentActivity').map((item, index) => {
      const recent = readObject(item, `recentActivity[${index}]`)
      return {
        objectType: readDashboardObjectType(recent.objectType, `recentActivity[${index}].objectType`),
        objectId: readId(recent.objectId, `recentActivity[${index}].objectId`),
        title: readString(recent.title, `recentActivity[${index}].title`),
        updatedAt: readIsoTimestamp(recent.updatedAt, `recentActivity[${index}].updatedAt`),
      }
    }),
  }
}
