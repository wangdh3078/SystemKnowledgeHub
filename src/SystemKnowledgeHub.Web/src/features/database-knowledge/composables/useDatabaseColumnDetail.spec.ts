import { flushPromises, mount } from '@vue/test-utils'
import { defineComponent, toRef } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getDatabaseColumnDetail } from '../api/databaseKnowledgeApi'
import { useDatabaseColumnDetail } from './useDatabaseColumnDetail'

vi.mock('../api/databaseKnowledgeApi', () => ({
  getDatabaseColumnDetail: vi.fn(),
}))

const detail = {
  id: 123,
  parent: { databaseObjectId: 45, qualifiedName: 'MES.TABLE_EQP' },
  system: { id: 12, name: 'MES' },
  concurrencyToken: 'v1_AAAAAAAAAAE',
  canDelete: true,
  databaseMetadata: {
    columnName: 'STATE_FLAG', dataType: 'VARCHAR2(2)', nullable: true,
    defaultValue: null, ordinalPosition: 3,
  },
  businessKnowledge: { description: '设备运行状态标志', knowledgeStatus: 'Inferred' as const },
  knownValues: [{ id: 703, value: '30', meaning: 'Unknown / Offline' }],
  evidence: [],
  relations: [],
  unknownItems: [],
  availableActions: [],
}

const Host = defineComponent({
  props: { columnId: { type: Number, required: true } },
  setup(props) {
    return useDatabaseColumnDetail(toRef(props, 'columnId'))
  },
  template: '<div>{{ detail?.databaseMetadata.columnName }} {{ errorMessage }}</div>',
})

describe('useDatabaseColumnDetail', () => {
  beforeEach(() => vi.mocked(getDatabaseColumnDetail).mockReset())

  it('loads the drawer-local response by column id', async () => {
    vi.mocked(getDatabaseColumnDetail).mockResolvedValue(detail)
    const wrapper = mount(Host, { props: { columnId: 123 } })
    await flushPromises()

    expect(getDatabaseColumnDetail).toHaveBeenCalledWith(123, expect.any(AbortSignal))
    expect(wrapper.text()).toContain('STATE_FLAG')
    wrapper.unmount()
  })
  it('late column A cannot replace B after the drawer selection changes', async () => {
    let completeA!: (value: typeof detail) => void
    vi.mocked(getDatabaseColumnDetail).mockImplementationOnce(() => new Promise(resolve => { completeA = resolve }))
      .mockResolvedValueOnce({ ...detail, id: 124, databaseMetadata: { ...detail.databaseMetadata, columnName: 'CURRENT_B' } })
    const wrapper = mount(Host, { props: { columnId: 123 } })
    await wrapper.setProps({ columnId: 124 }); await flushPromises()
    completeA(detail); await flushPromises()
    expect(wrapper.text()).toContain('CURRENT_B')
    expect(wrapper.text()).not.toContain('STATE_FLAG')
    wrapper.unmount()
  })

})
