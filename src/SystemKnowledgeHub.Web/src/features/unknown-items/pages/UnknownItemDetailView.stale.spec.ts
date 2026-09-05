import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter, RouterView } from 'vue-router'
import { describe, expect, it, vi } from 'vitest'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import { unknownItemsApi } from '../api/unknownItemsApi'
import type { UnknownItemDetailResponse } from '../api/unknownItemContracts'
import UnknownItemDetailView from './UnknownItemDetailView.vue'
vi.mock('element-plus', () => ({ ElMessage: { success: vi.fn(), error: vi.fn(), warning: vi.fn() }, ElMessageBox: { confirm: vi.fn(), prompt: vi.fn() } }))
vi.mock('../api/unknownItemsApi', () => ({ unknownItemsApi: { detail: vi.fn(), start: vi.fn() } }))
const identity = {
  id: 17, targetType: 'System', displayName: 'Legacy MES', isDeleted: true, isNavigable: false,
}
const fixture: UnknownItemDetailResponse = {
  id: 88,
  itemCode: 'UNK-0088',
  system: { name: 'Legacy MES', ...identity },
  concurrencyToken: 'unknown-token',
  question: {
    text: '历史对象为何下线？', context: '保留调查事实', priority: 'Medium', status: 'Closed',
    createdAt: '2026-08-20T01:00:00Z', updatedAt: '2026-08-21T01:00:00Z',
  },
  relatedObjects: [{ target: { type: 'System', id: 17 }, display: 'Legacy MES', primary: true, identity }],
  findings: [],
  evidence: [],
  resolution: { id: 2, conclusion: '系统已经下线', confirmedBy: null, confirmedAt: null },
  knowledgeUpdates: [{
    id: 3, target: { type: 'System', id: 17 }, targetIdentity: identity, subjectDetailKey: null,
    changeSummary: '记录下线状态', before: {}, after: {}, status: 'Applied',
  }],
  activity: [],
  contextRail: { knowledgeImpact: [], evidenceCount: 0, openGapCount: 0 },
  availableActions: [],
}


describe('pending confirmation route selection', () => {
  it('A slow / B fast / A late keeps URL, visible detail, and actual action subject B', async () => {
    const pinia = createPinia(); setActivePinia(pinia)
    const actor = useActorStore()
    vi.spyOn(actor, 'canEdit', 'get').mockReturnValue(true)
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/unknown-items/:id', component: UnknownItemDetailView }] })
    let completeA!: (value: UnknownItemDetailResponse) => void
    const b = { ...fixture, id: 89, itemCode: 'UNK-B', question: { ...fixture.question, text: 'Current B', status: 'Open' as const }, availableActions: ['StartInvestigation'] }
    vi.mocked(unknownItemsApi.detail).mockImplementation(id => id === 88 ? new Promise(resolve => { completeA = resolve }) : Promise.resolve(b))
    vi.mocked(unknownItemsApi.start).mockRejectedValue(new Error('controlled failure'))
    document.body.innerHTML = '<div id="context-rail-content"></div>'
    await router.push('/unknown-items/88'); await router.isReady()
    const wrapper = mount(RouterView, { global: { plugins: [pinia, router], components: { ElButton: { template: '<button><slot /></button>' } }, stubs: { UnknownItemContextRail: true, ElInput: true, ElSelect: true, ElOption: true, ElForm: true, ElFormItem: true } } })
    await flushPromises()
    useOverlayStore().openDialog({ kind: 'old-action', id: 88, mode: 'edit' })
    await router.push('/unknown-items/89'); await flushPromises()
    expect(useOverlayStore().currentOverlay).toBeNull()
    completeA(fixture); await flushPromises()
    expect(router.currentRoute.value.path).toBe('/unknown-items/89')
    expect(wrapper.text()).toContain('Current B')
    expect(wrapper.text()).not.toContain(fixture.question.text)
    await wrapper.findAll('button').find(button => button.text() === '开始调查')!.trigger('click')
    await flushPromises()
    expect(unknownItemsApi.start).toHaveBeenCalledWith(89, expect.any(Object), b.concurrencyToken)
    wrapper.unmount()
  })
})
