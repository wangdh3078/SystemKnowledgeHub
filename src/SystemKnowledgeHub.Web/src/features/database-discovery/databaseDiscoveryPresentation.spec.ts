import { describe, expect, it } from 'vitest'
import {
  formatSyncStructure,
  reconciliationReason,
  reconciliationStatusLabels,
  syncActionLabels,
  syncPlanStatusLabels,
} from './databaseDiscoveryPresentation'

describe('databaseDiscoveryPresentation', () => {
  it('maps workflow states and typed actions to user-facing Chinese', () => {
    expect(reconciliationStatusLabels.Unsupported).toBe('仅审查')
    expect(syncPlanStatusLabels.Applied).toBe('已应用')
    expect(syncActionLabels.LinkExistingDatabaseColumn).toBe('关联字段')
  })

  it.each([
    ['BoundTargetUnavailable', '已绑定目标不可用'],
    ['ReviewOnlyStructure', '仅审查结构'],
    ['UnsupportedOrdinal', '字段顺序不受支持'],
    ['RenameNotSupported', '不支持自动重命名'],
    ['UnsupportedIdentifierCollision', '标识冲突'],
    ['ActiveOrdinalConflict', '字段顺序冲突'],
    ['RebaselineRequired', '需要重新建立基线'],
    ['ParentObjectActionRequired', '缺少父对象操作'],
  ])('does not expose raw reason code %s', (code, label) => {
    expect(reconciliationReason(code).label).toBe(label)
    expect(reconciliationReason(code).description).not.toContain(code)
  })

  it('formats preview structures with Chinese field and object-type labels', () => {
    const text = formatSyncStructure({
      schemaName: 'APP',
      name: 'CUSTOMERS',
      objectType: 'Table',
      databaseComment: null,
      primaryKeyColumns: ['ID'],
      ordinalPosition: null,
      dataType: null,
      isNullable: null,
      defaultValue: null,
    })

    expect(text).toContain('架构（Schema）：APP')
    expect(text).toContain('对象类型：表')
    expect(text).not.toContain('objectType')
    expect(text).not.toContain('Table')
  })
})
