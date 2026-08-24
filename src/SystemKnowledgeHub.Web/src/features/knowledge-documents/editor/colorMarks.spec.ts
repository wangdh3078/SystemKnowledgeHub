import { Editor, defaultValueCtx, editorViewCtx, rootCtx } from '@milkdown/core'
import { gfm } from '@milkdown/preset-gfm'
import { AllSelection, TextSelection } from '@milkdown/prose/state'
import { callCommand, getMarkdown } from '@milkdown/utils'
import { afterEach, describe, expect, it } from 'vitest'
import {
  applyBackgroundColorCommand,
  applyTextColorCommand,
  clearBackgroundColorCommand,
  clearTextColorCommand,
  knowledgeDocumentColorExtension,
} from './colorMarks'
import { knowledgeDocumentCommonmark } from './milkdownConfig'

const editors: Editor[] = []
const roots: HTMLElement[] = []

afterEach(async () => {
  await Promise.all(editors.splice(0).map((editor) => editor.destroy()))
  roots.splice(0).forEach((root) => root.remove())
})

async function createEditor(markdown: string): Promise<Editor> {
  const root = document.createElement('div')
  document.body.append(root)
  roots.push(root)

  const editor = await Editor.make()
    .config((ctx) => {
      ctx.set(rootCtx, root)
      ctx.set(defaultValueCtx, markdown)
    })
    .use(knowledgeDocumentCommonmark)
    .use(gfm)
    .use(knowledgeDocumentColorExtension)
    .create()
  editors.push(editor)
  return editor
}

async function roundTrip(markdown: string): Promise<string> {
  return (await createEditor(markdown)).action(getMarkdown())
}

function selectWholeParagraph(editor: Editor): void {
  editor.action((ctx) => {
    const view = ctx.get(editorViewCtx)
    const paragraph = view.state.doc.firstChild
    expect(paragraph).not.toBeNull()
    view.dispatch(
      view.state.tr.setSelection(
        TextSelection.create(view.state.doc, 1, 1 + (paragraph?.content.size ?? 0)),
      ),
    )
  })
}

describe('Milkdown controlled color marks', () => {
  it('canonicalizes text color and remains stable across round trips', async () => {
    const first = await roundTrip('{color:#e53935|严重告警}')

    expect(first).toBe('{color:#E53935|严重告警}\n')
    expect(await roundTrip(first)).toBe(first)
  })

  it('round-trips nested text/background marks in a stable order', async () => {
    const markdown = '{bg:#fff59d|警告：{color:#e53935|立即处理}}'
    const editor = await createEditor(markdown)

    expect(editor.action(getMarkdown())).toBe('{bg:#FFF59D|警告：{color:#E53935|立即处理}}\n')
    editor.action((ctx) => {
      const markNames = new Set<string>()
      ctx.get(editorViewCtx).state.doc.descendants((node) => {
        node.marks.forEach((mark) => markNames.add(mark.type.name))
      })
      expect(markNames).toEqual(new Set(['knowledgeBackgroundColor', 'knowledgeTextColor']))
    })
  })

  it('retains standard inline Markdown inside a color span', async () => {
    const first = await roundTrip('{color:#E53935|**重点**}')

    expect(first).toBe('**{color:#E53935|重点}**\n')
    expect(await roundTrip(first)).toBe(first)
  })

  it.each([
    '{color:#E53|短色值}',
    '{color:red|命名色}',
    '{color:url(javascript:alert(1))|危险}',
    '{bg:expression(alert(1))|危险}',
    '{color:#E53935|}',
    '{color:#E53935|未闭合',
    '{color:#E53935|第一行\n第二行}',
    '`{color:#E53935|行内代码}`',
  ])('keeps invalid, multiline, or code syntax literal: %s', async (source) => {
    const serialized = await roundTrip(source)

    expect(serialized).toContain(source)
    expect(serialized).not.toMatch(/knowledge(Text|Background)Color/u)
  })

  it('applies nested colors and clears each mark independently', async () => {
    const editor = await createEditor('重点')
    selectWholeParagraph(editor)

    expect(editor.action(callCommand(applyTextColorCommand.key, '#e53935'))).toBe(true)
    expect(editor.action(getMarkdown())).toBe('{color:#E53935|重点}\n')

    expect(editor.action(callCommand(applyBackgroundColorCommand.key, '#fff59d'))).toBe(true)
    expect(editor.action(getMarkdown())).toBe('{bg:#FFF59D|{color:#E53935|重点}}\n')

    expect(editor.action(callCommand(clearTextColorCommand.key))).toBe(true)
    expect(editor.action(getMarkdown())).toBe('{bg:#FFF59D|重点}\n')

    expect(editor.action(callCommand(clearBackgroundColorCommand.key))).toBe(true)
    expect(editor.action(getMarkdown())).toBe('重点\n')
  })

  it('splits colors around hard breaks and leaves table cells structurally safe', async () => {
    const editor = await createEditor(
      ['第一行\\', '第二行', '', '| 字段 | 说明 |', '| --- | --- |', '| ID | 主键 |'].join('\n'),
    )
    editor.action((ctx) => {
      const view = ctx.get(editorViewCtx)
      view.dispatch(view.state.tr.setSelection(new AllSelection(view.state.doc)))
    })

    expect(editor.action(callCommand(applyTextColorCommand.key, '#e53935'))).toBe(true)
    expect(editor.action(callCommand(applyBackgroundColorCommand.key, '#fff3b0'))).toBe(true)

    const markdown = editor.action(getMarkdown())
    expect(markdown).toContain('{bg:#FFF3B0|{color:#E53935|第一行}}\\\n')
    expect(markdown).toContain('{bg:#FFF3B0|{color:#E53935|第二行}}')
    expect(markdown).toContain('| 字段 | 说明 |')
    expect(markdown).toContain('| ID | 主键 |')
    expect(await roundTrip(markdown)).toBe(markdown)
  })

  it('rejects unsafe command payloads without changing the document', async () => {
    const editor = await createEditor('重点')
    selectWholeParagraph(editor)

    expect(editor.action(callCommand(applyTextColorCommand.key, 'url(javascript:alert(1))'))).toBe(
      false,
    )
    expect(editor.action(getMarkdown())).toBe('重点\n')
  })

  it('uses and clears the stored text color at an empty selection', async () => {
    const editor = await createEditor('前')
    editor.action((ctx) => {
      const view = ctx.get(editorViewCtx)
      view.dispatch(view.state.tr.setSelection(TextSelection.create(view.state.doc, 2)))
    })

    expect(editor.action(callCommand(applyTextColorCommand.key, '#E53935'))).toBe(true)
    editor.action((ctx) => {
      const view = ctx.get(editorViewCtx)
      view.dispatch(view.state.tr.insertText('红'))
    })
    expect(editor.action(getMarkdown())).toBe('前{color:#E53935|红}\n')

    expect(editor.action(callCommand(clearTextColorCommand.key))).toBe(true)
    editor.action((ctx) => {
      const view = ctx.get(editorViewCtx)
      view.dispatch(view.state.tr.insertText('后'))
    })
    expect(editor.action(getMarkdown())).toBe('前{color:#E53935|红}后\n')
  })
})
