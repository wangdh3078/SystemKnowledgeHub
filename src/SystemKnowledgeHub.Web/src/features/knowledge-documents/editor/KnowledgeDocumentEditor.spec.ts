import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElIcon,
  ElInput,
  ElInputNumber,
  ElOption,
  ElSelect,
  ElTooltip,
} from 'element-plus'
import { afterEach, describe, expect, it, vi } from 'vitest'
import KnowledgeDocumentEditor from './KnowledgeDocumentEditor.vue'

const wrappers: VueWrapper[] = []
const components = {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElIcon,
  ElInput,
  ElInputNumber,
  ElOption,
  ElSelect,
  ElTooltip,
}

afterEach(() => wrappers.splice(0).forEach((wrapper) => wrapper.unmount()))

function mountEditor(markdown = '## 标题\n\n正文'): VueWrapper {
  const wrapper = mount(KnowledgeDocumentEditor, {
    attachTo: document.body,
    props: { modelValue: markdown },
    global: {
      components,
      stubs: {
        KnowledgeDocumentMarkdown: {
          props: ['markdown'],
          template: '<div class="rendered">{{ markdown }}</div>',
        },
      },
    },
  })
  wrappers.push(wrapper)
  return wrapper
}

async function waitForSourceEditor(wrapper: VueWrapper): Promise<void> {
  await vi.waitFor(() => expect(wrapper.find('.cm-content').exists()).toBe(true))
  await flushPromises()
}

describe('KnowledgeDocumentEditor', () => {
  it('uses CodeMirror raw Markdown as the only authoring surface and removes color/save actions', async () => {
    const wrapper = mountEditor()
    await waitForSourceEditor(wrapper)

    expect(wrapper.get('.cm-content').text()).toContain('## 标题')
    expect(wrapper.get('.cm-content').text()).toContain('正文')
    expect(wrapper.find('.ProseMirror').exists()).toBe(false)
    expect(wrapper.find('[aria-label="文字颜色"]').exists()).toBe(false)
    expect(wrapper.find('[aria-label="背景颜色"]').exists()).toBe(false)
    expect(wrapper.find('[aria-label="清除文字颜色"]').exists()).toBe(false)
    expect(wrapper.find('[aria-label="清除背景颜色"]').exists()).toBe(false)
    expect(wrapper.find('[aria-label="保存"]').exists()).toBe(false)
    expect(wrapper.get('[aria-label="图片上传功能开发中"]').attributes('disabled')).toBeDefined()
  })

  it('emits the page-level save request from Ctrl/Cmd+S without a toolbar save control', async () => {
    const wrapper = mountEditor()
    await waitForSourceEditor(wrapper)

    await wrapper.get('.cm-content').trigger('keydown', { key: 's', ctrlKey: true })

    expect(wrapper.emitted('request-save')).toHaveLength(1)
  })

  it('keeps preview as a rendered boundary and leaves the source editor mounted', async () => {
    const wrapper = mountEditor('`inline`')
    await waitForSourceEditor(wrapper)

    await wrapper.get('[aria-label="预览"]').trigger('click')
    expect(wrapper.emitted('preview')).toHaveLength(1)
    await wrapper.setProps({ previewing: true })
    expect(wrapper.get('.rendered').text()).toContain('`inline`')
    expect(wrapper.find('.cm-content').exists()).toBe(true)
  })
})
