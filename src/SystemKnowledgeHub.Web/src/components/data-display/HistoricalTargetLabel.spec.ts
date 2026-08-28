import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import HistoricalTargetLabel from './HistoricalTargetLabel.vue'

const identity = {
  id: 17,
  targetType: 'System',
  displayName: 'Legacy MES',
  isDeleted: false,
  isNavigable: true,
}

function mountLabel(overrides: Partial<typeof identity> = {}) {
  return mount(HistoricalTargetLabel, {
    props: {
      identity: { ...identity, ...overrides },
      to: { name: 'system-detail', params: { id: '17' } },
    },
    global: {
      stubs: {
        RouterLink: { props: ['to'], template: '<a data-testid="target-link"><slot /></a>' },
        ElTag: { template: '<span data-testid="deleted-tag"><slot /></span>' },
      },
    },
  })
}

describe('HistoricalTargetLabel', () => {
  it('renders a deleted target as a non-interactive tombstone', () => {
    const wrapper = mountLabel({ isDeleted: true, isNavigable: false })

    expect(wrapper.text()).toContain('Legacy MES')
    expect(wrapper.text()).toContain('已删除')
    expect(wrapper.classes()).toContain('is-deleted')
    expect(wrapper.find('a').exists()).toBe(false)
    expect(wrapper.find('button').exists()).toBe(false)
  })

  it('preserves existing navigation for an active navigable target', () => {
    const wrapper = mountLabel()

    expect(wrapper.get('[data-testid="target-link"]').text()).toBe('Legacy MES')
    expect(wrapper.text()).not.toContain('已删除')
  })
})
