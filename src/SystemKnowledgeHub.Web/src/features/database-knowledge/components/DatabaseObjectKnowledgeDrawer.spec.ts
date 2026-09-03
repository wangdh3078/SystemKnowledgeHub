import { mount, flushPromises } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../api/errors/ApiError'
import { getDatabaseObjectDetail, updateDatabaseObjectKnowledge } from '../api/databaseKnowledgeApi'
import DatabaseObjectKnowledgeDrawer from './DatabaseObjectKnowledgeDrawer.vue'

const overlay = vi.hoisted(() => ({ requestDrawerClose: vi.fn(), closeDrawer: vi.fn() }))
vi.mock('../../../app/stores/overlays', () => ({ useOverlayStore: () => overlay }))
vi.mock('../../../app/stores/actor', () => ({
  useActorStore: () => ({ actor: { displayName: '编辑者', role: null } }),
}))
vi.mock('../api/databaseKnowledgeApi', async () => {
  const actual = await vi.importActual<typeof import('../api/databaseKnowledgeApi')>(
    '../api/databaseKnowledgeApi',
  )
  return { ...actual, getDatabaseObjectDetail: vi.fn(), updateDatabaseObjectKnowledge: vi.fn() }
})

const detail = {
  id: 45,
  system: { id: 1, name: 'MES' },
  databaseSource: {
    id: 2,
    name: 'MES 数据库',
    engine: 'Oracle',
    concurrencyToken: 'source',
    canDelete: false,
  },
  overview: {
    qualifiedName: 'MES.TABLE_EQP',
    objectType: 'Table' as const,
    businessDescription: null,
    accessMode: 'Read' as const,
    knowledgeStatus: 'Unknown' as const,
  },
  metadata: { estimatedRows: 48000, primaryKeyColumns: ['ID'], businessKeyColumns: [] },
  columns: [],
  contextRail: {
    usedByFunctions: [],
    relatedRuleCount: 0,
    integrationCount: 0,
    openUnknownCount: 0,
  },
  selectedColumnDrawer: null,
  availableActions: [],
  concurrencyToken: 'token',
  canDelete: false,
}

const stubs = {
  ElButton: { template: '<button><slot /></button>' },
  ElIcon: { template: '<span><slot /></span>' },
  ElForm: { template: '<form><slot /></form>' },
  ElFormItem: {
    props: ['label', 'error'],
    template:
      '<label>{{ label }}<slot /><span v-if="error" role="alert">{{ error }}</span></label>',
  },
  ElInput: { template: '<input />' },
  ElInputNumber: {
    props: ['modelValue'],
    emits: ['update:modelValue', 'change'],
    template:
      '<input type="number" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value === \'\' ? undefined : Number($event.target.value))" @change="$emit(\'change\')" />',
  },
  ElSelect: { template: '<select><slot /></select>' },
  ElOption: true,
  ErrorState: true,
  LoadingState: true,
}

describe('DatabaseObjectKnowledgeDrawer', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getDatabaseObjectDetail).mockResolvedValue(detail)
    vi.mocked(updateDatabaseObjectKnowledge).mockResolvedValue({
      id: 45,
      businessDescription: null,
      estimatedRows: 52000,
      accessMode: 'Read',
      businessKeyColumns: [],
      knowledgeStatus: 'Unknown',
      concurrencyToken: 'token-2',
    })
  })

  it('shows the current optional estimated row count as an editable value', async () => {
    const wrapper = mount(DatabaseObjectKnowledgeDrawer, {
      props: { databaseObjectId: 45 },
      global: { stubs },
    })
    await flushPromises()

    expect(wrapper.text()).toContain('估算行数（可选）')
    const input = wrapper.get('input[type="number"]')
    expect(input.attributes('value')).toBe('48000')
    expect(input.attributes()).not.toHaveProperty('disabled')
  })

  it('sends a changed estimated row count in the existing C11 request and signals detail refresh', async () => {
    const changed = vi.fn()
    window.addEventListener('database-object:changed', changed)
    const wrapper = mount(DatabaseObjectKnowledgeDrawer, {
      props: { databaseObjectId: 45 },
      global: { stubs },
    })
    await flushPromises()

    await wrapper.get('input[type="number"]').setValue('52000')
    await wrapper
      .findAll('button')
      .find((button) => button.text() === '保存业务知识')
      ?.trigger('click')
    await flushPromises()

    expect(updateDatabaseObjectKnowledge).toHaveBeenCalledWith(
      45,
      expect.objectContaining({ estimatedRows: 52000, concurrencyToken: 'token' }),
    )
    expect(changed).toHaveBeenCalledOnce()
    expect(overlay.closeDrawer).toHaveBeenCalledOnce()
    window.removeEventListener('database-object:changed', changed)
  })

  it('sends null when the estimated row count is cleared', async () => {
    const wrapper = mount(DatabaseObjectKnowledgeDrawer, {
      props: { databaseObjectId: 45 },
      global: { stubs },
    })
    await flushPromises()

    await wrapper.get('input[type="number"]').setValue('')
    await wrapper
      .findAll('button')
      .find((button) => button.text() === '保存业务知识')
      ?.trigger('click')
    await flushPromises()

    expect(updateDatabaseObjectKnowledge).toHaveBeenCalledWith(
      45,
      expect.objectContaining({ estimatedRows: null }),
    )
  })

  it.each(['-1', '1.5', '9007199254740992'])(
    'rejects invalid estimated row input %s without sending or converting it',
    async (value) => {
      const wrapper = mount(DatabaseObjectKnowledgeDrawer, {
        props: { databaseObjectId: 45 },
        global: { stubs },
      })
      await flushPromises()

      await wrapper.get('input[type="number"]').setValue(value)
      await wrapper
        .findAll('button')
        .find((button) => button.text() === '保存业务知识')
        ?.trigger('click')
      await flushPromises()

      expect(updateDatabaseObjectKnowledge).not.toHaveBeenCalled()
      expect(wrapper.get('[role="alert"]').text()).toContain('0 至 9007199254740991')
      expect(wrapper.get<HTMLInputElement>('input[type="number"]').element.value).toBe(value)
    },
  )

  it('keeps the user input when an existing concurrency conflict is returned', async () => {
    vi.mocked(updateDatabaseObjectKnowledge).mockRejectedValueOnce(
      new ApiError(409, {
        code: 'conflict',
        message: '数据库对象已被其他操作更新，请刷新后重试。',
        fieldErrors: { concurrencyToken: ['并发令牌已过期。'] },
        details: null,
      }),
    )
    const wrapper = mount(DatabaseObjectKnowledgeDrawer, {
      props: { databaseObjectId: 45 },
      global: { stubs },
    })
    await flushPromises()

    await wrapper.get('input[type="number"]').setValue('53000')
    await wrapper
      .findAll('button')
      .find((button) => button.text() === '保存业务知识')
      ?.trigger('click')
    await flushPromises()

    expect(wrapper.get<HTMLInputElement>('input[type="number"]').element.value).toBe('53000')
    expect(wrapper.text()).toContain('数据库对象已被其他操作更新，请刷新后重试。')
    expect(overlay.closeDrawer).not.toHaveBeenCalled()
  })
})
