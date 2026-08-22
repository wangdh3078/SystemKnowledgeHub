<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ArrowRight, CircleCloseFilled, Clock, WarningFilled } from '@element-plus/icons-vue'
import { useRouter } from 'vue-router'
import { useOverlayStore } from '../../../app/stores/overlays'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import { getDatabaseColumnDetail } from '../../database-knowledge/api/databaseKnowledgeApi'
import type { DashboardNeedsAttention, DashboardObjectType, DashboardRecentActivity } from '../api/dashboardContracts'
import { useDashboard } from '../composables/useDashboard'

const router = useRouter()
const overlays = useOverlayStore()
const { data, loading, error, load } = useDashboard()
const navigationError = ref<string | null>(null)

const overviewItems = computed(() => {
  if (!data.value) return []
  return [
    { key: 'systems', label: '系统', value: data.value.knowledgeOverview.systems },
    { key: 'business-functions', label: '业务功能', value: data.value.knowledgeOverview.businessFunctions },
    { key: 'database-objects', label: '表 / 视图', value: data.value.knowledgeOverview.databaseObjects },
    { key: 'columns', label: '字段', value: data.value.knowledgeOverview.columns },
    { key: 'integrations', label: '集成关系', value: data.value.knowledgeOverview.integrations },
    { key: 'business-rules', label: '业务规则', value: data.value.knowledgeOverview.businessRules },
    { key: 'unknown-items', label: '待确认事项', value: data.value.knowledgeOverview.unknownItems },
  ] as const
})

const progressTotal = computed(() => {
  if (!data.value) return 0
  const progress = data.value.knowledgeProgress
  return progress.confirmed + progress.inferred + progress.unknown
})

const hasKnowledge = computed(() => overviewItems.value.some(item => item.value > 0))

function percentage(value: number): string {
  return progressTotal.value === 0 ? '0.00%' : `${((value / progressTotal.value) * 100).toFixed(2)}%`
}

function formatUpdatedAt(value: string): string {
  return new Intl.DateTimeFormat('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(new Date(value))
}

function objectTypeLabel(value: DashboardObjectType): string {
  return ({
    System: '系统',
    BusinessFunction: '业务功能',
    DatabaseObject: '数据库对象',
    DatabaseColumn: '字段',
    BusinessRule: '业务规则',
    Integration: '集成关系',
    UnknownItem: '待确认事项',
  } as const)[value]
}

function openSearch(): void {
  overlays.openDialog({ kind: 'global-search', id: null, mode: 'read' })
}

function openCreate(): void {
  overlays.openDialog({ kind: 'create-knowledge-object', id: null, mode: 'create' })
}

function openOverview(key: (typeof overviewItems.value)[number]['key']): void {
  if (key === 'systems') {
    void router.push({ name: 'systems-list' })
    return
  }
  if (key === 'business-functions') {
    void router.push({ name: 'business-functions-list' })
    return
  }
  if (key === 'database-objects' || key === 'columns') {
    void router.push({ name: 'database-objects-list' })
    return
  }
  if (key === 'unknown-items') {
    void router.push({ name: 'unknown-items-list' })
    return
  }
  openSearch()
}

function navigateAttention(item: DashboardNeedsAttention): void {
  if (item.kind === 'HighPriorityUnknownItem') {
    void router.push({ name: 'unknown-items-list' })
    return
  }
  if (item.kind === 'DatabaseObjectsWithoutBusinessDescription' || item.kind === 'DatabaseColumnsStillUnknown') {
    void router.push({ name: 'database-objects-list' })
    return
  }
  if (item.kind === 'BusinessFunctionsWithoutRelatedData') {
    void router.push({ name: 'business-functions-list' })
    return
  }
  void router.push({ name: 'systems-list' })
}

async function navigateRecent(item: DashboardRecentActivity): Promise<void> {
  navigationError.value = null
  if (item.objectType === 'System') {
    await router.push({ name: 'system-detail', params: { id: String(item.objectId) } })
    return
  }
  if (item.objectType === 'BusinessFunction') {
    await router.push({ name: 'business-function-detail', params: { id: String(item.objectId) } })
    return
  }
  if (item.objectType === 'DatabaseObject') {
    await router.push({ name: 'database-object-detail', params: { id: String(item.objectId) } })
    return
  }
  if (item.objectType === 'DatabaseColumn') {
    try {
      const column = await getDatabaseColumnDetail(item.objectId)
      await router.push({
        name: 'database-object-detail',
        params: { id: String(column.parent.databaseObjectId) },
        query: { selectedColumnId: String(item.objectId) },
      })
    } catch (requestError: unknown) {
      navigationError.value = requestError instanceof Error
        ? `无法打开字段：${requestError.message}`
        : '无法打开字段详情。'
    }
    return
  }
  if (item.objectType === 'BusinessRule') {
    await router.push({ name: 'business-rule-detail', params: { id: String(item.objectId) } })
    return
  }
  if (item.objectType === 'Integration') {
    await router.push({ name: 'integration-detail', params: { id: String(item.objectId) } })
    return
  }
  await router.push({ name: 'unknown-item-detail', params: { id: String(item.objectId) } })
}

onMounted(() => void load())
</script>

<template>
  <div class="dashboard-page">
    <header class="dashboard-page__header">
      <div>
        <h1>总览</h1>
        <p>了解系统知识的覆盖程度，并找到下一步最值得处理的内容。</p>
      </div>
      <span v-if="data?.scope.systemName" class="dashboard-page__scope">系统范围：{{ data.scope.systemName }}</span>
    </header>

    <LoadingState v-if="loading && !data" message="正在读取知识总览…" />
    <ErrorState v-else-if="error && !data" title="总览加载失败" :message="error" @retry="load" />

    <template v-else-if="data">
      <div v-if="!hasKnowledge" class="dashboard-page__empty">
        <EmptyState
          title="尚未登记知识"
          description="先记录一个系统，随后可以逐步补充业务功能、数据库对象、证据和待确认事项。"
        />
        <el-button type="primary" @click="openCreate">新增知识对象</el-button>
      </div>

      <template v-else>
        <section class="dashboard-section dashboard-section--overview" aria-labelledby="dashboard-overview-title">
          <h2 id="dashboard-overview-title">知识总览</h2>
          <div class="dashboard-overview-grid">
            <button
              v-for="item in overviewItems"
              :key="item.key"
              class="dashboard-overview-item"
              type="button"
              :title="item.key === 'integrations' || item.key === 'business-rules' ? '通过全局搜索继续浏览' : `浏览${item.label}`"
              @click="openOverview(item.key)"
            >
              <span>{{ item.label }}</span>
              <strong>{{ item.value.toLocaleString('zh-CN') }}</strong>
            </button>
          </div>
        </section>

        <section class="dashboard-section dashboard-progress" aria-labelledby="dashboard-progress-title">
          <h2 id="dashboard-progress-title">知识进展</h2>
          <div class="dashboard-progress__bar" aria-label="知识状态进展">
            <span class="dashboard-progress__segment dashboard-progress__segment--confirmed" :style="{ width: percentage(data.knowledgeProgress.confirmed) }"></span>
            <span class="dashboard-progress__segment dashboard-progress__segment--inferred" :style="{ width: percentage(data.knowledgeProgress.inferred) }"></span>
            <span class="dashboard-progress__segment dashboard-progress__segment--unknown" :style="{ width: percentage(data.knowledgeProgress.unknown) }"></span>
          </div>
          <div class="dashboard-progress__legend">
            <span><i class="dashboard-progress__dot dashboard-progress__dot--confirmed"></i>已确认 {{ percentage(data.knowledgeProgress.confirmed) }}（{{ data.knowledgeProgress.confirmed }}）</span>
            <span><i class="dashboard-progress__dot dashboard-progress__dot--inferred"></i>推断 {{ percentage(data.knowledgeProgress.inferred) }}（{{ data.knowledgeProgress.inferred }}）</span>
            <span><i class="dashboard-progress__dot dashboard-progress__dot--unknown"></i>未知 {{ percentage(data.knowledgeProgress.unknown) }}（{{ data.knowledgeProgress.unknown }}）</span>
            <span class="dashboard-progress__open">开放待确认事项 {{ data.knowledgeProgress.openUnknownItems }}</span>
          </div>
          <p>知识状态通过明确操作更新，不可直接点击切换。</p>
        </section>

        <div class="dashboard-lower-grid">
          <section class="dashboard-section dashboard-attention" aria-labelledby="dashboard-attention-title">
            <h2 id="dashboard-attention-title">需要关注</h2>
            <EmptyState
              v-if="data.needsAttention.length === 0"
              title="当前没有需要关注的知识缺口"
              description="继续通过全局搜索或新增知识对象补充记录。"
            />
            <div v-else class="dashboard-list">
              <button
                v-for="item in data.needsAttention"
                :key="item.kind"
                class="dashboard-list__row"
                type="button"
                @click="navigateAttention(item)"
              >
                <el-icon><WarningFilled /></el-icon>
                <strong>{{ item.label }}</strong>
                <span>{{ item.count }} 项</span>
                <el-icon class="dashboard-list__arrow"><ArrowRight /></el-icon>
              </button>
            </div>
          </section>

          <section class="dashboard-section dashboard-recent" aria-labelledby="dashboard-recent-title">
            <h2 id="dashboard-recent-title">最近整理</h2>
            <EmptyState
              v-if="data.recentActivity.length === 0"
              title="暂无最近整理记录"
              description="知识对象发生更新后会在这里显示。"
            />
            <div v-else class="dashboard-list">
              <button
                v-for="item in data.recentActivity"
                :key="`${item.objectType}-${item.objectId}`"
                class="dashboard-list__row dashboard-list__row--recent"
                type="button"
                @click="navigateRecent(item)"
              >
                <el-icon><Clock /></el-icon>
                <span class="dashboard-list__time">{{ formatUpdatedAt(item.updatedAt) }}</span>
                <em>{{ objectTypeLabel(item.objectType) }}</em>
                <strong class="technical-text">{{ item.title }}</strong>
                <el-icon class="dashboard-list__arrow"><ArrowRight /></el-icon>
              </button>
            </div>
            <p v-if="navigationError" class="dashboard-recent__error"><el-icon><CircleCloseFilled /></el-icon>{{ navigationError }}</p>
          </section>
        </div>

        <footer v-if="error" class="dashboard-page__refresh-error">刷新失败：{{ error }}</footer>
      </template>
    </template>
  </div>
</template>

<style src="../dashboard.css"></style>
