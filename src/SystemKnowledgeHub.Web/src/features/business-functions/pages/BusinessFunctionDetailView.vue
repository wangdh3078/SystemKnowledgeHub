<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { ArrowRight, Connection, Delete, Document, DocumentChecked, EditPen, Link, Plus, QuestionFilled } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { useRoute, useRouter } from 'vue-router'
import { parseSafeApiId } from '../../../api/contracts/id'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import KnowledgeStatusProgressionPanel from '../../knowledge-status/components/KnowledgeStatusProgressionPanel.vue'
import { relationTypeLabels, relationTypes } from '../../relationships/api/relationshipContracts'
import {
  functionTypeLabels,
  rewriteStatusLabels,
  type BusinessFunctionDetailResponse,
} from '../api/businessFunctionContracts'
import BusinessFunctionContextRail from '../components/BusinessFunctionContextRail.vue'
import BusinessFunctionOverviewSection from '../components/BusinessFunctionOverviewSection.vue'
import BusinessProcessSection from '../components/BusinessProcessSection.vue'
import {
  useBusinessFunctionDetail,
  type BusinessFunctionOverviewValues,
} from '../composables/useBusinessFunctionDetail'
import { deleteBusinessFunction } from '../api/businessFunctionsApi'
import { openDeleteDialog } from '../../soft-delete/deleteDialog'

const route = useRoute()
const router = useRouter()
const actorStore = useActorStore()
const overlayStore = useOverlayStore()
const overviewEditing = ref(false)
const processEditing = ref(false)

function getRelationTypeLabel(value: unknown): string {
  const relationType = relationTypes.find((item) => item === value)
  return relationType ? relationTypeLabels[relationType] : '未知关系'
}
const {
  detail,
  loading,
  error,
  overviewSaving,
  overviewSaveError,
  overviewConflict,
  processSaving,
  processSaveError,
  processConflict,
  load,
  saveOverview,
  saveProcess,
  clearOverviewError,
  clearProcessError,
} = useBusinessFunctionDetail()
const functionId = computed(() => parseSafeApiId(route.params.id))
const canEditOverview = computed(() =>
  actorStore.canEdit && detail.value?.availableActions.includes('UpdateBusinessFunctionOverview') === true,
)
const canEditProcess = computed(() =>
  actorStore.canEdit && detail.value?.availableActions.includes('ReplaceBusinessProcessSteps') === true,
)
const canAddEvidence = computed(() => actorStore.canEdit && detail.value?.availableActions.includes('AddEvidence') === true)
const canAddRelationship = computed(() => actorStore.canEdit && detail.value?.availableActions.includes('AddKnowledgeRelation') === true)
const canChangeKnowledgeStatus = computed(() => actorStore.canEdit && detail.value?.availableActions.includes('ChangeKnowledgeStatus') === true)
const humanConfirmationCount = computed(() =>
  detail.value?.evidence.filter((item) => item.evidenceType === 'HumanConfirmation').length ?? 0,
)

async function loadRoute(): Promise<void> {
  if (functionId.value === null) return
  overviewEditing.value = false
  processEditing.value = false
  clearOverviewError()
  clearProcessError()
  await load(functionId.value)
}

async function handleOverviewSave(values: BusinessFunctionOverviewValues): Promise<void> {
  const saved = await saveOverview(values, actorStore.actor)
  if (saved) ElMessage.success('业务功能概览已保存。')
}

async function handleProcessSave(
  steps: BusinessFunctionDetailResponse['businessProcess'],
): Promise<void> {
  const saved = await saveProcess(steps, actorStore.actor)
  if (saved) ElMessage.success('业务流程已保存。')
}

function startOverviewEdit(): void {
  processEditing.value = false
  clearOverviewError()
}

function startProcessEdit(): void {
  overviewEditing.value = false
  clearProcessError()
}

async function reloadAfterConflict(): Promise<void> {
  await loadRoute()
}

function handleRelatedDataRow(row: BusinessFunctionDetailResponse['relatedData'][number]): void {
  overlayStore.openDrawer({ kind: 'relationship', id: row.relationshipId, mode: 'read' })
}

function handleBusinessRuleRow(row: BusinessFunctionDetailResponse['businessRules'][number]): void {
  overlayStore.openDrawer({ kind: 'business-rule', id: row.id, mode: 'read' })
}

function handleIntegrationRow(row: BusinessFunctionDetailResponse['integrations'][number]): void {
  overlayStore.openDrawer({ kind: 'integration', id: row.id, mode: 'read' })
}

function openAddRelationship(): void {
  if (!detail.value) return
  overlayStore.openDrawer({
    kind: 'add-relationship',
    id: detail.value.id,
    mode: 'create',
    payload: {
      source: { type: 'BusinessFunction', id: detail.value.id },
      title: detail.value.header.name,
      systemId: detail.value.system.id,
      systemName: detail.value.system.name,
    },
  })
}

function openAddEvidence(): void {
  if (!detail.value) return
  overlayStore.openDrawer({
    kind: 'add-evidence',
    id: detail.value.id,
    mode: 'create',
    payload: {
      subject: { type: 'BusinessFunction', id: detail.value.id },
      title: `${detail.value.system.name} · ${detail.value.header.name}`,
      knowledgeStatus: detail.value.header.knowledgeStatus,
      subjectDetailKey: null,
    },
  })
}

function openEvidence(id: number): void {
  overlayStore.openDrawer({ kind: 'evidence', id, mode: 'read' })
}

function createUnknownItem(): void {
  if (!detail.value) return
  overlayStore.openDialog({
    kind: 'create-unknown-item', id: null, mode: 'create',
    payload: {
      systemId: detail.value.system.id,
      systemName: detail.value.system.name,
      target: { type: 'BusinessFunction', id: detail.value.id },
      title: detail.value.header.name,
    },
  })
}

function requestDelete(): void {
  if (!detail.value?.canDelete) return
  const current = detail.value
  openDeleteDialog(overlayStore, {
    objectTypeLabel: '业务功能', actionLabel: '删除业务功能', displayName: current.header.name,
    concurrencyToken: current.concurrencyToken,
    execute: () => deleteBusinessFunction(current.id, current.concurrencyToken),
    onDeleted: () => router.push({ name: 'business-functions-list' }),
    onRefresh: loadRoute,
    onUnavailable: () => router.push({ name: 'business-functions-list' }),
  })
}

function reloadEvidence(): void {
  void loadRoute()
}

function reloadRelationships(): void {
  void loadRoute()
}

watch(() => route.params.id, () => void loadRoute())
onMounted(() => {
  void loadRoute()
  window.addEventListener('evidence:changed', reloadEvidence)
  window.addEventListener('relationship:changed', reloadRelationships)
  window.addEventListener('knowledge-status:changed', reloadRelationships)
})
onUnmounted(() => {
  window.removeEventListener('evidence:changed', reloadEvidence)
  window.removeEventListener('relationship:changed', reloadRelationships)
  window.removeEventListener('knowledge-status:changed', reloadRelationships)
})
</script>

<template>
  <div class="business-function-detail-page">
    <ErrorState v-if="functionId === null" title="业务功能地址无效" message="请从业务功能列表重新进入。" />
    <LoadingState v-else-if="loading && !detail" message="正在读取业务功能详情…" />
    <ErrorState v-else-if="error && !detail" title="业务功能详情加载失败" :message="error" @retry="loadRoute" />
    <template v-else-if="detail">
      <header class="business-function-detail-header">
        <nav aria-label="面包屑">
          <button @click="router.push({ name: 'business-functions-list' })">业务功能</button><b>/</b>
          <button @click="router.push({ name: 'system-detail', params: { id: String(detail.system.id) } })">{{ detail.system.name }}</button><b>/</b>
          <span>{{ detail.header.name }}</span>
        </nav>
        <h1 class="technical-text">{{ detail.header.name }}</h1>
        <p>{{ detail.overview.purpose ?? '尚未记录功能用途' }}</p>
        <div class="business-function-detail-header__actions">
          <el-button v-if="detail.canDelete && !overviewEditing" type="danger" plain :icon="Delete" @click="requestDelete">删除业务功能</el-button>
          <el-button
            v-if="canEditOverview && !overviewEditing"
            text
            type="primary"
            :icon="EditPen"
            @click="overviewEditing = true; startOverviewEdit()"
          >编辑概览</el-button>
          <span v-else-if="overviewEditing" class="business-function-detail-header__editing">正在编辑概览</span>
        </div>
        <div class="business-function-detail-header__tags">
          <span>{{ functionTypeLabels[detail.header.functionType] ?? detail.header.functionType }}</span>
          <strong class="technical-text">{{ detail.system.name }}</strong>
          <KnowledgeStatusBadge :status="detail.header.knowledgeStatus" />
          <span class="rewrite-status" :class="`rewrite-status--${detail.header.rewriteStatus.toLowerCase()}`">{{ rewriteStatusLabels[detail.header.rewriteStatus] }}</span>
        </div>
      </header>

      <div v-if="error && detail" class="business-functions-inline-error">刷新失败：{{ error }}</div>

      <KnowledgeStatusProgressionPanel
        :id="detail.id"
        :title="`${detail.system.name} · ${detail.header.name}`"
        :status="detail.header.knowledgeStatus"
        :concurrency-token="detail.concurrencyToken"
        :evidence-count="detail.evidence.length"
        :human-confirmation-count="humanConfirmationCount"
        :can-change="canChangeKnowledgeStatus"
      />

      <BusinessFunctionOverviewSection
        v-model:editing="overviewEditing"
        :detail="detail"
        :can-edit="canEditOverview"
        :saving="overviewSaving"
        :save-error="overviewSaveError"
        :concurrency-conflict="overviewConflict"
        @start-edit="startOverviewEdit"
        @save="handleOverviewSave"
        @reload="reloadAfterConflict"
      />

      <BusinessProcessSection
        v-model:editing="processEditing"
        :steps="detail.businessProcess"
        :can-edit="canEditProcess"
        :saving="processSaving"
        :save-error="processSaveError"
        :concurrency-conflict="processConflict"
        @start-edit="startProcessEdit"
        @save="handleProcessSave"
        @reload="reloadAfterConflict"
      />

      <section class="business-function-section">
        <div class="business-function-section__heading"><h2>关联数据</h2><div><span>{{ detail.relatedData.length }} 项</span><el-button v-if="canAddRelationship" text type="primary" :icon="Plus" @click="openAddRelationship">添加关系</el-button></div></div>
        <el-table v-if="detail.relatedData.length" :data="detail.relatedData" class="business-function-compact-table" @row-click="handleRelatedDataRow">
          <el-table-column prop="name" label="数据对象" min-width="220"><template #default="scope"><strong class="technical-text">{{ scope.row.name }}</strong></template></el-table-column>
          <el-table-column label="关系类型" width="120">
            <template #default="scope">{{ getRelationTypeLabel(scope.row.relationType) }}</template>
          </el-table-column>
          <el-table-column prop="evidenceCount" label="证据" width="80" align="center" />
          <el-table-column width="34"><template #default><el-icon><ArrowRight /></el-icon></template></el-table-column>
        </el-table>
        <div v-else class="business-section-empty"><el-icon><Connection /></el-icon><span><strong>暂无已登记的关联数据</strong><small>关系必须作为显式知识记录；当前不使用流程文本推断关系。</small></span></div>
      </section>

      <section class="business-function-section">
        <div class="business-function-section__heading"><h2>业务规则</h2><span>{{ detail.businessRules.length }} 项</span></div>
        <el-table v-if="detail.businessRules.length" :data="detail.businessRules" class="business-function-compact-table" @row-click="handleBusinessRuleRow">
          <el-table-column prop="name" label="规则" min-width="220" />
          <el-table-column prop="knowledgeStatus" label="知识状态" width="100"><template #default="scope"><KnowledgeStatusBadge :status="scope.row.knowledgeStatus" /></template></el-table-column>
          <el-table-column prop="evidenceCount" label="证据" width="80" align="center" />
          <el-table-column width="34"><template #default><el-icon><ArrowRight /></el-icon></template></el-table-column>
        </el-table>
        <div v-else class="business-section-empty"><span>尚未记录业务规则。</span></div>
      </section>

      <section class="business-function-section business-function-section--two-columns">
        <div>
          <div class="business-function-section__heading"><h2>集成关系</h2><span>{{ detail.integrations.length }} 项</span></div>
          <div v-if="detail.integrations.length" class="business-function-evidence-list">
            <button v-for="item in detail.integrations" :key="item.relationshipId" @click="handleIntegrationRow(item)"><el-icon><Link /></el-icon><span><small>{{ getRelationTypeLabel(item.relationType) }}</small><strong class="technical-text">{{ item.name }}</strong></span><el-icon><ArrowRight /></el-icon></button>
          </div>
          <div v-else class="business-section-empty business-section-empty--compact"><el-icon><Link /></el-icon><span>尚未记录 MQ、API 或其他系统集成。</span></div>
        </div>
        <div>
          <div class="business-function-section__heading business-function-evidence-heading"><h2>证据</h2><div><span>{{ detail.evidence.length }} 条</span><el-button v-if="canAddEvidence" class="skh-section-action skh-evidence-action" type="primary" :icon="DocumentChecked" @click="openAddEvidence">添加证据</el-button></div></div>
          <div v-if="detail.evidence.length" class="business-function-evidence-list">
            <button v-for="item in detail.evidence" :key="item.id" @click="openEvidence(item.id)"><el-icon><Document /></el-icon><span><small>{{ item.evidenceType }}</small><strong>{{ item.sourceTitle }}</strong></span><el-icon><ArrowRight /></el-icon></button>
          </div>
          <div v-else class="business-section-empty business-section-empty--compact"><el-icon><Document /></el-icon><span>尚未添加支持该功能知识的证据。</span></div>
        </div>
      </section>

      <section class="business-function-section business-function-section--last">
<div class="business-function-section__heading"><h2>待确认事项</h2><div><span>{{ detail.unknownItems.length }} 项</span><el-button v-if="actorStore.canEdit" text type="primary" :icon="Plus" @click="createUnknownItem">创建待确认事项</el-button></div></div>
        <div v-if="detail.unknownItems.length" class="business-function-evidence-list">
          <button v-for="item in detail.unknownItems" :key="item.id" @click="router.push({ name: 'unknown-item-detail', params: { id: String(item.id) } })"><el-icon><QuestionFilled /></el-icon><span><small>{{ item.status }}</small><strong>{{ item.question }}</strong></span><el-icon><ArrowRight /></el-icon></button>
        </div>
        <div v-else class="business-section-empty business-section-empty--compact"><el-icon><QuestionFilled /></el-icon><span>当前没有功能级待确认事项。</span></div>
      </section>

      <Teleport defer to="#context-rail-content">
        <BusinessFunctionContextRail :function-name="detail.header.name" :context="detail.contextRail" :related-data-count="detail.relatedData.length" />
      </Teleport>
    </template>
  </div>
</template>

<style src="../business-functions.css"></style>
<style src="../../knowledge-status/knowledge-status.css"></style>
