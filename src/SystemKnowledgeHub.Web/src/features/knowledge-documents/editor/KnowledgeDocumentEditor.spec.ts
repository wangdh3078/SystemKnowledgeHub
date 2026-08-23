import { flushPromises, mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import KnowledgeDocumentEditor from './KnowledgeDocumentEditor.vue'

const components = {
  ElButton: {
    inheritAttrs: false,
    template: '<button type="button" v-bind="$attrs" @click="$emit(\'click\')"><slot /></button>',
  },
  ElTooltip: { template: '<span><slot /></span>' },
  ElIcon: { template: '<span><slot /></span>' },
}

describe('KnowledgeDocumentEditor', () => {
  it('initializes Milkdown from Markdown and exposes the focused toolbar', async () => {
    const wrapper = mount(KnowledgeDocumentEditor, {
      props: { modelValue: '# 标题\n\n正文' },
      global: { components },
    })

    await flushPromises()
    await new Promise((resolve) => setTimeout(resolve, 30))

    expect(wrapper.get('[aria-label="二级标题"]').text()).toContain('H2')
    expect(wrapper.find('[aria-label="粗体"]').exists()).toBe(true)
    expect(wrapper.find('[aria-label="表格"]').exists()).toBe(true)
    expect(wrapper.emitted('ready')?.[0]?.[0]).toContain('# 标题')
    expect(wrapper.find('.ProseMirror').exists()).toBe(true)
  })
})
