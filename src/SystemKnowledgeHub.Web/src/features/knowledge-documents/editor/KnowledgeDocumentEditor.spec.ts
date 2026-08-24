import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import {
  ElButton,
  ElDialog,
  ElIcon,
  ElInputNumber,
  ElOption,
  ElSelect,
  ElTooltip,
  ElMessageBox,
} from 'element-plus'
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import KnowledgeDocumentEditor from './KnowledgeDocumentEditor.vue'

const components = {
  ElButton,
  ElDialog,
  ElIcon,
  ElInputNumber,
  ElOption,
  ElSelect,
  ElTooltip,
}

const mainToolbarTools = [
  { aria: '段落与标题', tooltip: '段落与标题级别' },
  { aria: '加粗', tooltip: '加粗（Ctrl+B）' },
  { aria: '斜体', tooltip: '斜体（Ctrl+I）' },
  { aria: '行内代码', tooltip: '行内代码' },
  { aria: '无序列表', tooltip: '无序列表' },
  { aria: '有序列表', tooltip: '有序列表' },
  { aria: '任务列表', tooltip: '任务列表' },
  { aria: '引用', tooltip: '引用' },
  { aria: '代码块', tooltip: '代码块' },
  { aria: '插入链接', tooltip: '插入链接' },
  { aria: '插入表格', tooltip: '插入表格（2×2 至 10×10）' },
  { aria: '插入 Mermaid', tooltip: '插入 Mermaid' },
  { aria: '插入分隔线', tooltip: '插入分隔线' },
  { aria: '图片上传（待接入）', tooltip: '图片上传将在附件管理功能中启用' },
  { aria: '文字颜色', tooltip: '文字颜色' },
  { aria: '清除文字颜色', tooltip: '清除文字颜色' },
  { aria: '背景颜色', tooltip: '背景颜色' },
  { aria: '清除背景颜色', tooltip: '清除背景颜色' },
  { aria: '撤销', tooltip: '撤销（Ctrl+Z）' },
  { aria: '重做', tooltip: '重做（Ctrl+Y / Ctrl+Shift+Z）' },
  { aria: '保存', tooltip: '保存（Ctrl+S）' },
  { aria: '编辑', tooltip: '编辑' },
  { aria: '预览', tooltip: '预览' },
  { aria: '全屏', tooltip: '全屏' },
] as const

const wrappers: VueWrapper[] = []
const originalRangeRects = Object.getOwnPropertyDescriptor(Range.prototype, 'getClientRects')
const originalWindowScrollBy = Object.getOwnPropertyDescriptor(window, 'scrollBy')

beforeAll(() => {
  Object.defineProperty(Range.prototype, 'getClientRects', {
    configurable: true,
    value: (): DOMRectList => [new DOMRect(0, 0, 1, 1)] as unknown as DOMRectList,
  })
  Object.defineProperty(window, 'scrollBy', {
    configurable: true,
    value: (): void => undefined,
  })
})

afterAll(() => {
  if (originalRangeRects) {
    Object.defineProperty(Range.prototype, 'getClientRects', originalRangeRects)
  } else {
    Reflect.deleteProperty(Range.prototype, 'getClientRects')
  }
  if (originalWindowScrollBy) {
    Object.defineProperty(window, 'scrollBy', originalWindowScrollBy)
  } else {
    Reflect.deleteProperty(window, 'scrollBy')
  }
})

afterEach(() => {
  wrappers.splice(0).forEach((wrapper) => wrapper.unmount())
})

function mountEditor(
  modelValue = '正文',
  props: Partial<{
    previewing: boolean
    fullscreen: boolean
    canSave: boolean
    saving: boolean
  }> = {},
): VueWrapper {
  const wrapper = mount(KnowledgeDocumentEditor, {
    attachTo: document.body,
    props: {
      modelValue,
      ...props,
    },
    global: { components },
  })
  wrappers.push(wrapper)
  return wrapper
}

async function waitForEditor(wrapper: VueWrapper): Promise<void> {
  await vi.waitFor(
    () => {
      expect(wrapper.find('.ProseMirror').exists()).toBe(true)
    },
    { timeout: 2_000, interval: 10 },
  )
  await settleToolbar()
}

async function settleToolbar(): Promise<void> {
  await Promise.resolve()
  await flushPromises()
  await new Promise((resolve) => setTimeout(resolve, 0))
}

async function selectText(wrapper: VueWrapper, selector = '.ProseMirror p'): Promise<void> {
  const editor = wrapper.get('.ProseMirror').element as HTMLElement
  const target = wrapper.get(selector).element
  const textNode = target.firstChild
  if (!(textNode instanceof Text)) throw new Error(`No text node found for ${selector}.`)

  editor.focus()
  const range = document.createRange()
  range.setStart(textNode, 0)
  range.setEnd(textNode, textNode.data.length)
  const selection = window.getSelection()
  if (!selection) throw new Error('DOM selection is unavailable.')
  selection.removeAllRanges()
  selection.addRange(range)
  document.dispatchEvent(new Event('selectionchange'))
  await settleToolbar()
}

function latestMarkdown(wrapper: VueWrapper): string {
  const values = wrapper.emitted('update:modelValue')?.flat()
  const value = values?.at(-1)
  if (typeof value !== 'string') throw new Error('No Markdown update was emitted.')
  return value
}

function componentProp(wrapper: VueWrapper, name: string): unknown {
  const props: unknown = wrapper.props()
  if (typeof props !== 'object' || props === null) return undefined
  return Reflect.get(props, name)
}

function promptResult(value: string): Awaited<ReturnType<typeof ElMessageBox.prompt>> {
  // Element Plus 2.14 declares MessageBoxData as an impossible object/string
  // intersection even though prompt resolves this documented object shape.
  return { value, action: 'confirm' } as unknown as Awaited<ReturnType<typeof ElMessageBox.prompt>>
}

async function expectMarkdownContains(wrapper: VueWrapper, expected: string): Promise<void> {
  await vi.waitFor(() => expect(latestMarkdown(wrapper)).toContain(expected), {
    timeout: 1_000,
    interval: 20,
  })
}

async function changeBlockType(wrapper: VueWrapper, value: string): Promise<void> {
  const select = wrapper.findAllComponents(ElSelect)[0]
  if (!select) throw new Error('Block type select was not rendered.')
  select.vm.$emit('change', value)
  await settleToolbar()
}

describe('KnowledgeDocumentEditor', () => {
  it('labels every visible primary action and resolves focusable tooltips', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    try {
      const wrapper = mountEditor('# 标题\n\n正文', { canSave: true })
      await waitForEditor(wrapper)

      const toolbar = wrapper.get('[aria-label="Markdown 编辑工具"]')
      const renderedTooltips: VueWrapper[] = toolbar.findAllComponents(ElTooltip)
      const explicitTooltips = renderedTooltips.filter((tooltip: VueWrapper) =>
        mainToolbarTools.some((tool) => tool.tooltip === componentProp(tooltip, 'content')),
      )
      expect(explicitTooltips).toHaveLength(mainToolbarTools.length)

      mainToolbarTools.forEach(({ aria, tooltip }) => {
        expect(toolbar.find(`[aria-label="${aria}"]`).exists()).toBe(true)
        const tooltipComponent = explicitTooltips.find(
          (candidate: VueWrapper) => componentProp(candidate, 'content') === tooltip,
        )
        expect(tooltipComponent && componentProp(tooltipComponent, 'trigger')).toEqual([
          'hover',
          'focus',
        ])
      })

      expect(toolbar.get('[aria-label="图片上传（待接入）"]').attributes('disabled')).toBeDefined()
      expect(toolbar.get('[aria-label="撤销"]').attributes('disabled')).toBeDefined()
      expect(toolbar.get('[aria-label="重做"]').attributes('disabled')).toBeDefined()
      expect(toolbar.get('[aria-label="保存"]').attributes('disabled')).toBeUndefined()
      expect(wrapper.emitted('ready')?.[0]?.[0]).toContain('# 标题')
      expect(warn.mock.calls.flat().join(' ')).not.toContain('Failed to resolve component')
    } finally {
      warn.mockRestore()
    }
  })

  it('runs Paragraph and every H1-H6 command from the single block selector', async () => {
    const wrapper = mountEditor()
    await waitForEditor(wrapper)

    for (let level = 1; level <= 6; level += 1) {
      await changeBlockType(wrapper, `h${level}`)
      expect(wrapper.get(`.ProseMirror h${level}`).text()).toBe('正文')
    }

    await changeBlockType(wrapper, 'paragraph')
    expect(wrapper.find('.ProseMirror h1, .ProseMirror h2, .ProseMirror h3').exists()).toBe(false)
    expect(wrapper.get('.ProseMirror p').text()).toBe('正文')
  })

  it.each([
    { aria: '加粗', selector: '.ProseMirror strong' },
    { aria: '斜体', selector: '.ProseMirror em' },
    { aria: '行内代码', selector: '.ProseMirror code' },
  ])('runs the $aria inline formatting command on selected text', async ({ aria, selector }) => {
    const wrapper = mountEditor()
    await waitForEditor(wrapper)
    await selectText(wrapper)

    await wrapper.get(`[aria-label="${aria}"]`).trigger('click')
    await settleToolbar()

    expect(wrapper.get(selector).text()).toBe('正文')
  })

  it('returns focus to the editor after a toolbar command', async () => {
    const wrapper = mountEditor()
    await waitForEditor(wrapper)
    const surface = wrapper.get('.ProseMirror').element

    await wrapper.get('[aria-label="加粗"]').trigger('click')
    await settleToolbar()

    expect(document.activeElement).toBe(surface)
  })

  it.each([
    { aria: '无序列表', selector: '.ProseMirror ul' },
    { aria: '有序列表', selector: '.ProseMirror ol' },
    { aria: '引用', selector: '.ProseMirror blockquote' },
  ])('runs the $aria block command', async ({ aria, selector }) => {
    const wrapper = mountEditor()
    await waitForEditor(wrapper)

    await wrapper.get(`[aria-label="${aria}"]`).trigger('click')
    await settleToolbar()

    expect(wrapper.get(selector).text()).toContain('正文')
  })

  it('creates and toggles a real GFM task list', async () => {
    const wrapper = mountEditor('待办事项')
    await waitForEditor(wrapper)
    const button = wrapper.get('[aria-label="任务列表"]')

    await button.trigger('click')
    await settleToolbar()
    expect(wrapper.get('.ProseMirror li[data-item-type="task"]').attributes('data-checked')).toBe(
      'false',
    )
    await expectMarkdownContains(wrapper, '* [ ] 待办事项')
    expect(button.attributes('aria-pressed')).toBe('true')

    await button.trigger('click')
    await settleToolbar()
    expect(wrapper.get('.ProseMirror li[data-item-type="task"]').attributes('data-checked')).toBe(
      'true',
    )
    await expectMarkdownContains(wrapper, '* [x] 待办事项')
  })

  it('inserts a prompted code block and a safely prompted link', async () => {
    const codeWrapper = mountEditor()
    await waitForEditor(codeWrapper)
    const prompt = vi.spyOn(ElMessageBox, 'prompt')
    try {
      prompt.mockResolvedValueOnce(promptResult('SQL'))
      await codeWrapper.get('[aria-label="代码块"]').trigger('click')
      await settleToolbar()

      expect(codeWrapper.get('.ProseMirror pre[data-language="sql"]').text()).toBe('正文')
      await expectMarkdownContains(codeWrapper, '```sql\n正文\n```')

      const linkWrapper = mountEditor()
      await waitForEditor(linkWrapper)
      prompt
        .mockResolvedValueOnce(promptResult('文档入口'))
        .mockResolvedValueOnce(promptResult('/documents/1'))
      await linkWrapper.get('[aria-label="插入链接"]').trigger('click')
      await settleToolbar()

      const link = linkWrapper.get('.ProseMirror a[href="/documents/1"]')
      expect(link.text()).toBe('文档入口')
      await expectMarkdownContains(linkWrapper, '[文档入口](/documents/1)')
      expect(prompt).toHaveBeenCalledTimes(3)
    } finally {
      prompt.mockRestore()
    }
  })

  it('inserts a table and wires its contextual secondary operations', async () => {
    const wrapper = mountEditor()
    await waitForEditor(wrapper)

    await wrapper.get('[aria-label="插入表格"]').trigger('click')
    await settleToolbar()
    expect(document.body.querySelector('[role="dialog"]')).not.toBeNull()

    const insertButton = [...document.body.querySelectorAll('button')].find(
      (button) => button.textContent?.trim() === '插入表格',
    )
    if (!insertButton) throw new Error('Insert table confirmation button was not rendered.')
    insertButton.click()
    await settleToolbar()

    expect(wrapper.findAll('.ProseMirror table tr')).toHaveLength(3)
    expect(wrapper.findAll('.ProseMirror table th')).toHaveLength(3)
    const tableToolbar = wrapper.get('[aria-label="表格操作"]')
    const contextualTools = ['下方添加行', '删除当前行', '右侧添加列', '删除当前列', '删除整个表格']
    contextualTools.forEach((aria) => {
      expect(tableToolbar.find(`[aria-label="${aria}"]`).exists()).toBe(true)
    })
    expect(tableToolbar.findAllComponents(ElTooltip)).toHaveLength(contextualTools.length)
    expect(tableToolbar.get('[aria-label="删除当前行"]').attributes('disabled')).toBeDefined()
    expect(tableToolbar.get('[aria-label="删除当前列"]').attributes('disabled')).toBeUndefined()

    await tableToolbar.get('[aria-label="下方添加行"]').trigger('click')
    await settleToolbar()
    expect(wrapper.findAll('.ProseMirror table tr')).toHaveLength(4)

    await tableToolbar.get('[aria-label="右侧添加列"]').trigger('click')
    await settleToolbar()
    expect(wrapper.findAll('.ProseMirror table th')).toHaveLength(4)

    await tableToolbar.get('[aria-label="删除当前列"]').trigger('click')
    await settleToolbar()
    expect(wrapper.findAll('.ProseMirror table th')).toHaveLength(3)

    await tableToolbar.get('[aria-label="删除整个表格"]').trigger('click')
    await settleToolbar()
    expect(wrapper.find('.ProseMirror table').exists()).toBe(false)
    expect(wrapper.find('[aria-label="表格操作"]').exists()).toBe(false)
  })

  it('inserts Mermaid and a horizontal rule as canonical document nodes', async () => {
    const mermaidWrapper = mountEditor()
    await waitForEditor(mermaidWrapper)

    await mermaidWrapper.get('[aria-label="插入 Mermaid"]').trigger('click')
    await settleToolbar()
    expect(mermaidWrapper.get('.ProseMirror pre[data-language="mermaid"]').text()).toContain(
      'flowchart TD',
    )
    await expectMarkdownContains(mermaidWrapper, '```mermaid')

    const horizontalRuleWrapper = mountEditor()
    await waitForEditor(horizontalRuleWrapper)
    await horizontalRuleWrapper.get('[aria-label="插入分隔线"]').trigger('click')
    await settleToolbar()
    expect(horizontalRuleWrapper.find('.ProseMirror hr').exists()).toBe(true)
    await expectMarkdownContains(horizontalRuleWrapper, '***')
  })

  it('applies and clears controlled text and background colors', async () => {
    const wrapper = mountEditor()
    await waitForEditor(wrapper)
    const selects = wrapper.findAllComponents(ElSelect)
    const textColorSelect = selects[1]
    const backgroundColorSelect = selects[2]
    if (!textColorSelect || !backgroundColorSelect) {
      throw new Error('Color selectors were not rendered.')
    }

    await selectText(wrapper)
    textColorSelect.vm.$emit('change', '#E53935')
    await settleToolbar()
    expect(wrapper.get('[data-knowledge-text-color="#E53935"]').text()).toBe('正文')
    await expectMarkdownContains(wrapper, '{color:#E53935|正文}')

    await selectText(wrapper, '[data-knowledge-text-color="#E53935"]')
    await wrapper.get('[aria-label="清除文字颜色"]').trigger('click')
    await settleToolbar()
    expect(wrapper.find('[data-knowledge-text-color]').exists()).toBe(false)

    await selectText(wrapper)
    backgroundColorSelect.vm.$emit('change', '#FFF3B0')
    await settleToolbar()
    expect(wrapper.get('[data-knowledge-background-color="#FFF3B0"]').text()).toBe('正文')
    await expectMarkdownContains(wrapper, '{bg:#FFF3B0|正文}')

    await selectText(wrapper, '[data-knowledge-background-color="#FFF3B0"]')
    await wrapper.get('[aria-label="清除背景颜色"]').trigger('click')
    await settleToolbar()
    expect(wrapper.find('[data-knowledge-background-color]').exists()).toBe(false)
  })

  it('treats a mixed color selection as unset and can unify it with the same color', async () => {
    const wrapper = mountEditor('{color:#E53935|红色}无色')
    await waitForEditor(wrapper)
    const paragraph = wrapper.get('.ProseMirror p').element
    const first = paragraph.querySelector('[data-knowledge-text-color]')?.firstChild
    const last = paragraph.lastChild
    if (!(first instanceof Text) || !(last instanceof Text)) {
      throw new Error('Expected colored and uncolored text nodes.')
    }
    ;(wrapper.get('.ProseMirror').element as HTMLElement).focus()
    const selection = window.getSelection()
    if (!selection) throw new Error('DOM selection is unavailable.')
    const range = document.createRange()
    range.setStart(first, 0)
    range.setEnd(last, last.data.length)
    selection.removeAllRanges()
    selection.addRange(range)
    document.dispatchEvent(new Event('selectionchange'))
    await settleToolbar()

    const textColorSelect = wrapper.findAllComponents(ElSelect)[1]
    expect(textColorSelect?.props('modelValue')).toBe('')
    textColorSelect?.vm.$emit('change', '#E53935')
    await settleToolbar()
    await expectMarkdownContains(wrapper, '{color:#E53935|红色无色}')
  })

  it('enables and runs real Undo and Redo history commands', async () => {
    const wrapper = mountEditor()
    await waitForEditor(wrapper)
    const undo = wrapper.get('[aria-label="撤销"]')
    const redo = wrapper.get('[aria-label="重做"]')
    expect(undo.attributes('disabled')).toBeDefined()
    expect(redo.attributes('disabled')).toBeDefined()

    await changeBlockType(wrapper, 'h2')
    await vi.waitFor(() => expect(undo.attributes('disabled')).toBeUndefined())
    expect(wrapper.find('.ProseMirror h2').exists()).toBe(true)

    await undo.trigger('click')
    await settleToolbar()
    expect(wrapper.find('.ProseMirror h2').exists()).toBe(false)
    expect(wrapper.get('.ProseMirror p').text()).toBe('正文')
    await vi.waitFor(() => expect(redo.attributes('disabled')).toBeUndefined())

    await redo.trigger('click')
    await settleToolbar()
    expect(wrapper.get('.ProseMirror h2').text()).toBe('正文')
  })

  it('emits workspace actions and keeps the editor mounted while previewing', async () => {
    const wrapper = mountEditor('# 标题\n\n正文', { canSave: true })
    await waitForEditor(wrapper)
    const editorElement = wrapper.get('.ProseMirror').element

    await wrapper.get('[aria-label="保存"]').trigger('click')
    await wrapper.get('[aria-label="编辑"]').trigger('click')
    await wrapper.get('[aria-label="预览"]').trigger('click')
    await wrapper.get('[aria-label="全屏"]').trigger('click')
    expect(wrapper.emitted('save')).toHaveLength(1)
    expect(wrapper.emitted('edit')).toHaveLength(1)
    expect(wrapper.emitted('preview')).toHaveLength(1)
    expect(wrapper.emitted('toggle-fullscreen')).toHaveLength(1)

    await wrapper.setProps({ previewing: true })
    await settleToolbar()
    expect(wrapper.get('.ProseMirror').element).toBe(editorElement)
    expect(wrapper.get('.knowledge-document-editor__surface').attributes('style')).toContain(
      'display: none',
    )
    expect(wrapper.get('.knowledge-document-editor__preview').text()).toContain('预览未保存内容')
    expect(wrapper.get('.knowledge-document-markdown').text()).toContain('标题')
    expect(wrapper.get('[aria-label="加粗"]').attributes('disabled')).toBeDefined()

    await wrapper.setProps({ previewing: false, fullscreen: true })
    await settleToolbar()
    expect(wrapper.get('.ProseMirror').element).toBe(editorElement)
    expect(wrapper.get('.knowledge-document-editor').classes()).toContain('is-fullscreen')
    expect(wrapper.get('[aria-label="退出全屏"]').attributes('aria-pressed')).toBe('true')
  })

  it('canonicalizes a proven legacy BR fixture when the editor becomes ready', async () => {
    const wrapper = mountEditor('A\n\n<br />\n\nB')
    await waitForEditor(wrapper)

    expect(wrapper.emitted('ready')?.[0]?.[0]).toBe('A\n\n\\\nB\n')
    expect(
      wrapper
        .emitted('update:modelValue')
        ?.flat()
        .every((value) => typeof value !== 'string' || !value.includes('<br')),
    ).toBe(true)
  })
})
