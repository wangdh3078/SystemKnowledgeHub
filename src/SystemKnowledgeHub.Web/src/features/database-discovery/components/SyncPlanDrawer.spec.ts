import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import type { SyncPlan } from '../api/databaseDiscoverySyncContracts'
import SyncPlanDrawer from './SyncPlanDrawer.vue'

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
          databaseComment: null,
          primaryKeyColumns: ['ID'],
          ordinalPosition: null,
          dataType: null,
          isNullable: null,
          defaultValue: null,
        },
        summary: '创建对象',
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
  ElButton: { emits: ['click'], template: '<button @click="$emit(\'click\')"><slot /></button>' },
  ElCheckbox: {
    props: ['modelValue'],
    emits: ['change'],
    template: '<label><input type="checkbox" @change="$emit(\'change\', true)" /><slot /></label>',
  },
  ElIcon: { template: '<span><slot /></span>' },
  ElTag: { template: '<span><slot /></span>' },
  ElAlert: true,
  ElResult: { props: ['title'], template: '<div>{{ title }}</div>' },
  ElTable: { template: '<div><slot /></div>' },
  ElTableColumn: true,
}

describe('SyncPlanDrawer', () => {
  it('shows Chinese status, action summaries, preview, confirmation, and result sections', () => {
    const wrapper = mount(SyncPlanDrawer, {
      props: { plan, canEdit: true, mutating: false, confirmationChecked: false },
      global: { stubs },
    })

    expect(wrapper.text()).toContain('草稿')
    expect(wrapper.text()).toContain('创建对象')
    expect(wrapper.text()).toContain('创建字段')
    expect(wrapper.text()).toContain('预览校验值')
    expect(wrapper.text()).toContain('确认状态')
    expect(wrapper.text()).toContain('应用结果')
    expect(wrapper.text()).not.toContain('Draft')
  })

  it('keeps confirmation actions inside the drawer', async () => {
    const wrapper = mount(SyncPlanDrawer, {
      props: { plan, canEdit: true, mutating: false, confirmationChecked: false },
      global: { stubs },
    })

    await wrapper.get('input[type="checkbox"]').setValue(true)
    expect(wrapper.emitted('update:confirmationChecked')).toEqual([[true]])
  })
})
