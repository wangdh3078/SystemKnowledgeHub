import { Editor, defaultValueCtx, editorViewCtx, rootCtx } from '@milkdown/core'
import { gfm } from '@milkdown/preset-gfm'
import { TextSelection } from '@milkdown/prose/state'
import { callCommand, getMarkdown } from '@milkdown/utils'
import { afterEach, describe, expect, it } from 'vitest'
import { knowledgeDocumentCommonmark } from './milkdownConfig'
import {
  deleteCurrentTableCommand,
  deleteTableColumnCommand,
  deleteTableRowCommand,
  getTableCommandAvailability,
  insertMermaidBlockCommand,
  insertTableCommand,
  knowledgeDocumentEditorCommands,
  toggleTaskListCommand,
} from './editorCommands'

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
    .use(knowledgeDocumentEditorCommands)
    .create()
  editors.push(editor)
  return editor
}

describe('knowledge document editor commands', () => {
  it('creates and toggles a canonical GFM task list', async () => {
    const editor = await createEditor('待处理事项')

    expect(editor.action(callCommand(toggleTaskListCommand.key))).toBe(true)
    expect(editor.action(getMarkdown())).toBe('* [ ] 待处理事项\n')

    expect(editor.action(callCommand(toggleTaskListCommand.key))).toBe(true)
    expect(editor.action(getMarkdown())).toBe('* [x] 待处理事项\n')

    expect(editor.action(callCommand(toggleTaskListCommand.key))).toBe(true)
    expect(editor.action(getMarkdown())).toBe('* [ ] 待处理事项\n')
  })

  it('inserts a Mermaid fence after the current block without replacing source text', async () => {
    const editor = await createEditor('保留正文')
    const source = 'flowchart LR\n    A --> B'

    expect(
      editor.action(
        callCommand(insertMermaidBlockCommand.key, {
          source,
          cursorOffset: 0,
        }),
      ),
    ).toBe(true)

    expect(editor.action(getMarkdown())).toBe(`保留正文\n\n\`\`\`mermaid\n${source}\n\`\`\`\n`)
    editor.action((ctx) => {
      const { state } = ctx.get(editorViewCtx)
      expect(state.selection.$from.parent.type.name).toBe('code_block')
      expect(state.selection.$from.parent.attrs.language).toBe('mermaid')
      expect(state.selection.$from.parentOffset).toBe(0)
    })
  })

  it('guards the GFM table shape while deleting rows, columns, and a table', async () => {
    const editor = await createEditor('')

    expect(editor.action(callCommand(insertTableCommand.key, { row: 3, col: 2 }))).toBe(true)

    editor.action((ctx) => {
      const { state } = ctx.get(editorViewCtx)
      expect(getTableCommandAvailability(state)).toMatchObject({
        isInTable: true,
        canDeleteRow: false,
        canDeleteColumn: true,
      })
    })
    expect(editor.action(callCommand(deleteTableRowCommand.key))).toBe(false)

    editor.action((ctx) => {
      const view = ctx.get(editorViewCtx)
      const paragraphPositions: number[] = []
      view.state.doc.descendants((node, position) => {
        if (node.type.name === 'paragraph') paragraphPositions.push(position)
      })
      const firstBodyCellParagraph = paragraphPositions[2]
      expect(firstBodyCellParagraph).toBeDefined()
      view.dispatch(
        view.state.tr.setSelection(
          TextSelection.create(view.state.doc, firstBodyCellParagraph! + 1),
        ),
      )
    })

    expect(editor.action(callCommand(deleteTableRowCommand.key))).toBe(true)
    editor.action((ctx) => {
      const { state } = ctx.get(editorViewCtx)
      const table = state.doc.firstChild
      expect(table?.type.name).toBe('table')
      expect(table?.childCount).toBe(2)
      expect(getTableCommandAvailability(state).canDeleteRow).toBe(false)
    })
    expect(editor.action(callCommand(deleteTableRowCommand.key))).toBe(false)

    expect(editor.action(callCommand(deleteTableColumnCommand.key))).toBe(true)
    editor.action((ctx) => {
      const { state } = ctx.get(editorViewCtx)
      expect(state.doc.firstChild?.firstChild?.childCount).toBe(1)
      expect(getTableCommandAvailability(state).canDeleteColumn).toBe(false)
    })
    expect(editor.action(callCommand(deleteTableColumnCommand.key))).toBe(false)

    expect(editor.action(callCommand(deleteCurrentTableCommand.key))).toBe(true)
    editor.action((ctx) => {
      const { state } = ctx.get(editorViewCtx)
      expect(state.doc.firstChild?.type.name).not.toBe('table')
      expect(getTableCommandAvailability(state).isInTable).toBe(false)
    })
  })

  it('keeps table cells editable and moves to the next cell with the GFM Tab keymap', async () => {
    const editor = await createEditor('')
    expect(editor.action(callCommand(insertTableCommand.key, { row: 2, col: 2 }))).toBe(true)

    let beforeTab = 0
    editor.action((ctx) => {
      const view = ctx.get(editorViewCtx)
      view.dispatch(view.state.tr.insertText('字段'))
      beforeTab = view.state.selection.from
      view.focus()
      view.dom.dispatchEvent(
        new KeyboardEvent('keydown', {
          key: 'Tab',
          bubbles: true,
        }),
      )
    })

    editor.action((ctx) => {
      const { state } = ctx.get(editorViewCtx)
      expect(state.selection.from).toBeGreaterThan(beforeTab)
      expect(state.selection.$from.parent.type.name).toBe('paragraph')
    })
    expect(editor.action(getMarkdown())).toContain('| 字段 |')
  })

  it('reports contextual table commands unavailable outside a table', async () => {
    const editor = await createEditor('正文')

    editor.action((ctx) => {
      const { state } = ctx.get(editorViewCtx)
      expect(getTableCommandAvailability(state)).toEqual({
        isInTable: false,
        canAddRow: false,
        canAddColumn: false,
        canDeleteRow: false,
        canDeleteColumn: false,
        canDeleteTable: false,
      })
    })
    expect(editor.action(callCommand(deleteTableRowCommand.key))).toBe(false)
    expect(editor.action(callCommand(deleteTableColumnCommand.key))).toBe(false)
    expect(editor.action(callCommand(deleteCurrentTableCommand.key))).toBe(false)
  })
})
