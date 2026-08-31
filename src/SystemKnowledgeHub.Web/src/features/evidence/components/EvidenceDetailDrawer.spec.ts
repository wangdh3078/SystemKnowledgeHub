import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getEvidenceDetail } from '../api/evidenceApi'
import type { EvidenceDetailResponse } from '../api/evidenceContracts'
import EvidenceDetailDrawer from './EvidenceDetailDrawer.vue'

vi.mock('element-plus', () => ({ ElMessage: { success: vi.fn() } }))
vi.mock('../api/evidenceApi', () => ({ getEvidenceDetail: vi.fn(), updateEvidence: vi.fn() }))

const deletedEvidence: EvidenceDetailResponse = {
  id: 41,
  concurrencyToken: 'evidence-token',
  evidenceType: 'ExistingDocument',
  subject: { type: 'System', id: 17 },
  subjectIdentity: {
    id: 17, targetType: 'System', displayName: 'Legacy MES', isDeleted: true, isNavigable: false,
  },
  subjectDetailKey: null,
  knowledgeDocumentRevisionNumberSnapshot: null,
  sourceTitle: '原始证据',
  sourceReference: 'ARCHIVE-41',
  sourceLocator: null,
  summary: '历史摘要',
  supportReason: '保留历史事实',
  confidence: 'High',
  provider: {
    displayName: '历史确认人', roleOrIdentity: 'Owner', occurredAt: '2026-08-20T01:00:00Z',
    team: null, externalUserKey: null, source: 'Manual', note: null,
  },
  subjectContext: null,
  availableActions: ['UpdateEvidence'],
}

describe('EvidenceDetailDrawer historical subject', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(getEvidenceDetail).mockReset()
    vi.mocked(getEvidenceDetail).mockResolvedValue(deletedEvidence)
  })

  it('shows a deleted subject tombstone while preserving evidence and hiding current mutations', async () => {
    const wrapper = mount(EvidenceDetailDrawer, {
      props: { evidenceId: 41 },
      global: {
        components: {
          ElButton: { template: '<button type="button"><slot /></button>' },
          ElIcon: { template: '<span><slot /></span>' },
          ElTag: { template: '<span><slot /></span>' },
        },
        stubs: { KnowledgeStatusBadge: true },
      },
    })
    await flushPromises()

    expect(wrapper.text()).toContain('Legacy MES')
    expect(wrapper.text()).toContain('已删除')
    expect(wrapper.text()).toContain('保留历史事实')
    expect(wrapper.find('a').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('纠正记录')
    expect(wrapper.text()).not.toContain('添加人工确认')
  })

  it('presents HumanConfirmation as labeled confirmation data rather than a tag-like action', async () => {
    vi.mocked(getEvidenceDetail).mockResolvedValue({
      ...deletedEvidence,
      id: 42,
      evidenceType: 'HumanConfirmation',
      knowledgeDocumentRevisionNumberSnapshot: 3,
      sourceTitle: '需求 R-01',
      sourceLocator: { confirmationMethod: 'InSystem' },
      summary: '确认',
      supportReason: '确认',
      provider: {
        ...deletedEvidence.provider,
        displayName: '本地管理员',
        roleOrIdentity: '知识提供者（未配置知识身份）',
        source: 'InSystem',
      },
    })
    const wrapper = mount(EvidenceDetailDrawer, {
      props: { evidenceId: 42 },
      global: {
        components: { ElButton: { template: '<button type="button"><slot /></button>' } },
        stubs: { KnowledgeStatusBadge: true },
      },
    })
    await flushPromises()

    expect(wrapper.text()).toContain('类型：人工确认')
    expect(wrapper.text()).toContain('人工确认 · 本地管理员')
    expect(wrapper.text()).toContain('确认结论')
    expect(wrapper.text()).toContain('支持理由')
    expect(wrapper.text()).toContain('确认方式')
    expect(wrapper.text()).toContain('确认人')
    expect(wrapper.text()).toContain('知识身份')
    expect(wrapper.text()).toContain('确认时间')
    expect(wrapper.text()).toContain('确认修订')
    expect(wrapper.findAll('.el-tag')).toHaveLength(0)
  })
})
