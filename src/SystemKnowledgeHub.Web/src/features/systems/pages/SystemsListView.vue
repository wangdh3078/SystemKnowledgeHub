<script setup lang="ts">
import { computed, onMounted, watch } from 'vue'
import { ArrowRight, Plus, Search } from '@element-plus/icons-vue'
import { useRouter } from 'vue-router'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import { formatDateTime } from '../../../app/formatters/dateTime'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import CreateSystemFlow from '../components/CreateSystemFlow.vue'
import {
  systemLifecycleLabels,
  type SystemLifecycle,
  type SystemSummary,
  type SystemsSort,
} from '../api/systemsContracts'
import { useSystemsList } from '../composables/useSystemsList'

const {
  keyword,
  lifecycle,
  technology,
  knowledgeStatus,
  sort,
  page,
  loading,
  error,
  data,
  load,
  resetPageAndLoad,
  clearFilters,
} = useSystemsList()
const router = useRouter()
const actorStore = useActorStore()
const overlayStore = useOverlayStore()

const lifecycleOptions: readonly { readonly value: SystemLifecycle; readonly label: string }[] = [
  { value: 'Planned', label: systemLifecycleLabels.Planned },
  { value: 'InDevelopment', label: systemLifecycleLabels.InDevelopment },
  { value: 'Running', label: systemLifecycleLabels.Running },
  { value: 'Maintaining', label: systemLifecycleLabels.Maintaining },
  { value: 'Legacy', label: systemLifecycleLabels.Legacy },
  { value: 'Retired', label: systemLifecycleLabels.Retired },
]
const technologyOptions = [
  '.NET Framework 4.8',
  'Oracle',
  'Java',
  'PostgreSQL',
  'C#',
  'RabbitMQ',
  'SAP',
  'SQL Server',
]
const hasFilters = computed(() =>
  Boolean(keyword.value || lifecycle.value || technology.value || knowledgeStatus.value),
)
let keywordTimer: ReturnType<typeof setTimeout> | null = null

watch(keyword, () => {
  if (keywordTimer) clearTimeout(keywordTimer)
  keywordTimer = setTimeout(resetPageAndLoad, 280)
})

function handleSortChange(change: { prop: string; order: 'ascending' | 'descending' | null }): void {
  const ascending = change.order === 'ascending'
  const nextSort: SystemsSort = change.prop === 'name'
    ? ascending ? 'name:asc' : 'name:desc'
    : change.prop === 'knowledgeStatus'
      ? ascending ? 'knowledgeStatus:asc' : 'knowledgeStatus:desc'
      : ascending ? 'updatedAt:asc' : 'updatedAt:desc'
  sort.value = nextSort
  resetPageAndLoad()
}

function handlePageChange(nextPage: number): void {
  page.value = nextPage
  void load()
}

function openSystem(id: number): void {
  void router.push({ name: 'system-detail', params: { id: String(id) } })
}

function handleSystemRowClick(row: SystemSummary): void {
  openSystem(row.id)
}

function openCreate(): void {
  overlayStore.openDialog({ kind: 'create-system', id: null, mode: 'create' })
}

onMounted(() => void load())
</script>

<template>
  <div class="systems-page skh-page">
    <header class="systems-page__header skh-page-header">
      <div>
        <h1>系统</h1>
        <p>浏览系统及其知识覆盖情况。</p>
      </div>
      <div class="systems-page__header-actions skh-page-header__actions">
        <span v-if="data">共 {{ data.total }} 个系统</span>
        <el-button v-if="actorStore.canEdit" type="primary" :icon="Plus" @click="openCreate">新增系统</el-button>
      </div>
    </header>

    <section class="systems-filter-bar skh-filter-bar" aria-label="系统筛选">
      <el-input
        v-model="keyword"
        clearable
        :prefix-icon="Search"
        placeholder="搜索系统名称、显示名称或用途"
        aria-label="搜索系统"
      />
      <el-select v-model="lifecycle" placeholder="生命周期：全部" clearable @change="resetPageAndLoad">
        <el-option
          v-for="item in lifecycleOptions"
          :key="item.value"
          :label="item.label"
          :value="item.value"
        />
      </el-select>
      <el-select v-model="technology" placeholder="技术：全部" clearable filterable @change="resetPageAndLoad">
        <el-option v-for="item in technologyOptions" :key="item" :label="item" :value="item" />
      </el-select>
      <el-select v-model="knowledgeStatus" placeholder="知识状态：全部" clearable @change="resetPageAndLoad">
        <el-option label="未知" value="Unknown" />
        <el-option label="推断" value="Inferred" />
        <el-option label="已确认" value="Confirmed" />
      </el-select>
      <el-button v-if="hasFilters" text type="primary" @click="clearFilters">清除筛选</el-button>
    </section>

    <LoadingState v-if="loading && !data" message="正在读取系统列表…" />
    <ErrorState
      v-else-if="error && !data"
      title="系统列表加载失败"
      :message="error"
      @retry="load"
    />
    <section v-else class="systems-table-section skh-table-section" :aria-busy="loading">
      <EmptyState
        v-if="data && data.items.length === 0"
        title="没有找到系统"
        description="可以调整筛选条件，或通过右上角“新增系统”记录第一个系统。"
      />
      <el-table
        v-else
        :data="data?.items ?? []"
        row-key="id"
        class="systems-table skh-data-table"
        @row-click="handleSystemRowClick"
        @sort-change="handleSortChange"
      >
        <el-table-column prop="name" label="系统名称" min-width="130" sortable="custom">
          <template #default="scope"><button class="technical-text system-name skh-table-link" type="button" @click.stop="openSystem(scope.row.id)">{{ scope.row.name }}</button></template>
        </el-table-column>
        <el-table-column prop="displayName" label="显示名称" min-width="120" />
        <el-table-column prop="systemType" label="系统类型" min-width="125" />
        <el-table-column prop="purpose" label="用途" min-width="200" show-overflow-tooltip>
          <template #default="scope"><span :class="{ 'text-muted': !scope.row.purpose }">{{ scope.row.purpose ?? '尚未记录' }}</span></template>
        </el-table-column>
        <el-table-column label="技术" min-width="150" show-overflow-tooltip>
          <template #default="scope">
            <span :class="{ 'text-muted': scope.row.technologies.length === 0 }">{{ scope.row.technologies.join(' · ') || '尚未记录' }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="functionCount" label="业务功能" width="82" align="center" />
        <el-table-column prop="databaseObjectCount" label="数据库对象" width="92" align="center" />
        <el-table-column prop="openUnknownCount" label="开放待确认事项" width="112" align="center" />
        <el-table-column prop="knowledgeStatus" label="知识状态" width="94" sortable="custom">
          <template #default="scope"><KnowledgeStatusBadge :status="scope.row.knowledgeStatus" /></template>
        </el-table-column>
        <el-table-column prop="updatedAt" label="更新于" width="156" sortable="custom">
          <template #default="scope">{{ formatDateTime(scope.row.updatedAt) }}</template>
        </el-table-column>
        <el-table-column width="34" align="right">
          <template #default><el-icon class="systems-table__next" title="查看系统详情"><ArrowRight /></el-icon></template>
        </el-table-column>
      </el-table>

      <footer v-if="data && data.total > 0" class="systems-pagination skh-pagination">
        <span>{{ (data.page - 1) * data.pageSize + 1 }}–{{ Math.min(data.page * data.pageSize, data.total) }} / {{ data.total }}</span>
        <el-pagination
          background
          layout="prev, pager, next"
          :current-page="data.page"
          :page-size="data.pageSize"
          :total="data.total"
          @current-change="handlePageChange"
        />
      </footer>
      <p v-if="error && data" class="systems-inline-error">刷新失败：{{ error }}</p>
    </section>
    <CreateSystemFlow @created="resetPageAndLoad" />
  </div>
</template>

<style src="../systems.css"></style>
