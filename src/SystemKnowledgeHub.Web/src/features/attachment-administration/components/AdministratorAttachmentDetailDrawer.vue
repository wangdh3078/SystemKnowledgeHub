<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { Delete } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useRouter } from 'vue-router'
import { ApiError } from '../../../api/errors/ApiError'
import { formatDateTime } from '../../../app/formatters/dateTime'
import { useOverlayStore } from '../../../app/stores/overlays'
import type { AttachmentMetadata } from '../../knowledge-documents/api/attachmentContracts'
import { knowledgeDocumentAttachmentDownloadUrl } from '../../knowledge-documents/api/knowledgeDocumentAttachmentsApi'
import {
  checkAdministratorAttachmentIntegrity,
  deleteAdministratorAttachment,
  getAdministratorAttachment,
} from '../api/administratorAttachmentsApi'
import type {
  AdministratorAttachmentDetail,
  AdministratorAttachmentIntegrity,
} from '../api/administratorAttachmentContracts'
import {
  administratorAttachmentReferenceLabels,
  administratorAttachmentStorageLabels,
  formatAdministratorAttachmentReferenceCounts,
} from '../attachmentAdministrationPresentation'

const props = defineProps<{ attachmentId: number }>()
const emit = defineEmits<{ deleted: [attachmentId: number]; changed: [] }>()
const overlayStore = useOverlayStore()
const router = useRouter()
const detail = ref<AdministratorAttachmentDetail | null>(null)
const integrity = ref<AdministratorAttachmentIntegrity | null>(null)
const loading = ref(false)
const integrityLoading = ref(false)
const deleting = ref(false)
const error = ref<string | null>(null)
let requestController: AbortController | null = null

const readableReference = computed(() => {
  const attachment = detail.value
  if (!attachment) return null
  if (!attachment.owner.isDeleted) {
    const current = attachment.references.find((reference) => reference.isCurrent)
    if (current) return { revisionNumber: undefined }
  }
  const historical = attachment.references[0]
  return historical ? { revisionNumber: historical.revisionNumber } : null
})
const downloadUrl = computed(() => {
  if (!detail.value || !readableReference.value) return null
  return knowledgeDocumentAttachmentDownloadUrl(
    detail.value.owner.documentId,
    detail.value.attachmentId,
    readableReference.value.revisionNumber,
  )
})
const canOpenPreview = computed(
  () =>
    detail.value?.canPreview === true &&
    detail.value.previewMode !== 'Image' &&
    readableReference.value !== null,
)
const canDelete = computed(
  () =>
    detail.value?.referenceCount === 0 && detail.value.storageState === 'Ready' && !deleting.value,
)
const canRetry = computed(
  () =>
    detail.value?.referenceCount === 0 &&
    detail.value.storageState === 'DeletePending' &&
    !deleting.value,
)

function formatFileSize(sizeBytes: number): string {
  if (sizeBytes < 1024) return `${sizeBytes} B`
  if (sizeBytes < 1024 * 1024) return `${formatNumber(sizeBytes / 1024)} KB`
  return `${formatNumber(sizeBytes / (1024 * 1024))} MB`
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat('zh-CN', { maximumFractionDigits: 1 }).format(value)
}

async function load(): Promise<void> {
  requestController?.abort()
  requestController = new AbortController()
  loading.value = true
  error.value = null
  integrity.value = null
  try {
    detail.value = await getAdministratorAttachment(props.attachmentId, requestController.signal)
  } catch (reason: unknown) {
    if (reason instanceof DOMException && reason.name === 'AbortError') return
    error.value = reason instanceof Error ? reason.message : '附件详情加载失败。'
  } finally {
    loading.value = false
  }
}

async function checkIntegrity(): Promise<void> {
  if (!detail.value || integrityLoading.value) return
  integrityLoading.value = true
  try {
    integrity.value = await checkAdministratorAttachmentIntegrity(detail.value.attachmentId)
    ElMessage.success('附件完整性检查已完成。')
  } catch (reason: unknown) {
    ElMessage.error(reason instanceof Error ? reason.message : '附件完整性检查失败。')
  } finally {
    integrityLoading.value = false
  }
}

function openPreview(): void {
  const attachment = detail.value
  const context = readableReference.value
  if (!attachment || !context || !canOpenPreview.value) return
  const metadata: AttachmentMetadata = {
    attachmentId: attachment.attachmentId,
    kind: attachment.kind,
    originalFileName: attachment.originalFileName,
    extension: attachment.extension,
    contentType: attachment.contentType,
    sizeBytes: attachment.sizeBytes,
    sha256: attachment.sha256,
    previewMode: attachment.previewMode,
    canPreview: attachment.canPreview,
    canDownload: true,
  }
  overlayStore.openDialog({
    kind: 'attachment-preview',
    id: attachment.attachmentId,
    mode: 'read',
    payload: {
      documentId: attachment.owner.documentId,
      revisionNumber: context.revisionNumber,
      attachment: metadata,
    },
  })
}

async function openOwnerDocument(): Promise<void> {
  const attachment = detail.value
  if (!attachment || attachment.owner.isDeleted) return
  const target = {
    name: 'knowledge-document-detail' as const,
    params: { id: String(attachment.owner.documentId) },
  }

  await overlayStore.closeDrawerAfterClosed()
  await router.push(target)
}

async function permanentlyDelete(): Promise<void> {
  const attachment = detail.value
  if (!attachment || (!canDelete.value && !canRetry.value)) return
  const retrying = attachment.storageState === 'DeletePending'
  try {
    await ElMessageBox.confirm(
      `该操作会删除附件 metadata 和物理文件，且不可恢复。\n仅零引用附件允许删除。\n\n文件：${attachment.originalFileName}\n大小：${formatFileSize(attachment.sizeBytes)}`,
      retrying ? '重试永久删除附件？' : '永久删除附件？',
      {
        confirmButtonText: retrying ? '重试永久删除' : '永久删除',
        cancelButtonText: '取消',
        type: 'warning',
        distinguishCancelAndClose: true,
      },
    )
  } catch {
    return
  }

  deleting.value = true
  try {
    await deleteAdministratorAttachment(attachment.attachmentId, attachment.concurrencyToken)
    ElMessage.success('孤立附件已永久删除。')
    emit('deleted', attachment.attachmentId)
    overlayStore.closeDrawer()
  } catch (reason: unknown) {
    if (reason instanceof ApiError && reason.status === 409) {
      ElMessage.error('附件已被其他操作修改；未删除任何文件，请重新检查。')
    } else if (reason instanceof ApiError && reason.status === 422) {
      ElMessage.error('附件已新增修订引用，永久删除已拒绝。')
    } else if (reason instanceof ApiError && (reason.status === 503 || reason.status === 507)) {
      ElMessage.error('物理文件删除失败，附件已保留为等待删除重试状态，可稍后单项重试。')
    } else {
      ElMessage.error(reason instanceof Error ? reason.message : '附件永久删除失败。')
    }
    await load()
    emit('changed')
  } finally {
    deleting.value = false
  }
}

watch(
  () => props.attachmentId,
  () => void load(),
  { immediate: true },
)
onBeforeUnmount(() => requestController?.abort())
</script>

<template>
  <article class="attachment-admin-detail" aria-labelledby="attachment-admin-detail-title">
    <header class="attachment-admin-detail__header">
      <div>
        <span>ADMIN · ATTACHMENT GOVERNANCE</span>
        <h2 id="attachment-admin-detail-title" :title="detail?.originalFileName">附件详情</h2>
        <p>查看不可变 metadata、全修订引用和受控存储健康；不显示任何物理路径。</p>
      </div>
      <button type="button" aria-label="关闭附件详情" @click="overlayStore.closeDrawer">×</button>
    </header>

    <div v-if="loading" class="attachment-admin-detail__state" role="status">正在读取附件详情…</div>
    <div
      v-else-if="error"
      class="attachment-admin-detail__state attachment-admin-detail__state--error"
      role="alert"
    >
      <strong>附件详情加载失败</strong>
      <p>{{ error }}</p>
      <el-button @click="load">重试</el-button>
    </div>
    <template v-else-if="detail">
      <section class="attachment-admin-detail__identity">
        <div>
          <small>ATTACHMENT #{{ detail.attachmentId }}</small>
          <h3 :title="detail.originalFileName">{{ detail.originalFileName }}</h3>
          <p>{{ detail.contentType }} · {{ formatFileSize(detail.sizeBytes) }}</p>
        </div>
        <div class="attachment-admin-detail__badges">
          <el-tag
            :type="detail.referenceStatus === 'Orphan' ? 'warning' : 'success'"
            effect="plain"
          >
            {{ administratorAttachmentReferenceLabels[detail.referenceStatus] }}
          </el-tag>
          <el-tag :type="detail.storageHealth === 'Ready' ? 'success' : 'danger'" effect="plain">
            {{ administratorAttachmentStorageLabels[detail.storageHealth] }}
          </el-tag>
        </div>
      </section>

      <section
        class="attachment-admin-detail__section"
        aria-labelledby="attachment-admin-metadata-title"
      >
        <h3 id="attachment-admin-metadata-title">附件 metadata</h3>
        <dl class="attachment-admin-detail__grid">
          <div>
            <dt>Attachment ID</dt>
            <dd>{{ detail.attachmentId }}</dd>
          </div>
          <div>
            <dt>Kind</dt>
            <dd>{{ detail.kind }}</dd>
          </div>
          <div>
            <dt>Extension</dt>
            <dd>{{ detail.extension }}</dd>
          </div>
          <div>
            <dt>Preview Mode</dt>
            <dd>{{ detail.previewMode }}</dd>
          </div>
          <div>
            <dt>存储状态</dt>
            <dd>{{ administratorAttachmentStorageLabels[detail.storageState] }}</dd>
          </div>
          <div>
            <dt>Size</dt>
            <dd>{{ formatFileSize(detail.sizeBytes) }}</dd>
          </div>
          <div>
            <dt>上传人</dt>
            <dd>{{ detail.createdByDisplayName }}</dd>
          </div>
          <div>
            <dt>上传时间</dt>
            <dd>{{ formatDateTime(detail.createdAt) }}</dd>
          </div>
          <div class="attachment-admin-detail__hash">
            <dt>SHA-256</dt>
            <dd>{{ detail.sha256 }}</dd>
          </div>
        </dl>
      </section>

      <section
        class="attachment-admin-detail__section"
        aria-labelledby="attachment-admin-owner-title"
      >
        <h3 id="attachment-admin-owner-title">所属 KnowledgeDocument</h3>
        <div class="attachment-admin-detail__owner">
          <div>
            <small>DOCUMENT #{{ detail.owner.documentId }}</small>
            <strong>{{ detail.owner.title }}</strong>
            <span>{{ detail.owner.lifecycleStatus }}</span>
          </div>
          <el-tag v-if="detail.owner.isDeleted" type="danger" effect="plain">已删除</el-tag>
          <el-button
            v-else
            class="attachment-admin-detail__owner-link"
            link
            type="primary"
            @click="openOwnerDocument"
            >查看当前文档</el-button
          >
        </div>
      </section>

      <section
        class="attachment-admin-detail__section"
        aria-labelledby="attachment-admin-reference-title"
      >
        <div class="attachment-admin-detail__section-heading">
          <h3 id="attachment-admin-reference-title">Revision 引用</h3>
          <span>{{ formatAdministratorAttachmentReferenceCounts(detail) }}</span>
        </div>
        <p v-if="detail.referenceCount === 0" class="attachment-admin-detail__empty">
          全部修订引用数为 0；这是可永久删除的孤立附件。
        </p>
        <ol v-else class="attachment-admin-detail__references">
          <li v-for="reference in detail.references" :key="reference.revisionNumber">
            <span>Revision {{ reference.revisionNumber }}</span>
            <el-tag v-if="reference.isCurrent" size="small" effect="plain">当前指针</el-tag>
            <time>{{ formatDateTime(reference.createdAt) }}</time>
          </li>
        </ol>
        <p v-if="detail.referencesTruncated" class="attachment-admin-detail__notice">
          引用摘要已截断；Reference Count 仍为全部修订的精确计数。
        </p>
      </section>

      <section
        class="attachment-admin-detail__section"
        aria-labelledby="attachment-admin-integrity-title"
      >
        <div class="attachment-admin-detail__section-heading">
          <div>
            <h3 id="attachment-admin-integrity-title">存储完整性</h3>
            <p>按需读取并计算 SHA-256；不会修改 metadata 或文件。</p>
          </div>
          <el-button :loading="integrityLoading" @click="checkIntegrity">执行完整性检查</el-button>
        </div>
        <dl v-if="integrity" class="attachment-admin-detail__integrity" aria-live="polite">
          <div>
            <dt>检查结果</dt>
            <dd>{{ administratorAttachmentStorageLabels[integrity.status] }}</dd>
          </div>
          <div>
            <dt>实际大小</dt>
            <dd>
              {{
                integrity.actualSizeBytes === null
                  ? '不可读取'
                  : formatFileSize(integrity.actualSizeBytes)
              }}
            </dd>
          </div>
          <div>
            <dt>检查时间</dt>
            <dd>{{ formatDateTime(integrity.checkedAt) }}</dd>
          </div>
          <div v-if="integrity.actualSha256" class="attachment-admin-detail__hash">
            <dt>实际 SHA-256</dt>
            <dd>{{ integrity.actualSha256 }}</dd>
          </div>
        </dl>
      </section>

      <footer class="attachment-admin-detail__actions">
        <div>
          <el-button v-if="canOpenPreview" @click="openPreview">只读预览</el-button>
          <a
            v-if="downloadUrl"
            :href="downloadUrl"
            :download="detail.originalFileName"
            :aria-label="`下载附件 ${detail.originalFileName}`"
            >下载</a
          >
          <span v-else>零引用附件没有可用的 current / historical 下载上下文。</span>
        </div>
        <el-button
          v-if="canDelete || canRetry"
          type="danger"
          plain
          :icon="Delete"
          :loading="deleting"
          :aria-label="`${canRetry ? '重试永久删除' : '永久删除'}附件 ${detail.originalFileName}`"
          @click="permanentlyDelete"
          >{{ canRetry ? '重试永久删除' : '永久删除' }}</el-button
        >
      </footer>
    </template>
  </article>
</template>
