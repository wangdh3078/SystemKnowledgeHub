<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref, watch } from 'vue'
import { Close, EditPen, Refresh, UserFilled } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { ApiError } from '../../../api/errors/ApiError'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import { formatDateTime } from '../../../app/formatters/dateTime'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import HistoricalTargetLabel from '../../../components/data-display/HistoricalTargetLabel.vue'
import { getEvidenceDetail, updateEvidence, withdrawHumanConfirmation } from '../api/evidenceApi'
import {
  confirmationMethodLabels,
  confidenceLabels,
  evidenceTypeLabels,
  getHumanConfirmationMethod,
  type EvidenceConfidence,
  type EvidenceDetailResponse,
} from '../api/evidenceContracts'

import { getKnowledgeDocument } from '../../knowledge-documents/api/knowledgeDocumentsApi'
import { documentTypeLabels } from '../../knowledge-documents/api/knowledgeDocumentContracts'

const props = defineProps<{ evidenceId: number | null }>()
const overlayStore = useOverlayStore()
const actorStore = useActorStore()
const detail = ref<EvidenceDetailResponse | null>(null)
const loading = ref(false)
const saving = ref(false)
const editing = ref(false)
const withdrawing = ref(false)
const withdrawalReason = ref('')
const withdrawalError = ref<string | null>(null)
let loadSequence = 0
const lifecycle = computed(() => detail.value?.humanConfirmationLifecycle)
const canWithdraw = computed(() => actorStore.canEdit && lifecycle.value?.status === 'Active')
const errorMessage = ref<string | null>(null)
const conflict = ref(false)
const form = reactive({
  sourceTitle: '',
  sourceReference: '',
  summary: '',
  supportReason: '',
  confidence: '' as EvidenceConfidence | '',
  locatorJson: '',
  providerName: '',
  providerRole: '',
  occurredAt: '',
  team: '',
  providerExternalKey: '',
  providerSource: '',
  providerNote: '',
})

const sourceLocatorText = computed(() =>
  detail.value?.sourceLocator ? JSON.stringify(detail.value.sourceLocator, null, 2) : null,
)
const confirmationMethod = computed(() => {
  if (!detail.value) return null
  const method = getHumanConfirmationMethod(detail.value)
  return method === null ? null : confirmationMethodLabels[method]
})
const codeLocatorRows = computed(() => {
  if (detail.value?.evidenceType !== 'CodeReference' || !detail.value.sourceLocator) return []
  const labels: Readonly<Record<string, string>> = {
    repository: '代码仓库',
    file: '文件',
    class: '类',
    method: '方法',
    startLine: '起始行',
    endLine: '结束行',
  }
  return Object.entries(labels).flatMap(([key, label]) => {
    const value = detail.value?.sourceLocator?.[key]
    return typeof value === 'string' || typeof value === 'number'
      ? [[label, String(value)] as const]
      : []
  })
})
const subjectDeleted = computed(() => detail.value?.subjectIdentity?.isDeleted === true)

function normalize(value: string): string | null {
  const result = value.trim()
  return result.length ? result : null
}

function beginEdit(): void {
  if (!detail.value || detail.value.evidenceType === 'HumanConfirmation') return
  form.sourceTitle = detail.value.sourceTitle
  form.sourceReference = detail.value.sourceReference ?? ''
  form.locatorJson = detail.value.sourceLocator
    ? JSON.stringify(detail.value.sourceLocator, null, 2)
    : ''
  form.summary = detail.value.summary ?? ''
  form.supportReason = detail.value.supportReason
  form.confidence = detail.value.confidence ?? ''
  form.providerName = detail.value.provider.displayName
  form.providerRole = detail.value.provider.roleOrIdentity
  form.occurredAt = detail.value.provider.occurredAt
  form.team = detail.value.provider.team ?? ''
  form.providerExternalKey = detail.value.provider.externalUserKey ?? ''
  form.providerSource = detail.value.provider.source ?? ''
  form.providerNote = detail.value.provider.note ?? ''
  errorMessage.value = null
  conflict.value = false
  editing.value = true
}

async function load(): Promise<void> {
  const id = props.evidenceId
  const sequence = ++loadSequence
  if (id === null) {
    detail.value = null
    return
  }
  loading.value = true
  errorMessage.value = null
  try {
    const response = await getEvidenceDetail(id)
    if (sequence !== loadSequence || props.evidenceId !== id) return
    detail.value = response
    editing.value = false
    conflict.value = false
  } catch (error: unknown) {
    if (sequence === loadSequence)
      errorMessage.value = error instanceof Error ? error.message : '证据详情加载失败。'
  } finally {
    if (sequence === loadSequence) loading.value = false
  }
}

async function withdraw(): Promise<void> {
  const current = detail.value
  if (!current || !canWithdraw.value || saving.value) return
  const reason = withdrawalReason.value.trim()
  if (!reason || reason.length > 1000) {
    withdrawalError.value = '撤销原因须为 1～1000 个字符。'
    return
  }
  saving.value = true
  withdrawalError.value = null
  try {
    await withdrawHumanConfirmation(current.id, {
      reason,
      concurrencyToken: current.concurrencyToken,
    })
    window.dispatchEvent(new CustomEvent('evidence:changed', { detail: { subject: current.subject } }))
    window.dispatchEvent(
      new CustomEvent('human-confirmation:changed', { detail: { subject: current.subject } }),
    )
    if (props.evidenceId !== current.id) return
    withdrawing.value = false
    await load()
    ElMessage.success('人工确认已撤销；原确认记录已保留，知识状态未改变。')
  } catch (error: unknown) {
    if (props.evidenceId !== current.id) return
    withdrawalError.value = error instanceof Error ? error.message : '撤销确认失败。'
    conflict.value = error instanceof ApiError && error.status === 409
  } finally {
    saving.value = false
  }
}

async function reconfirm(): Promise<void> {
  const current = detail.value
  if (
    !current?.subjectContext ||
    subjectDeleted.value ||
    !actorStore.canEdit ||
    lifecycle.value?.replacedByHumanConfirmationId
  )
    return
  try {
    const currentDocument =
      current.subject.type === 'KnowledgeDocument'
        ? await getKnowledgeDocument(current.subject.id)
        : null
    const revision = currentDocument?.currentRevisionNumber
    if (props.evidenceId !== current.id) return
    const linked =
      revision === undefined || revision === current.knowledgeDocumentRevisionNumberSnapshot
    overlayStore.openDrawer({
      kind: 'human-confirmation',
      id: current.subject.id,
      mode: 'create',
      payload: {
        subject: current.subject,
        title: currentDocument
          ? `${documentTypeLabels[currentDocument.documentType]} · ${currentDocument.title}`
          : current.subjectContext.title,
        knowledgeStatus: current.subjectContext.knowledgeStatus,
        subjectDetailKey: current.subjectDetailKey,
        subjectRevisionNumber: revision,
        replacesHumanConfirmationId: linked ? current.id : null,
        replacementRevisionNumber: current.knowledgeDocumentRevisionNumberSnapshot,
        reconfirmationNote: linked
          ? '本次新确认将明确替代已撤销记录，原记录保持不变。'
          : '本次为当前修订的新确认，不建立旧修订的替代链接。',
      },
    })
  } catch (error: unknown) {
    errorMessage.value = error instanceof Error ? error.message : '无法读取当前确认目标。'
  }
}

async function save(): Promise<void> {
  if (!detail.value || props.evidenceId === null) return
  errorMessage.value = null
  conflict.value = false
  if (
    !form.sourceTitle.trim() ||
    !form.supportReason.trim() ||
    !form.providerName.trim() ||
    !form.providerRole.trim()
  ) {
    errorMessage.value = '请填写来源标题、支持理由和证据提供人信息。'
    return
  }
  let locator: Readonly<Record<string, unknown>> | null = null
  const rawLocator = form.locatorJson.trim()
  if (rawLocator) {
    try {
      const parsed: unknown = JSON.parse(rawLocator)
      if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
        errorMessage.value = '来源定位必须是 JSON 对象。'
        return
      }
      locator = parsed as Readonly<Record<string, unknown>>
    } catch {
      errorMessage.value = '来源定位不是有效的 JSON。'
      return
    }
  }
  if (!form.sourceReference.trim() && locator === null) {
    errorMessage.value = '来源引用与来源定位至少保留一项。'
    return
  }

  saving.value = true
  try {
    detail.value = await updateEvidence(props.evidenceId, {
      sourceTitle: form.sourceTitle.trim(),
      sourceReference: normalize(form.sourceReference),
      sourceLocator: locator,
      summary: normalize(form.summary),
      supportReason: form.supportReason.trim(),
      confidence: form.confidence || null,
      provider: {
        displayName: form.providerName.trim(),
        roleOrIdentity: form.providerRole.trim(),
        occurredAt: form.occurredAt,
        team: normalize(form.team),
        externalUserKey: normalize(form.providerExternalKey),
        source: normalize(form.providerSource),
        note: normalize(form.providerNote),
      },
      actor: actorStore.actor,
      concurrencyToken: detail.value.concurrencyToken,
    })
    editing.value = false
    ElMessage.success('证据已更新；支持对象与知识状态均未改变。')
    window.dispatchEvent(new CustomEvent('evidence:changed', { detail: { subject: detail.value.subject } }))
  } catch (error: unknown) {
    conflict.value = error instanceof ApiError && error.status === 409
    errorMessage.value = error instanceof Error ? error.message : '证据更新失败。'
  } finally {
    saving.value = false
  }
}

function openHumanConfirmation(): void {
  if (!detail.value?.subjectContext) return
  overlayStore.openDrawer({
    kind: 'human-confirmation',
    id: detail.value.subject.id,
    mode: 'create',
    payload: {
      subject: detail.value.subject,
      title: detail.value.subjectContext.title,
      knowledgeStatus: detail.value.subjectContext.knowledgeStatus,
      subjectDetailKey: detail.value.subjectDetailKey,
    },
  })
}

watch(
  () => props.evidenceId,
  () => {
    detail.value = null
    withdrawing.value = false
    withdrawalReason.value = ''
    void load()
  },
)
onMounted(() => void load())
onUnmounted(() => {
  loadSequence++
})
</script>

<template>
  <div class="evidence-drawer">
    <el-dialog
      v-model="withdrawing"
      title="撤销确认"
      width="min(560px, calc(100vw - 32px))"
      append-to-body
      :close-on-click-modal="false"
      :close-on-press-escape="!saving"
      :show-close="!saving"
    >
      <p>原确认内容将保留为历史记录，撤销后不再计入当前支持依据。知识状态不会自动改变。</p>
      <el-form label-position="top" @submit.prevent="withdraw"
        ><el-form-item label="撤销原因" required :error="withdrawalError ?? undefined"
          ><el-input
            v-model="withdrawalReason"
            type="textarea"
            :rows="4"
            maxlength="1000"
            show-word-limit /></el-form-item
      ></el-form>
      <template #footer
        ><el-button :disabled="saving" @click="withdrawing = false">取消</el-button
        ><el-button
          v-if="conflict"
          @click="
            () => {
              load()
              withdrawing = false
            }
          "
          >重新加载</el-button
        ><el-button type="danger" :loading="saving" :disabled="conflict" @click="withdraw"
          >确认撤销</el-button
        ></template
      >
    </el-dialog>
    <LoadingState v-if="loading && !detail" message="正在读取证据详情…" />
    <ErrorState
      v-else-if="errorMessage && !detail"
      title="证据详情加载失败"
      :message="errorMessage"
      @retry="load"
    />
    <template v-else-if="detail">
      <header class="evidence-drawer__header skh-drawer-header">
        <el-button
          text
          circle
          :icon="Close"
          aria-label="关闭证据详情"
          @click="overlayStore.requestDrawerClose()"
        />
        <span>类型：{{ evidenceTypeLabels[detail.evidenceType] }}</span>
        <h2>
          {{
            detail.evidenceType === 'HumanConfirmation'
              ? `人工确认 · ${detail.provider.displayName}`
              : detail.sourceTitle
          }}
        </h2>
        <p>证据详情</p>
      </header>

      <section class="evidence-subject-card">
        <div>
          <small>支持对象</small
          ><HistoricalTargetLabel
            v-if="detail.subjectIdentity"
            :identity="detail.subjectIdentity"
          /><strong v-else class="technical-text">{{
            detail.subjectContext?.title ?? `${detail.subject.type} #${detail.subject.id}`
          }}</strong
          ><em v-if="detail.subjectDetailKey" class="technical-text">{{
            detail.subjectDetailKey
          }}</em>
        </div>
        <KnowledgeStatusBadge
          v-if="detail.subjectContext"
          :status="detail.subjectContext.knowledgeStatus"
        />
      </section>

      <section v-if="lifecycle" class="evidence-detail-section">
        <h3>{{ lifecycle.status === 'Withdrawn' ? '已撤销' : '有效人工确认' }}</h3>
        <dl v-if="lifecycle.status === 'Withdrawn'" class="evidence-facts">
          <div>
            <dt>撤销时间</dt>
            <dd>{{ lifecycle.withdrawnAt ? formatDateTime(lifecycle.withdrawnAt) : '—' }}</dd>
          </div>
          <div>
            <dt>撤销人</dt>
            <dd>{{ lifecycle.withdrawnByDisplayName }}</dd>
          </div>
          <div>
            <dt>撤销原因</dt>
            <dd class="withdrawal-reason">{{ lifecycle.withdrawalReason }}</dd>
          </div>
        </dl>
        <p v-if="lifecycle.replacesHumanConfirmationId">
          替代记录：<el-button
            link
            @click="
              overlayStore.openDrawer({
                kind: 'evidence',
                id: lifecycle.replacesHumanConfirmationId,
                mode: 'read',
              })
            "
            >查看原确认</el-button
          >
        </p>
        <p v-if="lifecycle.replacedByHumanConfirmationId">
          已有替代确认：<el-button
            link
            @click="
              overlayStore.openDrawer({
                kind: 'evidence',
                id: lifecycle.replacedByHumanConfirmationId,
                mode: 'read',
              })
            "
            >查看新确认</el-button
          >
        </p>
        <el-button
          v-if="canWithdraw"
          type="danger"
          plain
          @click="
            () => {
              withdrawing = true
              withdrawalError = null
            }
          "
          >撤销确认</el-button
        >
        <el-button
          v-if="
            actorStore.canEdit &&
            lifecycle.status === 'Withdrawn' &&
            !subjectDeleted &&
            !lifecycle.replacedByHumanConfirmationId
          "
          @click="reconfirm"
          >重新确认</el-button
        >
      </section>
      <template v-if="!editing">
        <section class="evidence-detail-section">
          <div class="evidence-detail-section__heading">
            <h3>来源</h3>
            <el-button
              v-if="
                detail.evidenceType !== 'HumanConfirmation' &&
                !subjectDeleted &&
                detail.availableActions.includes('UpdateEvidence')
              "
              text
              type="primary"
              :icon="EditPen"
              @click="beginEdit"
              >纠正记录</el-button
            >
          </div>
          <dl class="evidence-facts">
            <div>
              <dt>来源标题</dt>
              <dd>{{ detail.sourceTitle }}</dd>
            </div>
            <div>
              <dt>来源引用</dt>
              <dd class="technical-text">{{ detail.sourceReference ?? '—' }}</dd>
            </div>
            <div v-if="detail.confidence">
              <dt>可信度</dt>
              <dd>{{ confidenceLabels[detail.confidence] }}</dd>
            </div>
          </dl>
          <dl v-if="codeLocatorRows.length" class="evidence-facts evidence-locator-facts">
            <div v-for="row in codeLocatorRows" :key="row[0]">
              <dt>{{ row[0] }}</dt>
              <dd class="technical-text">{{ row[1] }}</dd>
            </div>
          </dl>
          <pre v-else-if="sourceLocatorText" class="evidence-locator">{{ sourceLocatorText }}</pre>
        </section>

        <section class="evidence-detail-section evidence-detail-section--priority">
          <h3>{{ detail.evidenceType === 'HumanConfirmation' ? '人工确认内容' : '证据说明' }}</h3>
          <dl class="evidence-facts">
            <div v-if="detail.evidenceType === 'HumanConfirmation'">
              <dt>确认结论</dt>
              <dd>{{ detail.summary ?? '—' }}</dd>
            </div>
            <div v-else>
              <dt>摘要</dt>
              <dd>{{ detail.summary ?? '—' }}</dd>
            </div>
            <div>
              <dt>支持理由</dt>
              <dd>{{ detail.supportReason }}</dd>
            </div>
          </dl>
        </section>

        <section class="evidence-detail-section">
          <h3>{{ detail.evidenceType === 'HumanConfirmation' ? '确认信息' : '证据提供人' }}</h3>
          <dl class="evidence-facts">
            <div>
              <dt>{{ detail.evidenceType === 'HumanConfirmation' ? '确认人' : '姓名' }}</dt>
              <dd>{{ detail.provider.displayName }}</dd>
            </div>
            <div>
              <dt>
                {{ detail.evidenceType === 'HumanConfirmation' ? '知识身份' : '角色 / 身份' }}
              </dt>
              <dd>{{ detail.provider.roleOrIdentity }}</dd>
            </div>
            <div>
              <dt>团队</dt>
              <dd>{{ detail.provider.team ?? '—' }}</dd>
            </div>
            <div>
              <dt>{{ detail.evidenceType === 'HumanConfirmation' ? '确认时间' : '提供时间' }}</dt>
              <dd class="technical-text">{{ formatDateTime(detail.provider.occurredAt) }}</dd>
            </div>
            <div v-if="detail.evidenceType === 'HumanConfirmation'">
              <dt>确认方式</dt>
              <dd>{{ confirmationMethod ?? '—' }}</dd>
            </div>
            <div v-else>
              <dt>快照来源</dt>
              <dd>{{ detail.provider.source ?? '—' }}</dd>
            </div>
            <div
              v-if="
                detail.evidenceType === 'HumanConfirmation' &&
                detail.knowledgeDocumentRevisionNumberSnapshot !== null
              "
            >
              <dt>确认修订</dt>
              <dd>修订 {{ detail.knowledgeDocumentRevisionNumberSnapshot }}</dd>
            </div>
          </dl>
        </section>

        <section v-if="detail.subjectContext" class="evidence-knowledge-impact">
          <div><small>知识影响</small><strong>当前状态不会因证据自动变化</strong></div>
          <KnowledgeStatusBadge :status="detail.subjectContext.knowledgeStatus" />
        </section>
      </template>

      <el-form v-else class="evidence-form evidence-edit-form" label-position="top">
        <el-alert
          v-if="errorMessage"
          class="evidence-form-alert"
          type="error"
          :title="errorMessage"
          :closable="false"
          show-icon
        />
        <section class="evidence-form-section">
          <h3>纠正来源与说明</h3>
          <el-form-item label="来源标题" required
            ><el-input v-model="form.sourceTitle"
          /></el-form-item>
          <el-form-item label="来源引用"
            ><el-input v-model="form.sourceReference" class="technical-input"
          /></el-form-item>
          <el-form-item label="来源定位（JSON 对象）"
            ><el-input v-model="form.locatorJson" type="textarea" :rows="4" class="technical-input"
          /></el-form-item>
          <el-form-item label="证据摘要"
            ><el-input v-model="form.summary" type="textarea" :rows="2"
          /></el-form-item>
          <el-form-item label="为什么支持当前知识" required
            ><el-input v-model="form.supportReason" type="textarea" :rows="3"
          /></el-form-item>
          <el-form-item label="可信度"
            ><el-select v-model="form.confidence" clearable
              ><el-option label="高" value="High" /><el-option
                label="中"
                value="Medium" /><el-option label="低" value="Low" /></el-select
          ></el-form-item>
        </section>
        <section class="evidence-form-section">
          <h3>证据提供人快照</h3>
          <div class="evidence-person-grid">
            <el-form-item label="姓名" required
              ><el-input v-model="form.providerName"
            /></el-form-item>
            <el-form-item label="角色 / 身份" required
              ><el-input v-model="form.providerRole"
            /></el-form-item>
            <el-form-item label="团队"><el-input v-model="form.team" /></el-form-item>
            <el-form-item label="外部用户标识"
              ><el-input v-model="form.providerExternalKey" class="technical-input"
            /></el-form-item>
            <el-form-item label="快照来源"><el-input v-model="form.providerSource" /></el-form-item>
            <el-form-item label="提供时间（UTC）"
              ><el-input v-model="form.occurredAt" class="technical-input"
            /></el-form-item>
            <el-form-item label="备注"><el-input v-model="form.providerNote" /></el-form-item>
          </div>
        </section>
      </el-form>

      <div v-if="errorMessage && !editing" class="evidence-drawer__error">
        <span>{{ errorMessage }}</span
        ><el-button v-if="conflict" text type="primary" :icon="Refresh" @click="load"
          >重新加载</el-button
        >
      </div>
      <footer class="evidence-drawer__footer">
        <template v-if="editing"
          ><el-button @click="editing = false">取消</el-button
          ><el-button type="primary" :loading="saving" @click="save">保存纠正</el-button></template
        >
        <template v-else
          ><el-button @click="overlayStore.requestDrawerClose()">关闭</el-button
          ><el-button
            v-if="!subjectDeleted && detail.evidenceType !== 'HumanConfirmation'"
            class="skh-section-action skh-human-confirmation-action"
            plain
            :icon="UserFilled"
            @click="openHumanConfirmation"
            >添加人工确认</el-button
          ></template
        >
      </footer>
    </template>
  </div>
</template>

<style src="../evidence.css"></style>

<style scoped>
.withdrawal-reason {
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}
</style>
