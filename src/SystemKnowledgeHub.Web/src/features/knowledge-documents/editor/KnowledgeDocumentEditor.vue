<script setup lang="ts">
import { Minus } from '@element-plus/icons-vue'
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome'
import {
  faCode,
  faDiagramProject,
  faExpand,
  faFileCode,
  faImage,
  faLink,
  faListCheck,
  faListOl,
  faListUl,
  faQuoteLeft,
  faRotateLeft,
  faRotateRight,
  faTable,
  faEye,
  faCompress,
} from '@fortawesome/free-solid-svg-icons'
import { history, historyKeymap, redo, redoDepth, undo, undoDepth } from '@codemirror/commands'
import { defaultHighlightStyle, syntaxHighlighting } from '@codemirror/language'
import { markdown } from '@codemirror/lang-markdown'
import { EditorState } from '@codemirror/state'
import { EditorView, keymap } from '@codemirror/view'
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { ApiError, NetworkRequestError } from '../../../api/errors/ApiError'
import type { AttachmentMetadata } from '../api/attachmentContracts'
import { uploadKnowledgeDocumentImage } from '../api/knowledgeDocumentAttachmentsApi'
import KnowledgeDocumentMarkdown from '../markdown/KnowledgeDocumentMarkdown.vue'
import type { MarkdownAttachmentImageContext } from '../markdown/renderMarkdown'
import {
  applyHeading,
  insertCodeBlock,
  insertHorizontalRule,
  insertLink,
  insertMermaidDiagram,
  insertTable,
  toggleBulletList,
  toggleInlineWrap,
  toggleOrderedList,
  toggleQuote,
  toggleTaskList,
  type MarkdownHeadingLevel,
  type MarkdownSourceTransformResult,
  type MermaidDiagramType,
} from './sourceMarkdownTransforms'

const props = withDefaults(
  defineProps<{
    previewing?: boolean
    fullscreen?: boolean
    viewport?: 'detail' | 'dialog'
    documentId?: number
    attachmentReferences?: readonly AttachmentMetadata[]
  }>(),
  {
    previewing: false,
    fullscreen: false,
    viewport: 'detail',
    documentId: undefined,
    attachmentReferences: () => [],
  },
)
const model = defineModel<string>({ required: true })
const emit = defineEmits<{
  edit: []
  preview: []
  'request-save': []
  'toggle-fullscreen': []
  'uploading-change': [uploading: boolean]
}>()

type CodeLanguage =
  | 'plaintext'
  | 'csharp'
  | 'javascript'
  | 'typescript'
  | 'tsx'
  | 'jsx'
  | 'vue'
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
  | 'scss'
  | 'less'
  | 'jsonc'
  | 'xml'
  | 'yaml'
  | 'markdown'
  | 'dockerfile'
  | 'kotlin'
  | 'php'
  | 'ruby'
  | 'shell'
  | 'batch'
  | 'plsql'
  | 'toml'
  | 'ini'
  | 'nginx'

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
  { value: 'tsx', label: 'TSX' },
  { value: 'jsx', label: 'JSX' },
  { value: 'vue', label: 'Vue SFC' },
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
  { value: 'scss', label: 'SCSS' },
  { value: 'less', label: 'Less' },
  { value: 'jsonc', label: 'JSONC' },
  { value: 'xml', label: 'XML' },
  { value: 'yaml', label: 'YAML' },
  { value: 'markdown', label: 'Markdown' },
  { value: 'dockerfile', label: 'Dockerfile' },
  { value: 'kotlin', label: 'Kotlin' },
  { value: 'php', label: 'PHP' },
  { value: 'ruby', label: 'Ruby' },
  { value: 'shell', label: 'Shell' },
  { value: 'batch', label: 'Batch' },
  { value: 'plsql', label: 'PL/SQL' },
  { value: 'toml', label: 'TOML' },
  { value: 'ini', label: 'INI' },
  { value: 'nginx', label: 'Nginx' },
]
const diagramOptions: ReadonlyArray<{
  readonly value: MermaidDiagramType
  readonly label: string
}> = [
  { value: 'flowchart', label: '流程图' },
  { value: 'sequence', label: '时序图' },
  { value: 'gantt', label: '甘特图' },
  { value: 'class', label: '类图' },
  { value: 'state', label: '状态图' },
  { value: 'pie', label: '饼图' },
  { value: 'er', label: '关系图' },
  { value: 'journey', label: '旅程图' },
]
const tooltipTriggers: ('hover' | 'focus')[] = ['hover', 'focus']
const editorRoot = ref<HTMLElement | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)
const sourceReady = ref(false)
const tableDialogOpen = ref(false)
const linkDialogOpen = ref(false)
const codeDialogOpen = ref(false)
const diagramMenuOpen = ref(false)
const tableRows = ref(3)
const tableColumns = ref(3)
const codeLanguage = ref<CodeLanguage>('plaintext')
const linkUrl = ref('')
const linkDisplayText = ref('')
const linkError = ref<string | null>(null)
const canUndo = ref(false)
const canRedo = ref(false)
const uploadingImages = ref(false)
const dragActive = ref(false)
const uploadMessage = ref<string | null>(null)
const uploadError = ref<string | null>(null)
const transientImageUrls = ref<ReadonlyMap<number, string>>(new Map())
const fullscreenLabel = computed(() => (props.fullscreen ? '退出全屏' : '全屏'))
const formattingDisabled = computed(() => !sourceReady.value || props.previewing)
const imageUploadDisabled = computed(
  () => formattingDisabled.value || uploadingImages.value || props.documentId === undefined,
)
const imageUploadTooltip = computed(() => {
  if (props.documentId === undefined) return '创建草稿后可插入图片'
  if (uploadingImages.value) return '图片上传中'
  return '插入图片'
})
const imageAccept = '.png,.jpg,.jpeg,.gif,.webp,image/png,image/jpeg,image/gif,image/webp'
const previewImageContext = computed<MarkdownAttachmentImageContext | undefined>(() => {
  if (props.documentId === undefined) return undefined
  const savedIds = props.attachmentReferences
    .filter((attachment) => attachment.kind === 'Image')
    .map((attachment) => attachment.attachmentId)
  return {
    documentId: props.documentId,
    imageAttachmentIds: [...new Set([...savedIds, ...transientImageUrls.value.keys()])],
    transientImageUrls: transientImageUrls.value,
  }
})
let view: EditorView | null = null
let applyingExternalSource = false
let uploadAbortController: AbortController | null = null
let unmounted = false
const pendingInsertionAnchors = new Set<{ position: number }>()

interface PendingImage {
  readonly file: File
  readonly altOverride?: string
}

const approvedExtensions = new Set(['.png', '.jpg', '.jpeg', '.gif', '.webp'])
const approvedMimeTypes = new Set(['image/png', 'image/jpeg', 'image/gif', 'image/webp'])

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

function insertDiagramTemplate(value: unknown): void {
  const option = diagramOptions.find((candidate) => candidate.value === value)
  if (!option) return
  applyTransform((source, selection) => insertMermaidDiagram(source, selection, option.value))
  diagramMenuOpen.value = false
}

function toggleDiagramMenu(): void {
  if (formattingDisabled.value) return
  diagramMenuOpen.value = !diagramMenuOpen.value
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

function openImagePicker(): void {
  if (imageUploadDisabled.value) return
  uploadError.value = null
  fileInput.value?.click()
}

function fileExtension(fileName: string): string {
  const match = /\.[^.]+$/u.exec(fileName.trim())
  return match?.[0].toLowerCase() ?? ''
}

function isApprovedImageCandidate(file: File): boolean {
  const extension = fileExtension(file.name)
  const mime = file.type.toLowerCase()
  return (
    approvedExtensions.has(extension) &&
    (mime === '' || mime === 'application/octet-stream' || approvedMimeTypes.has(mime))
  )
}

function normalizedAlt(value: string): string {
  return (
    value
      .replace(/[\r\n\t]+/gu, ' ')
      .replace(/\s+/gu, ' ')
      .trim() || '图片'
  )
}

function safeMarkdownAlt(value: string): string {
  return normalizedAlt(value).replace(/\\/gu, '\\\\').replace(/\]/gu, '\\]')
}

function altFromMetadata(metadata: AttachmentMetadata): string {
  const extension = metadata.extension.toLowerCase()
  const name = metadata.originalFileName
  return normalizedAlt(
    name.toLowerCase().endsWith(extension) ? name.slice(0, -extension.length) : name,
  )
}

function uploadFailureMessage(reason: unknown, fileName: string): string {
  if (reason instanceof ApiError) {
    if (reason.status === 413) return `${fileName} 超过图片大小限制，未插入正文。`
    if (reason.status === 415) return `${fileName} 不是受支持的 PNG、JPEG、GIF 或 WEBP 图片。`
    if (reason.status === 401) return '登录状态已失效，图片未上传。'
    if (reason.status === 403) return '当前身份无权向此文档上传图片。'
    if (reason.status === 404) return '当前知识内容不存在或已删除，图片未上传。'
    if (reason.status === 409) return '当前文档已不可编辑，请刷新状态后重试。'
    if (reason.status === 422) return '图片引用上下文无效，未插入正文。'
    if (reason.status === 503 || reason.status === 507) return '附件存储暂不可用，图片未上传。'
    return reason.message
  }
  if (reason instanceof NetworkRequestError) return '网络连接失败，图片未上传；正文保持不变。'
  return reason instanceof Error ? reason.message : `${fileName} 上传失败，未插入正文。`
}

function setTransientImageUrl(attachmentId: number, file: File): void {
  const next = new Map(transientImageUrls.value)
  const previous = next.get(attachmentId)
  if (previous) URL.revokeObjectURL(previous)
  next.set(attachmentId, URL.createObjectURL(file))
  transientImageUrls.value = next
}

function insertImageToken(
  anchor: { position: number },
  metadata: AttachmentMetadata,
  alt: string,
): void {
  if (!view) return
  const token = `![${safeMarkdownAlt(alt)}](attachment:${metadata.attachmentId})`
  view.dispatch({
    changes: { from: anchor.position, insert: token },
    selection: { anchor: anchor.position + token.length },
  })
  view.contentDOM.focus({ preventScroll: true })
  refreshHistoryState()
}

async function uploadImages(
  images: readonly PendingImage[],
  insertionPosition?: number,
): Promise<void> {
  if (!view || props.documentId === undefined || images.length === 0) return
  if (uploadingImages.value) {
    uploadError.value = '已有图片正在上传，请等待完成后再试。'
    if (fileInput.value) fileInput.value.value = ''
    return
  }

  const candidates = images.filter((item) => isApprovedImageCandidate(item.file))
  const rejected = images.length - candidates.length
  uploadError.value =
    rejected > 0 ? `${rejected} 个文件不是受支持的 PNG、JPEG、GIF 或 WEBP 图片，未上传。` : null
  if (candidates.length === 0) {
    if (fileInput.value) fileInput.value.value = ''
    return
  }

  const anchor = {
    position:
      insertionPosition === undefined
        ? view.state.selection.main.head
        : Math.min(Math.max(insertionPosition, 0), view.state.doc.length),
  }
  pendingInsertionAnchors.add(anchor)
  uploadingImages.value = true
  emit('uploading-change', true)
  uploadMessage.value = `正在上传 1 / ${candidates.length}…`
  const failures: string[] = []
  let succeeded = 0
  try {
    for (let index = 0; index < candidates.length; index += 1) {
      if (unmounted) break
      const item = candidates[index]!
      uploadMessage.value = `正在上传 ${index + 1} / ${candidates.length}：${item.file.name}`
      const controller = new AbortController()
      uploadAbortController = controller
      try {
        const metadata = await uploadKnowledgeDocumentImage(
          props.documentId,
          item.file,
          controller.signal,
        )
        if (metadata.kind !== 'Image' || metadata.previewMode !== 'Image') {
          throw new Error('服务器未将该文件识别为可用图片。')
        }
        if (unmounted || !view) break
        setTransientImageUrl(metadata.attachmentId, item.file)
        insertImageToken(anchor, metadata, item.altOverride ?? altFromMetadata(metadata))
        succeeded += 1
      } catch (reason: unknown) {
        if (reason instanceof DOMException && reason.name === 'AbortError') continue
        failures.push(uploadFailureMessage(reason, item.file.name))
      }
    }
  } finally {
    uploadAbortController = null
    pendingInsertionAnchors.delete(anchor)
    uploadingImages.value = false
    emit('uploading-change', false)
    if (fileInput.value) fileInput.value.value = ''
  }

  if (unmounted) return
  if (succeeded > 0) {
    uploadMessage.value = `已上传并插入 ${succeeded} 张图片；保存文档后才会写入修订。取消编辑不会删除已上传文件。`
  } else {
    uploadMessage.value = null
  }
  if (failures.length > 0) {
    uploadError.value = [uploadError.value, ...failures].filter(Boolean).join(' ')
  }
}

function handleFileInput(event: Event): void {
  const target = event.target
  if (!(target instanceof HTMLInputElement)) return
  void uploadImages(Array.from(target.files ?? []).map((file) => ({ file })))
}

function hasDraggedFiles(event: DragEvent): boolean {
  return Array.from(event.dataTransfer?.types ?? []).includes('Files')
}

function handleDragEnter(event: DragEvent): boolean {
  if (props.documentId === undefined) return false
  if (!hasDraggedFiles(event)) return false
  event.preventDefault()
  dragActive.value = true
  return true
}

function handleDragOver(event: DragEvent): boolean {
  if (props.documentId === undefined) return false
  if (!hasDraggedFiles(event)) return false
  event.preventDefault()
  if (event.dataTransfer) event.dataTransfer.dropEffect = 'copy'
  dragActive.value = true
  return true
}

function handleDragLeave(event: DragEvent): boolean {
  if (props.documentId === undefined) return false
  if (!hasDraggedFiles(event)) return false
  if (!(event.relatedTarget instanceof Node) || !view?.dom.contains(event.relatedTarget)) {
    dragActive.value = false
  }
  return true
}

function handleDrop(event: DragEvent): boolean {
  if (props.documentId === undefined) return false
  if (!hasDraggedFiles(event)) return false
  event.preventDefault()
  dragActive.value = false
  const files = Array.from(event.dataTransfer?.files ?? [])
  let dropPosition: number | undefined
  try {
    dropPosition = view?.posAtCoords({ x: event.clientX, y: event.clientY }) ?? undefined
  } catch {
    dropPosition = undefined
  }
  void uploadImages(
    files.map((file) => ({ file })),
    dropPosition,
  )
  return true
}

function clipboardImageFiles(event: ClipboardEvent): readonly File[] {
  const items = Array.from(event.clipboardData?.items ?? [])
  return items
    .filter((item) => item.kind === 'file')
    .map((item) => item.getAsFile())
    .filter((file): file is File => file !== null)
    .filter(
      (file) =>
        file.type.toLowerCase().startsWith('image/') ||
        approvedExtensions.has(fileExtension(file.name)),
    )
    .map((file, index) => {
      const extensionByMime: Readonly<Record<string, string>> = {
        'image/png': '.png',
        'image/jpeg': '.jpg',
        'image/gif': '.gif',
        'image/webp': '.webp',
      }
      const extension =
        extensionByMime[file.type.toLowerCase()] ?? (fileExtension(file.name) || '.bin')
      const stamp = new Date().toISOString().replace(/[:.]/gu, '-')
      return new File([file], `截图-${stamp}-${index + 1}${extension}`, {
        type: file.type,
        lastModified: file.lastModified,
      })
    })
}

function handlePaste(event: ClipboardEvent): boolean {
  if (props.documentId === undefined) return false
  const files = clipboardImageFiles(event)
  if (files.length === 0) return false
  event.preventDefault()
  void uploadImages(files.map((file) => ({ file, altOverride: '截图' })))
  return true
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
          pendingInsertionAnchors.forEach((anchor) => {
            anchor.position = update.changes.mapPos(anchor.position, 1)
          })
          if (!applyingExternalSource) model.value = update.state.doc.toString()
          refreshHistoryState()
        }),
        EditorView.domEventHandlers({
          dragenter: handleDragEnter,
          dragover: handleDragOver,
          dragleave: handleDragLeave,
          drop: handleDrop,
          paste: handlePaste,
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
  unmounted = true
  uploadAbortController?.abort()
  uploadAbortController = null
  transientImageUrls.value.forEach((url) => URL.revokeObjectURL(url))
  transientImageUrls.value = new Map()
  if (uploadingImages.value) emit('uploading-change', false)
  sourceReady.value = false
  view?.destroy()
  view = null
})
</script>

<template>
  <section
    :class="[
      'knowledge-document-editor',
      `knowledge-document-editor--${viewport}`,
      { 'is-fullscreen': fullscreen },
    ]"
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
            class="knowledge-document-editor__icon-button"
            aria-label="行内代码"
            size="small"
            :disabled="formattingDisabled"
            @click="applyTransform((source, selection) => toggleInlineWrap(source, selection, '`'))"
            ><font-awesome-icon :icon="faCode" fixed-width /></el-button
        ></el-tooltip>
      </div>
      <div class="knowledge-document-editor__tool-group">
        <el-tooltip content="引用" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="引用"
            size="small"
            :disabled="formattingDisabled"
            @click="applyTransform(toggleQuote)"
            ><font-awesome-icon :icon="faQuoteLeft" fixed-width /></el-button
        ></el-tooltip>
        <el-tooltip content="无序列表" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="无序列表"
            size="small"
            :disabled="formattingDisabled"
            @click="applyTransform(toggleBulletList)"
            ><font-awesome-icon :icon="faListUl" fixed-width /></el-button
        ></el-tooltip>
        <el-tooltip content="有序列表" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button knowledge-document-editor__numbered-button"
            aria-label="有序列表"
            size="small"
            :disabled="formattingDisabled"
            @click="applyTransform(toggleOrderedList)"
            ><font-awesome-icon :icon="faListOl" fixed-width /></el-button
        ></el-tooltip>
        <el-tooltip content="任务列表" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="任务列表"
            size="small"
            :disabled="formattingDisabled"
            @click="applyTransform(toggleTaskList)"
            ><font-awesome-icon :icon="faListCheck" fixed-width /></el-button
        ></el-tooltip>
      </div>
      <div class="knowledge-document-editor__tool-group">
        <el-tooltip content="插入代码块" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="插入代码块"
            size="small"
            :disabled="formattingDisabled"
            @click="openCodeDialog"
            ><font-awesome-icon :icon="faFileCode" fixed-width /></el-button
        ></el-tooltip>
        <el-tooltip content="插入链接" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="插入链接"
            size="small"
            :disabled="formattingDisabled"
            @click="openLinkDialog"
            ><font-awesome-icon :icon="faLink" fixed-width /></el-button
        ></el-tooltip>
        <el-tooltip content="插入表格（2×2 至 10×10）" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="插入表格"
            size="small"
            :disabled="formattingDisabled"
            @click="openTableDialog"
            ><font-awesome-icon :icon="faTable" fixed-width /></el-button
        ></el-tooltip>
        <div class="knowledge-document-editor__diagram-menu">
          <el-tooltip content="插入图表" placement="top" :trigger="tooltipTriggers"
            ><el-button
              class="knowledge-document-editor__icon-button"
              aria-label="插入图表"
              aria-haspopup="menu"
              :aria-expanded="diagramMenuOpen"
              size="small"
              :disabled="formattingDisabled"
              @click="toggleDiagramMenu"
              ><font-awesome-icon :icon="faDiagramProject" fixed-width /></el-button
          ></el-tooltip>
          <div
            v-if="diagramMenuOpen"
            class="knowledge-document-editor__diagram-menu-popover"
            role="menu"
            aria-label="图表类型"
          >
            <button
              v-for="option in diagramOptions"
              :key="option.value"
              type="button"
              role="menuitem"
              @click="insertDiagramTemplate(option.value)"
            >
              {{ option.label }}
            </button>
          </div>
        </div>
        <el-tooltip content="插入分隔线" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="插入分隔线"
            size="small"
            :disabled="formattingDisabled"
            @click="applyTransform(insertHorizontalRule)"
            ><el-icon><Minus /></el-icon></el-button
        ></el-tooltip>
        <el-tooltip :content="imageUploadTooltip" placement="top" :trigger="tooltipTriggers"
          ><span class="knowledge-document-editor__disabled-tooltip"
            ><el-button
              class="knowledge-document-editor__icon-button"
              aria-label="插入图片"
              size="small"
              :loading="uploadingImages"
              :disabled="imageUploadDisabled"
              @click="openImagePicker"
              ><font-awesome-icon
                v-if="!uploadingImages"
                :icon="faImage"
                fixed-width /></el-button></span
        ></el-tooltip>
        <input
          ref="fileInput"
          class="knowledge-document-editor__file-input"
          type="file"
          :accept="imageAccept"
          multiple
          tabindex="-1"
          aria-hidden="true"
          @change="handleFileInput"
        />
      </div>
      <div class="knowledge-document-editor__tool-group knowledge-document-editor__workspace-group">
        <el-tooltip content="撤销（Ctrl+Z）" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            aria-label="撤销"
            size="small"
            :disabled="formattingDisabled || !canUndo"
            @click="undoSource"
            ><font-awesome-icon :icon="faRotateLeft" fixed-width /></el-button
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
            ><font-awesome-icon :icon="faRotateRight" fixed-width /></el-button
        ></el-tooltip>
      </div>
      <div class="knowledge-document-editor__tool-group knowledge-document-editor__workspace-group">
        <el-tooltip content="源码编辑" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button knowledge-document-editor__view-button"
            aria-label="源码编辑"
            :aria-pressed="!previewing"
            size="small"
            :type="previewing ? 'default' : 'primary'"
            plain
            @click="emit('edit')"
            ><font-awesome-icon :icon="faCode" fixed-width /></el-button
        ></el-tooltip>
        <el-tooltip content="预览" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button knowledge-document-editor__view-button"
            aria-label="预览"
            :aria-pressed="previewing"
            size="small"
            :type="previewing ? 'primary' : 'default'"
            plain
            @click="emit('preview')"
            ><font-awesome-icon :icon="faEye" fixed-width /></el-button
        ></el-tooltip>
        <el-tooltip :content="fullscreenLabel" placement="top" :trigger="tooltipTriggers"
          ><el-button
            class="knowledge-document-editor__icon-button"
            :aria-label="fullscreenLabel"
            :aria-pressed="fullscreen"
            size="small"
            @click="emit('toggle-fullscreen')"
            ><font-awesome-icon :icon="fullscreen ? faCompress : faExpand" fixed-width /></el-button
        ></el-tooltip>
      </div>
    </div>

    <div
      v-if="uploadingImages || uploadMessage || uploadError"
      class="knowledge-document-editor__upload-feedback"
      aria-live="polite"
    >
      <p v-if="uploadingImages || uploadMessage" role="status">{{ uploadMessage }}</p>
      <p v-if="uploadError" class="is-error" role="alert">{{ uploadError }}</p>
    </div>

    <div
      v-show="!previewing"
      ref="editorRoot"
      :class="['knowledge-document-editor__source', { 'is-drag-active': dragActive }]"
      aria-label="Markdown 原始源码"
    >
      <div v-if="dragActive" class="knowledge-document-editor__drop-feedback" role="status">
        释放以依次上传并插入图片
      </div>
    </div>
    <div v-show="previewing" class="knowledge-document-editor__preview" aria-live="polite">
      <p class="knowledge-document-editor__preview-note">预览未保存内容</p>
      <KnowledgeDocumentMarkdown
        :markdown="model"
        :attachment-image-context="previewImageContext"
      />
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
