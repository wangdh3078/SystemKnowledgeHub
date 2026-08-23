import { flushPromises, mount } from '@vue/test-utils'
import { ElButton, ElIcon, ElTooltip } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'
import KnowledgeDocumentEditor from './KnowledgeDocumentEditor.vue'

const components = {
  ElButton,
  ElTooltip,
  ElIcon,
}

const toolbarLabels = [
  '正文',
  '一级标题',
  '二级标题',
  '三级标题',
  '加粗',
  '斜体',
  '删除线',
  '行内代码',
  '无序列表',
  '有序列表',
  '引用',
  '代码块',
  '插入链接',
  '插入表格',
  '分隔线',
]

async function waitForEditor(): Promise<void> {
  await flushPromises()
  await new Promise((resolve) => setTimeout(resolve, 40))
}

describe('KnowledgeDocumentEditor', () => {
  it('resolves real tooltips and exposes only supported, labelled actions', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    try {
      const wrapper = mount(KnowledgeDocumentEditor, {
        props: { modelValue: '# 标题\n\n正文' },
        global: { components },
      })

      expect(wrapper.findAll('button').every((button) => button.attributes('disabled') !== undefined))
        .toBe(true)

      await waitForEditor()

      const tooltips = wrapper.findAllComponents(ElTooltip)
      expect(tooltips).toHaveLength(toolbarLabels.length)
      toolbarLabels.forEach((label, index) => {
        expect(wrapper.find(`[aria-label="${label}"]`).exists()).toBe(true)
        expect(tooltips[index]?.props('content')).toBe(label)
        expect(tooltips[index]?.props('trigger')).toEqual(['hover', 'focus'])
      })
      expect(wrapper.find('[aria-label="撤销"]').exists()).toBe(false)
      expect(wrapper.find('[aria-label="重做"]').exists()).toBe(false)
      expect(wrapper.findAll('button').every((button) => button.attributes('disabled') === undefined))
        .toBe(true)
      expect(wrapper.emitted('ready')?.[0]?.[0]).toContain('# 标题')
      expect(wrapper.find('.ProseMirror').exists()).toBe(true)
      expect(warn.mock.calls.flat().join(' ')).not.toContain(
        'Failed to resolve component: el-tooltip',
      )
    } finally {
      warn.mockRestore()
    }
  })

  it('dispatches heading and paragraph commands to the editor', async () => {
    const wrapper = mount(KnowledgeDocumentEditor, {
      props: { modelValue: '正文' },
      global: { components },
    })

    await waitForEditor()
    await wrapper.get('[aria-label="二级标题"]').trigger('click')
    await waitForEditor()

    expect(wrapper.get('.ProseMirror h2').text()).toBe('正文')

    await wrapper.get('[aria-label="正文"]').trigger('click')
    await waitForEditor()

    expect(wrapper.find('.ProseMirror h2').exists()).toBe(false)
    expect(wrapper.get('.ProseMirror p').text()).toBe('正文')
  })

  it('canonicalizes a proven legacy BR fixture when the editor becomes ready', async () => {
    const wrapper = mount(KnowledgeDocumentEditor, {
      props: { modelValue: 'A\n\n<br />\n\nB' },
      global: { components },
    })

    await waitForEditor()

    expect(wrapper.emitted('ready')?.[0]?.[0]).toBe('A\n\n\\\nB\n')
    expect(wrapper.emitted('update:modelValue')?.flat().every(
      (value) => typeof value !== 'string' || !value.includes('<br'),
    )).toBe(true)
  })
})
