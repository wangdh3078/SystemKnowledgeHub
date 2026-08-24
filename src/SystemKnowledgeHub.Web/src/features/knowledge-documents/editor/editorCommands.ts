import { history, redoCommand, undoCommand } from '@milkdown/plugin-history'
import { bulletListSchema, codeBlockSchema, listItemSchema } from '@milkdown/preset-commonmark'
import {
  addColAfterCommand,
  addColBeforeCommand,
  addRowAfterCommand,
  addRowBeforeCommand,
  insertTableCommand,
} from '@milkdown/preset-gfm'
import type { Node as ProseNode, NodeType } from '@milkdown/prose/model'
import { wrapInList } from '@milkdown/prose/schema-list'
import type { EditorState, Selection, Transaction } from '@milkdown/prose/state'
import { TextSelection } from '@milkdown/prose/state'
import {
  deleteColumn,
  deleteRow,
  deleteTable,
  isInTable,
  selectedRect,
} from '@milkdown/prose/tables'
import { $command } from '@milkdown/utils'

export {
  addColAfterCommand,
  addColBeforeCommand,
  addRowAfterCommand,
  addRowBeforeCommand,
  history,
  insertTableCommand,
  redoCommand,
  undoCommand,
}

export const DEFAULT_MERMAID_SOURCE = `flowchart TD
    A[开始] --> B[结束]`

export interface InsertMermaidBlockPayload {
  source?: string
  cursorOffset?: number
}

export interface TableCommandAvailability {
  isInTable: boolean
  canAddRow: boolean
  canAddColumn: boolean
  canDeleteRow: boolean
  canDeleteColumn: boolean
  canDeleteTable: boolean
}

function listItemPositions(doc: ProseNode, selection: Selection, listItemType: NodeType): number[] {
  const positions = new Set<number>()

  const addAncestor = (position: Selection['$from']): void => {
    for (let depth = position.depth; depth > 0; depth -= 1) {
      if (position.node(depth).type === listItemType) {
        positions.add(position.before(depth))
      }
    }
  }

  addAncestor(selection.$from)
  addAncestor(selection.$to)

  if (!selection.empty) {
    doc.nodesBetween(selection.from, selection.to, (node, position) => {
      if (node.type === listItemType) {
        positions.add(position)
      }
    })
  }

  return [...positions].sort((left, right) => left - right)
}

function nextCheckedValue(
  doc: ProseNode,
  positions: readonly number[],
  requestedValue: boolean | undefined,
): boolean {
  if (requestedValue !== undefined) return requestedValue

  const values = positions.map((position) => doc.nodeAt(position)?.attrs.checked)
  if (values.some((value) => value == null)) return false

  return !values.every((value) => value === true)
}

function setChecked(
  transaction: Transaction,
  positions: readonly number[],
  listItemType: NodeType,
  checked: boolean,
): Transaction {
  positions.forEach((position) => {
    const node = transaction.doc.nodeAt(position)
    if (node?.type !== listItemType) return

    transaction.setNodeMarkup(position, undefined, {
      ...node.attrs,
      checked,
    })
  })

  return transaction
}

function wrapSelectionInList(state: EditorState, listType: NodeType): Transaction | null {
  let transaction: Transaction | null = null
  const wrapped = wrapInList(listType)(state, (nextTransaction) => {
    transaction = nextTransaction
  })

  return wrapped ? transaction : null
}

/**
 * Converts the selected list items to GFM task items, or creates a bullet task
 * list around the current block. Subsequent calls toggle checked state.
 */
export const toggleTaskListCommand = $command<boolean, 'ToggleKnowledgeDocumentTaskList'>(
  'ToggleKnowledgeDocumentTaskList',
  (ctx) => (requestedValue) => (state, dispatch) => {
    const listItemType = listItemSchema.type(ctx)
    const currentPositions = listItemPositions(state.doc, state.selection, listItemType)

    if (currentPositions.length > 0) {
      if (!dispatch) return true

      const checked = nextCheckedValue(state.doc, currentPositions, requestedValue)
      const transaction = setChecked(
        state.tr,
        currentPositions,
        listItemType,
        checked,
      ).scrollIntoView()
      dispatch(transaction)
      return true
    }

    const bulletListType = bulletListSchema.type(ctx)
    if (!dispatch) return wrapInList(bulletListType)(state)

    const transaction = wrapSelectionInList(state, bulletListType)
    if (!transaction) return false

    const wrappedPositions = listItemPositions(transaction.doc, transaction.selection, listItemType)
    if (wrappedPositions.length === 0) return false

    setChecked(transaction, wrappedPositions, listItemType, requestedValue ?? false)
    dispatch(transaction.scrollIntoView())
    return true
  },
)

/** Inserts a canonical fenced Mermaid code block after the current top-level block. */
export const insertMermaidBlockCommand = $command<
  InsertMermaidBlockPayload,
  'InsertKnowledgeDocumentMermaidBlock'
>('InsertKnowledgeDocumentMermaidBlock', (ctx) => (payload) => (state, dispatch) => {
  const source = payload?.source ?? DEFAULT_MERMAID_SOURCE
  const codeBlockType = codeBlockSchema.type(ctx)
  const content = source.length > 0 ? state.schema.text(source) : undefined
  const mermaidNode = codeBlockType.create({ language: 'mermaid' }, content)
  const insertionIndex = state.selection.$to.indexAfter(0)

  if (!state.doc.canReplaceWith(insertionIndex, insertionIndex, codeBlockType)) {
    return false
  }

  if (!dispatch) return true

  const insertionPosition = state.selection.$to.posAtIndex(insertionIndex, 0)
  const transaction = state.tr.insert(insertionPosition, mermaidNode)
  const requestedOffset = payload?.cursorOffset ?? mermaidNode.content.size
  const cursorOffset = Math.min(Math.max(0, requestedOffset), mermaidNode.content.size)
  transaction.setSelection(
    TextSelection.create(transaction.doc, insertionPosition + 1 + cursorOffset),
  )
  dispatch(transaction.scrollIntoView())
  return true
})

export function getTableCommandAvailability(state: EditorState): TableCommandAvailability {
  if (!isInTable(state)) {
    return {
      isInTable: false,
      canAddRow: false,
      canAddColumn: false,
      canDeleteRow: false,
      canDeleteColumn: false,
      canDeleteTable: false,
    }
  }

  const rectangle = selectedRect(state)
  const selectedRows = rectangle.bottom - rectangle.top
  const selectedColumns = rectangle.right - rectangle.left

  return {
    isInTable: true,
    canAddRow: true,
    canAddColumn: true,
    // GFM requires one header row followed by at least one body row.
    canDeleteRow: rectangle.top > 0 && rectangle.map.height - selectedRows >= 2,
    canDeleteColumn: rectangle.map.width - selectedColumns >= 1,
    canDeleteTable: true,
  }
}

/** Deletes body rows only, preserving GFM's required header and final body row. */
export const deleteTableRowCommand = $command<undefined, 'DeleteKnowledgeDocumentTableRow'>(
  'DeleteKnowledgeDocumentTableRow',
  () => () => (state, dispatch) => {
    if (!getTableCommandAvailability(state).canDeleteRow) return false
    return deleteRow(state, dispatch)
  },
)

/** Deletes selected columns while preserving at least one table column. */
export const deleteTableColumnCommand = $command<undefined, 'DeleteKnowledgeDocumentTableColumn'>(
  'DeleteKnowledgeDocumentTableColumn',
  () => () => (state, dispatch) => {
    if (!getTableCommandAvailability(state).canDeleteColumn) return false
    return deleteColumn(state, dispatch)
  },
)

export const deleteCurrentTableCommand = $command<undefined, 'DeleteKnowledgeDocumentTable'>(
  'DeleteKnowledgeDocumentTable',
  () => () => (state, dispatch) => {
    if (!getTableCommandAvailability(state).canDeleteTable) return false
    return deleteTable(state, dispatch)
  },
)

export const knowledgeDocumentEditorCommands = [
  toggleTaskListCommand,
  insertMermaidBlockCommand,
  deleteTableRowCommand,
  deleteTableColumnCommand,
  deleteCurrentTableCommand,
]
