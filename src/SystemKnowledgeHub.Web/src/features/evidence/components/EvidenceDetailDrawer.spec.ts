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
})
