import { defineComponent, h } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../api/errors/ApiError'
import type { KnowledgeDocumentDetail } from '../../knowledge-documents/api/knowledgeDocumentContracts'
import { getKnowledgeDocument } from '../../knowledge-documents/api/knowledgeDocumentsApi'
import { addHumanConfirmation } from '../api/evidenceApi'
import AddHumanConfirmationDrawer from './AddHumanConfirmationDrawer.vue'

const actorState = vi.hoisted(() => ({
  canEdit: true,
  currentUser: {
    id: 9,
    displayName: '确认人',
    employeeNo: 'E009',
    departmentOrTeam: '知识组',
    jobTitle: '专家',
    knowledgeRoles: [],
  },
  initialize: vi.fn(),
  refreshCurrentUser: vi.fn(),
}))
const overlayState = vi.hoisted(() => ({ closeDrawer: vi.fn(), openDrawer: vi.fn() }))

vi.mock('../../../app/stores/actor', () => ({ useActorStore: () => actorState }))
vi.mock('../../../app/stores/overlays', () => ({ useOverlayStore: () => overlayState }))
vi.mock('element-plus', () => ({ ElMessage: { success: vi.fn() } }))
vi.mock('../api/evidenceApi', () => ({ addHumanConfirmation: vi.fn() }))
vi.mock('../../knowledge-documents/api/knowledgeDocumentsApi', () => ({
  getKnowledgeDocument: vi.fn(),
}))

const currentDocument: KnowledgeDocumentDetail = {
  id: 7,
  documentType: 'KnowledgeArticle',
  title: '确认上下文文档',
  summary: null,
  bodyMarkdown: '正文',
  lifecycleStatus: 'Draft',
  knowledgeStatus: 'Confirmed',
  createdByUserId: 9,
  createdByDisplayName: '确认人',
  updatedByUserId: 9,
  updatedByDisplayName: '确认人',
  createdAt: '2026-08-23T01:00:00Z',
  updatedAt: '2026-08-23T02:00:00Z',
  publishedAt: null,
  archivedAt: null,
  currentRevisionNumber: 3,
  latestPublishedRevisionNumber: null,
  confirmationCoverage: { state: 'ChangedSinceConfirmation', lastConfirmedRevisionNumber: 2 },
  concurrencyToken: 'current-token',
  canDelete: true,
}

const components = {
  ElButton: {
    props: { disabled: Boolean, loading: Boolean },
    emits: ['click'],
    template: '<button type="button" :disabled="disabled" @click="$emit(\'click\')"><slot /></button>',
  },
  ElAlert: { template: '<div><span>{{ $attrs.title }}</span><slot /></div>' },
  ElIcon: { template: '<span><slot /></span>' },
  ElForm: defineComponent({
    setup(_props, { slots, expose }) {
      expose({ validate: () => Promise.resolve(true) })
      return () => h('form', slots.default?.())
    },
  }),
  ElFormItem: { template: '<label><slot /></label>' },
  ElInput: {
    props: { modelValue: String },
    emits: ['update:modelValue', 'input'],
    template: '<textarea :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value); $emit(\'input\', $event.target.value)" />',
  },
  ElDatePicker: {
    props: { modelValue: String },
    emits: ['update:modelValue'],
    template: '<input :value="modelValue" />',
  },
  ElSelect: { template: '<select><slot /></select>' },
  ElOption: { template: '<option />' },
  KnowledgeStatusBadge: { template: '<span>知识状态</span>' },
}

function mountDrawer() {
  return mount(AddHumanConfirmationDrawer, {
    props: {
      payload: {
        subject: { type: 'KnowledgeDocument', id: 7 },
        title: '知识文章 · 确认上下文文档',
        knowledgeStatus: 'Confirmed',
        subjectRevisionNumber: 2,
      },
    },
    global: { components },
  })
}

async function fillFacts(wrapper: ReturnType<typeof mountDrawer>): Promise<void> {
  const fields = wrapper.findAll('textarea')
  await fields[0].setValue('确认当前文档内容正确。')
  await fields[1].setValue('专家已复核当前展示的完整内容。')
}

function button(wrapper: ReturnType<typeof mountDrawer>, label: string) {
  return wrapper.findAll('button').find((item) => item.text() === label)
}

describe('AddHumanConfirmationDrawer revision context', () => {
  beforeEach(() => {
    vi.mocked(addHumanConfirmation).mockReset()
    vi.mocked(getKnowledgeDocument).mockReset()
    overlayState.closeDrawer.mockReset()
    overlayState.openDrawer.mockReset()
    actorState.initialize.mockReset()
  })

  it('sends the current displayed revision as non-editable confirmation context', async () => {
    vi.mocked(addHumanConfirmation).mockResolvedValue({
      id: 81,
      evidenceType: 'HumanConfirmation',
      subject: { type: 'KnowledgeDocument', id: 7 },
      subjectDetailKey: null,
      knowledgeDocumentRevisionNumberSnapshot: 2,
      sourceTitle: '确认上下文文档',
      subjectKnowledgeStatus: 'Confirmed',
      knowledgeStatusChanged: false,
      concurrencyToken: 'evidence-token',
    })
    const wrapper = mountDrawer()
    await fillFacts(wrapper)
    expect(wrapper.text()).toContain('本次人工确认将覆盖当前显示的修订 2')
    await button(wrapper, '保存人工确认')?.trigger('click')
    await flushPromises()

    expect(addHumanConfirmation).toHaveBeenCalledTimes(1)
    expect(addHumanConfirmation).toHaveBeenCalledWith(expect.objectContaining({
      subject: { type: 'KnowledgeDocument', id: 7 },
      subjectRevisionNumber: 2,
      confirmationStatement: '确认当前文档内容正确。',
      supportReason: '专家已复核当前展示的完整内容。',
    }))
  })

  it('keeps fact fields on stale conflict, reloads the revision, and never auto-retries', async () => {
    vi.mocked(addHumanConfirmation)
      .mockRejectedValueOnce(new ApiError(409, {
        code: 'conflict',
        message: 'stale revision',
        fieldErrors: null,
        details: { currentRevisionNumber: 3 },
      }))
      .mockResolvedValueOnce({
        id: 82,
        evidenceType: 'HumanConfirmation',
        subject: { type: 'KnowledgeDocument', id: 7 },
        subjectDetailKey: null,
        knowledgeDocumentRevisionNumberSnapshot: 3,
        sourceTitle: '确认上下文文档',
        subjectKnowledgeStatus: 'Confirmed',
        knowledgeStatusChanged: false,
        concurrencyToken: 'evidence-token-2',
      })
    vi.mocked(getKnowledgeDocument).mockResolvedValue(currentDocument)
    const refreshedListener = vi.fn()
    window.addEventListener('knowledge-document:current-refreshed', refreshedListener)
    const wrapper = mountDrawer()
    await fillFacts(wrapper)
    await button(wrapper, '保存人工确认')?.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('当前修订已变化，请重新加载最新内容后再次明确确认')
    expect(addHumanConfirmation).toHaveBeenCalledTimes(1)
    expect(wrapper.findAll('textarea')[0].element.value).toBe('确认当前文档内容正确。')
    expect(wrapper.findAll('textarea')[1].element.value).toBe('专家已复核当前展示的完整内容。')

    await button(wrapper, '重新加载最新内容')?.trigger('click')
    await flushPromises()
    expect(getKnowledgeDocument).toHaveBeenCalledWith(7)
    expect(wrapper.text()).toContain('已重新加载当前修订 3，请再次明确确认最新内容')
    expect(wrapper.text()).toContain('本次人工确认将覆盖当前显示的修订 3')
    expect(addHumanConfirmation).toHaveBeenCalledTimes(1)
    expect(refreshedListener).toHaveBeenCalledTimes(1)

    await button(wrapper, '保存人工确认')?.trigger('click')
    await flushPromises()
    expect(addHumanConfirmation).toHaveBeenCalledTimes(2)
    expect(addHumanConfirmation).toHaveBeenLastCalledWith(expect.objectContaining({
      subjectRevisionNumber: 3,
      confirmationStatement: '确认当前文档内容正确。',
      supportReason: '专家已复核当前展示的完整内容。',
    }))
    window.removeEventListener('knowledge-document:current-refreshed', refreshedListener)
  })

  it('does not emit Evidence or Detail refresh events when the confirmation save fails', async () => {
    vi.mocked(addHumanConfirmation).mockRejectedValue(new ApiError(400, {
      code: 'validation_error',
      message: '确认信息无效。',
      fieldErrors: null,
      details: null,
    }))
    const evidenceChanged = vi.fn()
    const confirmationChanged = vi.fn()
    window.addEventListener('evidence:changed', evidenceChanged)
    window.addEventListener('human-confirmation:changed', confirmationChanged)

    try {
      const wrapper = mountDrawer()
      await fillFacts(wrapper)
      await button(wrapper, '保存人工确认')?.trigger('click')
      await flushPromises()

      expect(wrapper.text()).toContain('确认信息无效。')
      expect(evidenceChanged).not.toHaveBeenCalled()
      expect(confirmationChanged).not.toHaveBeenCalled()
      expect(getKnowledgeDocument).not.toHaveBeenCalled()
      expect(overlayState.openDrawer).not.toHaveBeenCalled()
    } finally {
      window.removeEventListener('evidence:changed', evidenceChanged)
      window.removeEventListener('human-confirmation:changed', confirmationChanged)
    }
  })
})
