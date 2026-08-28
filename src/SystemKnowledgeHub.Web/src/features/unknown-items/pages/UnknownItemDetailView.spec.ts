import { createPinia, setActivePinia } from 'pinia'
import { ref } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useUnknownItemDetail } from '../composables/useUnknownItemDetail'
import type { UnknownItemDetailResponse } from '../api/unknownItemContracts'
import UnknownItemDetailView from './UnknownItemDetailView.vue'

const route = { params: { id: '88' } }
const router = { push: vi.fn() }
vi.mock('vue-router', () => ({ useRoute: () => route, useRouter: () => router }))
vi.mock('element-plus', () => ({ ElMessage: { success: vi.fn(), error: vi.fn(), warning: vi.fn() }, ElMessageBox: { confirm: vi.fn(), prompt: vi.fn() } }))
vi.mock('../composables/useUnknownItemDetail', () => ({ useUnknownItemDetail: vi.fn() }))

const identity = {
  id: 17, targetType: 'System', displayName: 'Legacy MES', isDeleted: true, isNavigable: false,
}
const detail: UnknownItemDetailResponse = {
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

describe('UnknownItemDetailView historical targets', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    router.push.mockReset()
    vi.mocked(useUnknownItemDetail).mockReturnValue({
      detail: ref(detail), loading: ref(false), saving: ref(false), error: ref(null),
      load: vi.fn().mockResolvedValue(undefined), run: vi.fn().mockResolvedValue(false),
      person: vi.fn(() => ({
        displayName: 'Tester', roleOrIdentity: 'Tester', occurredAt: '2026-08-21T01:00:00Z',
        team: null, externalUserKey: null, source: 'Manual', note: null,
      })),
    })
    document.body.innerHTML = '<div id="context-rail-content"></div>'
  })

  it('renders closed workflow and applied-update tombstones without links or mutation actions', async () => {
    const wrapper = mount(UnknownItemDetailView, {
      global: {
        components: {
          ElButton: { template: '<button type="button"><slot /></button>' },
          ElTag: { template: '<span><slot /></span>' },
        },
        stubs: {
          UnknownItemContextRail: true,
          ElInput: true, ElSelect: true, ElOption: true, ElForm: true, ElFormItem: true,
        },
      },
    })
    await flushPromises()

    expect(wrapper.text()).toContain('系统已经下线')
    expect(wrapper.text()).toContain('记录下线状态')
    expect(wrapper.findAll('.historical-target-label.is-deleted')).toHaveLength(3)
    expect(wrapper.find('a').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('应用知识更新')
    expect(wrapper.text()).not.toContain('重新打开待确认事项')
  })
})
