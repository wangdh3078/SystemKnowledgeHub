import { apiClient } from '../../../api/client/apiClient'
import { isSafeApiId } from '../../../api/contracts/id'
import {
  decodeConnectionTest,
  decodeConstraintPage,
  decodeDifference,
  decodeDifferenceHistory,
  decodeDifferenceEntries,
  decodeObjectHeader,
  decodeObjectReview,
  decodeObjects,
  decodeProfile,
  decodeProfiles,
  decodeRecordPage,
  decodeRun,
  decodeRunFilterOptions,
  decodeRuns,
  decodeSnapshotHistory,
  decodeSchemas,
  decodeSequences,
  decodeSnapshotSummary,
  decodeSourceOptions,
  type ConnectionProfile,
  type ConnectionTestResult,
  type DatabaseProviderType,
  type DifferenceEntry,
  type DifferenceHistoryItem,
  type DifferenceState,
  type DifferenceSummary,
  type DiscoveryRun,
  type DiscoveryEntityKind,
  type DiscoveryObjectType,
  type RunFilterOptions,
  type Page,
  type SnapshotObject,
  type SnapshotHistoryItem,
  type SnapshotObjectHeader,
  type SnapshotObjectReview,
  type SnapshotConstraint,
  type SnapshotSequence,
  type SnapshotSchema,
  type SnapshotSummary,
  type SourceOption,
} from './databaseDiscoveryContracts'

export interface ProfilePayload {
  readonly databaseSourceId: number
  readonly name: string
  readonly providerType: DatabaseProviderType
  readonly host: string
  readonly port: number
  readonly databaseName: string | null
  readonly serviceName: string | null
  readonly authenticationMode: 'UsernamePassword'
  readonly username: string
  readonly providerSpecificOptions: { readonly version: 1 }
  readonly includedSchemas: readonly string[]
  readonly isEnabled: boolean
}

const safe = (id: number): string => {
  if (!isSafeApiId(id)) throw new RangeError('ID 无效。')
  return encodeURIComponent(String(id))
}
export const listProfiles = (signal?: AbortSignal): Promise<readonly ConnectionProfile[]> =>
  apiClient.get('/admin/database-connection-profiles', { signal, decode: decodeProfiles })
export const listSourceOptions = (
  search = '',
  signal?: AbortSignal,
): Promise<readonly SourceOption[]> =>
  apiClient.get(
    `/admin/database-connection-profiles/database-sources?search=${encodeURIComponent(search)}`,
    { signal, decode: decodeSourceOptions },
  )
export const createProfile = (request: ProfilePayload): Promise<ConnectionProfile> =>
  apiClient.post('/admin/database-connection-profiles', request, { decode: decodeProfile })
export const updateProfile = (
  id: number,
  request: Omit<ProfilePayload, 'databaseSourceId' | 'isEnabled'> & {
    readonly concurrencyToken: string
  },
): Promise<ConnectionProfile> =>
  apiClient.put(`/admin/database-connection-profiles/${safe(id)}`, request, {
    decode: decodeProfile,
  })
export const setProfileEnabled = (
  profile: ConnectionProfile,
  isEnabled: boolean,
): Promise<ConnectionProfile> =>
  apiClient.put(
    `/admin/database-connection-profiles/${safe(profile.id)}/enabled-state`,
    { isEnabled, concurrencyToken: profile.concurrencyToken },
    { decode: decodeProfile },
  )
export const setSecret = (
  profile: ConnectionProfile,
  password: string,
): Promise<ConnectionProfile> =>
  apiClient.post(
    `/admin/database-connection-profiles/${safe(profile.id)}/secret`,
    { password, concurrencyToken: profile.concurrencyToken },
    { decode: decodeProfile },
  )
export const replaceSecret = (
  profile: ConnectionProfile,
  password: string,
): Promise<ConnectionProfile> =>
  apiClient.put(
    `/admin/database-connection-profiles/${safe(profile.id)}/secret`,
    { password, concurrencyToken: profile.concurrencyToken },
    { decode: decodeProfile },
  )
export const clearSecret = (profile: ConnectionProfile): Promise<ConnectionProfile> =>
  apiClient.deleteWithBody(
    `/admin/database-connection-profiles/${safe(profile.id)}/secret`,
    { concurrencyToken: profile.concurrencyToken },
    { decode: decodeProfile },
  )
export const testConnection = (profile: ConnectionProfile): Promise<ConnectionTestResult> =>
  apiClient.post(
    `/admin/database-connection-profiles/${safe(profile.id)}/test-connection`,
    { concurrencyToken: profile.concurrencyToken },
    { decode: decodeConnectionTest },
  )
export const triggerDiscovery = (profile: ConnectionProfile): Promise<DiscoveryRun> =>
  apiClient.post(
    `/admin/database-connection-profiles/${safe(profile.id)}/discovery-runs`,
    { concurrencyToken: profile.concurrencyToken },
    { decode: decodeRun },
  )
export const listRuns = (
  page: number,
  pageSize: number,
  profileId?: number,
  databaseSourceId?: number,
  signal?: AbortSignal,
): Promise<Page<DiscoveryRun>> => {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (profileId) query.set('profileId', String(profileId))
  if (databaseSourceId) query.set('databaseSourceId', String(databaseSourceId))
  return apiClient.get(`/database-discovery/runs?${query}`, { signal, decode: decodeRuns })
}
export const getRun = (id: number, signal?: AbortSignal): Promise<DiscoveryRun> =>
  apiClient.get(`/database-discovery/runs/${safe(id)}`, { signal, decode: decodeRun })
export const listSnapshots = (
  page: number,
  pageSize: number,
  profileId?: number,
  databaseSourceId?: number,
  signal?: AbortSignal,
): Promise<Page<SnapshotHistoryItem>> => {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (profileId) query.set('profileId', String(profileId))
  if (databaseSourceId) query.set('databaseSourceId', String(databaseSourceId))
  return apiClient.get(`/database-discovery/snapshots?${query}`, {
    signal,
    decode: decodeSnapshotHistory,
  })
}
export const listDifferences = (
  page: number,
  pageSize: number,
  profileId?: number,
  databaseSourceId?: number,
  signal?: AbortSignal,
): Promise<Page<DifferenceHistoryItem>> => {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (profileId) query.set('profileId', String(profileId))
  if (databaseSourceId) query.set('databaseSourceId', String(databaseSourceId))
  return apiClient.get(`/database-discovery/differences?${query}`, {
    signal,
    decode: decodeDifferenceHistory,
  })
}
export const cancelRun = (run: DiscoveryRun): Promise<DiscoveryRun> =>
  apiClient.post(
    `/database-discovery/runs/${safe(run.id)}/cancel`,
    { concurrencyToken: run.concurrencyToken },
    { decode: decodeRun },
  )
export const getSnapshotSummary = (id: number, signal?: AbortSignal): Promise<SnapshotSummary> =>
  apiClient.get(`/database-discovery/snapshots/${safe(id)}/summary`, {
    signal,
    decode: decodeSnapshotSummary,
  })
export const getRunFilterOptions = (signal?: AbortSignal): Promise<RunFilterOptions> =>
  apiClient.get('/database-discovery/run-filter-options', {
    signal,
    decode: decodeRunFilterOptions,
  })
export const getSnapshotSchemas = (
  id: number,
  page: number,
  search: string,
  signal?: AbortSignal,
): Promise<Page<SnapshotSchema>> =>
  apiClient.get(
    `/database-discovery/snapshots/${safe(id)}/schemas?page=${page}&pageSize=100&search=${encodeURIComponent(search)}`,
    { signal, decode: decodeSchemas },
  )
export const getSnapshotObjects = (
  id: number,
  page: number,
  pageSize: number,
  schema: string,
  objectType: DiscoveryObjectType | '',
  search: string,
  signal?: AbortSignal,
): Promise<Page<SnapshotObject>> => {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    schema,
    objectType,
    search,
  })
  return apiClient.get(`/database-discovery/snapshots/${safe(id)}/objects?${query}`, {
    signal,
    decode: decodeObjects,
  })
}
export const getSnapshotObjectHeader = (
  id: number,
  logicalIdentity: string,
  signal?: AbortSignal,
): Promise<SnapshotObjectHeader> =>
  apiClient.get(
    `/database-discovery/snapshots/${safe(id)}/object-header?logicalIdentity=${encodeURIComponent(logicalIdentity)}`,
    { signal, decode: decodeObjectHeader },
  )
export const getSnapshotObjectReview = (
  id: number,
  logicalIdentity: string,
  columnPage: number,
  constraintPage: number,
  indexPage: number,
  pageSize: number,
  signal?: AbortSignal,
): Promise<SnapshotObjectReview> => {
  const query = new URLSearchParams({
    logicalIdentity,
    columnPage: String(columnPage),
    constraintPage: String(constraintPage),
    indexPage: String(indexPage),
    pageSize: String(pageSize),
  })
  return apiClient.get(`/database-discovery/snapshots/${safe(id)}/object-review?${query}`, {
    signal,
    decode: decodeObjectReview,
  })
}
export const getSnapshotObjectColumns = (
  id: number,
  logicalIdentity: string,
  page: number,
  signal?: AbortSignal,
): Promise<Page<Record<string, unknown>>> =>
  apiClient.get(
    `/database-discovery/snapshots/${safe(id)}/object-columns?logicalIdentity=${encodeURIComponent(logicalIdentity)}&page=${page}&pageSize=50`,
    { signal, decode: decodeRecordPage },
  )
export const getSnapshotObjectConstraints = (
  id: number,
  logicalIdentity: string,
  page: number,
  signal?: AbortSignal,
): Promise<Page<SnapshotConstraint>> =>
  apiClient.get(
    `/database-discovery/snapshots/${safe(id)}/object-constraints?logicalIdentity=${encodeURIComponent(logicalIdentity)}&page=${page}&pageSize=50`,
    { signal, decode: decodeConstraintPage },
  )
export const getSnapshotObjectIndexes = (
  id: number,
  logicalIdentity: string,
  page: number,
  signal?: AbortSignal,
): Promise<Page<Record<string, unknown>>> =>
  apiClient.get(
    `/database-discovery/snapshots/${safe(id)}/object-indexes?logicalIdentity=${encodeURIComponent(logicalIdentity)}&page=${page}&pageSize=50`,
    { signal, decode: decodeRecordPage },
  )
export const getSnapshotSequences = (
  id: number,
  page: number,
  pageSize: number,
  schema: string,
  search: string,
  signal?: AbortSignal,
): Promise<Page<SnapshotSequence>> => {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    schema,
    search,
  })
  return apiClient.get(`/database-discovery/snapshots/${safe(id)}/sequences?${query}`, {
    signal,
    decode: decodeSequences,
  })
}
export const getDifference = (id: number, signal?: AbortSignal): Promise<DifferenceSummary> =>
  apiClient.get(`/database-discovery/differences/${safe(id)}`, { signal, decode: decodeDifference })
export const getDifferenceEntries = (
  id: number,
  state: DifferenceState,
  page: number,
  pageSize: number,
  entityKind: DiscoveryEntityKind | '',
  schema: string,
  search: string,
  signal?: AbortSignal,
): Promise<Page<DifferenceEntry>> => {
  const query = new URLSearchParams({
    state,
    page: String(page),
    pageSize: String(pageSize),
    entityKind,
    schema,
    search,
  })
  return apiClient.get(`/database-discovery/differences/${safe(id)}/entries?${query}`, {
    signal,
    decode: decodeDifferenceEntries,
  })
}
