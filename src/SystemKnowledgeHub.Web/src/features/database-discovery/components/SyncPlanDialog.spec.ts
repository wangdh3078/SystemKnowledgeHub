import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import type { SyncPlan } from '../api/databaseDiscoverySyncContracts'
import SyncPlanDialog from './SyncPlanDialog.vue'

const plan: SyncPlan = {
  id: 9,
  profileId: 1,
  profileName: 'Oracle 只读',
  databaseSourceId: 2,
  databaseSourceName: '核心数据库',
  profileConfigurationRevision: 1,
  baseSnapshotId: 40,
  targetSnapshotId: 41,
  targetDifferenceId: 7,
  scopeGenerationId: 3,
  identityAlgorithmVersion: 1,
  status: 'Draft',
  actions: [
    { actionType: 'CreateDatabaseObject', logicalIdentity: 'object-a', targetId: null },
    { actionType: 'CreateDatabaseColumn', logicalIdentity: 'column-a', targetId: null },
  ],
  preview: {
    planId: 9,
    targetSnapshotId: 41,
    scopeGenerationId: 3,
    previewHash: 'hash-1',
    counts: {},
    warnings: [],
    actions: [
      {
        actionType: 'CreateDatabaseObject',
        entityKind: 'DatabaseObject',
        schemaLogicalIdentity: 'schema-a',
        logicalIdentity: 'object-a',
        parentLogicalIdentity: null,
        targetId: null,
        before: null,
        after: {
          schemaName: 'APP',
          name: 'CUSTOMERS',
          objectType: 'Table',
          databaseComment: '客户主数据',
          primaryKeyColumns: ['ID'],
          ordinalPosition: null,
          dataType: null,
          isNullable: null,
          defaultValue: null,
        },
        summary: '创建对象',
        objectSchemaName: 'APP',
        objectName: 'CUSTOMERS',
        objectType: 'Table',
        objectDatabaseComment: '客户主数据',
      },
      {
        actionType: 'CreateDatabaseColumn',
        entityKind: 'Column',
        schemaLogicalIdentity: 'schema-a',
        logicalIdentity: 'column-a',
        parentLogicalIdentity: 'object-a',
        targetId: null,
        before: null,
        after: {
          schemaName: null,
          name: 'ID',
          objectType: null,
          databaseComment: '客户编号',
          primaryKeyColumns: null,
          ordinalPosition: 1,
          dataType: 'NUMBER(19)',
          isNullable: false,
          defaultValue: null,
        },
        summary: '创建字段',
        objectSchemaName: 'APP',
        objectName: 'CUSTOMERS',
        objectType: 'Table',
        objectDatabaseComment: '客户主数据',
      },
    ],
  },
  confirmedPreviewHash: null,
  createdAt: '2026-09-01T00:00:00Z',
  updatedAt: '2026-09-01T00:00:00Z',
  confirmedAt: null,
  appliedAt: null,
  result: null,
  concurrencyToken: 'token',
}

const stubs = {
  ElButton: {
    props: ['disabled'],
    emits: ['click'],
    template: '<button :disabled="disabled" @click="$emit(\'click\')"><slot /></button>',
  },
  ElCheckbox: {
    props: ['modelValue'],
    emits: ['change'],
    template: '<label><input type="checkbox" @change="$emit(\'change\', true)" /><slot /></label>',
  },
  ElTag: { template: '<span><slot /></span>' },
  ElAlert: true,
  ElResult: { props: ['title'], template: '<div>{{ title }}</div>' },
}

function mountPlan(value: SyncPlan = plan) {
  return mount(SyncPlanDialog, {
    props: { plan: value, canEdit: true, mutating: false, confirmationChecked: false },
    global: { stubs },
  })
}

describe('SyncPlanDialog', () => {
  it('groups preview actions by business object and shows readable field facts', () => {
    const wrapper = mountPlan()

    expect(wrapper.get('[role="tab"][aria-selected="true"]').text()).toBe('变更明细')
    expect(wrapper.text()).toContain('APP.CUSTOMERS')
    expect(wrapper.text()).toContain('客户主数据')
    expect(wrapper.text()).toContain('ID')
    expect(wrapper.text()).toContain('NUMBER(19)')
    expect(wrapper.text()).toContain('客户编号')
    expect(wrapper.get('.sync-plan-dialog__technical').attributes('open')).toBeUndefined()
  })

  it('shows all ten typed action counts and keeps technical identifiers collapsed', async () => {
    const wrapper = mountPlan()
    await wrapper
      .findAll('[role="tab"]')
      .find((item) => item.text() === '概览')!
      .trigger('click')

    expect(wrapper.findAll('.sync-plan-dialog__counts > div')).toHaveLength(10)
    expect(wrapper.text()).toContain('标记对象来源缺失')
    expect(wrapper.text()).toContain('清除字段来源缺失')
    expect(wrapper.get('.sync-plan-dialog__technical').attributes('open')).toBeUndefined()
  })

  it('keeps explicit confirmation in the fixed footer', async () => {
    const wrapper = mountPlan()

    expect(wrapper.get('.sync-plan-dialog__footer').text()).toContain('确认当前预览')
    await wrapper.get('input[type="checkbox"]').setValue(true)
    expect(wrapper.emitted('update:confirmationChecked')).toEqual([[true]])
  })

  it('opens an applied history plan on overview and offers only close in the footer', async () => {
    const applied: SyncPlan = {
      ...plan,
      status: 'Applied',
      confirmedAt: '2026-09-01T00:01:00Z',
      appliedAt: '2026-09-01T00:02:00Z',
      result: {
        createdObjects: 1,
        linkedObjects: 0,
        createdColumns: 1,
        linkedColumns: 0,
        updatedObjects: 0,
        updatedColumns: 0,
        markedMissing: 0,
        clearedMissing: 0,
        appliedAt: '2026-09-01T00:02:00Z',
        appliedByDisplayName: '同步审查人',
      },
    }
    const wrapper = mountPlan(applied)

    expect(wrapper.get('[role="tab"][aria-selected="true"]').text()).toBe('概览')
    expect(wrapper.get('.sync-plan-dialog__footer').text()).toBe('关闭')
    await wrapper
      .findAll('[role="tab"]')
      .find((item) => item.text() === '应用结果')!
      .trigger('click')
    expect(wrapper.text()).toContain('同步计划已应用')
    expect(wrapper.text()).toContain('同步审查人')
  })
})
