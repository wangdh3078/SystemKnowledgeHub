import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useOverlayStore } from '../../../app/stores/overlays'
import { changeKnowledgeStatus } from '../api/knowledgeStatusApi'
import KnowledgeStatusDialogContent from './KnowledgeStatusDialogContent.vue'

vi.mock('../api/knowledgeStatusApi', () => ({ changeKnowledgeStatus: vi.fn() }))

describe('KnowledgeStatusDialogContent', () => {
  beforeEach(() => vi.mocked(changeKnowledgeStatus).mockReset().mockResolvedValue({
    target: { type: 'System', id: 1 },
    previousStatus: 'Unknown',
    knowledgeStatus: 'Inferred',
    reason: null,
    changedAt: '2026-08-28T00:00:00Z',
    concurrencyToken: 'next-token',
  }))

  it('does not collect an unreadable forward-transition note and submits null', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    useOverlayStore().openDialog({
      kind: 'change-knowledge-status',
      id: 1,
      mode: 'edit',
      payload: {
        target: { type: 'System', id: 1 },
        title: 'MES',
        knowledgeStatus: 'Unknown',
        concurrencyToken: 'token',
        evidenceCount: 1,
        humanConfirmationCount: 0,
      },
    })
    const wrapper = mount(KnowledgeStatusDialogContent, {
      global: {
        plugins: [pinia],
        stubs: {
          ElButton: { emits: ['click'], template: '<button @click="$emit(\'click\')"><slot /></button>' },
          ElIcon: { template: '<span><slot /></span>' },
          KnowledgeStatusBadge: { template: '<span />' },
        },
      },
    })

    expect(wrapper.text()).not.toContain('修改说明')
    const submit = wrapper.findAll('button').find(button => button.text().includes('确认推进为'))
    await submit?.trigger('click')
    await flushPromises()
    expect(changeKnowledgeStatus).toHaveBeenCalledWith(expect.objectContaining({ reason: null }))
  })
})
