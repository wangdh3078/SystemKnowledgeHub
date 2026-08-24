<script setup lang="ts">
import {
  ChatLineSquare,
  Check,
  EditPen,
  FullScreen,
  Grid,
  Link,
  List,
  Minus,
  Picture,
  RefreshLeft,
  RefreshRight,
  View,
} from '@element-plus/icons-vue'
import { commandsCtx, defaultValueCtx, Editor, editorViewCtx, rootCtx } from '@milkdown/core'
import { listener, listenerCtx } from '@milkdown/plugin-listener'
import type { MarkType } from '@milkdown/prose/model'
import { redo, undo } from '@milkdown/prose/history'
import type { EditorState } from '@milkdown/prose/state'
import {
  createCodeBlockCommand,
  insertHrCommand,
  linkSchema,
  toggleEmphasisCommand,
  toggleInlineCodeCommand,
  toggleStrongCommand,
  turnIntoTextCommand,
  wrapInBlockquoteCommand,
  wrapInBulletListCommand,
  wrapInHeadingCommand,
  wrapInOrderedListCommand,
} from '@milkdown/preset-commonmark'
import { gfm } from '@milkdown/preset-gfm'
import { callCommand, getMarkdown, type $Command } from '@milkdown/utils'
import { ElMessageBox } from 'element-plus'
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import KnowledgeDocumentMarkdown from '../markdown/KnowledgeDocumentMarkdown.vue'
import { canonicalizeLegacyBreakParagraphs } from '../markdown/legacyMarkdownBreaks'
import {
  applyBackgroundColorCommand,
  applyTextColorCommand,
  backgroundColorMark,
  clearBackgroundColorCommand,
  clearTextColorCommand,
  knowledgeDocumentColorExtension,
  textColorMark,
} from './colorMarks'
import {
  addColAfterCommand,
  addRowAfterCommand,
  deleteCurrentTableCommand,
  deleteTableColumnCommand,
  deleteTableRowCommand,
  getTableCommandAvailability,
  history,
  insertMermaidBlockCommand,
  insertTableCommand,
  knowledgeDocumentEditorCommands,
  redoCommand,
  toggleTaskListCommand,
  undoCommand,
} from './editorCommands'
import { knowledgeDocumentCommonmark } from './milkdownConfig'

type BlockType = 'paragraph' | 'h1' | 'h2' | 'h3' | 'h4' | 'h5' | 'h6'

interface PaletteColor {
  readonly value: string
  readonly label: string
}

const props = withDefaults(
  defineProps<{
    previewing?: boolean
    fullscreen?: boolean
    canSave?: boolean
    saving?: boolean
  }>(),
  {
    previewing: false,
    fullscreen: false,
    canSave: false,
    saving: false,
  },
)
const model = defineModel<string>({ required: true })
const emit = defineEmits<{
  ready: [markdown: string]
  save: []
  edit: []
  preview: []
  'toggle-fullscreen': []
}>()

const blockOptions: ReadonlyArray<{ readonly value: BlockType; readonly label: string }> = [
  { value: 'paragraph', label: '正文' },
  { value: 'h1', label: 'H1' },
  { value: 'h2', label: 'H2' },
  { value: 'h3', label: 'H3' },
  { value: 'h4', label: 'H4' },
  { value: 'h5', label: 'H5' },
  { value: 'h6', label: 'H6' },
]
const textPalette: readonly PaletteColor[] = [
  { value: '#E53935', label: '红' },
  { value: '#F57C00', label: '橙' },
  { value: '#F9A825', label: '黄' },
  { value: '#2E7D32', label: '绿' },
  { value: '#1565C0', label: '蓝' },
  { value: '#6A1B9A', label: '紫' },
  { value: '#616161', label: '灰' },
]
const backgroundPalette: readonly PaletteColor[] = [
  { value: '#FFCDD2', label: '浅红' },
  { value: '#FFE0B2', label: '浅橙' },
  { value: '#FFF3B0', label: '浅黄' },
  { value: '#C8E6C9', label: '浅绿' },
  { value: '#BBDEFB', label: '浅蓝' },
  { value: '#E1BEE7', label: '浅紫' },
  { value: '#E0E0E0', label: '浅灰' },
]

const editorRoot = ref<HTMLElement | null>(null)
const initializationError = ref<string | null>(null)
const editorReady = ref(false)
const blockType = ref<BlockType>('paragraph')
const taskListActive = ref(false)
const selectedTextColor = ref('')
const selectedBackgroundColor = ref('')
const canUndo = ref(false)
const canRedo = ref(false)
const inTable = ref(false)
const canDeleteTableRow = ref(false)
const canDeleteTableColumn = ref(false)
const tableDialogOpen = ref(false)
const tableRows = ref(3)
const tableColumns = ref(3)
const tooltipTriggers: ('hover' | 'focus')[] = ['hover', 'focus']
const formattingDisabled = computed(() => !editorReady.value || props.previewing)
const fullscreenLabel = computed(() => (props.fullscreen ? '退出全屏' : '全屏'))
let editor: Editor | null = null

function selectedUniformMarkHex(state: EditorState, markType: MarkType): string {
  if (state.selection.empty) {
    const marks = state.storedMarks ?? state.selection.$from.marks()
    const value: unknown = marks.find((mark) => mark.type === markType)?.attrs.hex
    return typeof value === 'string' ? value : ''
  }

  let uniformHex: string | null = null
  let sawText = false
  let mixed = false
  state.doc.nodesBetween(state.selection.from, state.selection.to, (node, position) => {
    if (!node.isText) return
    const intersectsSelection =
      position < state.selection.to && position + node.nodeSize > state.selection.from
    if (!intersectsSelection) return
    sawText = true
    const value: unknown = node.marks.find((mark) => mark.type === markType)?.attrs.hex
    if (typeof value !== 'string') {
      mixed = true
      return
    }
    if (uniformHex === null) uniformHex = value
    else if (uniformHex !== value) mixed = true
  })
  return sawText && !mixed ? (uniformHex ?? '') : ''
}

function refreshToolbarState(): void {
  if (!editor || !editorReady.value) return
  editor.action((ctx) => {
    const view = ctx.get(editorViewCtx)
    const { state } = view
    const { $from } = state.selection
    let nextBlockType: BlockType = 'paragraph'
    for (let depth = $from.depth; depth >= 0; depth -= 1) {
      const node = $from.node(depth)
      if (node.type.name !== 'heading') continue
      const level: unknown = node.attrs.level
      if (typeof level === 'number' && level >= 1 && level <= 6) {
        nextBlockType = `h${level}` as BlockType
      }
      break
    }
    blockType.value = nextBlockType

    taskListActive.value = false
    for (let depth = $from.depth; depth > 0; depth -= 1) {
      const node = $from.node(depth)
      if (node.type.name === 'list_item' && node.attrs.checked != null) {
        taskListActive.value = true
        break
      }
    }

    const textColorType = textColorMark.type(ctx)
    const backgroundColorType = backgroundColorMark.type(ctx)
    selectedTextColor.value = selectedUniformMarkHex(state, textColorType)
    selectedBackgroundColor.value = selectedUniformMarkHex(state, backgroundColorType)

    canUndo.value = undo(state)
    canRedo.value = redo(state)
    const tableAvailability = getTableCommandAvailability(state)
    inTable.value = tableAvailability.isInTable
    canDeleteTableRow.value = tableAvailability.canDeleteRow
    canDeleteTableColumn.value = tableAvailability.canDeleteColumn
  })
}

function run<T>(command: $Command<T>, payload?: T): boolean {
  if (!editor || !editorReady.value) return false
  const result = editor.action(callCommand(command.key, payload))
  if (result) {
    editor.action((ctx) => ctx.get(editorViewCtx).focus())
  }
  queueMicrotask(refreshToolbarState)
  return result
}

function canRun<T>(command: $Command<T>, payload?: T): boolean {
  if (!editor || !editorReady.value) return false
  return editor.action((ctx) => {
    const view = ctx.get(editorViewCtx)
    return ctx.get(commandsCtx).get(command.key)(payload)(view.state)
  })
}

function changeBlockType(value: unknown): void {
  if (typeof value !== 'string') return
  if (value === 'paragraph') {
    run(turnIntoTextCommand)
    return
  }
  if (!/^h[1-6]$/.test(value)) return
  run(wrapInHeadingCommand, Number(value.slice(1)))
}

function selectedEditorText(): string {
  if (!editor || !editorReady.value) return ''
  return editor.action((ctx) => {
    const { state } = ctx.get(editorViewCtx)
    return state.doc.textBetween(state.selection.from, state.selection.to, ' ')
  })
}

async function addLink(): Promise<void> {
  if (formattingDisabled.value || !editor) return
  try {
    const currentText = selectedEditorText()
    const textResponse = await ElMessageBox.prompt(
      '请输入链接显示文本。已选中的文本会作为默认值。',
      '插入链接',
      {
        confirmButtonText: '下一步',
        cancelButtonText: '取消',
        inputValue: currentText,
        inputPattern: /\S+/,
        inputErrorMessage: '显示文本不能为空。',
      },
    )
    const urlResponse = await ElMessageBox.prompt(
      '请输入 http(s)、mailto 或站内相对地址。',
      '插入链接',
      {
        confirmButtonText: '插入',
        cancelButtonText: '取消',
        inputPattern: /^(https?:\/\/|mailto:|\/)[^\s]+$/,
        inputErrorMessage: '请输入安全的链接地址。',
      },
    )
    const displayText = textResponse.value.trim()
    const href = urlResponse.value.trim()
    if (!displayText || !/^(https?:\/\/|mailto:|\/)[^\s]+$/.test(href)) return

    editor.action((ctx) => {
      const view = ctx.get(editorViewCtx)
      const link = linkSchema.type(ctx).create({ href, title: null })
      const node = view.state.schema.text(displayText, [link])
      view.dispatch(view.state.tr.replaceSelectionWith(node, false).scrollIntoView())
      view.focus()
    })
    queueMicrotask(refreshToolbarState)
  } catch {
    // Cancelling either prompt leaves the Markdown source unchanged.
  }
}

async function createCodeBlock(): Promise<void> {
  if (formattingDisabled.value) return
  try {
    const { value } = await ElMessageBox.prompt(
      '请输入代码语言，可留空；例如 sql、json、plain。',
      '插入代码块',
      {
        confirmButtonText: '插入',
        cancelButtonText: '取消',
        inputPattern: /^[A-Za-z0-9_+-]*$/,
        inputErrorMessage: '语言标识只能包含字母、数字、_、+ 或 -。',
      },
    )
    run(createCodeBlockCommand, value.trim().toLowerCase())
  } catch {
    // Cancelling the language prompt leaves the Markdown source unchanged.
  }
}

function openTableDialog(): void {
  if (formattingDisabled.value || !canRun(insertTableCommand, { row: 3, col: 3 })) return
  tableRows.value = 3
  tableColumns.value = 3
  tableDialogOpen.value = true
}

function insertTable(): void {
  const row = Math.min(10, Math.max(2, tableRows.value))
  const col = Math.min(10, Math.max(2, tableColumns.value))
  if (run(insertTableCommand, { row, col })) tableDialogOpen.value = false
}

function applyTextColor(value: unknown): void {
  if (typeof value === 'string' && textPalette.some((color) => color.value === value)) {
    run(applyTextColorCommand, value)
  }
}

function applyBackgroundColor(value: unknown): void {
  if (typeof value === 'string' && backgroundPalette.some((color) => color.value === value)) {
    run(applyBackgroundColorCommand, value)
  }
}

onMounted(async () => {
  if (!editorRoot.value) return
  try {
    editor = await Editor.make()
      .config((ctx) => {
        ctx.set(rootCtx, editorRoot.value)
        ctx.set(defaultValueCtx, canonicalizeLegacyBreakParagraphs(model.value))
        ctx
          .get(listenerCtx)
          .markdownUpdated((_ctx, markdown) => {
            model.value = canonicalizeLegacyBreakParagraphs(markdown)
            queueMicrotask(refreshToolbarState)
          })
          .updated(() => queueMicrotask(refreshToolbarState))
          .selectionUpdated(() => queueMicrotask(refreshToolbarState))
      })
      .use(knowledgeDocumentCommonmark)
      .use(gfm)
      .use(history)
      .use(knowledgeDocumentEditorCommands)
      .use(knowledgeDocumentColorExtension)
      .use(listener)
      .create()
    editorReady.value = true
    const markdown = canonicalizeLegacyBreakParagraphs(editor.action(getMarkdown()))
    model.value = markdown
    emit('ready', markdown)
    refreshToolbarState()
  } catch (reason: unknown) {
    editorReady.value = false
    initializationError.value =
      reason instanceof Error ? reason.message : '无法初始化 Markdown 编辑器。'
  }
})

onBeforeUnmount(() => {
  editorReady.value = false
  if (editor) void editor.destroy()
  editor = null
})
</script>

<template>
  <section
    :class="['knowledge-document-editor', { 'is-fullscreen': fullscreen }]"
    aria-label="Markdown 编辑器"
  >
    <div class="knowledge-document-editor__toolbar" role="toolbar" aria-label="Markdown 编辑工具">
      <div class="knowledge-document-editor__tool-group">
        <el-tooltip content="段落与标题级别" placement="top" :trigger="tooltipTriggers">
          <el-select
            :model-value="blockType"
            class="knowledge-document-editor__block-select"
            size="small"
            aria-label="段落与标题"
            :disabled="formattingDisabled"
            @change="changeBlockType"
          >
            <el-option
              v-for="option in blockOptions"
              :key="option.value"
              :label="option.label"
              :value="option.value"
            />
          </el-select>
        </el-tooltip>
      </div>
      <div class="knowledge-document-editor__tool-group">
        <el-tooltip content="加粗（Ctrl+B）" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="加粗"
            size="small"
            :disabled="formattingDisabled"
            @click="run(toggleStrongCommand)"
            ><strong>B</strong></el-button
          ></el-tooltip
        >
        <el-tooltip content="斜体（Ctrl+I）" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="斜体"
            size="small"
            :disabled="formattingDisabled"
            @click="run(toggleEmphasisCommand)"
            ><em>I</em></el-button
          ></el-tooltip
        >
        <el-tooltip content="行内代码" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button knowledge-document-editor__code-label"
            aria-label="行内代码"
            size="small"
            :disabled="formattingDisabled"
            @click="run(toggleInlineCodeCommand)"
            >&lt;/&gt;</el-button
          ></el-tooltip
        >
      </div>
      <div class="knowledge-document-editor__tool-group">
        <el-tooltip content="无序列表" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="无序列表"
            size="small"
            :disabled="formattingDisabled"
            @click="run(wrapInBulletListCommand)"
            ><el-icon><List /></el-icon></el-button
        ></el-tooltip>
        <el-tooltip content="有序列表" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button knowledge-document-editor__numbered-button"
            aria-label="有序列表"
            size="small"
            :disabled="formattingDisabled"
            @click="run(wrapInOrderedListCommand)"
            >1.</el-button
          ></el-tooltip
        >
        <el-tooltip content="任务列表" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="任务列表"
            :aria-pressed="taskListActive"
            size="small"
            :disabled="formattingDisabled"
            @click="run(toggleTaskListCommand)"
            ><el-icon><Check /></el-icon></el-button
        ></el-tooltip>
        <el-tooltip content="引用" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="引用"
            size="small"
            :disabled="formattingDisabled"
            @click="run(wrapInBlockquoteCommand)"
            ><el-icon><ChatLineSquare /></el-icon></el-button
        ></el-tooltip>
        <el-tooltip content="代码块" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button knowledge-document-editor__code-label"
            aria-label="代码块"
            size="small"
            :disabled="formattingDisabled"
            @click="createCodeBlock"
            >{ }</el-button
          ></el-tooltip
        >
      </div>
      <div class="knowledge-document-editor__tool-group">
        <el-tooltip content="插入链接" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="插入链接"
            size="small"
            :disabled="formattingDisabled"
            @click="addLink"
            ><el-icon><Link /></el-icon></el-button
        ></el-tooltip>
        <el-tooltip content="插入表格（2×2 至 10×10）" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="插入表格"
            size="small"
            :disabled="formattingDisabled"
            @click="openTableDialog"
            ><el-icon><Grid /></el-icon></el-button
        ></el-tooltip>
        <el-tooltip content="插入 Mermaid" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button knowledge-document-editor__mermaid-button"
            aria-label="插入 Mermaid"
            size="small"
            :disabled="formattingDisabled"
            @click="run(insertMermaidBlockCommand)"
            >M</el-button
          ></el-tooltip
        >
        <el-tooltip content="插入分隔线" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="插入分隔线"
            size="small"
            :disabled="formattingDisabled"
            @click="run(insertHrCommand)"
            ><el-icon><Minus /></el-icon></el-button
        ></el-tooltip>
        <el-tooltip
          content="图片上传将在附件管理功能中启用"
          placement="top"
          :trigger="tooltipTriggers"
        >
          <span class="knowledge-document-editor__disabled-tooltip" tabindex="0"
            ><el-button
              class="knowledge-document-editor__placeholder-button"
              aria-label="图片上传（待接入）"
              size="small"
              disabled
              ><el-icon><Picture /></el-icon>图片（待接入）</el-button
            ></span
          >
        </el-tooltip>
      </div>
      <div class="knowledge-document-editor__tool-group knowledge-document-editor__color-group">
        <el-tooltip content="文字颜色" placement="top" :trigger="tooltipTriggers">
          <el-select
            :model-value="selectedTextColor"
            class="knowledge-document-editor__color-select"
            size="small"
            placeholder="文字颜色"
            aria-label="文字颜色"
            :disabled="formattingDisabled"
            @change="applyTextColor"
          >
            <el-option
              v-for="color in textPalette"
              :key="`text-${color.value}`"
              :label="color.label"
              :value="color.value"
              ><span class="knowledge-document-editor__palette-option"
                ><i :style="{ backgroundColor: color.value }"></i>{{ color.label }}</span
              ></el-option
            >
          </el-select>
        </el-tooltip>
        <el-tooltip content="清除文字颜色" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button knowledge-document-editor__clear-color"
            aria-label="清除文字颜色"
            size="small"
            :disabled="formattingDisabled"
            @click="run(clearTextColorCommand)"
            >A×</el-button
          ></el-tooltip
        >
        <el-tooltip content="背景颜色" placement="top" :trigger="tooltipTriggers">
          <el-select
            :model-value="selectedBackgroundColor"
            class="knowledge-document-editor__color-select"
            size="small"
            placeholder="背景颜色"
            aria-label="背景颜色"
            :disabled="formattingDisabled"
            @change="applyBackgroundColor"
          >
            <el-option
              v-for="color in backgroundPalette"
              :key="`background-${color.value}`"
              :label="color.label"
              :value="color.value"
              ><span class="knowledge-document-editor__palette-option"
                ><i :style="{ backgroundColor: color.value }"></i>{{ color.label }}</span
              ></el-option
            >
          </el-select>
        </el-tooltip>
        <el-tooltip content="清除背景颜色" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button knowledge-document-editor__clear-color"
            aria-label="清除背景颜色"
            size="small"
            :disabled="formattingDisabled"
            @click="run(clearBackgroundColorCommand)"
            >▧×</el-button
          ></el-tooltip
        >
      </div>
      <div class="knowledge-document-editor__tool-group knowledge-document-editor__workspace-group">
        <el-tooltip content="撤销（Ctrl+Z）" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="撤销"
            size="small"
            :disabled="formattingDisabled || !canUndo"
            @click="run(undoCommand)"
            ><el-icon><RefreshLeft /></el-icon></el-button
        ></el-tooltip>
        <el-tooltip
          content="重做（Ctrl+Y / Ctrl+Shift+Z）"
          placement="top"
          :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="重做"
            size="small"
            :disabled="formattingDisabled || !canRedo"
            @click="run(redoCommand)"
            ><el-icon><RefreshRight /></el-icon></el-button
        ></el-tooltip>
        <el-tooltip content="保存（Ctrl+S）" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__action-button"
            aria-label="保存"
            size="small"
            type="primary"
            :disabled="!canSave || saving"
            :loading="saving"
            @click="emit('save')"
            >{{ saving ? '保存中…' : '保存' }}</el-button
          ></el-tooltip
        >
      </div>
      <div class="knowledge-document-editor__tool-group knowledge-document-editor__workspace-group">
        <el-tooltip content="编辑" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__action-button"
            aria-label="编辑"
            :aria-pressed="!previewing"
            size="small"
            :type="previewing ? 'default' : 'primary'"
            plain
            @click="emit('edit')"
            ><el-icon><EditPen /></el-icon>编辑</el-button
          ></el-tooltip
        >
        <el-tooltip content="预览" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__action-button"
            aria-label="预览"
            :aria-pressed="previewing"
            size="small"
            :type="previewing ? 'primary' : 'default'"
            plain
            @click="emit('preview')"
            ><el-icon><View /></el-icon>预览</el-button
          ></el-tooltip
        >
        <el-tooltip :content="fullscreenLabel" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__action-button"
            :aria-label="fullscreenLabel"
            :aria-pressed="fullscreen"
            size="small"
            @click="emit('toggle-fullscreen')"
            ><el-icon><FullScreen /></el-icon>{{ fullscreenLabel }}</el-button
          ></el-tooltip
        >
      </div>
    </div>

    <div
      v-if="inTable && !previewing"
      class="knowledge-document-editor__table-tools"
      role="toolbar"
      aria-label="表格操作"
    >
      <span>表格操作</span>
      <el-tooltip content="下方添加行" placement="top" :trigger="tooltipTriggers"
        ><el-button aria-label="下方添加行" size="small" @click="run(addRowAfterCommand)"
          >+ 行</el-button
        ></el-tooltip
      >
      <el-tooltip content="删除当前行" placement="top" :trigger="tooltipTriggers"
        ><el-button
          aria-label="删除当前行"
          size="small"
          :disabled="!canDeleteTableRow"
          @click="run(deleteTableRowCommand)"
          >− 行</el-button
        ></el-tooltip
      >
      <el-tooltip content="右侧添加列" placement="top" :trigger="tooltipTriggers"
        ><el-button aria-label="右侧添加列" size="small" @click="run(addColAfterCommand)"
          >+ 列</el-button
        ></el-tooltip
      >
      <el-tooltip content="删除当前列" placement="top" :trigger="tooltipTriggers"
        ><el-button
          aria-label="删除当前列"
          size="small"
          :disabled="!canDeleteTableColumn"
          @click="run(deleteTableColumnCommand)"
          >− 列</el-button
        ></el-tooltip
      >
      <el-tooltip content="删除整个表格" placement="top" :trigger="tooltipTriggers"
        ><el-button
          aria-label="删除整个表格"
          size="small"
          type="danger"
          plain
          @click="run(deleteCurrentTableCommand)"
          >删除表格</el-button
        ></el-tooltip
      >
    </div>

    <p v-if="initializationError" class="knowledge-document-error">
      编辑器加载失败，请刷新后重试。
    </p>
    <div v-show="!previewing" ref="editorRoot" class="knowledge-document-editor__surface"></div>
    <div v-show="previewing" class="knowledge-document-editor__preview" aria-live="polite">
      <p class="knowledge-document-editor__preview-note">预览未保存内容</p>
      <KnowledgeDocumentMarkdown :markdown="model" />
    </div>

    <el-dialog v-model="tableDialogOpen" title="插入表格" width="380px" append-to-body>
      <p class="knowledge-document-editor__dialog-help">选择 2×2 至 10×10；第一行为表头。</p>
      <div class="knowledge-document-editor__table-size">
        <label
          >行数<el-input-number v-model="tableRows" :min="2" :max="10" aria-label="表格行数"
        /></label>
        <span>×</span>
        <label
          >列数<el-input-number v-model="tableColumns" :min="2" :max="10" aria-label="表格列数"
        /></label>
      </div>
      <template #footer>
        <el-button @click="tableDialogOpen = false">取消</el-button>
        <el-button type="primary" @click="insertTable">插入表格</el-button>
      </template>
    </el-dialog>
  </section>
</template>
