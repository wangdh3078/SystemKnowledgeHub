import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'
import CreateKnowledgeObjectChooser from './CreateKnowledgeObjectChooser.vue'

describe('CreateKnowledgeObjectChooser', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('offers KnowledgeDocument with the existing global structured-knowledge choices and emits the reuse handoff', async () => {
    const wrapper = mount(CreateKnowledgeObjectChooser, {
      props: {
        enabledKinds: ['system', 'knowledge-document'],
      },
      global: {
        stubs: { ElIcon: { template: '<span><slot /></span>' } },
      },
    })

    const choice = wrapper.get('button[title="新增知识内容"]')
    expect(choice.text()).toContain('记录需求、规格、测试用例、SOP、故障排查和知识文章')
    await choice.trigger('click')
    expect(wrapper.emitted('chooseKnowledgeDocument')).toHaveLength(1)
  })

  it('does not enable KnowledgeDocument creation when the caller does not grant create access', () => {
    const wrapper = mount(CreateKnowledgeObjectChooser, {
      props: { enabledKinds: ['system'] },
      global: { stubs: { ElIcon: { template: '<span><slot /></span>' } } },
    })

    expect(wrapper.get('button[title="请从对应知识对象进入知识内容维护"]').attributes('disabled')).toBeDefined()
  })
})
