import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import SystemContextRail from './SystemContextRail.vue'

describe('SystemContextRail static summaries', () => {
  it('keeps related system names readable without promising navigation', () => {
    const wrapper = mount(SystemContextRail, {
      props: {
        systemName: 'DEMO_EAP',
        context: {
          relatedSystems: [
            { id: 1, name: 'DEMO_MES' },
            { id: 3, name: 'DEMO_OCS' },
          ],
          integrationCount: 2,
          mainDatabase: null,
          highPriorityUnknownCount: 0,
          knowledgeGaps: [],
        },
      },
      global: { stubs: { ElIcon: { template: '<span><slot /></span>' } } },
    })
    const list = wrapper.get('.system-rail-list')
    expect(list.findAll('li').map((row) => row.text())).toEqual(['DEMO_MES', 'DEMO_OCS'])
    expect(list.find('svg, button, a, [role="button"], [tabindex]').exists()).toBe(false)
    wrapper.unmount()
  })
})
