import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useOverlayStore } from '../../../app/stores/overlays'
import { getSystemsList } from '../../systems/api/systemsApi'
import CreateBusinessRuleFlow from './CreateBusinessRuleFlow.vue'

vi.mock('../../systems/api/systemsApi', () => ({ getSystemsList: vi.fn() }))

const firstSystem = {
  id: 1,
  name: 'MES',
  displayName: '制造执行系统',
} as never
const secondSystem = {
  id: 2,
  name: 'ERP',
  displayName: '企业资源计划',
} as never

describe('CreateBusinessRuleFlow authoritative system options', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(getSystemsList).mockReset()
    document.body.innerHTML = '<div id="dialog-feature-content"></div>'
  })

  it('reloads systems on every open and does not retain the previous option set', async () => {
    vi.mocked(getSystemsList)
      .mockResolvedValueOnce({ items: [firstSystem] } as never)
      .mockResolvedValueOnce({ items: [firstSystem, secondSystem] } as never)
    const overlays = useOverlayStore()
    const wrapper = mount(CreateBusinessRuleFlow, {
      global: {
        stubs: {
          Teleport: true,
          CreateBusinessRuleDialog: {
            name: 'CreateBusinessRuleDialog',
            props: ['systems'],
            template: '<div data-test="systems">{{ systems.map((item) => item.name).join(",") }}</div>',
          },
        },
      },
    })

    overlays.openDialog({ kind: 'create-business-rule', id: null, mode: 'create' })
    await flushPromises()
    expect(wrapper.get('[data-test="systems"]').text()).toBe('MES')

    overlays.closeDialog()
    await flushPromises()
    overlays.openDialog({ kind: 'create-business-rule', id: null, mode: 'create' })
    await flushPromises()

    expect(getSystemsList).toHaveBeenCalledTimes(2)
    expect(wrapper.get('[data-test="systems"]').text()).toBe('MES,ERP')
  })
})
