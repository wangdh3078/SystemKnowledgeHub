import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getEvidenceList } from '../../evidence/api/evidenceApi'
import { getDatabaseObjectDetail } from '../api/databaseKnowledgeApi'
import DatabaseObjectDetailView from './DatabaseObjectDetailView.vue'

const actorState = vi.hoisted(() => ({ canEdit: true }))
const overlayState = vi.hoisted(() => ({ openDrawer: vi.fn(), currentDrawer: null }))
const routerState = vi.hoisted(() => ({ replace: vi.fn(), push: vi.fn() }))

vi.mock('../../../app/stores/actor', () => ({
  useActorStore: () => actorState,
}))
vi.mock('../../../app/stores/overlays', () => ({
  useOverlayStore: () => overlayState,
}))
vi.mock('vue-router', () => ({
  useRoute: () => ({ params: { id: '45' }, query: {} }),
  useRouter: () => routerState,
}))
vi.mock('../api/databaseKnowledgeApi', async () => {
  const actual = await vi.importActual<typeof import('../api/databaseKnowledgeApi')>('../api/databaseKnowledgeApi')
  return { ...actual, getDatabaseObjectDetail: vi.fn() }
})
vi.mock('../../evidence/api/evidenceApi', () => ({ getEvidenceList: vi.fn() }))

const detail = {
  id: 45,
  system: { id: 12, name: 'MES' },
  databaseSource: { id: 9, name: 'MES Oracle', engine: 'Oracle', concurrencyToken: 'source-token', canDelete: true },
  concurrencyToken: 'object-token',
  canDelete: true,
  overview: {
    qualifiedName: 'MES.TABLE_EQP',
    objectType: 'Table' as const,
    businessDescription: '设备主数据',
    accessMode: 'ReadWrite' as const,
    knowledgeStatus: 'Inferred' as const,
  },
  metadata: {
    estimatedRows: 100,
    primaryKeyColumns: ['ID'],
    businessKeyColumns: ['CODE'],
  },
  columns: [{
    id: 91,
    ordinalPosition: 1,
    columnName: 'ID',
    dataType: 'NUMBER',
    nullable: false,
    businessDescription: '主键',
    evidenceCount: 7,
    unknownCount: 0,
    knowledgeStatus: 'Confirmed' as const,
    selected: false,
  }],
  contextRail: {
    usedByFunctions: [],
    relatedRuleCount: 0,
    integrationCount: 0,
    openUnknownCount: 0,
  },
  selectedColumnDrawer: null,
  availableActions: ['ChangeKnowledgeStatus'],
}

const evidence = {
  id: 501,
  evidenceType: 'HumanConfirmation' as const,
  knowledgeDocumentRevisionNumberSnapshot: null,
  sourceTitle: '数据库对象评审',
  sourceReference: null,
  sourceLocator: { confirmationMethod: 'Meeting' },
  summary: null,
  supportReason: 'DBA 已确认当前表的业务用途。',
  provider: {
    displayName: 'DBA',
    roleOrIdentity: '数据库专家',
    occurredAt: '2026-08-26T09:00:00Z',
    team: null,
    externalUserKey: null,
    source: null,
    note: null,
  },
}

const stubs = {
  ElButton: {
    emits: ['click'],
    template: '<button type="button" @click="$emit(\'click\')"><slot /></button>',
  },
  ElIcon: { template: '<span><slot /></span>' },
  ElInput: { template: '<input />' },
  ElTable: { template: '<div><slot /></div>' },
  ElTableColumn: { template: '<div><slot :row="{}" /></div>' },
  KnowledgeStatusBadge: { template: '<span>知识状态</span>' },
  LoadingState: { template: '<div>加载中</div>' },
  ErrorState: { template: '<div>错误</div>' },
  EmptyState: { props: ['title'], template: '<div>{{ title }}</div>' },
  DatabaseObjectContextRail: { template: '<div />' },
  RegisterDatabaseColumnDialog: { template: '<div />' },
  KnowledgeStatusProgressionPanel: {
    props: ['targetType', 'evidenceCount', 'humanConfirmationCount', 'status'],
    template: '<div data-test="progression">{{ targetType }}|{{ evidenceCount }}|{{ humanConfirmationCount }}|{{ status }}</div>',
  },
}

describe('DatabaseObjectDetailView object-level trust closure', () => {
  beforeEach(() => {
    actorState.canEdit = true
    vi.mocked(getDatabaseObjectDetail).mockReset().mockResolvedValue(detail)
    vi.mocked(getEvidenceList).mockReset().mockResolvedValue({ items: [evidence] })
    overlayState.openDrawer.mockReset()
    routerState.replace.mockReset()
    routerState.push.mockReset()
    document.body.innerHTML = '<div id="context-rail-content"></div>'
  })

  it('does not expose object-level write actions to Viewer', async () => {
    actorState.canEdit = false
    const wrapper = mount(DatabaseObjectDetailView, { global: { stubs } })
    await flushPromises()

    const buttonLabels = wrapper.findAll('button').map((button) => button.text())
    expect(buttonLabels).not.toContain('删除数据库对象')
    expect(buttonLabels).not.toContain('编辑')
    expect(buttonLabels).not.toContain('添加证据')
    expect(buttonLabels).not.toContain('添加人工确认')
    expect(buttonLabels).not.toContain('登记字段')
    wrapper.unmount()
  })

  it('keeps object evidence independent from column evidence and exposes both authoring entries', async () => {
    const wrapper = mount(DatabaseObjectDetailView, { global: { stubs } })
    await flushPromises()

    expect(getEvidenceList).toHaveBeenCalledWith('DatabaseObject', 45)
    expect(wrapper.text()).toContain('1 条对象级证据')
    expect(wrapper.text()).toContain('字段证据继续在对应字段详情中独立维护')
    expect(wrapper.get('[data-test="progression"]').text()).toBe('DatabaseObject|1|1|Inferred')

    const buttons = wrapper.findAll('button')
    await buttons.find((button) => button.text() === '添加证据')?.trigger('click')
    expect(overlayState.openDrawer).toHaveBeenLastCalledWith(expect.objectContaining({
      kind: 'add-evidence',
      payload: expect.objectContaining({ subject: { type: 'DatabaseObject', id: 45 } }),
    }))

    await buttons.find((button) => button.text() === '添加人工确认')?.trigger('click')
    expect(overlayState.openDrawer).toHaveBeenLastCalledWith(expect.objectContaining({
      kind: 'human-confirmation',
      payload: expect.objectContaining({ subject: { type: 'DatabaseObject', id: 45 } }),
    }))

    window.dispatchEvent(new Event('evidence:changed'))
    await flushPromises()
    expect(getEvidenceList).toHaveBeenCalledTimes(2)
    wrapper.unmount()
  })

  it('navigates only real breadcrumb ancestors and leaves the current object as text', async () => {
    const wrapper = mount(DatabaseObjectDetailView, { global: { stubs } })
    await flushPromises()
    const breadcrumb = wrapper.get('.database-breadcrumb')
    const links = breadcrumb.findAll('button')

    expect(links.map((link) => link.text())).toEqual(['数据库', 'MES', 'MES Oracle'])
    expect(breadcrumb.find('strong').text()).toBe('MES.TABLE_EQP')

    await links[0]?.trigger('click')
    expect(routerState.push).toHaveBeenLastCalledWith({ name: 'database-objects-list' })
    await links[1]?.trigger('click')
    expect(routerState.push).toHaveBeenLastCalledWith({ name: 'system-detail', params: { id: '12' } })
    await links[2]?.trigger('click')
    expect(routerState.push).toHaveBeenLastCalledWith({
      name: 'database-objects-list',
      query: { systemId: '12', databaseSourceId: '9' },
    })
    wrapper.unmount()
  })
})
