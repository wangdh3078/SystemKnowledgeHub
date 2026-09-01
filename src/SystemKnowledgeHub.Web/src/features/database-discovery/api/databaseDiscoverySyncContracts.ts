import { isSafeApiId } from '../../../api/contracts/id'
import type { DatabaseProviderType, DiscoveryEntityKind, Page } from './databaseDiscoveryContracts'

export type SyncPlanStatus = 'Draft' | 'Ready' | 'Applied' | 'Superseded'
export type ReconciliationStatus = 'Applicable' | 'NoAction' | 'Conflict' | 'Unsupported'
export type SyncActionType =
  | 'CreateDatabaseObject'
  | 'LinkExistingDatabaseObject'
  | 'CreateDatabaseColumn'
  | 'LinkExistingDatabaseColumn'
  | 'UpdateDatabaseObjectStructure'
  | 'UpdateDatabaseColumnStructure'
  | 'MarkObjectSourceMissing'
  | 'ClearObjectSourceMissing'
  | 'MarkColumnSourceMissing'
  | 'ClearColumnSourceMissing'

export interface SyncSelection {
  readonly actionType: SyncActionType
  readonly logicalIdentity: string
  readonly targetId: number | null
}
export interface ReconciliationCandidate {
  readonly key: string
  readonly category: string
  readonly entityKind: DiscoveryEntityKind
  readonly status: ReconciliationStatus
  readonly suggestedAction: SyncActionType | null
  readonly blockCode: string | null
  readonly schemaLogicalIdentity: string
  readonly logicalIdentity: string
  readonly parentLogicalIdentity: string | null
  readonly schemaName: string
  readonly objectName: string
  readonly childName: string | null
  readonly targetId: number | null
  readonly targetConcurrencyToken: string | null
  readonly summary: string
}
export interface ReconciliationPage extends Page<ReconciliationCandidate> {
  readonly profileId: number
  readonly profileName: string
  readonly databaseSourceId: number
  readonly databaseSourceName: string
  readonly providerType: DatabaseProviderType
  readonly targetSnapshotId: number
  readonly targetDifferenceId: number | null
  readonly scopeGenerationId: number
  readonly identityAlgorithmVersion: number
}
export interface ReconciliationObjectGroup {
  readonly key: string
  readonly schemaLogicalIdentity: string
  readonly objectLogicalIdentity: string
  readonly schemaName: string
  readonly objectName: string
  readonly objectType: string
  readonly targetId: number | null
  readonly status: ReconciliationStatus
  readonly objectCandidates: readonly ReconciliationCandidate[]
  readonly requiredParentAction: SyncSelection | null
  readonly totalColumnCount: number
  readonly selectableColumnCount: number
  readonly totalChildCount: number
  readonly selectableCount: number
  readonly selectedCount: number
  readonly conflictCount: number
  readonly unsupportedCount: number
  readonly noActionCount: number
  readonly summary: string
}
export interface ReconciliationObjectGroupPage extends Page<ReconciliationObjectGroup> {
  readonly profileId: number
  readonly profileName: string
  readonly databaseSourceId: number
  readonly databaseSourceName: string
  readonly providerType: DatabaseProviderType
  readonly targetSnapshotId: number
  readonly targetDifferenceId: number | null
  readonly scopeGenerationId: number
  readonly identityAlgorithmVersion: number
  readonly maximumSyncPlanActions: number
  readonly ungroupedReviewOnlyCount: number
}
export interface ReconciliationChild {
  readonly key: string
  readonly entityKind: DiscoveryEntityKind
  readonly logicalIdentity: string
  readonly name: string | null
  readonly status: ReconciliationStatus
  readonly candidates: readonly ReconciliationCandidate[]
  readonly selectableCount: number
  readonly selectedCount: number
  readonly blockCodes: readonly string[]
  readonly summary: string
}
export interface ReconciliationObjectChildrenPage extends Page<ReconciliationChild> {
  readonly profileId: number
  readonly targetSnapshotId: number
  readonly objectLogicalIdentity: string
}
export interface ReconciliationObjectSelection {
  readonly actions: readonly SyncSelection[]
  readonly selectedCount: number
  readonly maximumSyncPlanActions: number
  readonly objectSelectableCount: number
  readonly objectSelectedCount: number
}
export interface SyncStructure {
  readonly schemaName: string | null
  readonly name: string | null
  readonly objectType: string | null
  readonly databaseComment: string | null
  readonly primaryKeyColumns: readonly string[] | null
  readonly ordinalPosition: number | null
  readonly dataType: string | null
  readonly isNullable: boolean | null
  readonly defaultValue: string | null
}
export interface SyncPreviewAction {
  readonly actionType: SyncActionType
  readonly entityKind: DiscoveryEntityKind
  readonly schemaLogicalIdentity: string
  readonly logicalIdentity: string
  readonly parentLogicalIdentity: string | null
  readonly targetId: number | null
  readonly before: SyncStructure | null
  readonly after: SyncStructure | null
  readonly summary: string
}
export interface SyncPreview {
  readonly planId: number
  readonly targetSnapshotId: number
  readonly scopeGenerationId: number
  readonly previewHash: string
  readonly counts: Record<string, number>
  readonly actions: readonly SyncPreviewAction[]
  readonly warnings: readonly string[]
}
export interface SyncApplyResult {
  readonly createdObjects: number
  readonly linkedObjects: number
  readonly createdColumns: number
  readonly linkedColumns: number
  readonly updatedObjects: number
  readonly updatedColumns: number
  readonly markedMissing: number
  readonly clearedMissing: number
  readonly appliedAt: string
  readonly appliedByDisplayName: string
}
export interface SyncPlan {
  readonly id: number
  readonly profileId: number
  readonly profileName: string
  readonly databaseSourceId: number
  readonly databaseSourceName: string
  readonly profileConfigurationRevision: number
  readonly baseSnapshotId: number | null
  readonly targetSnapshotId: number
  readonly targetDifferenceId: number | null
  readonly scopeGenerationId: number
  readonly identityAlgorithmVersion: number
  readonly status: SyncPlanStatus
  readonly actions: readonly SyncSelection[]
  readonly preview: SyncPreview | null
  readonly confirmedPreviewHash: string | null
  readonly createdAt: string
  readonly updatedAt: string
  readonly confirmedAt: string | null
  readonly appliedAt: string | null
  readonly result: SyncApplyResult | null
  readonly concurrencyToken: string
}

const obj = (value: unknown, field: string): Record<string, unknown> => {
  if (typeof value !== 'object' || value === null || Array.isArray(value))
    throw new Error(`${field} must be an object`)
  return value as Record<string, unknown>
}
const str = (value: unknown, field: string): string => {
  if (typeof value !== 'string') throw new Error(`${field} must be a string`)
  return value
}
const nullableStr = (value: unknown, field: string): string | null =>
  value === null ? null : str(value, field)
const bool = (value: unknown, field: string): boolean => {
  if (typeof value !== 'boolean') throw new Error(`${field} must be a boolean`)
  return value
}
const num = (value: unknown, field: string, minimum = 0): number => {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < minimum)
    throw new Error(`${field} must be an integer`)
  return value
}
const id = (value: unknown, field: string): number => {
  const result = num(value, field, 1)
  if (!isSafeApiId(result)) throw new Error(`${field} must be a safe id`)
  return result
}
const nullableId = (value: unknown, field: string): number | null =>
  value === null ? null : id(value, field)
const strings = (value: unknown, field: string): readonly string[] => {
  if (!Array.isArray(value)) throw new Error(`${field} must be an array`)
  return value.map((item, index) => str(item, `${field}[${index}]`))
}
const provider = (value: unknown, field: string): DatabaseProviderType => {
  if (value === 'Oracle' || value === 'PostgreSql' || value === 'SqlServer') return value
  throw new Error(`${field} is unsupported`)
}
const actionType = (value: unknown, field: string): SyncActionType => {
  const valueString = str(value, field)
  if (
    [
      'CreateDatabaseObject',
      'LinkExistingDatabaseObject',
      'CreateDatabaseColumn',
      'LinkExistingDatabaseColumn',
      'UpdateDatabaseObjectStructure',
      'UpdateDatabaseColumnStructure',
      'MarkObjectSourceMissing',
      'ClearObjectSourceMissing',
      'MarkColumnSourceMissing',
      'ClearColumnSourceMissing',
    ].includes(valueString)
  )
    return valueString as SyncActionType
  throw new Error(`${field} is unsupported`)
}
const entityKind = (value: unknown, field: string): DiscoveryEntityKind => {
  const valueString = str(value, field)
  if (
    [
      'Schema',
      'DatabaseObject',
      'Column',
      'PrimaryKey',
      'ForeignKey',
      'UniqueConstraint',
      'Index',
      'Sequence',
    ].includes(valueString)
  )
    return valueString as DiscoveryEntityKind
  throw new Error(`${field} is unsupported`)
}
const structure = (value: unknown, field: string): SyncStructure | null => {
  if (value === null) return null
  const r = obj(value, field)
  return {
    schemaName: nullableStr(r.schemaName, `${field}.schemaName`),
    name: nullableStr(r.name, `${field}.name`),
    objectType: nullableStr(r.objectType, `${field}.objectType`),
    databaseComment: nullableStr(r.databaseComment, `${field}.databaseComment`),
    primaryKeyColumns:
      r.primaryKeyColumns === null
        ? null
        : strings(r.primaryKeyColumns, `${field}.primaryKeyColumns`),
    ordinalPosition:
      r.ordinalPosition === null ? null : num(r.ordinalPosition, `${field}.ordinalPosition`, 1),
    dataType: nullableStr(r.dataType, `${field}.dataType`),
    isNullable: r.isNullable === null ? null : bool(r.isNullable, `${field}.isNullable`),
    defaultValue: nullableStr(r.defaultValue, `${field}.defaultValue`),
  }
}
const selection = (value: unknown, field: string): SyncSelection => {
  const r = obj(value, field)
  return {
    actionType: actionType(r.actionType, `${field}.actionType`),
    logicalIdentity: str(r.logicalIdentity, `${field}.logicalIdentity`),
    targetId: nullableId(r.targetId, `${field}.targetId`),
  }
}
const reconciliationCandidate = (value: unknown, field: string): ReconciliationCandidate => {
  const c = obj(value, field)
  return {
    key: str(c.key, `${field}.key`),
    category: str(c.category, `${field}.category`),
    entityKind: entityKind(c.entityKind, `${field}.entityKind`),
    status: str(c.status, `${field}.status`) as ReconciliationStatus,
    suggestedAction:
      c.suggestedAction === null ? null : actionType(c.suggestedAction, `${field}.suggestedAction`),
    blockCode: nullableStr(c.blockCode, `${field}.blockCode`),
    schemaLogicalIdentity: str(c.schemaLogicalIdentity, `${field}.schemaLogicalIdentity`),
    logicalIdentity: str(c.logicalIdentity, `${field}.logicalIdentity`),
    parentLogicalIdentity: nullableStr(c.parentLogicalIdentity, `${field}.parentLogicalIdentity`),
    schemaName: str(c.schemaName, `${field}.schemaName`),
    objectName: str(c.objectName, `${field}.objectName`),
    childName: nullableStr(c.childName, `${field}.childName`),
    targetId: nullableId(c.targetId, `${field}.targetId`),
    targetConcurrencyToken: nullableStr(
      c.targetConcurrencyToken,
      `${field}.targetConcurrencyToken`,
    ),
    summary: str(c.summary, `${field}.summary`),
  }
}
const preview = (value: unknown, field: string): SyncPreview | null => {
  if (value === null) return null
  const r = obj(value, field)
  if (!Array.isArray(r.actions)) throw new Error(`${field}.actions must be an array`)
  const countsRoot = obj(r.counts, `${field}.counts`)
  return {
    planId: id(r.planId, `${field}.planId`),
    targetSnapshotId: id(r.targetSnapshotId, `${field}.targetSnapshotId`),
    scopeGenerationId: id(r.scopeGenerationId, `${field}.scopeGenerationId`),
    previewHash: str(r.previewHash, `${field}.previewHash`),
    counts: Object.fromEntries(
      Object.entries(countsRoot).map(([key, item]) => [key, num(item, `${field}.counts.${key}`)]),
    ),
    actions: r.actions.map((item, index) => {
      const a = obj(item, `${field}.actions[${index}]`)
      return {
        actionType: actionType(a.actionType, 'actionType'),
        entityKind: entityKind(a.entityKind, 'entityKind'),
        schemaLogicalIdentity: str(a.schemaLogicalIdentity, 'schemaLogicalIdentity'),
        logicalIdentity: str(a.logicalIdentity, 'logicalIdentity'),
        parentLogicalIdentity: nullableStr(a.parentLogicalIdentity, 'parentLogicalIdentity'),
        targetId: nullableId(a.targetId, 'targetId'),
        before: structure(a.before, 'before'),
        after: structure(a.after, 'after'),
        summary: str(a.summary, 'summary'),
      }
    }),
    warnings: strings(r.warnings, `${field}.warnings`),
  }
}

export function decodeReconciliation(value: unknown): ReconciliationPage {
  const r = obj(value, 'reconciliation')
  if (!Array.isArray(r.items)) throw new Error('reconciliation.items must be an array')
  return {
    profileId: id(r.profileId, 'profileId'),
    profileName: str(r.profileName, 'profileName'),
    databaseSourceId: id(r.databaseSourceId, 'databaseSourceId'),
    databaseSourceName: str(r.databaseSourceName, 'databaseSourceName'),
    providerType: provider(r.providerType, 'providerType'),
    targetSnapshotId: id(r.targetSnapshotId, 'targetSnapshotId'),
    targetDifferenceId: nullableId(r.targetDifferenceId, 'targetDifferenceId'),
    scopeGenerationId: id(r.scopeGenerationId, 'scopeGenerationId'),
    identityAlgorithmVersion: num(r.identityAlgorithmVersion, 'identityAlgorithmVersion', 1),
    items: r.items.map((item, index) => reconciliationCandidate(item, `items[${index}]`)),
    page: num(r.page, 'page', 1),
    pageSize: num(r.pageSize, 'pageSize', 1),
    total: num(r.total, 'total'),
  }
}

export function decodeReconciliationObjectGroups(value: unknown): ReconciliationObjectGroupPage {
  const r = obj(value, 'objectGroups')
  if (!Array.isArray(r.items)) throw new Error('objectGroups.items must be an array')
  return {
    profileId: id(r.profileId, 'profileId'),
    profileName: str(r.profileName, 'profileName'),
    databaseSourceId: id(r.databaseSourceId, 'databaseSourceId'),
    databaseSourceName: str(r.databaseSourceName, 'databaseSourceName'),
    providerType: provider(r.providerType, 'providerType'),
    targetSnapshotId: id(r.targetSnapshotId, 'targetSnapshotId'),
    targetDifferenceId: nullableId(r.targetDifferenceId, 'targetDifferenceId'),
    scopeGenerationId: id(r.scopeGenerationId, 'scopeGenerationId'),
    identityAlgorithmVersion: num(r.identityAlgorithmVersion, 'identityAlgorithmVersion', 1),
    maximumSyncPlanActions: num(r.maximumSyncPlanActions, 'maximumSyncPlanActions', 1),
    ungroupedReviewOnlyCount: num(r.ungroupedReviewOnlyCount, 'ungroupedReviewOnlyCount'),
    items: r.items.map((item, index) => {
      const group = obj(item, `items[${index}]`)
      if (!Array.isArray(group.objectCandidates))
        throw new Error(`items[${index}].objectCandidates must be an array`)
      return {
        key: str(group.key, 'key'),
        schemaLogicalIdentity: str(group.schemaLogicalIdentity, 'schemaLogicalIdentity'),
        objectLogicalIdentity: str(group.objectLogicalIdentity, 'objectLogicalIdentity'),
        schemaName: str(group.schemaName, 'schemaName'),
        objectName: str(group.objectName, 'objectName'),
        objectType: str(group.objectType, 'objectType'),
        targetId: nullableId(group.targetId, 'targetId'),
        status: str(group.status, 'status') as ReconciliationStatus,
        objectCandidates: group.objectCandidates.map((candidate, candidateIndex) =>
          reconciliationCandidate(candidate, `objectCandidates[${candidateIndex}]`),
        ),
        requiredParentAction:
          group.requiredParentAction === null
            ? null
            : selection(group.requiredParentAction, 'requiredParentAction'),
        totalColumnCount: num(group.totalColumnCount, 'totalColumnCount'),
        selectableColumnCount: num(group.selectableColumnCount, 'selectableColumnCount'),
        totalChildCount: num(group.totalChildCount, 'totalChildCount'),
        selectableCount: num(group.selectableCount, 'selectableCount'),
        selectedCount: num(group.selectedCount, 'selectedCount'),
        conflictCount: num(group.conflictCount, 'conflictCount'),
        unsupportedCount: num(group.unsupportedCount, 'unsupportedCount'),
        noActionCount: num(group.noActionCount, 'noActionCount'),
        summary: str(group.summary, 'summary'),
      }
    }),
    page: num(r.page, 'page', 1),
    pageSize: num(r.pageSize, 'pageSize', 1),
    total: num(r.total, 'total'),
  }
}

export function decodeReconciliationObjectChildren(
  value: unknown,
): ReconciliationObjectChildrenPage {
  const r = obj(value, 'objectChildren')
  if (!Array.isArray(r.items)) throw new Error('objectChildren.items must be an array')
  return {
    profileId: id(r.profileId, 'profileId'),
    targetSnapshotId: id(r.targetSnapshotId, 'targetSnapshotId'),
    objectLogicalIdentity: str(r.objectLogicalIdentity, 'objectLogicalIdentity'),
    items: r.items.map((item, index) => {
      const child = obj(item, `items[${index}]`)
      if (!Array.isArray(child.candidates))
        throw new Error(`items[${index}].candidates must be an array`)
      return {
        key: str(child.key, 'key'),
        entityKind: entityKind(child.entityKind, 'entityKind'),
        logicalIdentity: str(child.logicalIdentity, 'logicalIdentity'),
        name: nullableStr(child.name, 'name'),
        status: str(child.status, 'status') as ReconciliationStatus,
        candidates: child.candidates.map((candidate, candidateIndex) =>
          reconciliationCandidate(candidate, `candidates[${candidateIndex}]`),
        ),
        selectableCount: num(child.selectableCount, 'selectableCount'),
        selectedCount: num(child.selectedCount, 'selectedCount'),
        blockCodes: strings(child.blockCodes, 'blockCodes'),
        summary: str(child.summary, 'summary'),
      }
    }),
    page: num(r.page, 'page', 1),
    pageSize: num(r.pageSize, 'pageSize', 1),
    total: num(r.total, 'total'),
  }
}

export function decodeReconciliationObjectSelection(value: unknown): ReconciliationObjectSelection {
  const r = obj(value, 'objectSelection')
  if (!Array.isArray(r.actions)) throw new Error('objectSelection.actions must be an array')
  return {
    actions: r.actions.map((item, index) => selection(item, `actions[${index}]`)),
    selectedCount: num(r.selectedCount, 'selectedCount'),
    maximumSyncPlanActions: num(r.maximumSyncPlanActions, 'maximumSyncPlanActions', 1),
    objectSelectableCount: num(r.objectSelectableCount, 'objectSelectableCount'),
    objectSelectedCount: num(r.objectSelectedCount, 'objectSelectedCount'),
  }
}

export function decodeSyncPlan(value: unknown): SyncPlan {
  const r = obj(value, 'plan')
  if (!Array.isArray(r.actions)) throw new Error('plan.actions must be an array')
  return {
    id: id(r.id, 'id'),
    profileId: id(r.profileId, 'profileId'),
    profileName: str(r.profileName, 'profileName'),
    databaseSourceId: id(r.databaseSourceId, 'databaseSourceId'),
    databaseSourceName: str(r.databaseSourceName, 'databaseSourceName'),
    profileConfigurationRevision: num(
      r.profileConfigurationRevision,
      'profileConfigurationRevision',
      1,
    ),
    baseSnapshotId: nullableId(r.baseSnapshotId, 'baseSnapshotId'),
    targetSnapshotId: id(r.targetSnapshotId, 'targetSnapshotId'),
    targetDifferenceId: nullableId(r.targetDifferenceId, 'targetDifferenceId'),
    scopeGenerationId: id(r.scopeGenerationId, 'scopeGenerationId'),
    identityAlgorithmVersion: num(r.identityAlgorithmVersion, 'identityAlgorithmVersion', 1),
    status: (() => {
      const value = str(r.status, 'status')
      if (!['Draft', 'Ready', 'Applied', 'Superseded'].includes(value))
        throw new Error('status is unsupported')
      return value as SyncPlanStatus
    })(),
    actions: r.actions.map((item, index) => selection(item, `actions[${index}]`)),
    preview: preview(r.preview, 'preview'),
    confirmedPreviewHash: nullableStr(r.confirmedPreviewHash, 'confirmedPreviewHash'),
    createdAt: str(r.createdAt, 'createdAt'),
    updatedAt: str(r.updatedAt, 'updatedAt'),
    confirmedAt: nullableStr(r.confirmedAt, 'confirmedAt'),
    appliedAt: nullableStr(r.appliedAt, 'appliedAt'),
    result: decodeApplyResult(r.result),
    concurrencyToken: str(r.concurrencyToken, 'concurrencyToken'),
  }
}

function decodeApplyResult(value: unknown): SyncApplyResult | null {
  if (value === null) return null
  const r = obj(value, 'result')
  return {
    createdObjects: num(r.createdObjects, 'result.createdObjects'),
    linkedObjects: num(r.linkedObjects, 'result.linkedObjects'),
    createdColumns: num(r.createdColumns, 'result.createdColumns'),
    linkedColumns: num(r.linkedColumns, 'result.linkedColumns'),
    updatedObjects: num(r.updatedObjects, 'result.updatedObjects'),
    updatedColumns: num(r.updatedColumns, 'result.updatedColumns'),
    markedMissing: num(r.markedMissing, 'result.markedMissing'),
    clearedMissing: num(r.clearedMissing, 'result.clearedMissing'),
    appliedAt: str(r.appliedAt, 'result.appliedAt'),
    appliedByDisplayName: str(r.appliedByDisplayName, 'result.appliedByDisplayName'),
  }
}
export function decodeSyncPlans(value: unknown): Page<SyncPlan> {
  const r = obj(value, 'plans')
  if (!Array.isArray(r.items)) throw new Error('plans.items must be an array')
  return {
    items: r.items.map(decodeSyncPlan),
    page: num(r.page, 'page', 1),
    pageSize: num(r.pageSize, 'pageSize', 1),
    total: num(r.total, 'total'),
  }
}
