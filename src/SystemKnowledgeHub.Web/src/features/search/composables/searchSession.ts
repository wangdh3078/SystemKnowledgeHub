import type { SearchResultItem } from '../api/searchContracts'

const RecentQueriesKey = 'system-knowledge-hub.recent-searches'
const RecentVisitsKey = 'system-knowledge-hub.recent-visits'
const MaximumEntries = 5

export interface RecentVisit {
  readonly title: string
  readonly systemContext: string
  readonly objectType: string
  readonly knowledgeStatus: SearchResultItem['knowledgeStatus']
  readonly unknownItemStatus: SearchResultItem['unknownItemStatus']
  readonly navigation: SearchResultItem['navigation']
}

function readJson<TValue>(key: string): TValue | null {
  try {
    const value = sessionStorage.getItem(key)
    return value === null ? null : JSON.parse(value) as TValue
  } catch {
    return null
  }
}

function writeJson(key: string, value: unknown): void {
  try {
    sessionStorage.setItem(key, JSON.stringify(value))
  } catch {
    // 会话辅助信息不可写入不影响搜索与导航。
  }
}

export function readRecentQueries(): readonly string[] {
  const values = readJson<unknown[]>(RecentQueriesKey)
  return Array.isArray(values)
    ? values.filter((value): value is string => typeof value === 'string' && value.trim().length > 0).slice(0, MaximumEntries)
    : []
}

export function rememberQuery(query: string): readonly string[] {
  const normalized = query.trim()
  if (!normalized) return readRecentQueries()
  const values = [normalized, ...readRecentQueries().filter(value => value !== normalized)].slice(0, MaximumEntries)
  writeJson(RecentQueriesKey, values)
  return values
}

export function clearRecentQueries(): void {
  try {
    sessionStorage.removeItem(RecentQueriesKey)
  } catch {
    // 会话辅助信息不可清除不影响搜索与导航。
  }
}

export function readRecentVisits(): readonly RecentVisit[] {
  const values = readJson<unknown[]>(RecentVisitsKey)
  if (!Array.isArray(values)) return []
  return values.filter(isRecentVisit).slice(0, MaximumEntries)
}

export function rememberVisit(item: SearchResultItem, objectType: string): readonly RecentVisit[] {
  const visit: RecentVisit = {
    title: item.title,
    systemContext: item.systemContext,
    objectType,
    knowledgeStatus: item.knowledgeStatus,
    unknownItemStatus: item.unknownItemStatus,
    navigation: item.navigation,
  }
  const values = [visit, ...readRecentVisits().filter(value =>
    value.navigation.routeObjectType !== visit.navigation.routeObjectType
    || value.navigation.routeObjectId !== visit.navigation.routeObjectId
    || value.navigation.drawerObjectId !== visit.navigation.drawerObjectId,
  )].slice(0, MaximumEntries)
  writeJson(RecentVisitsKey, values)
  return values
}

function isRecentVisit(value: unknown): value is RecentVisit {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return false
  const visit = value as Record<string, unknown>
  const navigation = visit.navigation
  if (typeof navigation !== 'object' || navigation === null || Array.isArray(navigation)) return false
  const routeObjectId = (navigation as Record<string, unknown>).routeObjectId
  return typeof visit.title === 'string'
    && typeof visit.systemContext === 'string'
    && typeof visit.objectType === 'string'
    && Number.isSafeInteger(routeObjectId)
}
