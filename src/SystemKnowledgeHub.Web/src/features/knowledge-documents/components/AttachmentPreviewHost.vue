<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { ApiError, UnexpectedResponseError } from '../../../api/errors/ApiError'
import { useOverlayStore } from '../../../app/stores/overlays'
import type {
  AttachmentJsonPreview,
  AttachmentPreviewContext,
  AttachmentPreviewMode,
} from '../api/attachmentContracts'
import {
  getKnowledgeDocumentAttachmentPreview,
  getKnowledgeDocumentPdfPreview,
  knowledgeDocumentAttachmentDownloadUrl,
} from '../api/knowledgeDocumentAttachmentsApi'
import KnowledgeDocumentMarkdown from '../markdown/KnowledgeDocumentMarkdown.vue'

const overlayStore = useOverlayStore()
const preview = ref<AttachmentJsonPreview | null>(null)
const pdfUrl = ref<string | null>(null)
const loading = ref(false)
const pdfFrameLoading = ref(false)
const error = ref<string | null>(null)
let activeRequest: AbortController | null = null
let requestSequence = 0

const modeLabels: Readonly<Record<AttachmentPreviewMode, string>> = {
  Image: '图片',
  Pdf: 'PDF',
  Text: '文本',
  Markdown: 'Markdown',
  Csv: 'CSV',
  Spreadsheet: '电子表格',
  None: '不可预览',
}

const context = computed<AttachmentPreviewContext | null>(() => {
  const descriptor = overlayStore.currentDialog
  if (descriptor?.kind !== 'attachment-preview' || descriptor.payload == null) return null
  return descriptor.payload as AttachmentPreviewContext
})
const downloadUrl = computed(() =>
  context.value
    ? knowledgeDocumentAttachmentDownloadUrl(
        context.value.documentId,
        context.value.attachment.attachmentId,
        context.value.revisionNumber,
      )
    : '#',
)
const contextLabel = computed(() =>
  context.value?.revisionNumber === undefined
    ? '当前修订'
    : `历史修订 ${context.value.revisionNumber}`,
)
const csvColumnCount = computed(() =>
  preview.value?.mode === 'Csv' ? Math.max(0, ...preview.value.rows.map((row) => row.length)) : 0,
)
const spreadsheetColumnCount = computed(() =>
  preview.value?.mode === 'Spreadsheet'
    ? Math.max(0, ...preview.value.rows.map((row) => row.cells.length))
    : 0,
)

function formatFileSize(sizeBytes: number): string {
  if (sizeBytes < 1024) return `${sizeBytes} B`
  if (sizeBytes < 1024 * 1024) return `${formatNumber(sizeBytes / 1024)} KB`
  return `${formatNumber(sizeBytes / (1024 * 1024))} MB`
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat('zh-CN', { maximumFractionDigits: 1 }).format(value)
}

function columnLabel(index: number): string {
  let value = index + 1
  let label = ''
  while (value > 0) {
    value -= 1
    label = String.fromCharCode(65 + (value % 26)) + label
    value = Math.floor(value / 26)
  }
  return label
}

function truncationReasonLabel(reason: string): string {
  const labels: Readonly<Record<string, string>> = {
    Rows: '行数',
    Columns: '列数',
    Characters: '字符数',
    Sheets: '工作表数量',
  }
  return labels[reason] ?? reason
}

function previewErrorMessage(reason: unknown): string {
  if (!(reason instanceof ApiError)) {
    return reason instanceof Error ? reason.message : '预览加载失败，请稍后重试。'
  }
  if (reason.status === 401) return '登录状态已失效，请重新登录后重试。'
  if (reason.status === 403) return '当前身份没有读取该附件的权限。'
  if (reason.status === 404) return '当前文档或修订中不存在该附件。'
  if (reason.status === 422 && reason.response.code === 'preview_not_supported') {
    return '该附件不支持在线预览，请下载原文件。'
  }
  if (reason.status === 422 && reason.response.code === 'preview_limit_exceeded') {
    return '该附件超过安全预览限制，请下载原文件查看。'
  }
  if (reason.status === 503) return '附件内容暂不可用，请稍后重试或联系管理员。'
  return reason.message || '预览加载失败，请稍后重试。'
}

function revokePdfUrl(): void {
  if (pdfUrl.value) URL.revokeObjectURL(pdfUrl.value)
  pdfUrl.value = null
}

function cancelActiveRequest(): void {
  activeRequest?.abort()
  activeRequest = null
  requestSequence += 1
}

function resetPreview(): void {
  cancelActiveRequest()
  revokePdfUrl()
  preview.value = null
  loading.value = false
  pdfFrameLoading.value = false
  error.value = null
}

async function loadPreview(sheet?: string): Promise<void> {
  const requestedContext = context.value
  if (!requestedContext) return
  cancelActiveRequest()
  const controller = new AbortController()
  activeRequest = controller
  const sequence = requestSequence
  loading.value = true
  error.value = null
  pdfFrameLoading.value = false

  try {
    if (requestedContext.attachment.previewMode === 'Pdf') {
      const blob = await getKnowledgeDocumentPdfPreview(
        requestedContext.documentId,
        requestedContext.attachment.attachmentId,
        requestedContext.revisionNumber,
        controller.signal,
      )
      if (sequence !== requestSequence || controller.signal.aborted) return
      revokePdfUrl()
      pdfUrl.value = URL.createObjectURL(blob)
      preview.value = null
      pdfFrameLoading.value = false
    } else {
      const result = await getKnowledgeDocumentAttachmentPreview(
        requestedContext.documentId,
        requestedContext.attachment.attachmentId,
        requestedContext.revisionNumber,
        sheet,
        controller.signal,
      )
      if (sequence !== requestSequence || controller.signal.aborted) return
      if (
        result.attachment.attachmentId !== requestedContext.attachment.attachmentId ||
        result.mode !== requestedContext.attachment.previewMode
      ) {
        throw new UnexpectedResponseError('服务器返回的附件预览上下文不符合预期。')
      }
      revokePdfUrl()
      preview.value = result
    }
  } catch (reason: unknown) {
    if (controller.signal.aborted || sequence !== requestSequence) return
    revokePdfUrl()
    preview.value = null
    error.value = previewErrorMessage(reason)
  } finally {
    if (sequence === requestSequence) {
      loading.value = false
      if (activeRequest === controller) activeRequest = null
    }
  }
}

function handleSheetChange(event: Event): void {
  const select = event.target
  if (!(select instanceof HTMLSelectElement)) return
  void loadPreview(select.value)
}

function handlePdfFrameError(): void {
  pdfFrameLoading.value = false
  error.value = '浏览器无法显示该 PDF，请下载原文件。'
}

function close(): void {
  overlayStore.closeDialog()
}

watch(
  context,
  (value) => {
    resetPreview()
    if (value) void loadPreview()
  },
  { immediate: true },
)
onBeforeUnmount(resetPreview)
</script>

<template>
  <article v-if="context" class="attachment-preview" aria-labelledby="attachment-preview-title">
    <header class="attachment-preview__header">
      <div class="attachment-preview__identity">
        <small>{{ contextLabel }} · {{ modeLabels[context.attachment.previewMode] }}预览</small>
        <h2 id="attachment-preview-title" :title="context.attachment.originalFileName">
          {{ context.attachment.originalFileName }}
        </h2>
        <p>
          {{ context.attachment.extension.replace(/^\./u, '').toUpperCase() }} ·
          {{ formatFileSize(context.attachment.sizeBytes) }} ·
          {{ modeLabels[context.attachment.previewMode] }}
        </p>
      </div>
      <div class="attachment-preview__header-actions">
        <a
          v-if="context.attachment.canDownload"
          class="attachment-preview__download"
          :href="downloadUrl"
          :download="context.attachment.originalFileName"
          :aria-label="`下载原文件 ${context.attachment.originalFileName}`"
          >下载原文件</a
        >
        <button type="button" aria-label="关闭附件预览" @click="close">×</button>
      </div>
    </header>

    <div class="attachment-preview__body">
      <div v-if="loading" class="attachment-preview__loading" role="status" aria-live="polite">
        正在加载附件预览…
      </div>

      <div v-else-if="error" class="attachment-preview__error" role="alert">
        <strong>附件预览不可用</strong>
        <p>{{ error }}</p>
        <div>
          <el-button type="primary" plain @click="loadPreview()">重新加载</el-button>
          <a
            v-if="context.attachment.canDownload"
            :href="downloadUrl"
            :download="context.attachment.originalFileName"
            >下载原文件</a
          >
        </div>
      </div>

      <section v-else-if="pdfUrl" class="attachment-preview__pdf" aria-label="PDF 只读预览">
        <p v-if="pdfFrameLoading" role="status" aria-live="polite">正在初始化 PDF 阅读器…</p>
        <iframe
          :src="pdfUrl"
          :title="`PDF 预览 ${context.attachment.originalFileName}`"
          @load="pdfFrameLoading = false"
          @error="handlePdfFrameError"
        ></iframe>
      </section>

      <section
        v-else-if="preview?.mode === 'Text'"
        class="attachment-preview__text"
        aria-label="纯文本只读预览"
      >
        <p v-if="preview.truncated" class="attachment-preview__notice" role="status">
          内容已截断：返回 {{ preview.returnedBytes }} / 最多
          {{ preview.maximumBytes }} 字节；下载可查看完整文件。
        </p>
        <pre tabindex="0">{{ preview.text }}</pre>
      </section>

      <section
        v-else-if="preview?.mode === 'Markdown'"
        class="attachment-preview__markdown"
        aria-label="Markdown 只读预览"
      >
        <p v-if="preview.truncated" class="attachment-preview__notice" role="status">
          Markdown 已按 {{ preview.maximumBytes }} 字节上限截断；下载可查看完整文件。
        </p>
        <KnowledgeDocumentMarkdown :markdown="preview.text" />
      </section>

      <section
        v-else-if="preview?.mode === 'Csv'"
        class="attachment-preview__structured"
        aria-label="CSV 只读预览"
      >
        <p v-if="preview.truncated" class="attachment-preview__notice" role="status">
          CSV 预览已截断（{{
            preview.truncationReasons.map(truncationReasonLabel).join('、')
          }}）；上限 {{ preview.maximumRows }} 行、{{ preview.maximumColumns }} 列。
        </p>
        <div class="attachment-preview__table-scroll" tabindex="0">
          <table>
            <caption>
              CSV 附件内容
            </caption>
            <thead>
              <tr>
                <th v-for="column in csvColumnCount" :key="column" scope="col">列 {{ column }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(row, rowIndex) in preview.rows" :key="rowIndex">
                <td v-for="column in csvColumnCount" :key="column">{{ row[column - 1] ?? '' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section
        v-else-if="preview?.mode === 'Spreadsheet'"
        class="attachment-preview__structured"
        aria-label="XLSX 只读预览"
      >
        <div class="attachment-preview__sheet-selector">
          <label for="attachment-preview-sheet">工作表</label>
          <select
            id="attachment-preview-sheet"
            :value="preview.selectedSheet"
            :disabled="loading"
            @change="handleSheetChange"
          >
            <option v-for="sheet in preview.sheets" :key="sheet.name" :value="sheet.name">
              {{ sheet.name }}{{ sheet.visibility === 'Visible' ? '' : `（${sheet.visibility}）` }}
            </option>
          </select>
        </div>
        <p v-if="preview.truncated" class="attachment-preview__notice" role="status">
          工作表预览已截断（{{
            preview.truncationReasons.map(truncationReasonLabel).join('、')
          }}）；上限 {{ preview.maximumRows }} 行、{{ preview.maximumColumns }} 列。
        </p>
        <div class="attachment-preview__table-scroll" tabindex="0">
          <table>
            <caption>
              {{
                preview.selectedSheet
              }}
              工作表
            </caption>
            <thead>
              <tr>
                <th scope="col">行</th>
                <th v-for="column in spreadsheetColumnCount" :key="column" scope="col">
                  {{ columnLabel(column - 1) }}
                </th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in preview.rows" :key="row.rowNumber">
                <th scope="row">{{ row.rowNumber }}</th>
                <td v-for="column in spreadsheetColumnCount" :key="column">
                  {{ row.cells[column - 1] ?? '' }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </div>
  </article>
</template>
