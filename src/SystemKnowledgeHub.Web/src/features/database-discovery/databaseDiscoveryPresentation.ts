import type {
  ReconciliationStatus,
  SyncActionType,
  SyncPlanStatus,
  SyncStructure,
} from './api/databaseDiscoverySyncContracts'

export const syncActionLabels: Readonly<Record<SyncActionType, string>> = {
  CreateDatabaseObject: '创建对象',
  LinkExistingDatabaseObject: '关联对象',
  CreateDatabaseColumn: '创建字段',
  LinkExistingDatabaseColumn: '关联字段',
  UpdateDatabaseObjectStructure: '更新对象结构',
  UpdateDatabaseColumnStructure: '更新字段结构',
  MarkObjectSourceMissing: '标记对象来源缺失',
  ClearObjectSourceMissing: '清除对象来源缺失',
  MarkColumnSourceMissing: '标记字段来源缺失',
  ClearColumnSourceMissing: '清除字段来源缺失',
}

export const syncPlanStatusLabels: Readonly<Record<SyncPlanStatus, string>> = {
  Draft: '草稿',
  Ready: '待应用',
  Applied: '已应用',
  Superseded: '已失效',
}

export const reconciliationStatusLabels: Readonly<Record<ReconciliationStatus, string>> = {
  Applicable: '可处理',
  NoAction: '无需操作',
  Conflict: '冲突',
  Unsupported: '仅审查',
}

export const reconciliationStatusHelp = [
  { label: '可处理', description: '系统能够明确生成安全的同步动作，可以加入同步计划。' },
  {
    label: '部分可处理',
    description: '当前对象只有部分结构可以安全同步；父级选择只会选择可处理项。',
  },
  { label: '无需操作', description: '当前知识库与最新发现结构已经一致，无需加入同步计划。' },
  { label: '仅审查', description: '该结构可供查看，但不属于当前允许自动写入的范围。' },
  { label: '冲突', description: '系统无法安全判断或执行同步，需要先处理冲突原因。' },
] as const

export interface ReconciliationReasonPresentation {
  readonly label: string
  readonly description: string
}

const reasonPresentations: Readonly<Record<string, ReconciliationReasonPresentation>> = {
  BoundTargetUnavailable: {
    label: '已绑定目标不可用',
    description: '已绑定的数据库对象或字段不存在或已删除，需要先处理原有绑定关系。',
  },
  RenameNotSupported: {
    label: '不支持自动重命名',
    description: '发现身份对应的名称发生变化，需要人工确认并处理名称关系。',
  },
  UnsupportedIdentifierCollision: {
    label: '标识冲突',
    description: '名称、类型或字段顺序与现有知识发生冲突，无法安全关联。',
  },
  UnsupportedOrdinal: {
    label: '字段顺序不受支持',
    description: '当前字段顺序无法安全同步，需要人工检查。',
  },
  ActiveOrdinalConflict: {
    label: '字段顺序冲突',
    description: '目标顺序已被其他活动字段占用，需要先处理顺序冲突。',
  },
  RebaselineRequired: {
    label: '需要重新建立基线',
    description: '现有绑定与当前发现范围或技术身份版本不兼容。',
  },
  ParentObjectActionRequired: {
    label: '缺少父对象操作',
    description: '字段同步需要先创建或关联所属数据库对象。',
  },
  ReviewOnlyStructure: {
    label: '仅审查结构',
    description: '该结构可以查看，但不属于当前手工同步允许写入的范围。',
  },
}

const unknownReason: ReconciliationReasonPresentation = {
  label: '需要人工检查',
  description: '当前结构存在系统无法自动处理的情况，请结合详情人工检查。',
}

export function reconciliationReason(code: string): ReconciliationReasonPresentation {
  return reasonPresentations[code] ?? unknownReason
}

export function formatSyncStructure(value: SyncStructure | null): string {
  if (value === null) return '—'
  const rows = [
    value.schemaName ? `架构（Schema）：${value.schemaName}` : null,
    value.name ? `名称：${value.name}` : null,
    value.objectType
      ? `对象类型：${value.objectType === 'Table' ? '表' : value.objectType === 'View' ? '视图' : '其他'}`
      : null,
    value.databaseComment ? `数据库注释：${value.databaseComment}` : null,
    value.primaryKeyColumns ? `主键：${value.primaryKeyColumns.join('、') || '—'}` : null,
    value.ordinalPosition === null ? null : `字段顺序：${value.ordinalPosition}`,
    value.dataType ? `数据类型：${value.dataType}` : null,
    value.isNullable === null ? null : `允许为空：${value.isNullable ? '是' : '否'}`,
    value.defaultValue ? `默认值：${value.defaultValue}` : null,
  ].filter((row): row is string => row !== null)
  return rows.length === 0 ? '—' : rows.join('\n')
}
