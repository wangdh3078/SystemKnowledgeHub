<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ApiError } from '../../../api/errors/ApiError'
import { isSafeApiId } from '../../../api/contracts/id'
import { useOverlayStore } from '../../../app/stores/overlays'
import {
  getKnowledgeDocument,
  restoreKnowledgeDocumentRevision,
} from '../api/knowledgeDocumentsApi'
import {
  lifecycleLabels,
  type KnowledgeDocumentDetail,
  type KnowledgeDocumentRevisionDetail,
  type RevisionOrigin,
} from '../api/knowledgeDocumentContracts'
import KnowledgeDocumentMarkdown from '../markdown/KnowledgeDocumentMarkdown.vue'

interface RestoreDialogPayload {
  readonly document: KnowledgeDocumentDetail
  readonly revision: KnowledgeDocumentRevisionDetail
}

const originLabels: Readonly<Record<RevisionOrigin, string>> = {
  Created: '创建',
  ContentSave: '内容保存',
  Restore: '历史恢复',
  MigrationBaseline: '迁移基线',
}
const overlayStore = useOverlayStore()
const reason = ref('')
const submitting = ref(false)
const errorMessage = ref<string | null>(null)
const reasonServerError = ref<string | null>(null)
const conflict = ref(false)
const refreshing = ref(false)
const currentDocument = ref<KnowledgeDocumentDetail | null>(null)
const payload = computed(() => {
  const current = overlayStore.currentDialog
  return current?.kind === 'restore-knowledge-document-revision' &&
    isRestoreDialogPayload(current.payload)
    ? current.payload
    : null
})
const normalizedReason = computed(() => reason.value.trim())
const reasonError = computed(() => {
  if (reasonServerError.value) return reasonServerError.value
  if (normalizedReason.value.length === 0) return '请输入恢复原因。'
  if (normalizedReason.value.length < 5) return '恢复原因至少需要 5 个字符。'
  if (normalizedReason.value.length > 500) return '恢复原因不能超过 500 个字符。'
  return null
})
const sourceEqualsCurrent = computed(() =>
  Boolean(
    payload.value &&
    currentDocument.value &&
    payload.value.revision.title === currentDocument.value.title &&
    payload.value.revision.summary === currentDocument.value.summary &&
    payload.value.revision.bodyMarkdown === currentDocument.value.bodyMarkdown,
  ),
)
const restorable = computed(() =>
  Boolean(
    payload.value &&
    currentDocument.value?.lifecycleStatus === 'Draft' &&
    payload.value.revision.revisionNumber < currentDocument.value.currentRevisionNumber &&
    !sourceEqualsCurrent.value,
  ),
)
const canSubmit = computed(
  () => restorable.value && reasonError.value === null && !submitting.value && !refreshing.value,
)

watch(
  payload,
  (value) => {
    reason.value = ''
    submitting.value = false
    errorMessage.value = null
    reasonServerError.value = null
    conflict.value = false
    refreshing.value = false
    currentDocument.value = value?.document ?? null
  },
  { immediate: true },
)

function isJsonObject(value: unknown): value is Readonly<Record<string, unknown>> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function isRestoreDialogPayload(value: unknown): value is RestoreDialogPayload {
  if (!isJsonObject(value) || !isJsonObject(value.document) || !isJsonObject(value.revision)) {
    return false
  }
  return (
    typeof value.document.id === 'number' &&
    isSafeApiId(value.document.id) &&
    typeof value.document.currentRevisionNumber === 'number' &&
    isSafeApiId(value.document.currentRevisionNumber) &&
    typeof value.document.concurrencyToken === 'string' &&
    (value.document.lifecycleStatus === 'Draft' ||
      value.document.lifecycleStatus === 'Published' ||
      value.document.lifecycleStatus === 'Archived') &&
    typeof value.revision.revisionNumber === 'number' &&
    isSafeApiId(value.revision.revisionNumber) &&
    value.revision.knowledgeDocumentId === value.document.id &&
    typeof value.revision.title === 'string' &&
    (value.revision.summary === null || typeof value.revision.summary === 'string') &&
    typeof value.revision.bodyMarkdown === 'string'
  )
}

function formatDate(value: string): string {
  return value.replace('T', ' ').slice(0, 16)
}

function authorLabel(revision: KnowledgeDocumentRevisionDetail): string {
  return revision.revisionOrigin === 'MigrationBaseline'
    ? '历史作者未知'
    : (revision.authorDisplayName ?? '历史作者未知')
}

function announceCurrentRefresh(document: KnowledgeDocumentDetail): void {
  window.dispatchEvent(
    new CustomEvent('knowledge-document:current-refreshed', {
      detail: { document },
    }),
  )
  window.dispatchEvent(
    new CustomEvent('knowledge-document:history-refresh', {
      detail: { documentId: document.id },
    }),
  )
}

async function refreshCurrent(): Promise<KnowledgeDocumentDetail | null> {
  if (!payload.value || refreshing.value) return null
  refreshing.value = true
  try {
    const latest = await getKnowledgeDocument(payload.value.document.id)
    currentDocument.value = latest
    conflict.value = false
    announceCurrentRefresh(latest)
    return latest
  } catch (error: unknown) {
    errorMessage.value =
      error instanceof Error ? `重新加载失败：${error.message}` : '重新加载当前文档失败。'
    return null
  } finally {
    refreshing.value = false
  }
}

async function reloadAfterConflict(): Promise<void> {
  const latest = await refreshCurrent()
  if (!latest) return
  if (latest.lifecycleStatus !== 'Draft') {
    errorMessage.value = '当前文档已不处于草稿状态，无法恢复。请查看最新状态。'
  } else if (sourceEqualsCurrent.value) {
    errorMessage.value = '所选历史修订内容与当前版本相同，不能创建虚假恢复修订。'
  } else {
    errorMessage.value = `已重新加载当前修订 ${latest.currentRevisionNumber}，请再次明确确认恢复。`
  }
}

async function submit(): Promise<void> {
  if (!payload.value || !currentDocument.value || !canSubmit.value) return
  submitting.value = true
  errorMessage.value = null
  reasonServerError.value = null
  conflict.value = false
  try {
    const restored = await restoreKnowledgeDocumentRevision(
      currentDocument.value.id,
      payload.value.revision.revisionNumber,
      {
        concurrencyToken: currentDocument.value.concurrencyToken,
        reason: normalizedReason.value,
      },
    )
    const sourceRevisionNumber = payload.value.revision.revisionNumber
    overlayStore.closeDialog()
    window.dispatchEvent(
      new CustomEvent('knowledge-document:restored', {
        detail: { document: restored, sourceRevisionNumber },
      }),
    )
  } catch (error: unknown) {
    if (error instanceof ApiError) {
      if (error.status === 400) {
        reasonServerError.value = error.response.fieldErrors?.reason?.[0] ?? null
        errorMessage.value = '恢复请求不符合要求，请检查恢复原因。'
      } else if (error.status === 409 && error.response.code === 'conflict') {
        conflict.value = true
        errorMessage.value = '当前文档已被其他操作修改，请重新加载最新内容后再重试恢复。'
      } else if (error.status === 409 && error.response.code === 'invalid_state') {
        errorMessage.value = '当前文档已不处于草稿状态，无法恢复。请重新加载后查看最新状态。'
        await refreshCurrent()
      } else if (error.status === 422 && error.response.code === 'business_rule_violation') {
        errorMessage.value = error.message
        await refreshCurrent()
      } else {
        errorMessage.value = error.message
      }
    } else {
      errorMessage.value = error instanceof Error ? error.message : '恢复失败，请稍后重试。'
    }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <section
    v-if="payload && currentDocument"
    class="knowledge-document-restore-dialog"
    aria-labelledby="restore-dialog-title"
  >
    <header>
      <div>
        <small>历史内容恢复</small>
        <h2 id="restore-dialog-title">恢复修订 {{ payload.revision.revisionNumber }}</h2>
        <p>
          当前版本：修订 {{ currentDocument.currentRevisionNumber }} ·
          {{ lifecycleLabels[currentDocument.lifecycleStatus] }}
        </p>
      </div>
      <el-tooltip content="关闭恢复确认" placement="top">
        <button
          type="button"
          class="skh-icon-action"
          aria-label="关闭恢复确认"
          @click="overlayStore.closeDialog"
        >
          ×
        </button>
      </el-tooltip>
    </header>

    <dl class="knowledge-document-restore-dialog__snapshot">
      <div>
        <dt>来源</dt>
        <dd>{{ originLabels[payload.revision.revisionOrigin] }}</dd>
      </div>
      <div>
        <dt>作者快照</dt>
        <dd>{{ authorLabel(payload.revision) }}</dd>
      </div>
      <div>
        <dt>生成时间</dt>
        <dd>{{ formatDate(payload.revision.createdAt) }}</dd>
      </div>
      <div>
        <dt>历史标题</dt>
        <dd>{{ payload.revision.title }}</dd>
      </div>
      <div>
        <dt>历史摘要</dt>
        <dd>{{ payload.revision.summary ?? '（空）' }}</dd>
      </div>
    </dl>

    <div class="knowledge-document-restore-dialog__warning" role="note">
      <strong>恢复不会删除后续修订。</strong>
      <p>系统会把该历史内容复制为新的当前版本，并创建新的修订。</p>
    </div>

    <section
      class="knowledge-document-restore-dialog__preview"
      aria-labelledby="restore-preview-title"
    >
      <h3 id="restore-preview-title">历史正文预览</h3>
      <KnowledgeDocumentMarkdown :markdown="payload.revision.bodyMarkdown" />
    </section>

    <label class="knowledge-document-restore-dialog__reason">
      <span>恢复原因 <b aria-hidden="true">*</b></span>
      <el-input
        v-model="reason"
        type="textarea"
        :rows="4"
        maxlength="500"
        show-word-limit
        placeholder="说明为什么需要恢复该历史内容（5～500 个字符）"
        @input="reasonServerError = null"
      />
      <small :class="{ 'is-error': reasonError }">{{
        reasonError ?? `${normalizedReason.length} / 500`
      }}</small>
    </label>

    <p v-if="sourceEqualsCurrent" class="knowledge-document-restore-dialog__error" role="alert">
      所选历史修订内容与当前版本相同，不能创建虚假恢复修订。
    </p>
    <p
      v-else-if="currentDocument.lifecycleStatus !== 'Draft'"
      class="knowledge-document-restore-dialog__error"
      role="alert"
    >
      当前文档不处于草稿状态，无法恢复历史内容。
    </p>
    <p v-if="errorMessage" class="knowledge-document-restore-dialog__error" role="alert">
      {{ errorMessage }}
    </p>

    <footer>
      <el-button :disabled="submitting" @click="overlayStore.closeDialog">取消</el-button>
      <el-button
        v-if="conflict"
        :loading="refreshing"
        :disabled="submitting"
        @click="reloadAfterConflict"
        >重新加载最新内容</el-button
      >
      <el-button type="primary" :loading="submitting" :disabled="!canSubmit" @click="submit"
        >恢复并创建新修订</el-button
      >
    </footer>
  </section>
</template>
