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

  it('uses on-demand Font Awesome Free icons with icon-only source, preview, and fullscreen controls', async () => {
    const wrapper = mountEditor()
    await waitForSourceEditor(wrapper)

    expect(wrapper.get('.knowledge-document-editor__toolbar').classes()).toContain(
      'knowledge-document-editor__toolbar',
    )
    const expectedIcons: ReadonlyArray<readonly [string, string]> = [
      ['无序列表', 'list-ul'],
      ['有序列表', 'list-ol'],
      ['任务列表', 'list-check'],
      ['引用', 'quote-left'],
      ['行内代码', 'code'],
      ['插入代码块', 'file-code'],
      ['插入链接', 'link'],
      ['插入表格', 'table'],
      ['插入图表', 'diagram-project'],
      ['图片上传功能开发中', 'image'],
      ['撤销', 'rotate-left'],
      ['重做', 'rotate-right'],
      ['源码编辑', 'code'],
      ['预览', 'eye'],
      ['全屏', 'expand'],
    ]
    expectedIcons.forEach(([label, icon]) => {
      expect(wrapper.find(`[aria-label="${label}"] svg[data-icon="${icon}"]`).exists()).toBe(true)
    })
    expect(wrapper.get('[aria-label="源码编辑"]').text()).toBe('')
    expect(wrapper.get('[aria-label="预览"]').text()).toBe('')
    expect(wrapper.get('[aria-label="全屏"]').text()).toBe('')
    expect(wrapper.find('[aria-label="源码"]').exists()).toBe(false)
    expect(wrapper.get('[aria-label="图片上传功能开发中"]').attributes('disabled')).toBeDefined()

    await wrapper.setProps({ fullscreen: true })
    expect(wrapper.find('[aria-label="退出全屏"] svg[data-icon="compress"]').exists()).toBe(true)
  })

  it('exposes all eight Mermaid source templates in the compact diagram menu', async () => {
    const wrapper = mountEditor()
    await waitForSourceEditor(wrapper)

    await wrapper.get('[aria-label="插入图表"]').trigger('click')

    expect(wrapper.get('[role="menu"]').attributes('aria-label')).toBe('图表类型')
    expect(wrapper.findAll('[role="menuitem"]')).toHaveLength(8)
    expect(wrapper.findAll('[role="menuitem"]').map((item) => item.text())).toEqual([
      '流程图',
      '时序图',
      '甘特图',
      '类图',
      '状态图',
      '饼图',
      '关系图',
      '旅程图',
    ])
  })

  it('keeps the toolbar outside bounded detail and dialog source/preview regions', async () => {
    const detail = mountEditor(Array.from({ length: 600 }, (_, index) => `第 ${index + 1} 行`).join('\n'))
    await waitForSourceEditor(detail)

    expect(detail.classes()).toContain('knowledge-document-editor--detail')
    expect(detail.find('.knowledge-document-editor__toolbar').exists()).toBe(true)
    expect(detail.find('.knowledge-document-editor__source').exists()).toBe(true)
    expect(detail.find('.knowledge-document-editor__preview').exists()).toBe(true)
    expect(detail.find('.knowledge-document-editor__source .cm-scroller').exists()).toBe(true)

    const dialog = mount(KnowledgeDocumentEditor, {
      props: { modelValue: '正文', viewport: 'dialog' },
      global: { components, stubs: { KnowledgeDocumentMarkdown: true } },
    })
    wrappers.push(dialog)
    await waitForSourceEditor(dialog)
    expect(dialog.classes()).toContain('knowledge-document-editor--dialog')
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
