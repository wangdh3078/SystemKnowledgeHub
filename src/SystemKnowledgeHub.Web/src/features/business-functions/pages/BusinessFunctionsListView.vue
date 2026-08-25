<script setup lang="ts">
import { computed, nextTick, onMounted, watch } from 'vue'
import { Plus, Search } from '@element-plus/icons-vue'
import { useRoute, useRouter } from 'vue-router'
import { parseSafeApiId } from '../../../api/contracts/id'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import {
  functionTypeLabels,
  rewriteStatusLabels,
  type BusinessFunctionSummary,
  type BusinessFunctionsSort,
  type CreateBusinessFunctionResponse,
  type RewriteStatus,
} from '../api/businessFunctionContracts'
import { useBusinessFunctionsList } from '../composables/useBusinessFunctionsList'
import CreateBusinessFunctionFlow from '../components/CreateBusinessFunctionFlow.vue'
import { useOverlayStore } from '../../../app/stores/overlays'
import { useActorStore } from '../../../app/stores/actor'

const route = useRoute()
const router = useRouter()
const overlays = useOverlayStore()
const actorStore = useActorStore()
const initialSystemId = parseSafeApiId(route.query.systemId) ?? undefined
const {
  keyword,
  systemId,
  functionType,
  rewriteStatus,
  knowledgeStatus,
  hasUnknownItems,
  sort,
  page,
  loading,
  error,
  data,
  systemOptions,
  load,
  loadSystemOptions,
  resetPageAndLoad,
  clearFilters,
} = useBusinessFunctionsList(initialSystemId)

const functionTypeOptions = [
  { value: 'Query', label: functionTypeLabels.Query },
  { value: 'ServiceQuery', label: functionTypeLabels.ServiceQuery },
  { value: 'BusinessOperation', label: functionTypeLabels.BusinessOperation },
  { value: 'IntegrationTask', label: functionTypeLabels.IntegrationTask },
  { value: 'Batch', label: functionTypeLabels.Batch },
]
const rewriteStatusOptions: readonly { readonly value: RewriteStatus; readonly label: string }[] = [
  { value: 'Keep', label: rewriteStatusLabels.Keep },
  { value: 'Change', label: rewriteStatusLabels.Change },
  { value: 'Remove', label: rewriteStatusLabels.Remove },
  { value: 'Unknown', label: rewriteStatusLabels.Unknown },
]
const hasFilters = computed(() => Boolean(
  keyword.value
  || systemId.value
  || functionType.value
  || rewriteStatus.value
  || knowledgeStatus.value
  || hasUnknownItems.value !== undefined,
))
let keywordTimer: ReturnType<typeof setTimeout> | null = null

watch(keyword, () => {
  if (keywordTimer) clearTimeout(keywordTimer)
  keywordTimer = setTimeout(resetPageAndLoad, 280)
})

function handleSortChange(change: { prop: string; order: 'ascending' | 'descending' | null }): void {
  const ascending = change.order === 'ascending'
  const nextSort: BusinessFunctionsSort = change.prop === 'name'
    ? ascending ? 'name:asc' : 'name:desc'
    : change.prop === 'knowledgeStatus'
      ? ascending ? 'knowledgeStatus:asc' : 'knowledgeStatus:desc'
      : ascending ? 'updatedAt:asc' : 'updatedAt:desc'
  sort.value = nextSort
  resetPageAndLoad()
}

function formatDate(value: string): string {
  return value.slice(0, 10)
}

function formatRewriteStatus(value: RewriteStatus): string {
  return rewriteStatusLabels[value]
}

function openDetail(id: number): void {
  void router.push({ name: 'business-function-detail', params: { id: String(id) } })
}

function handleRowClick(row: BusinessFunctionSummary): void {
  openDetail(row.id)
}

function handlePageChange(nextPage: number): void {
  page.value = nextPage
  void load()
}

function openCreate(): void {
  overlays.openDialog({ kind: 'create-business-function', id: null, mode: 'create' })
}

async function handleCreated(created: CreateBusinessFunctionResponse): Promise<void> {
  systemId.value = created.system.id
  keyword.value = created.name
  page.value = 1
  await nextTick()
  if (keywordTimer) clearTimeout(keywordTimer)
  await load()
}

onMounted(() => {
  void loadSystemOptions()
  void load()
})
</script>

<template>
  <div class="business-functions-page skh-page">
    <header class="business-functions-page__header skh-page-header">
      <div>
        <h1>业务功能</h1>
        <p>查找旧系统中的业务能力、处理逻辑与改写范围。</p>
      </div>
      <div class="business-functions-page__header-actions skh-page-header__actions">
        <span v-if="data">共 {{ data.total }} 个业务功能</span>
        <el-button v-if="actorStore.canEdit" type="primary" :icon="Plus" @click="openCreate">新增业务功能</el-button>
      </div>
    </header>

    <section class="business-functions-filter skh-filter-bar" aria-label="业务功能筛选">
      <el-input v-model="keyword" clearable :prefix-icon="Search" placeholder="搜索功能名称或用途" aria-label="搜索业务功能" />
      <el-select v-model="systemId" placeholder="系统：全部" clearable filterable @change="resetPageAndLoad">
        <el-option v-for="system in systemOptions" :key="system.id" :label="system.name" :value="system.id" />
      </el-select>
      <el-select v-model="functionType" placeholder="功能类型：全部" clearable @change="resetPageAndLoad">
        <el-option v-for="item in functionTypeOptions" :key="item.value" :label="item.label" :value="item.value" />
      </el-select>
      <el-select v-model="rewriteStatus" placeholder="改写状态：全部" clearable @change="resetPageAndLoad">
        <el-option v-for="item in rewriteStatusOptions" :key="item.value" :label="item.label" :value="item.value" />
      </el-select>
      <el-select v-model="knowledgeStatus" placeholder="知识状态：全部" clearable @change="resetPageAndLoad">
        <el-option label="未知" value="Unknown" />
        <el-option label="推断" value="Inferred" />
        <el-option label="已确认" value="Confirmed" />
      </el-select>
      <el-select v-model="hasUnknownItems" placeholder="待确认事项：全部" clearable @change="resetPageAndLoad">
        <el-option label="有待确认事项" :value="true" />
        <el-option label="无待确认事项" :value="false" />
      </el-select>
      <el-button v-if="hasFilters" text type="primary" @click="clearFilters">清除筛选</el-button>
    </section>

    <LoadingState v-if="loading && !data" message="正在读取业务功能列表…" />
    <ErrorState v-else-if="error && !data" title="业务功能列表加载失败" :message="error" @retry="load" />
    <section v-else class="business-functions-table-section skh-table-section" :aria-busy="loading">
      <EmptyState
        v-if="data && data.items.length === 0"
        title="没有找到业务功能"
        description="请调整筛选条件，或通过右上角“新增”记录业务功能。"
      />
      <el-table
        v-else
        :data="data?.items ?? []"
        row-key="id"
        class="business-functions-table skh-data-table"
        @row-click="handleRowClick"
        @sort-change="handleSortChange"
      >
        <el-table-column prop="name" label="功能名称" min-width="185" sortable="custom">
          <template #default="scope"><button class="technical-text function-name skh-table-link" type="button" @click.stop="openDetail(scope.row.id)">{{ scope.row.name }}</button></template>
        </el-table-column>
        <el-table-column label="系统" width="88"><template #default="scope"><strong class="technical-text">{{ scope.row.system.name }}</strong></template></el-table-column>
        <el-table-column prop="functionType" label="类型" width="108"><template #default="scope">{{ functionTypeLabels[scope.row.functionType] ?? scope.row.functionType }}</template></el-table-column>
        <el-table-column prop="purpose" label="用途" min-width="230" show-overflow-tooltip><template #default="scope"><span :class="{ 'text-muted': !scope.row.purpose }">{{ scope.row.purpose ?? '尚未记录' }}</span></template></el-table-column>
        <el-table-column prop="relatedDataCount" label="关联数据" width="78" align="center" />
        <el-table-column prop="ruleCount" label="业务规则" width="78" align="center" />
        <el-table-column prop="unknownCount" label="待确认事项" width="90" align="center" />
        <el-table-column prop="rewriteStatus" label="改写状态" width="88"><template #default="scope"><span class="rewrite-status" :class="`rewrite-status--${scope.row.rewriteStatus.toLowerCase()}`">{{ formatRewriteStatus(scope.row.rewriteStatus) }}</span></template></el-table-column>
        <el-table-column prop="knowledgeStatus" label="知识状态" width="92" sortable="custom"><template #default="scope"><KnowledgeStatusBadge :status="scope.row.knowledgeStatus" /></template></el-table-column>
        <el-table-column prop="updatedAt" label="更新于" width="108" sortable="custom"><template #default="scope">{{ formatDate(scope.row.updatedAt) }}</template></el-table-column>
      </el-table>

      <footer v-if="data && data.total > 0" class="business-functions-pagination skh-pagination">
        <span>{{ (data.page - 1) * data.pageSize + 1 }}–{{ Math.min(data.page * data.pageSize, data.total) }} / {{ data.total }}</span>
        <el-pagination background layout="prev, pager, next" :current-page="data.page" :page-size="data.pageSize" :total="data.total" @current-change="handlePageChange" />
      </footer>
      <p v-if="error && data" class="business-functions-inline-error">刷新失败：{{ error }}</p>
    </section>

    <CreateBusinessFunctionFlow
      :systems="systemOptions"
      :initial-system-id="systemId"
      @created="handleCreated"
    />
  </div>
</template>

<style src="../business-functions.css"></style>
