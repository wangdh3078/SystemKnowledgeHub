<script setup lang="ts">
import { onMounted, watch } from 'vue'
import { Plus, Search } from '@element-plus/icons-vue'
import { useRouter } from 'vue-router'
import { useOverlayStore } from '../../../app/stores/overlays'
import { useActorStore } from '../../../app/stores/actor'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import { priorityLabels, unknownItemStatusLabels, type UnknownItemListRow, type UnknownItemPriority, type UnknownItemStatus } from '../api/unknownItemContracts'
import { useUnknownItemsList } from '../composables/useUnknownItemsList'

const router = useRouter(); const overlays = useOverlayStore()
const actorStore = useActorStore()
const { data, loading, error, filters, load } = useUnknownItemsList()
let timer: number | undefined
watch(() => [filters.keyword, filters.priority, filters.status], () => {
  filters.page = 1; window.clearTimeout(timer); timer = window.setTimeout(() => void load(), 250)
})
function create(): void { overlays.openDialog({ kind: 'create-unknown-item', id: null, mode: 'create' }) }
function openRow(row: UnknownItemListRow): void { void router.push({ name: 'unknown-item-detail', params: { id: String(row.id) } }) }
function priorityLabel(value: string): string { return value in priorityLabels ? priorityLabels[value as UnknownItemPriority] : value }
function statusLabel(value: string): string { return value in unknownItemStatusLabels ? unknownItemStatusLabels[value as UnknownItemStatus] : value }
onMounted(() => void load())
</script>

<template>
  <main class="unknown-list-page skh-page">
    <header class="unknown-list-header skh-page-header"><div><p>知识发现 / 待确认事项</p><h1>待确认事项</h1><span>集中处理尚未确认的问题、调查发现与证据。</span></div><el-button v-if="actorStore.canEdit" type="primary" :icon="Plus" @click="create">新增待确认事项</el-button></header>
    <section class="unknown-list-toolbar skh-filter-bar" aria-label="待确认事项筛选">
      <el-input v-model="filters.keyword" :prefix-icon="Search" clearable placeholder="搜索问题、上下文或关联对象" />
      <el-select v-model="filters.priority" clearable placeholder="优先级"><el-option label="高" value="High" /><el-option label="中" value="Medium" /><el-option label="低" value="Low" /></el-select>
      <el-select v-model="filters.status" clearable placeholder="事项状态"><el-option label="待处理" value="Open" /><el-option label="调查中" value="Investigating" /><el-option label="结论已确认" value="ConclusionConfirmed" /><el-option label="已关闭" value="Closed" /></el-select>
      <span>{{ data?.total ?? 0 }} 项</span>
    </section>
    <LoadingState v-if="loading && !data" message="正在读取待确认事项…" />
    <ErrorState v-else-if="error && !data" title="待确认事项加载失败" :message="error" @retry="load" />
    <EmptyState v-else-if="!data?.items.length" title="暂无待确认事项" description="从业务功能、字段等知识对象发现问题时创建事项。" />
    <el-table v-else :data="data.items" class="unknown-list-table skh-data-table" @row-click="openRow">
      <el-table-column label="问题" min-width="330"><template #default="scope"><div class="unknown-question-cell"><button class="skh-table-link" type="button" @click.stop="openRow(scope.row)">{{ scope.row.question }}</button><small class="technical-text">{{ scope.row.itemCode }} · {{ scope.row.primaryTarget.display }}</small></div></template></el-table-column>
      <el-table-column label="系统" width="110"><template #default="scope"><span class="technical-text">{{ scope.row.system.name }}</span></template></el-table-column>
      <el-table-column label="优先级" width="86"><template #default="scope"><span :class="`priority priority--${scope.row.priority.toLowerCase()}`">{{ priorityLabel(scope.row.priority) }}</span></template></el-table-column>
      <el-table-column label="事项状态" width="120"><template #default="scope"><span :class="`unknown-status unknown-status--${scope.row.status.toLowerCase()}`">{{ statusLabel(scope.row.status) }}</span></template></el-table-column>
      <el-table-column prop="findingCount" label="发现" width="74" align="center" /><el-table-column prop="evidenceCount" label="证据" width="74" align="center" />
      <el-table-column label="更新于" width="150"><template #default="scope">{{ new Date(scope.row.updatedAt).toLocaleString('zh-CN') }}</template></el-table-column>
    </el-table>
    <div v-if="data && data.total > data.pageSize" class="skh-pagination"><span>{{ (data.page - 1) * data.pageSize + 1 }}–{{ Math.min(data.page * data.pageSize, data.total) }} / {{ data.total }}</span><el-pagination v-model:current-page="filters.page" background :page-size="filters.pageSize" :total="data.total" layout="prev, pager, next" @current-change="load" /></div>
  </main>
</template>
<style src="../unknown-items.css"></style>
