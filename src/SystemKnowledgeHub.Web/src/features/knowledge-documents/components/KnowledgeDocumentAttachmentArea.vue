<script setup lang="ts">
import { computed, ref } from 'vue'
import { ElMessageBox } from 'element-plus'
import { ApiError } from '../../../api/errors/ApiError'
import type { AttachmentMetadata, AttachmentPreviewMode } from '../api/attachmentContracts'
import {
  knowledgeDocumentAttachmentDownloadUrl,
  uploadKnowledgeDocumentAttachment,
} from '../api/knowledgeDocumentAttachmentsApi'

const props = withDefaults(
  defineProps<{
    documentId: number
    attachments: readonly AttachmentMetadata[]
    editable?: boolean
    revisionNumber?: number
  }>(),
  { editable: false, revisionNumber: undefined },
)
const emit = defineEmits<{
  'update:attachments': [attachments: readonly AttachmentMetadata[]]
  'uploading-change': [uploading: boolean]
  preview: [attachment: AttachmentMetadata]
}>()

const acceptedExtensions = new Set([
  '.pdf',
  '.docx',
  '.xlsx',
  '.pptx',
  '.txt',
  '.log',
  '.sql',
  '.md',
  '.csv',
  '.json',
  '.xml',
  '.zip',
])
const canonicalContentTypes: Readonly<Record<string, string>> = {
  '.pdf': 'application/pdf',
  '.docx': 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  '.xlsx': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  '.pptx': 'application/vnd.openxmlformats-officedocument.presentationml.presentation',
  '.txt': 'text/plain',
  '.log': 'text/plain',
  '.sql': 'text/plain',
  '.md': 'text/markdown',
  '.csv': 'text/csv',
  '.json': 'application/json',
  '.xml': 'application/xml',
  '.zip': 'application/zip',
}
const accept = [...acceptedExtensions].join(',')
const previewLabels: Readonly<Record<AttachmentPreviewMode, string>> = {
  Image: '图片',
  Pdf: 'PDF',
  Text: '文本',
  Markdown: 'Markdown',
  Csv: 'CSV',
  Spreadsheet: '电子表格',
  None: '不可用',
}

const fileInput = ref<HTMLInputElement | null>(null)
const uploading = ref(false)
const activeFileName = ref<string | null>(null)
const uploadProgress = ref({ current: 0, total: 0 })
const uploadSummary = ref<string | null>(null)
const uploadErrors = ref<readonly string[]>([])
const fileAttachments = computed(() =>
  props.attachments.filter((attachment) => attachment.kind === 'File'),
)

function finalExtension(fileName: string): string {
  const dot = fileName.lastIndexOf('.')
  return dot > -1 ? fileName.slice(dot).toLowerCase() : ''
}

function uploadErrorMessage(fileName: string, reason: unknown): string {
  const prefix = `${fileName}：`
  if (!(reason instanceof ApiError)) {
    return `${prefix}${reason instanceof Error ? reason.message : '上传失败，请检查网络后重试。'}`
  }
  const serverMessage = reason.response.fieldErrors?.file?.[0]
  if (reason.status === 413) return `${prefix}文件超过服务器允许的大小限制。`
  if (reason.status === 415) return `${prefix}${serverMessage ?? '文件类型或内容不受支持。'}`
  if (reason.status === 401) return `${prefix}登录状态已失效，请重新登录后重试。`
  if (reason.status === 403) return `${prefix}当前身份没有上传附件的权限。`
  if (reason.status === 404) return `${prefix}当前知识内容已不存在。`
  if (reason.status === 409) return `${prefix}当前文档状态不可上传，或附件数量已达上限。`
  if (reason.status === 422) return `${prefix}附件引用上下文无效。`
  if (reason.status === 503 || reason.status === 507) {
    return `${prefix}附件存储暂不可用，请稍后重试。`
  }
  return `${prefix}${reason.message || '上传失败，请稍后重试。'}`
}

function normalizeOrdinaryFile(file: File, extension: string): File {
  const contentType = canonicalContentTypes[extension]
  return file.type.toLowerCase() === contentType
    ? file
    : new File([file], file.name, { type: contentType, lastModified: file.lastModified })
}

async function uploadFiles(files: readonly File[]): Promise<void> {
  if (!props.editable || uploading.value || files.length === 0) return
  uploading.value = true
  emit('uploading-change', true)
  uploadErrors.value = []
  uploadSummary.value = null
  uploadProgress.value = { current: 0, total: files.length }
  const desired = [...fileAttachments.value]
  const errors: string[] = []
  let succeeded = 0
  try {
    for (const [index, file] of files.entries()) {
      uploadProgress.value = { current: index + 1, total: files.length }
      activeFileName.value = file.name
      const extension = finalExtension(file.name)
      if (!acceptedExtensions.has(extension)) {
        errors.push(`${file.name || '未命名文件'}：文件扩展名不在普通附件允许列表中。`)
        continue
      }
      try {
        const metadata = await uploadKnowledgeDocumentAttachment(
          props.documentId,
          normalizeOrdinaryFile(file, extension),
        )
        if (metadata.kind !== 'File') {
          errors.push(`${file.name}：该文件属于图片，请使用正文中的“插入图片”。`)
          continue
        }
        if (!desired.some((attachment) => attachment.attachmentId === metadata.attachmentId)) {
          desired.push(metadata)
          emit('update:attachments', [...desired])
          succeeded += 1
        }
      } catch (reason: unknown) {
        errors.push(uploadErrorMessage(file.name || '未命名文件', reason))
      }
    }
    uploadErrors.value = errors
    if (succeeded > 0) {
      uploadSummary.value = `已添加 ${succeeded} 个附件到待保存集合；保存文档后生效。`
    } else if (errors.length === 0) {
      uploadSummary.value = '没有新增附件。'
    }
  } finally {
    activeFileName.value = null
    uploading.value = false
    emit('uploading-change', false)
  }
}

function chooseFiles(): void {
  if (!props.editable || uploading.value) return
  fileInput.value?.click()
}

function handleFileChange(event: Event): void {
  const input = event.target as HTMLInputElement
  const files = Array.from(input.files ?? [])
  input.value = ''
  void uploadFiles(files)
}

async function removeAttachment(attachment: AttachmentMetadata): Promise<void> {
  if (!props.editable || uploading.value) return
  try {
    await ElMessageBox.confirm(
      `确认移除“${attachment.originalFileName}”的当前引用？保存后只会从新修订移除，历史修订和文件本身仍会保留。`,
      '移除附件引用',
      {
        confirmButtonText: '移除',
        cancelButtonText: '取消',
        type: 'warning',
      },
    )
  } catch {
    return
  }
  emit(
    'update:attachments',
    fileAttachments.value.filter((item) => item.attachmentId !== attachment.attachmentId),
  )
  uploadSummary.value = `已从待保存集合移除“${attachment.originalFileName}”；保存文档后生效。`
}

function fileTypeLabel(attachment: AttachmentMetadata): string {
  return attachment.extension.replace(/^\./u, '').toUpperCase() || attachment.contentType
}

function formatFileSize(sizeBytes: number): string {
  if (sizeBytes < 1024) return `${sizeBytes} B`
  if (sizeBytes < 1024 * 1024) return `${formatNumber(sizeBytes / 1024)} KB`
  return `${formatNumber(sizeBytes / (1024 * 1024))} MB`
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat('zh-CN', { maximumFractionDigits: 1 }).format(value)
}

function previewCapability(attachment: AttachmentMetadata): string {
  return attachment.canPreview ? `支持${previewLabels[attachment.previewMode]}预览` : '仅支持下载'
}

function openPreview(attachment: AttachmentMetadata): void {
  if (!attachment.canPreview || !attachment.canDownload || attachment.previewMode === 'None') return
  emit('preview', attachment)
}

function downloadUrl(attachment: AttachmentMetadata): string {
  return knowledgeDocumentAttachmentDownloadUrl(
    props.documentId,
    attachment.attachmentId,
    props.revisionNumber,
  )
}
</script>

<template>
  <section
    class="knowledge-document-attachments"
    aria-labelledby="knowledge-document-attachments-heading"
  >
    <header class="knowledge-document-attachments__heading">
      <div>
        <h2 id="knowledge-document-attachments-heading">附件</h2>
        <p v-if="editable">上传成功后先进入待保存集合；保存文档时统一创建新修订。</p>
      </div>
      <el-button
        v-if="editable"
        type="primary"
        plain
        :disabled="uploading"
        :loading="uploading"
        aria-label="添加普通附件"
        @click="chooseFiles"
        >{{ uploading ? '上传中…' : '添加附件' }}</el-button
      >
      <input
        v-if="editable"
        ref="fileInput"
        class="knowledge-document-attachments__file-input"
        type="file"
        multiple
        :accept="accept"
        aria-label="选择普通附件文件"
        @change="handleFileChange"
      />
    </header>

    <p
      v-if="uploading"
      class="knowledge-document-attachments__status"
      role="status"
      aria-live="polite"
    >
      正在上传 {{ uploadProgress.current }}/{{ uploadProgress.total }}：{{ activeFileName }}
    </p>
    <p
      v-else-if="uploadSummary"
      class="knowledge-document-attachments__status"
      role="status"
      aria-live="polite"
    >
      {{ uploadSummary }}
    </p>
    <ul v-if="uploadErrors.length" class="knowledge-document-attachments__errors" role="alert">
      <li v-for="message in uploadErrors" :key="message">{{ message }}</li>
    </ul>

    <p v-if="fileAttachments.length === 0" class="knowledge-document-attachments__empty">
      暂无附件
    </p>
    <div v-else class="knowledge-document-attachments__list">
      <article
        v-for="attachment in fileAttachments"
        :key="attachment.attachmentId"
        class="knowledge-document-attachments__item"
        :data-attachment-id="attachment.attachmentId"
      >
        <div class="knowledge-document-attachments__identity">
          <strong :title="attachment.originalFileName">{{ attachment.originalFileName }}</strong>
          <span>{{ fileTypeLabel(attachment) }} · {{ formatFileSize(attachment.sizeBytes) }}</span>
          <small>{{ previewCapability(attachment) }}</small>
        </div>
        <div class="knowledge-document-attachments__actions">
          <el-button
            v-if="attachment.canPreview && attachment.previewMode !== 'None'"
            text
            type="primary"
            :disabled="!attachment.canDownload"
            :aria-label="`预览附件 ${attachment.originalFileName}`"
            @click="openPreview(attachment)"
            >{{ attachment.canDownload ? '预览' : '保存后可预览' }}</el-button
          >
          <a
            v-if="attachment.canDownload"
            class="knowledge-document-attachments__download"
            :href="downloadUrl(attachment)"
            :download="attachment.originalFileName"
            :aria-label="`下载附件 ${attachment.originalFileName}`"
            >下载</a
          >
          <span v-else class="knowledge-document-attachments__unavailable">
            {{ editable ? '保存后可下载' : '当前不可下载' }}
          </span>
          <el-button
            v-if="editable"
            text
            type="danger"
            :disabled="uploading"
            :aria-label="`移除附件 ${attachment.originalFileName}`"
            @click="removeAttachment(attachment)"
            >移除</el-button
          >
        </div>
      </article>
    </div>
  </section>
</template>
