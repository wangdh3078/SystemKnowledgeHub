import { apiClient } from '../../../api/client/apiClient'
import {
  decodeReconciliation,
  decodeReconciliationObjectChildren,
  decodeReconciliationObjectGroups,
  decodeReconciliationObjectSelection,
  decodeSyncPlan,
  decodeSyncPlans,
} from './databaseDiscoverySyncContracts'
import type {
  ReconciliationObjectChildrenPage,
  ReconciliationObjectGroupPage,
  ReconciliationObjectSelection,
  ReconciliationPage,
  SyncPlan,
  SyncSelection,
} from './databaseDiscoverySyncContracts'
import type { Page } from './databaseDiscoveryContracts'

const safe = (id: number): number => {
  if (!Number.isSafeInteger(id) || id <= 0) throw new Error('id must be safe')
  return id
}
export const getReconciliation = (
  profileId: number,
  category: string,
  search: string,
  page: number,
  signal?: AbortSignal,
): Promise<ReconciliationPage> => {
  const query = new URLSearchParams({
    profileId: String(safe(profileId)),
    category,
    search,
    page: String(page),
    pageSize: '50',
  })
  return apiClient.get(`/database-discovery/reconciliation?${query}`, {
    signal,
    decode: decodeReconciliation,
  })
}
export const queryReconciliationObjectGroups = (
  profileId: number,
  targetSnapshotId: number | null,
  category: string,
  search: string,
  page: number,
  selectedActions: readonly SyncSelection[],
  signal?: AbortSignal,
): Promise<ReconciliationObjectGroupPage> =>
  apiClient.post(
    '/database-discovery/reconciliation/object-groups/query',
    {
      profileId: safe(profileId),
      targetSnapshotId,
      category,
      search,
      page,
      pageSize: 20,
      selectedActions,
    },
    { signal, decode: decodeReconciliationObjectGroups },
  )
export const queryReconciliationObjectChildren = (
  profileId: number,
  targetSnapshotId: number,
  objectLogicalIdentity: string,
  category: string,
  search: string,
  page: number,
  selectedActions: readonly SyncSelection[],
  signal?: AbortSignal,
): Promise<ReconciliationObjectChildrenPage> =>
  apiClient.post(
    '/database-discovery/reconciliation/object-children/query',
    {
      profileId: safe(profileId),
      targetSnapshotId: safe(targetSnapshotId),
      objectLogicalIdentity,
      category,
      search,
      page,
      pageSize: 20,
      selectedActions,
    },
    { signal, decode: decodeReconciliationObjectChildren },
  )
export const setWholeObjectSelection = (
  profileId: number,
  targetSnapshotId: number,
  objectLogicalIdentity: string,
  selected: boolean,
  currentActions: readonly SyncSelection[],
): Promise<ReconciliationObjectSelection> =>
  apiClient.post(
    '/database-discovery/reconciliation/object-selection',
    {
      profileId: safe(profileId),
      targetSnapshotId: safe(targetSnapshotId),
      objectLogicalIdentity,
      selected,
      currentActions,
    },
    { decode: decodeReconciliationObjectSelection },
  )
export const createSyncPlan = (
  profileId: number,
  targetSnapshotId: number,
  actions: readonly SyncSelection[],
): Promise<SyncPlan> =>
  apiClient.post(
    '/database-discovery/sync-plans',
    { profileId, targetSnapshotId, actions },
    { decode: decodeSyncPlan },
  )
export const updateSyncPlanSelections = (
  plan: SyncPlan,
  actions: readonly SyncSelection[],
): Promise<SyncPlan> =>
  apiClient.put(
    `/database-discovery/sync-plans/${safe(plan.id)}/actions`,
    { actions, concurrencyToken: plan.concurrencyToken },
    { decode: decodeSyncPlan },
  )
export const previewSyncPlan = (plan: SyncPlan): Promise<SyncPlan> =>
  apiClient.post(
    `/database-discovery/sync-plans/${safe(plan.id)}/preview`,
    { concurrencyToken: plan.concurrencyToken },
    { decode: decodeSyncPlan },
  )
export const confirmSyncPlan = (plan: SyncPlan): Promise<SyncPlan> =>
  apiClient.post(
    `/database-discovery/sync-plans/${safe(plan.id)}/confirm`,
    { previewHash: plan.preview?.previewHash, concurrencyToken: plan.concurrencyToken },
    { decode: decodeSyncPlan },
  )
export const applySyncPlan = (plan: SyncPlan): Promise<SyncPlan> =>
  apiClient.post(
    `/database-discovery/sync-plans/${safe(plan.id)}/apply`,
    { previewHash: plan.preview?.previewHash, concurrencyToken: plan.concurrencyToken },
    { decode: decodeSyncPlan },
  )
export const getSyncPlan = (id: number, signal?: AbortSignal): Promise<SyncPlan> =>
  apiClient.get(`/database-discovery/sync-plans/${safe(id)}`, { signal, decode: decodeSyncPlan })
export const listSyncPlans = (
  page = 1,
  profileId?: number,
  signal?: AbortSignal,
): Promise<Page<SyncPlan>> => {
  const query = new URLSearchParams({ page: String(page), pageSize: '20' })
  if (profileId) query.set('profileId', String(safe(profileId)))
  return apiClient.get(`/database-discovery/sync-plans?${query}`, {
    signal,
    decode: decodeSyncPlans,
  })
}
