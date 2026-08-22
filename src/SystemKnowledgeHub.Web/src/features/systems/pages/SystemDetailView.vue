<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { ArrowRight, EditPen, Link, OfficeBuilding } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { useRoute, useRouter } from 'vue-router'
import { parseSafeApiId } from '../../../api/contracts/id'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import {
  systemLifecycleLabels,
  type SystemBusinessFunctionSummary,
  type SystemDatabaseObjectSummary,
  type SystemLifecycle,
} from '../api/systemsContracts'
import SystemContextRail from '../components/SystemContextRail.vue'
import SystemOverviewSection from '../components/SystemOverviewSection.vue'
import SystemTechnologyLifecycleSection from '../components/SystemTechnologyLifecycleSection.vue'
import { useSystemDetail, type SystemOverviewValues } from '../composables/useSystemDetail'

const route = useRoute()
const router = useRouter()
const actorStore = useActorStore()
const overlayStore = useOverlayStore()
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
  await load(systemId.value)
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

function openBusinessFunctionsList(): void {
  if (!detail.value) return
  void router.push({
    name: 'business-functions-list',
    query: { systemId: String(detail.value.id) },
  })
}

function handleBusinessFunctionRowClick(row: SystemBusinessFunctionSummary): void {
  openBusinessFunction(row.id)
}

function handleDatabaseObjectRowClick(row: SystemDatabaseObjectSummary): void {
  openDatabaseObject(row.id)
}

function openIntegration(id: number): void {
  overlayStore.openDrawer({ kind: 'integration', id, mode: 'read' })
}

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
          <h2>知识概况</h2>
          <span>{{ knowledgeTotal }} 条已梳理知识</span>
        </div>
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

      <section class="system-detail-section">
        <div class="system-section-heading">
          <h2>业务功能</h2>
          <span>{{ detail.businessFunctions.length }} 项 · <button type="button" @click="openBusinessFunctionsList">查看全部</button></span>
        </div>
        <EmptyState
          v-if="detail.businessFunctions.length === 0"
          title="暂无业务功能"
          description="当前 Slice 不提前创建业务功能；后续可在系统上下文中渐进补充。"
        />
        <el-table
          v-else
          :data="detail.businessFunctions"
          row-key="id"
          class="system-object-table"
          @row-click="handleBusinessFunctionRowClick"
        >
          <el-table-column prop="name" label="功能名称" min-width="210">
            <template #default="scope"><strong class="technical-text">{{ scope.row.name }}</strong></template>
          </el-table-column>
          <el-table-column prop="purpose" label="用途" min-width="260" show-overflow-tooltip>
            <template #default="scope"><span :class="{ 'text-muted': !scope.row.purpose }">{{ scope.row.purpose ?? '尚未记录' }}</span></template>
          </el-table-column>
          <el-table-column prop="knowledgeStatus" label="知识状态" width="98">
            <template #default="scope"><KnowledgeStatusBadge :status="scope.row.knowledgeStatus" /></template>
          </el-table-column>
          <el-table-column prop="unknownCount" label="待确认事项" width="100" align="center" />
          <el-table-column width="38" align="right"><template #default><el-icon><ArrowRight /></el-icon></template></el-table-column>
        </el-table>
      </section>

      <section class="system-detail-section">
        <div class="system-section-heading"><h2>数据库对象</h2><span>{{ detail.databaseObjects.length }} 项</span></div>
        <EmptyState v-if="detail.databaseObjects.length === 0" title="暂无数据库对象" />
        <el-table
          v-else
          :data="detail.databaseObjects"
          row-key="id"
          class="system-object-table"
          @row-click="handleDatabaseObjectRowClick"
        >
          <el-table-column prop="qualifiedName" label="对象名称" min-width="210">
            <template #default="scope"><strong class="technical-text">{{ scope.row.qualifiedName }}</strong></template>
          </el-table-column>
          <el-table-column prop="objectType" label="类型" width="90">
            <template #default="scope">{{ scope.row.objectType === 'Table' ? '表' : '视图' }}</template>
          </el-table-column>
          <el-table-column label="业务说明" min-width="210">
            <template #default><span class="text-muted">进入对象详情查看</span></template>
          </el-table-column>
          <el-table-column prop="knowledgeStatus" label="知识状态" width="98">
            <template #default="scope"><KnowledgeStatusBadge :status="scope.row.knowledgeStatus" /></template>
          </el-table-column>
          <el-table-column prop="unknownCount" label="待确认事项" width="100" align="center" />
          <el-table-column width="38" align="right"><template #default><el-icon><ArrowRight /></el-icon></template></el-table-column>
        </el-table>
      </section>

      <section class="system-detail-section system-detail-section--split">
        <div>
          <div class="system-section-heading"><h2>集成关系</h2><span>{{ detail.integrations.length }} 项</span></div>
          <div v-if="detail.integrations.length" class="system-integration-list">
            <button v-for="integration in detail.integrations" :key="integration.id" type="button" @click="openIntegration(integration.id)">
              <el-icon><Link /></el-icon><span><strong class="technical-text">{{ integration.name }}</strong><small>{{ integration.integrationType }} · {{ integration.relatedSystem }}</small></span><KnowledgeStatusBadge :status="integration.knowledgeStatus"/><el-icon><ArrowRight /></el-icon>
            </button>
          </div>
          <div v-else class="system-compact-empty"><el-icon><Link /></el-icon><span>暂无集成关系记录</span></div>
        </div>
        <div>
          <div class="system-section-heading"><h2>代码 / 仓库</h2></div>
          <div class="system-repository-summary">
            <el-icon><OfficeBuilding /></el-icon>
            <div><strong class="technical-text">{{ detail.overview.repository.name ?? '尚未记录' }}</strong><small class="technical-text">{{ detail.overview.repository.url ?? '暂无仓库地址' }}</small></div>
          </div>
        </div>
      </section>

      <section class="system-detail-section system-detail-section--last">
        <div class="system-section-heading"><h2>系统级待确认事项</h2><span>{{ detail.unknownItems.length }} 项</span></div>
        <div class="system-compact-empty"><span>暂无系统级待确认事项。</span></div>
      </section>

      <Teleport defer to="#context-rail-content">
        <SystemContextRail :system-name="detail.overview.name" :context="detail.contextRail" />
      </Teleport>
    </template>
  </div>
</template>

<style src="../systems.css"></style>
