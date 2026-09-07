import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getEvidenceDetail, withdrawHumanConfirmation } from '../api/evidenceApi'
import type { EvidenceDetailResponse } from '../api/evidenceContracts'
import EvidenceDetailDrawer from './EvidenceDetailDrawer.vue'

const actor = vi.hoisted(() => ({ canEdit: true }))
vi.mock('../../../app/stores/actor', () => ({ useActorStore: () => actor }))
vi.mock('element-plus', () => ({ ElMessage: { success: vi.fn() } }))
vi.mock('../api/evidenceApi', () => ({
  getEvidenceDetail: vi.fn(),
  updateEvidence: vi.fn(),
  withdrawHumanConfirmation: vi.fn(),
}))

const deletedEvidence: EvidenceDetailResponse = {
  id: 41,
  concurrencyToken: 'evidence-token',
  evidenceType: 'ExistingDocument',
  subject: { type: 'System', id: 17 },
  subjectIdentity: {
    id: 17,
    targetType: 'System',
    displayName: 'Legacy MES',
    isDeleted: true,
    isNavigable: false,
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
    displayName: '历史确认人',
    roleOrIdentity: 'Owner',
    occurredAt: '2026-08-20T01:00:00Z',
    team: null,
    externalUserKey: null,
    source: 'Manual',
    note: null,
  },
  subjectContext: null,
  availableActions: ['UpdateEvidence'],
}

describe('EvidenceDetailDrawer historical subject', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    actor.canEdit = true
    vi.mocked(withdrawHumanConfirmation).mockReset()
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

describe('HumanConfirmation lifecycle controls', () => {
  const active = {
    ...deletedEvidence,
    evidenceType: 'HumanConfirmation' as const,
    humanConfirmationLifecycle: {
      status: 'Active' as const,
      withdrawnAt: null,
      withdrawnByDisplayName: null,
      withdrawalReason: null,
      replacesHumanConfirmationId: null,
      replacedByHumanConfirmationId: null,
    },
  }
  function render() {
    return mount(EvidenceDetailDrawer, {
      props: { evidenceId: 41 },
      global: {
        components: {
          ElButton: { template: '<button><slot /></button>' },
          ElDialog: {
            props: ['modelValue'],
            template: '<div v-if="modelValue"><slot /><slot name="footer" /></div>',
          },
          ElInput: {
            props: ['modelValue'],
            emits: ['update:modelValue'],
            template:
              '<textarea :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
          },
        },
        stubs: { KnowledgeStatusBadge: true },
      },
    })
  }
  beforeEach(() => {
    setActivePinia(createPinia())
    actor.canEdit = true
    vi.mocked(getEvidenceDetail).mockResolvedValue(active)
  })
  it('allows withdrawal on a deleted subject but never editing or reconfirmation', async () => {
    const wrapper = render()
    await flushPromises()
    expect(wrapper.text()).toContain('撤销确认')
    expect(wrapper.text()).not.toContain('纠正记录')
    expect(wrapper.text()).not.toContain('重新确认')
    wrapper.unmount()
  })
  it('hides withdrawal for Viewer', async () => {
    actor.canEdit = false
    const wrapper = render()
    await flushPromises()
    expect(wrapper.text()).not.toContain('撤销确认')
    wrapper.unmount()
  })
  it('submits only trimmed reason and token then preserves original facts with withdrawal audit', async () => {
    const wrapper = render()
    await flushPromises()
    await wrapper
      .findAll('button')
      .find((b) => b.text() === '撤销确认')!
      .trigger('click')
    await wrapper.find('textarea').setValue('  原确认结论存在业务口径错误  ')
    vi.mocked(withdrawHumanConfirmation).mockResolvedValue({ id: 41 })
    vi.mocked(getEvidenceDetail).mockResolvedValue({
      ...active,
      humanConfirmationLifecycle: {
        ...active.humanConfirmationLifecycle,
        status: 'Withdrawn',
        withdrawnAt: '2026-09-07T01:00:00Z',
        withdrawnByDisplayName: '撤销人',
        withdrawalReason: '原确认结论存在业务口径错误',
      },
    })
    await wrapper
      .findAll('button')
      .find((b) => b.text() === '确认撤销')!
      .trigger('click')
    await flushPromises()
    expect(withdrawHumanConfirmation).toHaveBeenCalledWith(41, {
      reason: '原确认结论存在业务口径错误',
      concurrencyToken: 'evidence-token',
    })
    expect(wrapper.text()).toContain('已撤销')
    expect(wrapper.text()).toContain('保留历史事实')
    expect(wrapper.text()).not.toContain('撤销确认')
    wrapper.unmount()
  })
})
