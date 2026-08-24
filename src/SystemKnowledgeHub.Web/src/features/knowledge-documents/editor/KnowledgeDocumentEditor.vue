<script setup lang="ts">
import {
  Check,
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
import { history, historyKeymap, redo, redoDepth, undo, undoDepth } from '@codemirror/commands'
import { defaultHighlightStyle, syntaxHighlighting } from '@codemirror/language'
import { markdown } from '@codemirror/lang-markdown'
import { EditorState } from '@codemirror/state'
import { EditorView, keymap } from '@codemirror/view'
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import KnowledgeDocumentMarkdown from '../markdown/KnowledgeDocumentMarkdown.vue'
import {
  applyHeading,
  insertCodeBlock,
  insertHorizontalRule,
  insertLink,
  insertMermaid,
  insertTable,
  toggleBulletList,
  toggleInlineWrap,
  toggleOrderedList,
  toggleQuote,
  toggleTaskList,
  type MarkdownHeadingLevel,
  type MarkdownSourceTransformResult,
} from './sourceMarkdownTransforms'

const props = withDefaults(
  defineProps<{
    previewing?: boolean
    fullscreen?: boolean
  }>(),
  { previewing: false, fullscreen: false },
)
const model = defineModel<string>({ required: true })
const emit = defineEmits<{
  edit: []
  preview: []
  'request-save': []
  'toggle-fullscreen': []
}>()

type CodeLanguage =
  | 'plaintext'
  | 'csharp'
  | 'javascript'
  | 'typescript'
  | 'json'
  | 'sql'
  | 'bash'
  | 'powershell'
  | 'python'
  | 'java'
  | 'cpp'
  | 'c'
  | 'go'
  | 'rust'
  | 'html'
  | 'css'
  | 'xml'
  | 'yaml'
  | 'markdown'
  | 'dockerfile'

const blockOptions: ReadonlyArray<{
  readonly value: MarkdownHeadingLevel
  readonly label: string
}> = [
  { value: 'paragraph', label: '正文' },
  { value: 'h1', label: 'H1' },
  { value: 'h2', label: 'H2' },
  { value: 'h3', label: 'H3' },
  { value: 'h4', label: 'H4' },
  { value: 'h5', label: 'H5' },
  { value: 'h6', label: 'H6' },
]
const codeLanguages: ReadonlyArray<{ readonly value: CodeLanguage; readonly label: string }> = [
  { value: 'plaintext', label: 'Plain text' },
  { value: 'csharp', label: 'C#' },
  { value: 'javascript', label: 'JavaScript' },
  { value: 'typescript', label: 'TypeScript' },
  { value: 'json', label: 'JSON' },
  { value: 'sql', label: 'SQL' },
  { value: 'bash', label: 'Bash' },
  { value: 'powershell', label: 'PowerShell' },
  { value: 'python', label: 'Python' },
  { value: 'java', label: 'Java' },
  { value: 'cpp', label: 'C++' },
  { value: 'c', label: 'C' },
  { value: 'go', label: 'Go' },
  { value: 'rust', label: 'Rust' },
  { value: 'html', label: 'HTML' },
  { value: 'css', label: 'CSS' },
  { value: 'xml', label: 'XML' },
  { value: 'yaml', label: 'YAML' },
  { value: 'markdown', label: 'Markdown' },
  { value: 'dockerfile', label: 'Dockerfile' },
]
const tooltipTriggers: ('hover' | 'focus')[] = ['hover', 'focus']
const editorRoot = ref<HTMLElement | null>(null)
const sourceReady = ref(false)
const tableDialogOpen = ref(false)
const linkDialogOpen = ref(false)
const codeDialogOpen = ref(false)
const tableRows = ref(3)
const tableColumns = ref(3)
const codeLanguage = ref<CodeLanguage>('plaintext')
const linkUrl = ref('')
const linkDisplayText = ref('')
const linkError = ref<string | null>(null)
const canUndo = ref(false)
const canRedo = ref(false)
const fullscreenLabel = computed(() => (props.fullscreen ? '退出全屏' : '全屏'))
const formattingDisabled = computed(() => !sourceReady.value || props.previewing)
let view: EditorView | null = null
let applyingExternalSource = false

function refreshHistoryState(): void {
  if (!view) return
  canUndo.value = undoDepth(view.state) > 0
  canRedo.value = redoDepth(view.state) > 0
}

function currentSelection(): { readonly anchor: number; readonly head: number } | null {
  return view?.state.selection.main ?? null
}

function selectedSource(): string {
  if (!view) return ''
  const selection = view.state.selection.main
  return view.state.doc.sliceString(selection.from, selection.to)
}

function applyTransform(
  transform: (
    source: string,
    selection: { readonly anchor: number; readonly head: number },
  ) => MarkdownSourceTransformResult,
): void {
  if (formattingDisabled.value || !view) return
  const selection = currentSelection()
  if (!selection) return
  const next = transform(view.state.doc.toString(), selection)
  view.dispatch({
    changes: { from: 0, to: view.state.doc.length, insert: next.source },
    selection: next.selection,
    scrollIntoView: true,
  })
  view.focus()
  refreshHistoryState()
}

function changeBlockType(value: unknown): void {
  const option = blockOptions.find((candidate) => candidate.value === value)
  if (!option) return
  applyTransform((source, selection) => applyHeading(source, selection, option.value))
}

function openLinkDialog(): void {
  if (formattingDisabled.value) return
  linkDisplayText.value = selectedSource()
  linkUrl.value = ''
  linkError.value = null
  linkDialogOpen.value = true
}

function isSafeLinkUrl(value: string): boolean {
  return /^(https?:\/\/|mailto:|\/)[^\s]+$/i.test(value)
}

function insertLinkFromDialog(): void {
  const text = linkDisplayText.value.trim()
  const url = linkUrl.value.trim()
  if (!text) {
    linkError.value = '请填写链接显示文本。'
    return
  }
  if (!isSafeLinkUrl(url)) {
    linkError.value = '仅支持 http(s)、mailto 或站内相对链接。'
    return
  }
  applyTransform((source, selection) => insertLink(source, selection, text, url))
  linkDialogOpen.value = false
}

function openCodeDialog(): void {
  if (formattingDisabled.value) return
  codeLanguage.value = 'plaintext'
  codeDialogOpen.value = true
}

function insertCodeFromDialog(): void {
  applyTransform((source, selection) => insertCodeBlock(source, selection, codeLanguage.value))
  codeDialogOpen.value = false
}

function openTableDialog(): void {
  if (formattingDisabled.value) return
  tableRows.value = 3
  tableColumns.value = 3
  tableDialogOpen.value = true
}

function insertTableFromDialog(): void {
  applyTransform((source, selection) =>
    insertTable(source, selection, tableRows.value, tableColumns.value),
  )
  tableDialogOpen.value = false
}

function undoSource(): void {
  if (formattingDisabled.value || !view) return
  undo(view)
  view.focus()
  refreshHistoryState()
}

function redoSource(): void {
  if (formattingDisabled.value || !view) return
  redo(view)
  view.focus()
  refreshHistoryState()
}

function replaceExternalSource(nextSource: string): void {
  if (!view || view.state.doc.toString() === nextSource) return
  applyingExternalSource = true
  view.dispatch({ changes: { from: 0, to: view.state.doc.length, insert: nextSource } })
  applyingExternalSource = false
  refreshHistoryState()
}

watch(() => model.value, replaceExternalSource)

onMounted(() => {
  if (!editorRoot.value) return
  view = new EditorView({
    state: EditorState.create({
      doc: model.value,
      extensions: [
        history(),
        markdown(),
        syntaxHighlighting(defaultHighlightStyle),
        EditorView.lineWrapping,
        keymap.of([
          {
            key: 'Mod-s',
            run: () => {
              emit('request-save')
              return true
            },
          },
          ...historyKeymap,
        ]),
        EditorView.updateListener.of((update) => {
          if (!update.docChanged) return
          if (!applyingExternalSource) model.value = update.state.doc.toString()
          refreshHistoryState()
        }),
      ],
    }),
    parent: editorRoot.value,
  })
  sourceReady.value = true
  refreshHistoryState()
  void nextTick(() => view?.focus())
})

onBeforeUnmount(() => {
  sourceReady.value = false
  view?.destroy()
  view = null
})
</script>

<template>
  <section
    :class="['knowledge-document-editor', { 'is-fullscreen': fullscreen }]"
    aria-label="Markdown 源码编辑器"
  >
    <div class="knowledge-document-editor__toolbar" role="toolbar" aria-label="Markdown 源码工具">
      <div class="knowledge-document-editor__tool-group">
        <el-tooltip content="段落与标题级别" placement="top" :trigger="tooltipTriggers">
          <el-select
            class="knowledge-document-editor__block-select"
            size="small"
            aria-label="段落与标题"
            :model-value="'paragraph'"
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
            @click="
              applyTransform((source, selection) => toggleInlineWrap(source, selection, '**'))
            "
            ><strong>B</strong></el-button
          ></el-tooltip
        >
        <el-tooltip content="斜体（Ctrl+I）" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="斜体"
            size="small"
            :disabled="formattingDisabled"
            @click="applyTransform((source, selection) => toggleInlineWrap(source, selection, '*'))"
            ><em>I</em></el-button
          ></el-tooltip
        >
        <el-tooltip content="行内代码" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button knowledge-document-editor__code-label"
            aria-label="行内代码"
            size="small"
            :disabled="formattingDisabled"
            @click="applyTransform((source, selection) => toggleInlineWrap(source, selection, '`'))"
            >&lt;/&gt;</el-button
          ></el-tooltip
        >
      </div>
      <div class="knowledge-document-editor__tool-group">
        <el-tooltip content="引用" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="引用"
            size="small"
            :disabled="formattingDisabled"
            @click="applyTransform(toggleQuote)"
            >❝</el-button
          ></el-tooltip
        >
        <el-tooltip content="无序列表" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="无序列表"
            size="small"
            :disabled="formattingDisabled"
            @click="applyTransform(toggleBulletList)"
            ><el-icon><List /></el-icon></el-button
        ></el-tooltip>
        <el-tooltip content="有序列表" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button knowledge-document-editor__numbered-button"
            aria-label="有序列表"
            size="small"
            :disabled="formattingDisabled"
            @click="applyTransform(toggleOrderedList)"
            >1.</el-button
          ></el-tooltip
        >
        <el-tooltip content="任务列表" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="任务列表"
            size="small"
            :disabled="formattingDisabled"
            @click="applyTransform(toggleTaskList)"
            ><el-icon><Check /></el-icon></el-button
        ></el-tooltip>
      </div>
      <div class="knowledge-document-editor__tool-group">
        <el-tooltip content="插入代码块" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button knowledge-document-editor__code-label"
            aria-label="插入代码块"
            size="small"
            :disabled="formattingDisabled"
            @click="openCodeDialog"
            >{ }</el-button
          ></el-tooltip
        >
        <el-tooltip content="插入链接" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="插入链接"
            size="small"
            :disabled="formattingDisabled"
            @click="openLinkDialog"
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
            @click="applyTransform(insertMermaid)"
            >M</el-button
          ></el-tooltip
        >
        <el-tooltip content="插入分隔线" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="插入分隔线"
            size="small"
            :disabled="formattingDisabled"
            @click="applyTransform(insertHorizontalRule)"
            ><el-icon><Minus /></el-icon></el-button
        ></el-tooltip>
        <el-tooltip content="图片上传功能开发中" placement="top" :trigger="tooltipTriggers"
          ><span class="knowledge-document-editor__disabled-tooltip" tabindex="0"
            ><el-button
              class="knowledge-document-editor__icon-button"
              aria-label="图片上传功能开发中"
              size="small"
              disabled
              ><el-icon><Picture /></el-icon></el-button></span
        ></el-tooltip>
      </div>
      <div class="knowledge-document-editor__tool-group knowledge-document-editor__workspace-group">
        <el-tooltip content="撤销（Ctrl+Z）" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="撤销"
            size="small"
            :disabled="formattingDisabled || !canUndo"
            @click="undoSource"
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
            @click="redoSource"
            ><el-icon><RefreshRight /></el-icon></el-button
        ></el-tooltip>
      </div>
      <div class="knowledge-document-editor__tool-group knowledge-document-editor__workspace-group">
        <el-tooltip content="编辑源码" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__action-button"
            aria-label="编辑源码"
            :aria-pressed="!previewing"
            size="small"
            :type="previewing ? 'default' : 'primary'"
            plain
            @click="emit('edit')"
            >源码</el-button
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
            class="knowledge-document-editor__icon-button"
            :aria-label="fullscreenLabel"
            :aria-pressed="fullscreen"
            size="small"
            @click="emit('toggle-fullscreen')"
            ><el-icon><FullScreen /></el-icon></el-button
        ></el-tooltip>
      </div>
    </div>

    <div
      v-show="!previewing"
      ref="editorRoot"
      class="knowledge-document-editor__source"
      aria-label="Markdown 原始源码"
    ></div>
    <div v-show="previewing" class="knowledge-document-editor__preview" aria-live="polite">
      <p class="knowledge-document-editor__preview-note">预览未保存内容</p>
      <KnowledgeDocumentMarkdown :markdown="model" />
    </div>

    <el-dialog v-model="linkDialogOpen" title="插入链接" width="420px" append-to-body>
      <el-form label-position="top">
        <el-form-item label="URL" required
          ><el-input v-model="linkUrl" aria-label="链接 URL" placeholder="https://example.com"
        /></el-form-item>
        <el-form-item label="显示文本"
          ><el-input v-model="linkDisplayText" aria-label="链接显示文本"
        /></el-form-item>
        <el-form-item label="页面标题状态"
          ><p class="knowledge-document-editor__dialog-status">
            本阶段暂不自动读取页面标题。
          </p></el-form-item
        >
      </el-form>
      <p v-if="linkError" class="knowledge-document-error">{{ linkError }}</p>
      <template #footer
        ><el-button @click="linkDialogOpen = false">取消</el-button
        ><el-button type="primary" @click="insertLinkFromDialog">插入链接</el-button></template
      >
    </el-dialog>

    <el-dialog v-model="codeDialogOpen" title="插入代码块" width="420px" append-to-body>
      <el-form label-position="top"
        ><el-form-item label="代码语言"
          ><el-select v-model="codeLanguage" filterable aria-label="代码语言"
            ><el-option
              v-for="language in codeLanguages"
              :key="language.value"
              :label="language.label"
              :value="language.value" /></el-select></el-form-item
      ></el-form>
      <template #footer
        ><el-button @click="codeDialogOpen = false">取消</el-button
        ><el-button type="primary" @click="insertCodeFromDialog">插入代码块</el-button></template
      >
    </el-dialog>

    <el-dialog v-model="tableDialogOpen" title="插入表格" width="380px" append-to-body>
      <p class="knowledge-document-editor__dialog-help">选择 2×2 至 10×10；第一行为表头。</p>
      <div class="knowledge-document-editor__table-size">
        <label
          >行数<el-input-number
            v-model="tableRows"
            :min="2"
            :max="10"
            aria-label="表格行数" /></label
        ><span>×</span
        ><label
          >列数<el-input-number v-model="tableColumns" :min="2" :max="10" aria-label="表格列数"
        /></label>
      </div>
      <template #footer
        ><el-button @click="tableDialogOpen = false">取消</el-button
        ><el-button type="primary" @click="insertTableFromDialog">插入表格</el-button></template
      >
    </el-dialog>
  </section>
</template>
