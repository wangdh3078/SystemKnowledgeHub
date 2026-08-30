import { ref } from 'vue'
import { mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { DatabaseColumnDetailResponse } from '../api/databaseKnowledgeContracts'
import { useDatabaseColumnDetail } from '../composables/useDatabaseColumnDetail'
import ColumnDetailDrawer from './ColumnDetailDrawer.vue'

const actorState = vi.hoisted(() => ({
  canEdit: false,
  actor: { displayName: '查看者', role: null },
}))
const overlayState = vi.hoisted(() => ({
  openDialog: vi.fn(),
  openDrawer: vi.fn(),
  closeDialog: vi.fn(),
  requestDrawerClose: vi.fn(),
}))

vi.mock('../../../app/stores/actor', () => ({ useActorStore: () => actorState }))
vi.mock('../../../app/stores/overlays', () => ({ useOverlayStore: () => overlayState }))
vi.mock('../composables/useDatabaseColumnDetail', () => ({ useDatabaseColumnDetail: vi.fn() }))

const detail: DatabaseColumnDetailResponse = {
  id: 123,
  parent: { databaseObjectId: 45, qualifiedName: 'MES.TABLE_EQP' },
  system: { id: 12, name: 'MES' },
  concurrencyToken: 'column-token',
  databaseMetadata: {
    columnName: 'STATE_FLAG',
    dataType: 'VARCHAR2(20)',
    nullable: true,
    defaultValue: null,
    ordinalPosition: 2,
  },
  businessKnowledge: { description: '设备状态', knowledgeStatus: 'Inferred' },
  knownValues: [],
  evidence: [],
  relations: [],
  unknownItems: [],
  canDelete: true,
  availableActions: ['ChangeKnowledgeStatus'],
}

const stubs = {
  ElButton: { template: '<button type="button"><slot /></button>' },
  ElCollapse: { template: '<div><slot /></div>' },
  ElCollapseItem: { template: '<section><slot name="title" /><slot /></section>' },
  ElIcon: { template: '<span><slot /></span>' },
  ElForm: { template: '<form><slot /></form>' },
  ElFormItem: { template: '<label><slot /></label>' },
  ElInput: { template: '<input />' },
  ElInputNumber: { template: '<input />' },
  EmptyState: { template: '<div />' },
  ErrorState: { template: '<div />' },
  KnowledgeStatusBadge: { template: '<span>知识状态</span>' },
  KnowledgeStatusProgressionPanel: { template: '<div data-test="knowledge-status" />' },
  LoadingState: { template: '<div />' },
}

describe('ColumnDetailDrawer write-action visibility', () => {
  beforeEach(() => {
    actorState.canEdit = false
    vi.mocked(useDatabaseColumnDetail).mockReturnValue({
      detail: ref(detail),
      loading: ref(false),
      errorMessage: ref<string | null>(null),
      reload: vi.fn().mockResolvedValue(undefined),
    })
  })

  it('keeps Viewer read-only even when the column is otherwise deletable and mutable', () => {
    const wrapper = mount(ColumnDetailDrawer, {
      props: { columnId: 123 },
      global: { stubs },
    })

    const buttonLabels = wrapper.findAll('button').map((button) => button.text())
    expect(buttonLabels).not.toContain('编辑')
    expect(buttonLabels).not.toContain('添加')
    expect(buttonLabels).not.toContain('删除数据库字段')
    expect(buttonLabels).not.toContain('编辑字段知识')
    expect(buttonLabels).not.toContain('添加证据')
    expect(buttonLabels).not.toContain('新建待确认事项')
  })
})
