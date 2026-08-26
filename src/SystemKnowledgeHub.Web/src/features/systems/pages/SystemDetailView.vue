<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { EditPen } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { useRoute, useRouter } from 'vue-router'
import { parseSafeApiId } from '../../../api/contracts/id'
import { useActorStore } from '../../../app/stores/actor'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import { systemLifecycleLabels, type SystemLifecycle } from '../api/systemsContracts'
import SystemContextRail from '../components/SystemContextRail.vue'
import SystemOverviewSection from '../components/SystemOverviewSection.vue'
import SystemTechnologyLifecycleSection from '../components/SystemTechnologyLifecycleSection.vue'
import SystemUnifiedKnowledgeView from '../components/SystemUnifiedKnowledgeView.vue'
import { useSystemDetail, type SystemOverviewValues } from '../composables/useSystemDetail'
import { useSystemKnowledgeView } from '../composables/useSystemKnowledgeView'

const route = useRoute()
const router = useRouter()
const actorStore = useActorStore()
const overviewEditing = ref(false)
const {
  detail,
  loading,
  pageError,
  saving,
  saveError,
  concurrencyConflict,
  load,
  saveOverview,
  saveTechnology,
  saveLifecycle,
  clearSaveError,
} = useSystemDetail()
const { view: knowledgeView, loading: knowledgeViewLoading, error: knowledgeViewError, load: loadKnowledgeView } = useSystemKnowledgeView()

const systemId = computed(() => parseSafeApiId(route.params.id))
const canEditOverview = computed(() =>
  actorStore.canEdit && detail.value?.availableActions.includes('UpdateSystemOverview') === true,
)
const canEditTechnology = computed(() =>
  actorStore.canEdit && detail.value?.availableActions.includes('UpdateSystemTechnology') === true,
)
const canEditLifecycle = computed(() =>
  actorStore.canEdit && detail.value?.availableActions.includes('UpdateSystemLifecycle') === true,
)
const knowledgeTotal = computed(() => {
  if (!detail.value) return 0
  const summary = detail.value.knowledgeSummary
  return summary.confirmed + summary.inferred + summary.unknown
})
const knowledgePercent = computed(() => {
  if (!detail.value || knowledgeTotal.value === 0) {
    return { confirmed: 0, inferred: 0, unknown: 0 }
  }
  const summary = detail.value.knowledgeSummary
  return {
    confirmed: Math.round(summary.confirmed / knowledgeTotal.value * 100),
    inferred: Math.round(summary.inferred / knowledgeTotal.value * 100),
    unknown: Math.round(summary.unknown / knowledgeTotal.value * 100),
  }
})

async function loadRoute(): Promise<void> {
  if (systemId.value === null) return
  overviewEditing.value = false
  clearSaveError()
  await Promise.all([load(systemId.value), loadKnowledgeView(systemId.value)])
}

async function handleSave(values: SystemOverviewValues): Promise<void> {
  const saved = await saveOverview(values, actorStore.actor)
  if (saved) ElMessage.success('系统概览已保存。')
}

async function handleSaveTechnology(technologies: string[]): Promise<void> {
  const saved = await saveTechnology(technologies, actorStore.actor)
  if (saved) ElMessage.success('系统技术已保存。')
}

async function handleSaveLifecycle(lifecycle: SystemLifecycle): Promise<void> {
  const saved = await saveLifecycle(lifecycle, actorStore.actor)
  if (saved) ElMessage.success('系统生命周期已保存。')
}

async function reloadAfterConflict(): Promise<void> {
  overviewEditing.value = false
  await loadRoute()
}

function openDatabaseObject(id: number): void {
  void router.push({ name: 'database-object-detail', params: { id: String(id) } })
}

function openBusinessFunction(id: number): void {
  void router.push({ name: 'business-function-detail', params: { id: String(id) } })
}

function openBusinessRule(id: number): void { void router.push({ name: 'business-rule-detail', params: { id: String(id) } }) }
function openIntegration(id: number): void { void router.push({ name: 'integration-detail', params: { id: String(id) } }) }
function openDocument(id: number): void { void router.push({ name: 'knowledge-document-detail', params: { id: String(id) } }) }
function openUnknownItem(id: number): void { void router.push({ name: 'unknown-item-detail', params: { id: String(id) } }) }

watch(() => route.params.id, () => void loadRoute())
watch(overviewEditing, (editing) => {
  if (editing) clearSaveError()
})
onMounted(() => void loadRoute())
</script>

<template>
  <div class="system-detail-page">
    <ErrorState
      v-if="systemId === null"
      title="系统地址无效"
      message="请从系统列表重新进入。"
    />
    <LoadingState v-else-if="loading && !detail" message="正在读取系统详情…" />
    <ErrorState
      v-else-if="pageError && !detail"
      title="系统详情加载失败"
      :message="pageError"
      @retry="loadRoute"
    />
    <template v-else-if="detail">
      <header class="system-detail-header">
        <nav aria-label="面包屑"><button @click="router.push('/systems')">系统</button><b>/</b><span>{{ detail.overview.name }}</span></nav>
        <div class="system-detail-header__title">
          <div>
            <h1 class="technical-text">{{ detail.overview.name }}</h1>
            <p>{{ detail.overview.displayName }}</p>
          </div>
          <el-button
            v-if="canEditOverview && !overviewEditing"
            text
            type="primary"
            :icon="EditPen"
            @click="overviewEditing = true"
          >编辑概览</el-button>
          <span v-else-if="overviewEditing" class="system-detail-header__editing">正在编辑概览</span>
        </div>
        <div class="system-detail-header__tags">
          <span>{{ detail.overview.systemType }}</span>
          <span class="lifecycle-tag">{{ systemLifecycleLabels[detail.overview.lifecycle] }}</span>
          <span v-if="detail.overview.technologies.length" class="technical-text">
            {{ detail.overview.technologies.slice(0, 2).join(' / ') }}
          </span>
          <label>知识状态：<KnowledgeStatusBadge :status="detail.overview.knowledgeStatus" /></label>
        </div>
      </header>

      <div v-if="pageError && detail" class="system-page-inline-error">刷新失败：{{ pageError }}</div>

      <SystemOverviewSection
        v-model:editing="overviewEditing"
        :overview="detail.overview"
        :main-database-name="detail.contextRail.mainDatabase?.name ?? null"
        :can-edit="canEditOverview"
        :saving="saving"
        :save-error="saveError"
        :concurrency-conflict="concurrencyConflict"
        @save="handleSave"
        @reload="reloadAfterConflict"
      />

      <SystemTechnologyLifecycleSection
        :overview="detail.overview"
        :can-edit-technology="canEditTechnology"
        :can-edit-lifecycle="canEditLifecycle"
        :saving="saving"
        :save-error="saveError"
        :concurrency-conflict="concurrencyConflict"
        @save-technology="handleSaveTechnology"
        @save-lifecycle="handleSaveLifecycle"
        @reload="reloadAfterConflict"
      />

      <section class="system-knowledge-summary">
        <div class="system-section-heading">
          <h2>结构化知识状态概况</h2>
          <span>{{ knowledgeTotal }} 个统计对象</span>
        </div>
        <p class="system-knowledge-summary__scope">统计系统本身、业务功能、数据库对象、字段与集成的知识状态；不包含关联知识文档或业务规则。</p>
        <div v-if="knowledgeTotal" class="knowledge-summary-bar" aria-label="知识状态分布">
          <span class="knowledge-summary-bar__confirmed" :style="{ width: `${knowledgePercent.confirmed}%` }"></span>
          <span class="knowledge-summary-bar__inferred" :style="{ width: `${knowledgePercent.inferred}%` }"></span>
          <span class="knowledge-summary-bar__unknown" :style="{ width: `${knowledgePercent.unknown}%` }"></span>
        </div>
        <div v-else class="knowledge-summary-bar knowledge-summary-bar--empty"></div>
        <div class="knowledge-summary-legend">
          <span><i class="confirmed"></i>已确认 <strong>{{ knowledgePercent.confirmed }}%</strong>（{{ detail.knowledgeSummary.confirmed }}）</span>
          <span><i class="inferred"></i>推断 <strong>{{ knowledgePercent.inferred }}%</strong>（{{ detail.knowledgeSummary.inferred }}）</span>
          <span><i class="unknown"></i>未知 <strong>{{ knowledgePercent.unknown }}%</strong>（{{ detail.knowledgeSummary.unknown }}）</span>
          <span>开放待确认事项 <strong>{{ detail.knowledgeSummary.openUnknownItems }}</strong> 项</span>
        </div>
      </section>

      <SystemUnifiedKnowledgeView
        :view="knowledgeView"
        :loading="knowledgeViewLoading"
        :error="knowledgeViewError"
        @open-business-function="openBusinessFunction"
        @open-database-object="openDatabaseObject"
        @open-business-rule="openBusinessRule"
        @open-integration="openIntegration"
        @open-document="openDocument"
        @open-unknown-item="openUnknownItem"
      />

      <Teleport defer to="#context-rail-content">
        <SystemContextRail :system-name="detail.overview.name" :context="detail.contextRail" />
      </Teleport>
    </template>
  </div>
</template>

<style src="../systems.css"></style>
