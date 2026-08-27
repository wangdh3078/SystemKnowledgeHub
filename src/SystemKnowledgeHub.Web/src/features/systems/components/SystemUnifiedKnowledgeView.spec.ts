import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import SystemUnifiedKnowledgeView from './SystemUnifiedKnowledgeView.vue'
import type { SystemKnowledgeView } from '../api/systemKnowledgeViewContracts'
import { formatDateTime } from '../../../app/formatters/dateTime'

const populated: SystemKnowledgeView = {
  systemId: 12,
  overview: { businessFunctionCount: 1, databaseObjectCount: 1, businessRuleCount: 1, integrationCount: 1, documentCount: 1, evidenceCount: 1, openUnknownItemCount: 1 },
  businessFunctions: [{ id: 1, title: '设备状态查询', description: '查询状态', knowledgeStatus: 'Confirmed' }],
  databaseObjects: [{ id: 2, title: 'MES.TABLE_EQP', description: null, knowledgeStatus: 'Inferred' }],
  businessRules: [{ id: 3, title: '状态规则', description: '规则说明', knowledgeStatus: 'Unknown' }],
  integrations: [{ id: 4, name: 'MES 同步', integrationType: 'HttpApi', direction: 'OneWay', relatedParty: 'ERP', knowledgeStatus: 'Inferred' }],
  documents: [{ id: 5, documentType: 'Sop', title: '设备操作规程', lifecycleStatus: 'Published', knowledgeStatus: 'Confirmed', updatedAt: '2026-08-22T00:00:00Z', relationTypes: ['AppliesTo', 'Documents'] }],
  relationships: [],
  evidence: [{ id: 6, evidenceType: 'CodeReference', sourceTitle: 'MES 服务代码', summary: '可定位实现', providedAt: '2026-08-22T00:00:00Z' }],
  unknownItems: [{ id: 7, itemCode: 'U-01', question: '状态来源？', priority: 'High', status: 'Open', updatedAt: '2026-08-22T00:00:00Z' }],
}

const stubs = {
  KnowledgeStatusBadge: { template: '<span><slot /></span>' },
  EmptyState: { props: ['title'], template: '<p>{{ title }}</p>' },
  LoadingState: { props: ['message'], template: '<p>{{ message }}</p>' },
}

describe('SystemUnifiedKnowledgeView', () => {
  it('shows a compact loading state and independent error state', () => {
    expect(mount(SystemUnifiedKnowledgeView, { props: { view: null, loading: true, error: null }, global: { stubs } }).text()).toContain('正在汇总系统知识')
    expect(mount(SystemUnifiedKnowledgeView, { props: { view: null, loading: false, error: '读取失败' }, global: { stubs } }).text()).toContain('读取失败')
  })

  it('shows aggregated counts, documents and bounded section content', async () => {
    const wrapper = mount(SystemUnifiedKnowledgeView, { props: { view: populated, loading: false, error: null }, global: { stubs } })
    expect(wrapper.text()).toContain('只读投影')
    expect(wrapper.text()).toContain('设备操作规程')
    expect(wrapper.text()).toContain('仅显示已建立关系的文档')
    expect(wrapper.text()).toContain(formatDateTime(populated.documents[0].updatedAt))
    expect(wrapper.text()).toContain(formatDateTime(populated.unknownItems[0].updatedAt))
    await wrapper.get('button').trigger('click')
    expect(wrapper.emitted('openBusinessFunction')).toEqual([[1]])
  })

  it('keeps empty sections explicit', () => {
    const empty: SystemKnowledgeView = { ...populated, businessFunctions: [], databaseObjects: [], businessRules: [], integrations: [], documents: [], relationships: [], evidence: [], unknownItems: [] }
    const wrapper = mount(SystemUnifiedKnowledgeView, { props: { view: empty, loading: false, error: null }, global: { stubs } })
    expect(wrapper.text()).toContain('暂无关联知识内容')
    expect(wrapper.text()).toContain('暂无待确认事项')
  })
})
