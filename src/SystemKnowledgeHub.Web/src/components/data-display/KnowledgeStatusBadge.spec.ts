import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import KnowledgeStatusBadge from './KnowledgeStatusBadge.vue'

describe('KnowledgeStatusBadge', () => {
  it.each([
    ['Unknown', '未知'],
    ['Inferred', '推断'],
    ['Confirmed', '已确认'],
  ] as const)('maps %s to the frozen Chinese UI label', (status, label) => {
    const wrapper = mount(KnowledgeStatusBadge, { props: { status } })
    expect(wrapper.text()).toBe(label)
    expect(wrapper.classes()).toContain(`knowledge-status-badge--${status.toLowerCase()}`)
  })
})
