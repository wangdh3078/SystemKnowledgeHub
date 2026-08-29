<script setup lang="ts">
import { computed, defineAsyncComponent, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Delete, DocumentChecked, UserFilled } from '@element-plus/icons-vue'
import { onBeforeRouteLeave, useRoute, useRouter } from 'vue-router'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import KnowledgeStatusProgressionPanel from '../../knowledge-status/components/KnowledgeStatusProgressionPanel.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import { formatDateTime } from '../../../app/formatters/dateTime'
import { deleteRelationship, getRelatedKnowledge } from '../../relationships/api/relationshipApi'
import {
  contextualRelationTypeLabel,
  type KnowledgeTargetType,
  type RelatedKnowledge,
} from '../../relationships/api/relationshipContracts'
import {
  deleteKnowledgeDocument,
  getKnowledgeDocument,
  updateKnowledgeDocumentContent,
  updateKnowledgeDocumentLifecycle,
} from '../api/knowledgeDocumentsApi'
import {
  documentTypeLabels,
  decodeKnowledgeDocumentDetail,
  lifecycleLabels,
  type DocumentLifecycleStatus,
  type KnowledgeDocumentDetail,
} from '../api/knowledgeDocumentContracts'
import KnowledgeDocumentMarkdown from '../markdown/KnowledgeDocumentMarkdown.vue'
import type { MarkdownAttachmentImageContext } from '../markdown/renderMarkdown'
import {
  confirmDocumentEditDiscard,
  isDocumentEditDirty,
  setActiveDocumentEditDirty,
  type DocumentEditSnapshot,
} from '../editor/documentEditState'
import { ApiError } from '../../../api/errors/ApiError'
import { getEvidenceList } from '../../evidence/api/evidenceApi'
import KnowledgeDocumentRevisionHistory from '../components/KnowledgeDocumentRevisionHistory.vue'
import {
  confirmationMethodLabels,
  evidenceTypeLabels,
  getHumanConfirmationListMethod,
  type EvidenceListItemResponse,
  type EvidenceSubjectPayload,
} from '../../evidence/api/evidenceContracts'
import { traceDocumentTypes, type TraceDocumentType } from '../api/traceabilityContracts'
import TraceabilitySection from '../components/TraceabilitySection.vue'
import ImpactContextSection from '../components/ImpactContextSection.vue'
import { openDeleteDialog } from '../../soft-delete/deleteDialog'
import KnowledgeDocumentAttachmentArea from '../components/KnowledgeDocumentAttachmentArea.vue'
import type { AttachmentMetadata } from '../api/attachmentContracts'

const KnowledgeDocumentEditor = defineAsyncComponent(
  () => import('../editor/KnowledgeDocumentEditor.vue'),
)

const route = useRoute()
const router = useRouter()
const actorStore = useActorStore()
const overlayStore = useOverlayStore()
const data = ref<KnowledgeDocumentDetail | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)
const transitionError = ref<string | null>(null)
const saveError = ref<string | null>(null)
const savedMessage = ref<string | null>(null)
const historyMode = ref(route.query?.view === 'history')
const editing = ref(false)
const previewing = ref(false)
const editorFullscreen = ref(false)
const saving = ref(false)
const saveConfirming = ref(false)
const imageUploading = ref(false)
const fileUploading = ref(false)
const editTitle = ref('')
const editSummary = ref('')
const editBodyMarkdown = ref('')
const editFileAttachments = ref<readonly AttachmentMetadata[]>([])
const initialEdit = ref<DocumentEditSnapshot | null>(null)
const validationErrors = ref<Readonly<Record<string, readonly string[]>>>({})
const relations = ref<readonly RelatedKnowledge[]>([])
const relationsLoading = ref(false)
const relationsError = ref<string | null>(null)
const evidence = ref<readonly EvidenceListItemResponse[]>([])
const evidenceLoading = ref(false)
const evidenceError = ref<string | null>(null)
const traceabilitySection = ref<InstanceType<typeof TraceabilitySection> | null>(null)
const impactContextSection = ref<InstanceType<typeof ImpactContextSection> | null>(null)
let detailLoadSequence = 0
const id = computed(() => {
  const parsed = Number(route.params.id)
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null
})
const canEdit = computed(() => actorStore.canEdit)
const isArchived = computed(() => data.value?.lifecycleStatus === 'Archived')
const supportsTraceability = computed(
  () =>
    data.value !== null &&
    traceDocumentTypes.includes(data.value.documentType as TraceDocumentType),
)
const evidenceSubject = computed<EvidenceSubjectPayload | null>(() =>
  data.value
    ? {
        subject: { type: 'KnowledgeDocument', id: data.value.id },
        title: `${documentTypeLabels[data.value.documentType]} · ${data.value.title}`,
        knowledgeStatus: data.value.knowledgeStatus,
        subjectRevisionNumber: data.value.currentRevisionNumber,
      }
    : null,
)
const validEvidenceCount = computed(
  () => evidence.value.filter((item) => item.sourceReference || item.sourceLocator).length,
)
const humanConfirmationCount = computed(
  () => evidence.value.filter((item) => item.evidenceType === 'HumanConfirmation').length,
)
const confirmationCoverageText = computed(() => {
  const coverage = data.value?.confirmationCoverage
  if (!coverage || coverage.state === 'NoConfirmation') return null
  if (coverage.state === 'LegacyConfirmationUnknown') {
    return '迁移前人工确认无法确定覆盖的修订。'
  }
  if (coverage.state === 'CurrentRevisionConfirmed') {
    return `人工确认覆盖当前修订 ${coverage.lastConfirmedRevisionNumber}`
  }
  return '内容在最近一次确认后已修改'
})
const editSnapshot = computed<DocumentEditSnapshot>(() => ({
  title: editTitle.value,
  summary: editSummary.value,
  bodyMarkdown: editBodyMarkdown.value,
  fileAttachmentIds: editFileAttachments.value.map((attachment) => attachment.attachmentId),
}))
const dirty = computed(
  () => initialEdit.value !== null && isDocumentEditDirty(editSnapshot.value, initialEdit.value),
)
const currentImageContext = computed<MarkdownAttachmentImageContext | undefined>(() =>
  data.value
    ? {
        documentId: data.value.id,
        imageAttachmentIds: data.value.attachmentReferences
          .filter((attachment) => attachment.kind === 'Image')
          .map((attachment) => attachment.attachmentId),
      }
    : undefined,
)
const currentFileAttachments = computed(
  () => data.value?.attachmentReferences.filter((attachment) => attachment.kind === 'File') ?? [],
)
const titleValid = computed(
  () => editTitle.value.trim().length > 0 && editTitle.value.trim().length <= 300,
)
const canSave = computed(
  () =>
    editing.value &&
    dirty.value &&
    titleValid.value &&
    !saving.value &&
    !saveConfirming.value &&
    !imageUploading.value &&
    !fileUploading.value,
)
async function load(): Promise<void> {
  const requestedId = id.value
  const requestSequence = ++detailLoadSequence
  if (requestedId === null) {
    loading.value = false
    error.value = '文档 ID 无效。'
    return
  }
  loading.value = true
  error.value = null
  try {
    const document = await getKnowledgeDocument(requestedId)
    if (requestSequence === detailLoadSequence && id.value === requestedId) {
      data.value = document
    }
  } catch (reason: unknown) {
    if (requestSequence === detailLoadSequence && id.value === requestedId) {
      error.value = reason instanceof Error ? reason.message : '无法读取知识内容。'
    }
  } finally {
    if (requestSequence === detailLoadSequence) loading.value = false
  }
}
async function loadRelations(): Promise<void> {
  if (id.value === null) return
  relationsLoading.value = true
  relationsError.value = null
  try {
    relations.value = await getRelatedKnowledge('KnowledgeDocument', id.value)
  } catch (reason: unknown) {
    relationsError.value = reason instanceof Error ? reason.message : '无法加载关联对象。'
  } finally {
    relationsLoading.value = false
  }
}
async function loadEvidence(): Promise<void> {
  if (id.value === null) return
  evidenceLoading.value = true
  evidenceError.value = null
  try {
    evidence.value = (await getEvidenceList('KnowledgeDocument', id.value)).items
  } catch (reason: unknown) {
    evidenceError.value = reason instanceof Error ? reason.message : '无法加载证据。'
  } finally {
    evidenceLoading.value = false
  }
}
function refreshTraceability(): void {
  traceabilitySection.value?.refresh()
}
function refreshImpactContext(): void {
  impactContextSection.value?.refresh()
}
function addEvidence(): void {
  if (!evidenceSubject.value || !canEdit.value || isArchived.value || editing.value) return
  overlayStore.openDrawer({
    kind: 'add-evidence',
    id: null,
    mode: 'create',
    payload: evidenceSubject.value,
  })
}
function addHumanConfirmation(): void {
  if (!evidenceSubject.value || !canEdit.value || isArchived.value || editing.value) return
  overlayStore.openDrawer({
    kind: 'human-confirmation',
    id: null,
    mode: 'create',
    payload: evidenceSubject.value,
  })
}
function addRelation(): void {
  if (!data.value || !canEdit.value || isArchived.value) return
  overlayStore.openDrawer({
    kind: 'add-relationship',
    id: data.value.id,
    mode: 'create',
    payload: {
      source: { type: 'KnowledgeDocument', id: data.value.id },
      title: data.value.title,
      documentType: data.value.documentType,
    },
  })
}
function openAttachmentPreview(attachment: AttachmentMetadata): void {
  if (!data.value || !attachment.canPreview || !attachment.canDownload) return
  overlayStore.openDialog({
    kind: 'attachment-preview',
    id: attachment.attachmentId,
    mode: 'read',
    payload: { documentId: data.value.id, attachment },
  })
}
function relatedRoute(item: RelatedKnowledge): { name: string; params: { id: string } } | null {
  const routes: Readonly<Partial<Record<KnowledgeTargetType, string>>> = {
    System: 'system-detail',
    BusinessFunction: 'business-function-detail',
    DatabaseObject: 'database-object-detail',
    BusinessRule: 'business-rule-detail',
    Integration: 'integration-detail',
    KnowledgeDocument: 'knowledge-document-detail',
  }
  const name = routes[item.related.type]
  return name ? { name, params: { id: String(item.related.id) } } : null
}
function openRelated(item: RelatedKnowledge): void {
  const target = relatedRoute(item)
  if (target) void router.push(target)
}
async function removeRelation(item: RelatedKnowledge): Promise<void> {
  try {
    await ElMessageBox.confirm('确认移除此关联？该操作不会删除关联对象。', '移除关联', {
      type: 'warning',
    })
    await deleteRelationship(item.id)
    window.dispatchEvent(new CustomEvent('relationship:changed'))
    ElMessage.success('关联已移除。')
  } catch (reason: unknown) {
    if (reason !== 'cancel' && reason !== 'close')
      relationsError.value = reason instanceof Error ? reason.message : '移除关联失败。'
  }
}
function beginEdit(): void {
  if (!data.value || isArchived.value || !canEdit.value) return
  editTitle.value = data.value.title
  editSummary.value = data.value.summary ?? ''
  editBodyMarkdown.value = data.value.bodyMarkdown
  editFileAttachments.value = data.value.attachmentReferences.filter(
    (attachment) => attachment.kind === 'File',
  )
  initialEdit.value = {
    title: editTitle.value,
    summary: editSummary.value,
    bodyMarkdown: editBodyMarkdown.value,
    fileAttachmentIds: editFileAttachments.value.map((attachment) => attachment.attachmentId),
  }
  previewing.value = false
  editorFullscreen.value = false
  saveError.value = null
  imageUploading.value = false
  fileUploading.value = false
  validationErrors.value = {}
  savedMessage.value = null
  editing.value = true
}
async function confirmDiscard(): Promise<boolean> {
  if (imageUploading.value || fileUploading.value) {
    try {
      await ElMessageBox.confirm(
        '附件仍在上传。离开编辑会中止本次请求；服务端若已完成上传，文件会保留为未引用附件。未保存内容也会丢失。',
        '确认离开编辑',
        { confirmButtonText: '确认离开', cancelButtonText: '继续编辑', type: 'warning' },
      )
      return true
    } catch {
      return false
    }
  }
  if (!dirty.value) return true
  setActiveDocumentEditDirty(true)
  return confirmDocumentEditDiscard()
}
async function cancelEdit(): Promise<void> {
  if (!(await confirmDiscard())) return
  finishEdit()
}
function finishEdit(): void {
  editing.value = false
  previewing.value = false
  editorFullscreen.value = false
  imageUploading.value = false
  fileUploading.value = false
  editFileAttachments.value = []
  initialEdit.value = null
  saveError.value = null
  validationErrors.value = {}
}
async function enterHistory(): Promise<void> {
  if (editing.value && !(await confirmDiscard())) return
  if (editing.value) finishEdit()
  historyMode.value = true
  await router.replace({ query: { ...route.query, view: 'history' } })
}
function returnToCurrentContent(): void {
  if (!data.value) {
    void router.push({ name: 'knowledge-documents-list' })
    return
  }
  historyMode.value = false
  const query = { ...route.query }
  delete query.view
  void router.replace({ query })
}
function requestDelete(): void {
  if (!data.value?.canDelete || editing.value) return
  const current = data.value
  openDeleteDialog(overlayStore, {
    objectTypeLabel: '知识内容',
    actionLabel: '删除知识内容',
    displayName: current.title,
    concurrencyToken: current.concurrencyToken,
    execute: () => deleteKnowledgeDocument(current.id, current.concurrencyToken),
    onDeleted: () => router.push({ name: 'knowledge-documents-list' }),
    onRefresh: load,
    onUnavailable: () => router.push({ name: 'knowledge-documents-list' }),
  })
}
function fieldError(field: string): string | null {
  return validationErrors.value[field]?.[0] ?? null
}
async function performSave(): Promise<void> {
  if (
    !data.value ||
    !editing.value ||
    !dirty.value ||
    !titleValid.value ||
    saving.value ||
    imageUploading.value ||
    fileUploading.value
  )
    return
  saving.value = true
  saveError.value = null
  validationErrors.value = {}
  try {
    const response = await updateKnowledgeDocumentContent(data.value.id, {
      title: editTitle.value.trim(),
      summary: editSummary.value.trim() || null,
      bodyMarkdown: editBodyMarkdown.value,
      concurrencyToken: data.value.concurrencyToken,
      fileAttachmentIds: editFileAttachments.value.map((attachment) => attachment.attachmentId),
    })
    data.value = response
    refreshTraceability()
    initialEdit.value = {
      title: response.title,
      summary: response.summary ?? '',
      bodyMarkdown: response.bodyMarkdown,
      fileAttachmentIds: response.attachmentReferences
        .filter((attachment) => attachment.kind === 'File')
        .map((attachment) => attachment.attachmentId),
    }
    editing.value = false
    previewing.value = false
    editorFullscreen.value = false
    ElMessage.success('已保存。')
  } catch (reason: unknown) {
    if (reason instanceof ApiError && reason.status === 409) {
      saveError.value = '文档已被其他操作修改。请重新加载最新内容后再继续编辑。'
      return
    }
    if (reason instanceof ApiError && reason.status === 400) {
      validationErrors.value = reason.response.fieldErrors ?? {}
      saveError.value = '文档内容不符合要求，请检查后重试。'
      return
    }
    if (reason instanceof ApiError && reason.status === 403) {
      await actorStore.refreshCurrentUser()
      saveError.value = '权限已变化，未保存内容仍保留。'
      return
    }
    if (reason instanceof ApiError && reason.status === 404) {
      saveError.value = '当前文档已不存在；未保存正文仍保留，请复制需要的内容后返回列表。'
      return
    }
    if (reason instanceof ApiError && reason.status === 422) {
      saveError.value = '图片或普通附件引用无效或不可用；未保存内容仍保留，请检查附件后重试。'
      return
    }
    if (reason instanceof ApiError && reason.status === 503) {
      saveError.value = '附件存储暂不可用；未保存正文仍保留，请稍后重试。'
      return
    }
    saveError.value = reason instanceof Error ? reason.message : '保存失败，请稍后重试。'
  } finally {
    saving.value = false
  }
}
async function requestSave(): Promise<void> {
  if (!data.value || !canSave.value) return
  if (data.value.lifecycleStatus !== 'Published') {
    await performSave()
    return
  }

  saveConfirming.value = true
  try {
    await ElMessageBox.confirm(
      '保存后新内容立即成为已发布内容并生成新修订。',
      '确认保存已发布内容',
      {
        confirmButtonText: '确认保存并立即发布',
        cancelButtonText: '取消',
        type: 'warning',
      },
    )
    await performSave()
  } catch (reason: unknown) {
    if (reason !== 'cancel' && reason !== 'close') {
      saveError.value = reason instanceof Error ? reason.message : '保存确认失败。'
    }
  } finally {
    saveConfirming.value = false
  }
}
async function reloadAfterConflict(): Promise<void> {
  if (!(await confirmDiscard())) return
  editing.value = false
  previewing.value = false
  editorFullscreen.value = false
  initialEdit.value = null
  saveError.value = null
  validationErrors.value = {}
  await load()
  refreshTraceability()
}
function handleShortcut(event: KeyboardEvent): void {
  if (event.key === 'Escape' && editorFullscreen.value) {
    if (document.querySelector('.el-overlay-message-box, .el-overlay-dialog')) return
    event.preventDefault()
    editorFullscreen.value = false
    return
  }
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's' && canSave.value) {
    event.preventDefault()
    void requestSave()
  }
}
function beforeUnload(event: BeforeUnloadEvent): void {
  if (!editing.value || (!dirty.value && !imageUploading.value && !fileUploading.value)) return
  event.preventDefault()
  event.returnValue = ''
}
function confirmMessage(target: DocumentLifecycleStatus): string {
  return target === 'Published'
    ? '确认发布这篇文档？发布不会改变知识状态。'
    : target === 'Archived'
      ? '确认归档这篇文档？归档后默认列表不会显示它。'
      : '确认恢复为草稿？'
}
async function transition(target: DocumentLifecycleStatus): Promise<void> {
  if (!data.value) return
  try {
    await ElMessageBox.confirm(confirmMessage(target), '确认生命周期变更', {
      confirmButtonText: '确认',
      cancelButtonText: '取消',
      type: 'warning',
    })
    transitionError.value = null
    data.value = await updateKnowledgeDocumentLifecycle(
      data.value.id,
      target,
      data.value.concurrencyToken,
    )
    refreshTraceability()
  } catch (reason: unknown) {
    if (reason === 'cancel' || reason === 'close') return
    transitionError.value =
      reason instanceof Error ? reason.message : '生命周期变更失败，请刷新后重试。'
  }
}
function readEventDocument(event: Event): KnowledgeDocumentDetail | null {
  if (!(event instanceof CustomEvent)) return null
  const detailValue: unknown = event.detail
  if (typeof detailValue !== 'object' || detailValue === null || !('document' in detailValue)) {
    return null
  }
  try {
    return decodeKnowledgeDocumentDetail(detailValue.document)
  } catch {
    return null
  }
}
function handleCurrentRefreshed(event: Event): void {
  const document = readEventDocument(event)
  if (document && document.id === id.value) {
    detailLoadSequence += 1
    loading.value = false
    data.value = document
    refreshTraceability()
  }
}
function handleRelationshipChanged(): void {
  void loadRelations()
  refreshTraceability()
  refreshImpactContext()
}
function handleEvidenceChanged(): void {
  void loadEvidence()
  refreshTraceability()
}
function handleKnowledgeStatusChanged(): void {
  void load()
  refreshTraceability()
}
function handleHumanConfirmationChanged(event: Event): void {
  if (!(event instanceof CustomEvent)) return
  const eventDetail: unknown = event.detail
  if (typeof eventDetail !== 'object' || eventDetail === null || !('subject' in eventDetail)) return
  const subject: unknown = eventDetail.subject
  if (
    typeof subject !== 'object' ||
    subject === null ||
    !('type' in subject) ||
    subject.type !== 'KnowledgeDocument' ||
    !('id' in subject) ||
    subject.id !== id.value
  )
    return
  void load()
  refreshTraceability()
}
function handleRestored(event: Event): void {
  const document = readEventDocument(event)
  if (!document || document.id !== id.value || !(event instanceof CustomEvent)) return
  const detailValue: unknown = event.detail
  if (
    typeof detailValue !== 'object' ||
    detailValue === null ||
    !('sourceRevisionNumber' in detailValue) ||
    typeof detailValue.sourceRevisionNumber !== 'number' ||
    !Number.isSafeInteger(detailValue.sourceRevisionNumber) ||
    detailValue.sourceRevisionNumber < 1
  )
    return
  detailLoadSequence += 1
  loading.value = false
  data.value = document
  refreshTraceability()
  historyMode.value = false
  savedMessage.value = `已从修订 ${detailValue.sourceRevisionNumber} 恢复，并创建修订 ${document.currentRevisionNumber}`
}
watch(id, () => {
  historyMode.value = route.query?.view === 'history'
  data.value = null
  void load()
  void loadRelations()
  void loadEvidence()
})
watch(
  () => route.query?.view,
  (value) => {
    historyMode.value = value === 'history'
  },
)
watch(
  [editing, dirty, imageUploading, fileUploading],
  ([isEditing, isDirty, isImageUploading, isFileUploading]) => {
    setActiveDocumentEditDirty(isEditing && (isDirty || isImageUploading || isFileUploading))
  },
  { immediate: true },
)
watch(editorFullscreen, (active) => {
  document.body.classList.toggle('knowledge-document-editor-fullscreen-active', active)
})
onBeforeRouteLeave(async () => !editing.value || (await confirmDiscard()))
onMounted(() => {
  window.addEventListener('keydown', handleShortcut)
  window.addEventListener('beforeunload', beforeUnload)
  void load()
  void loadRelations()
  void loadEvidence()
  window.addEventListener('relationship:changed', handleRelationshipChanged)
  window.addEventListener('evidence:changed', handleEvidenceChanged)
  window.addEventListener('human-confirmation:changed', handleHumanConfirmationChanged)
  window.addEventListener('knowledge-status:changed', handleKnowledgeStatusChanged)
  window.addEventListener('knowledge-document:current-refreshed', handleCurrentRefreshed)
  window.addEventListener('knowledge-document:restored', handleRestored)
})
onBeforeUnmount(() => {
  setActiveDocumentEditDirty(false)
  document.body.classList.remove('knowledge-document-editor-fullscreen-active')
  window.removeEventListener('keydown', handleShortcut)
  window.removeEventListener('beforeunload', beforeUnload)
  window.removeEventListener('relationship:changed', handleRelationshipChanged)
  window.removeEventListener('evidence:changed', handleEvidenceChanged)
  window.removeEventListener('human-confirmation:changed', handleHumanConfirmationChanged)
  window.removeEventListener('knowledge-status:changed', handleKnowledgeStatusChanged)
  window.removeEventListener('knowledge-document:current-refreshed', handleCurrentRefreshed)
  window.removeEventListener('knowledge-document:restored', handleRestored)
})
</script>

<template>
  <div class="knowledge-document-detail-page">
    <KnowledgeDocumentRevisionHistory
      v-if="historyMode && id !== null && !data"
      :document-id="id"
      :document="null"
      :can-restore="false"
      @return="returnToCurrentContent"
    />
    <LoadingState v-else-if="loading && !data" message="正在读取知识内容…" /><ErrorState
      v-else-if="error && !data"
      title="知识内容加载失败"
      :message="error"
      @retry="load"
    /><template v-else-if="data"
      ><header class="knowledge-document-detail__header">
        <nav>
          <button type="button" @click="router.push({ name: 'knowledge-documents-list' })">
            知识内容</button
          ><b>/</b><span>{{ data.title }}</span>
        </nav>
        <div class="knowledge-document-detail__title">
          <div>
            <h1>{{ data.title }}</h1>
            <p v-if="data.summary">{{ data.summary }}</p>
          </div>
          <div v-if="!historyMode" class="knowledge-document-detail__actions">
            <el-button @click="enterHistory"
              >修订历史（{{ data.currentRevisionNumber }}）</el-button
            >
            <el-button
              v-if="data.canDelete && !editing"
              type="danger"
              plain
              :icon="Delete"
              @click="requestDelete"
              >删除知识内容</el-button
            >
            <template v-if="editing">
              <el-button :disabled="saving" @click="cancelEdit">取消</el-button>
              <el-button
                type="primary"
                :disabled="!canSave"
                :loading="saving"
                @click="requestSave"
                >{{ saving ? '保存中…' : '保存' }}</el-button
              >
            </template>
            <template v-else-if="canEdit">
              <el-button v-if="!isArchived" type="primary" @click="beginEdit">编辑</el-button>
              <el-button
                v-if="data.lifecycleStatus === 'Draft'"
                type="primary"
                @click="transition('Published')"
                >发布</el-button
              ><el-button v-if="data.lifecycleStatus === 'Published'" @click="transition('Draft')"
                >退回草稿</el-button
              ><el-button
                v-if="data.lifecycleStatus === 'Published'"
                type="danger"
                plain
                @click="transition('Archived')"
                >归档</el-button
              ><el-button
                v-if="data.lifecycleStatus === 'Archived'"
                type="primary"
                plain
                @click="transition('Draft')"
                >恢复为草稿</el-button
              >
            </template>
          </div>
        </div>
        <div class="knowledge-document-detail__tags">
          <span>{{ documentTypeLabels[data.documentType] }}</span
          ><el-tag effect="plain">{{ lifecycleLabels[data.lifecycleStatus] }}</el-tag
          ><KnowledgeStatusBadge :status="data.knowledgeStatus" />
        </div>
        <p
          v-if="confirmationCoverageText"
          :class="[
            'knowledge-document-confirmation-coverage',
            { 'is-warning': data.confirmationCoverage.state === 'ChangedSinceConfirmation' },
          ]"
          role="status"
        >
          {{ confirmationCoverageText }}
        </p>
      </header>
      <KnowledgeDocumentRevisionHistory
        v-if="historyMode"
        :document-id="data.id"
        :document="data"
        :can-restore="canEdit"
        @return="returnToCurrentContent"
      />
      <p v-if="!historyMode && savedMessage" class="knowledge-document-saved">{{ savedMessage }}</p>
      <p v-if="!historyMode && transitionError" class="knowledge-document-error">
        {{ transitionError }}
      </p>
      <section v-if="!historyMode && editing" class="knowledge-document-edit">
        <div class="knowledge-document-edit__mode">
          <span class="knowledge-document-edit__editing-state"><i></i>编辑中</span>
          <span
            :class="[
              'knowledge-document-edit__save-state',
              { 'is-dirty': dirty, 'is-saving': saving },
            ]"
          >
            {{
              fileUploading
                ? '附件上传中…'
                : imageUploading
                  ? '图片上传中…'
                  : saving
                    ? '正在保存…'
                    : dirty
                      ? '未保存'
                      : '已保存'
            }}
          </span>
        </div>
        <p
          v-if="data.lifecycleStatus === 'Published'"
          class="knowledge-document-published-warning"
          role="note"
        >
          保存后新内容立即成为已发布内容并生成新修订。
        </p>
        <el-form label-position="top">
          <el-form-item label="标题" required :error="fieldError('title') ?? undefined"
            ><el-input v-model="editTitle" maxlength="300" show-word-limit
          /></el-form-item>
          <el-form-item label="摘要" :error="fieldError('summary') ?? undefined"
            ><el-input
              v-model="editSummary"
              type="textarea"
              :rows="2"
              maxlength="2000"
              show-word-limit
          /></el-form-item>
        </el-form>
        <KnowledgeDocumentEditor
          v-model="editBodyMarkdown"
          :previewing="previewing"
          :fullscreen="editorFullscreen"
          :document-id="data.id"
          :attachment-references="data.attachmentReferences"
          @edit="previewing = false"
          @preview="previewing = true"
          @request-save="requestSave"
          @toggle-fullscreen="editorFullscreen = !editorFullscreen"
          @uploading-change="imageUploading = $event"
        />
        <p v-if="fieldError('bodyMarkdown')" class="knowledge-document-error">
          {{ fieldError('bodyMarkdown') }}
        </p>
        <KnowledgeDocumentAttachmentArea
          :document-id="data.id"
          :attachments="editFileAttachments"
          editable
          @update:attachments="editFileAttachments = $event"
          @uploading-change="fileUploading = $event"
          @preview="openAttachmentPreview"
        />
        <p v-if="fieldError('fileAttachmentIds')" class="knowledge-document-error">
          {{ fieldError('fileAttachmentIds') }}
        </p>
        <p v-if="saveError" class="knowledge-document-error">
          {{ saveError }}
          <el-button
            v-if="saveError.includes('其他操作')"
            text
            type="primary"
            @click="reloadAfterConflict"
            >重新加载</el-button
          >
        </p>
      </section>
      <section v-else-if="!historyMode" class="knowledge-document-body">
        <p v-if="!data.bodyMarkdown.trim()" class="text-muted">该文档暂无正文。</p>
        <KnowledgeDocumentMarkdown
          v-else
          :markdown="data.bodyMarkdown"
          :attachment-image-context="currentImageContext"
        />
      </section>
      <KnowledgeDocumentAttachmentArea
        v-if="!historyMode && !editing"
        :document-id="data.id"
        :attachments="currentFileAttachments"
        @preview="openAttachmentPreview"
      />
      <TraceabilitySection
        v-if="!historyMode && supportsTraceability"
        ref="traceabilitySection"
        :document-id="data.id"
      />
      <ImpactContextSection
        v-if="!historyMode && supportsTraceability"
        ref="impactContextSection"
        :document-id="data.id"
      />
      <section v-if="!historyMode && !editing" class="knowledge-document-relations">
        <div class="knowledge-document-relations__heading">
          <h2>关联对象</h2>
          <el-button
            v-if="canEdit && !isArchived"
            type="primary"
            plain
            size="small"
            @click="addRelation"
            >添加关联</el-button
          >
        </div>
        <p v-if="relationsLoading">正在加载关联对象…</p>
        <p v-else-if="relationsError" class="knowledge-document-error">
          无法加载关联对象 <el-button text type="primary" @click="loadRelations">重试</el-button>
        </p>
        <p v-else-if="!relations.length" class="text-muted">暂无关联对象</p>
        <div v-else class="knowledge-document-relations__list">
          <div v-for="item in relations" :key="item.id" class="knowledge-document-relations__row">
            <span>{{ item.direction === 'Outgoing' ? '指向' : '来自' }}</span
            ><span>{{ contextualRelationTypeLabel(item.relationType, item.direction) }}</span
            ><button type="button" @click="openRelated(item)">
              {{ item.objectTypeLabel }} · {{ item.title }}</button
            ><el-button
              v-if="canEdit && !isArchived"
              text
              type="danger"
              size="small"
              @click="removeRelation(item)"
              >移除</el-button
            >
          </div>
        </div>
      </section>
      <section v-if="!historyMode && !editing" class="knowledge-document-evidence">
        <div class="knowledge-document-evidence__heading">
          <div>
            <h2>证据与人工确认</h2>
            <p>记录支持当前知识结论的依据；保存后不会自动改变知识状态。</p>
          </div>
          <div v-if="canEdit && !isArchived" class="knowledge-document-evidence__actions">
            <el-button class="skh-section-action skh-evidence-action" type="primary" :icon="DocumentChecked" @click="addEvidence">添加证据</el-button
            ><el-button class="skh-section-action skh-human-confirmation-action" plain :icon="UserFilled" @click="addHumanConfirmation">添加人工确认</el-button>
          </div>
        </div>
        <p v-if="evidenceLoading">正在加载证据…</p>
        <p v-else-if="evidenceError" class="knowledge-document-error">
          无法加载证据 <el-button text type="primary" @click="loadEvidence">重试</el-button>
        </p>
        <p v-else-if="!evidence.length" class="text-muted">暂无证据或人工确认。</p>
        <div v-else class="knowledge-document-evidence__list">
          <article
            v-for="item in evidence"
            :key="item.id"
            class="knowledge-document-evidence__item"
          >
            <div>
              <el-tag size="small" effect="plain">{{
                evidenceTypeLabels[item.evidenceType]
              }}</el-tag
              ><strong>{{ item.sourceTitle }}</strong>
            </div>
            <p v-if="item.summary">{{ item.summary }}</p>
            <p>{{ item.supportReason }}</p>
            <small
              >提供者：{{ item.provider.displayName }} · {{ item.provider.roleOrIdentity
              }}<template v-if="getHumanConfirmationListMethod(item)">
                · {{ confirmationMethodLabels[getHumanConfirmationListMethod(item)!] }}</template
              ></small
            >
          </article>
        </div>
      </section>
      <KnowledgeStatusProgressionPanel
        v-if="!historyMode && !editing"
        :id="data.id"
        target-type="KnowledgeDocument"
        :title="data.title"
        :status="data.knowledgeStatus"
        :concurrency-token="data.concurrencyToken"
        :subject-revision-number="data.currentRevisionNumber"
        :evidence-count="validEvidenceCount"
        :human-confirmation-count="humanConfirmationCount"
        :can-change="canEdit && !isArchived && !editing"
      />
      <section v-if="!historyMode" class="knowledge-document-meta">
        <h2>元数据</h2>
        <dl>
          <div>
            <dt>创建人</dt>
            <dd>{{ data.createdByDisplayName }}</dd>
          </div>
          <div>
            <dt>创建时间</dt>
            <dd>{{ formatDateTime(data.createdAt) }}</dd>
          </div>
          <div>
            <dt>更新人</dt>
            <dd>{{ data.updatedByDisplayName }}</dd>
          </div>
          <div>
            <dt>更新时间</dt>
            <dd>{{ formatDateTime(data.updatedAt) }}</dd>
          </div>
          <div>
            <dt>发布时间</dt>
            <dd>{{ formatDateTime(data.publishedAt) }}</dd>
          </div>
          <div>
            <dt>归档时间</dt>
            <dd>{{ formatDateTime(data.archivedAt) }}</dd>
          </div>
        </dl>
      </section></template
    >
  </div>
</template>

<style src="../knowledge-documents.css"></style>
