<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { ArrowRight, Coin, Delete, Plus, Search } from '@element-plus/icons-vue'
import { useRoute, useRouter } from 'vue-router'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import { useOverlayStore } from '../../../app/stores/overlays'
import { getSystemsList } from '../../systems/api/systemsApi'
import type { SystemSummary } from '../../systems/api/systemsContracts'
import { deleteDatabaseSource, parseSafeApiId } from '../api/databaseKnowledgeApi'
import type {
  DatabaseObjectListItem,
  DatabaseObjectType,
  DatabaseObjectsSort,
} from '../api/databaseKnowledgeContracts'
import { useDatabaseObjectsList } from '../composables/useDatabaseObjectsList'
import { openDeleteDialog } from '../../soft-delete/deleteDialog'
import type { DatabaseSourceContext } from '../api/databaseKnowledgeContracts'

const route = useRoute()
const router = useRouter()
const overlayStore = useOverlayStore()
const systemOptions = ref<readonly SystemSummary[]>([])
const systemOptionsError = ref<string | null>(null)
const {
  systemId,
  databaseSourceId,
  schema,
  objectType,
  knowledgeStatus,
  keyword,
  sort,
  page,
  loading,
  error,
  data,
  load,
  resetPageAndLoad,
  clearFilters,
} = useDatabaseObjectsList()

const hasFilters = computed(() => Boolean(
  systemId.value
  || databaseSourceId.value
  || schema.value
  || objectType.value
  || knowledgeStatus.value
  || keyword.value,
))

let keywordTimer: ReturnType<typeof setTimeout> | null = null
watch(keyword, () => {
  if (keywordTimer) clearTimeout(keywordTimer)
  keywordTimer = setTimeout(resetPageAndLoad, 280)
})

function objectTypeLabel(value: DatabaseObjectType): string {
  return value === 'Table' ? '表' : '视图'
}

function accessModeLabel(value: string): string {
  return ({ Read: '只读', Write: '只写', ReadWrite: '读 / 写', Unknown: '待确认' } as Record<string, string>)[value] ?? value
}

function formatRows(value: number | null): string {
  return value === null ? '—' : new Intl.NumberFormat('zh-CN', { notation: 'compact', maximumFractionDigits: 1 }).format(value)
}

function applyRouteQuery(): void {
  systemId.value = parseSafeApiId(route.query.systemId) ?? undefined
  databaseSourceId.value = parseSafeApiId(route.query.databaseSourceId) ?? undefined
  schema.value = typeof route.query.schema === 'string' ? route.query.schema : ''
}

function updateRouteAndLoad(next: { systemId?: number; databaseSourceId?: number; schema?: string } = {}): void {
  void router.replace({
    query: {
      ...(next.systemId === undefined ? {} : { systemId: String(next.systemId) }),
      ...(next.databaseSourceId === undefined ? {} : { databaseSourceId: String(next.databaseSourceId) }),
      ...(next.schema ? { schema: next.schema } : {}),
    },
  })
}

function selectSystem(value: number | undefined): void {
  systemId.value = value
  databaseSourceId.value = undefined
  schema.value = ''
  page.value = 1
  updateRouteAndLoad({ systemId: value })
  void load()
}

function selectSource(value: number | undefined): void {
  databaseSourceId.value = value
  schema.value = ''
  page.value = 1
  updateRouteAndLoad({ systemId: systemId.value, databaseSourceId: value })
  void load()
}

function selectSchema(value: string): void {
  schema.value = value
  page.value = 1
  updateRouteAndLoad({
    systemId: systemId.value,
    databaseSourceId: databaseSourceId.value,
    schema: value,
  })
  void load()
}

function handleFilterChange(): void {
  resetPageAndLoad()
}

function handleSortChange(change: { prop: string; order: 'ascending' | 'descending' | null }): void {
  const direction = change.order === 'ascending' ? 'asc' : 'desc'
  const field = change.prop === 'schema'
    ? 'schema'
    : change.prop === 'estimatedRows'
      ? 'estimatedRows'
      : change.prop === 'knowledgeStatus'
        ? 'knowledgeStatus'
        : change.prop === 'unknownCount'
          ? 'unknownCount'
          : 'objectName'
  sort.value = `${field}:${direction}` as DatabaseObjectsSort
  resetPageAndLoad()
}

function handlePageChange(nextPage: number): void {
  page.value = nextPage
  void load()
}

function openObject(row: DatabaseObjectListItem): void {
  void router.push({ name: 'database-object-detail', params: { id: String(row.id) } })
}

function startCreate(): void {
  overlayStore.openDialog({ kind: 'create-database-knowledge', id: null, mode: 'create' })
}

function requestSourceDelete(source: DatabaseSourceContext): void {
  if (!source.canDelete) return
  openDeleteDialog(overlayStore, {
    objectTypeLabel: '数据库来源', actionLabel: '删除数据库源', displayName: source.name,
    concurrencyToken: source.concurrencyToken,
    execute: () => deleteDatabaseSource(source.id, source.concurrencyToken),
    onDeleted: async () => {
      if (databaseSourceId.value === source.id) {
        databaseSourceId.value = undefined
        await router.replace({ query: systemId.value ? { systemId: String(systemId.value) } : {} })
      }
      await load()
    },
    onRefresh: load,
    onUnavailable: load,
  })
}

async function loadSystemOptions(): Promise<void> {
  try {
    const response = await getSystemsList({ sort: 'name:asc', page: 1, pageSize: 100 })
    systemOptions.value = response.items
  } catch (loadError: unknown) {
    systemOptionsError.value = loadError instanceof Error ? loadError.message : '系统筛选加载失败。'
  }
}

watch(
  () => route.query,
  () => {
    applyRouteQuery()
    void load()
  },
)

onMounted(() => {
  applyRouteQuery()
  void Promise.all([load(), loadSystemOptions()])
})
</script>

<template>
  <div class="database-objects-list-page skh-page">
    <header class="database-objects-list-page__header skh-page-header">
      <div>
        <div class="page-eyebrow"><el-icon><Coin /></el-icon>数据库知识</div>
        <h1>数据库对象</h1>
        <p>按数据库来源与 Schema 浏览 Table、View 和关联字段。</p>
      </div>
      <div class="database-objects-list-page__header-actions skh-page-header__actions">
        <span v-if="data">共 {{ data.total }} 个对象</span>
        <el-button type="primary" :icon="Plus" @click="startCreate">新增数据库对象</el-button>
      </div>
    </header>

    <section class="database-objects-list-page__workspace">
      <aside class="database-browser" aria-label="数据库与 Schema 浏览">
        <div class="database-browser__heading">
          <strong>数据库 / Schema</strong>
          <span>浏览上下文</span>
        </div>
        <el-select
          :model-value="systemId"
          clearable
          filterable
          placeholder="所有系统"
          aria-label="筛选系统"
          @update:model-value="selectSystem"
        >
          <el-option v-for="item in systemOptions" :key="item.id" :label="item.name" :value="item.id" />
        </el-select>
        <p v-if="systemOptionsError" class="database-browser__error">{{ systemOptionsError }}</p>

        <div class="database-browser__group">
          <button
            class="database-browser__node"
            :class="{ 'database-browser__node--active': !databaseSourceId }"
            type="button"
            @click="selectSource(undefined)"
          >
            <span>全部数据库来源</span>
            <small>{{ data?.browseContext.databaseSources.length ?? 0 }}</small>
          </button>
          <div
            v-for="source in data?.browseContext.databaseSources ?? []"
            :key="source.id"
            class="database-browser__source-row"
          >
            <button
              class="database-browser__node database-browser__node--source"
              :class="{ 'database-browser__node--active': databaseSourceId === source.id }"
              type="button"
              @click="selectSource(source.id)"
            ><span><strong>{{ source.name }}</strong><small>{{ source.engine }}</small></span></button>
            <el-tooltip v-if="source.canDelete" content="删除数据库源" placement="right">
              <el-button class="skh-icon-action" text circle type="danger" :icon="Delete" aria-label="删除数据库源" @click="requestSourceDelete(source)" />
            </el-tooltip>
          </div>
        </div>

        <div class="database-browser__group database-browser__group--schemas">
          <span class="database-browser__group-label">Schema</span>
          <button
            class="database-browser__node"
            :class="{ 'database-browser__node--active': !schema }"
            type="button"
            @click="selectSchema('')"
          >全部 Schema</button>
          <button
            v-for="item in data?.browseContext.schemas ?? []"
            :key="item"
            class="database-browser__node technical-text"
            :class="{ 'database-browser__node--active': schema === item }"
            type="button"
            @click="selectSchema(item)"
          >{{ item }}</button>
        </div>
      </aside>

      <section class="database-objects-list-page__content">
        <section class="database-objects-filter-bar skh-filter-bar" aria-label="数据库对象筛选">
          <el-input
            v-model="keyword"
            clearable
            :prefix-icon="Search"
            placeholder="搜索表、视图、字段或业务说明"
            aria-label="搜索数据库对象"
          />
          <el-select v-model="objectType" clearable placeholder="对象类型：全部" @change="handleFilterChange">
            <el-option label="表" value="Table" />
            <el-option label="视图" value="View" />
          </el-select>
          <el-select v-model="knowledgeStatus" clearable placeholder="知识状态：全部" @change="handleFilterChange">
            <el-option label="未知" value="Unknown" />
            <el-option label="推断" value="Inferred" />
            <el-option label="已确认" value="Confirmed" />
          </el-select>
          <el-button v-if="hasFilters" text type="primary" @click="clearFilters">清除筛选</el-button>
        </section>

        <LoadingState v-if="loading && !data" message="正在读取数据库对象…" />
        <ErrorState
          v-else-if="error && !data"
          title="数据库对象列表加载失败"
          :message="error"
          @retry="load"
        />
        <section v-else class="database-objects-table-section" :aria-busy="loading">
          <EmptyState
            v-if="data && data.items.length === 0"
            title="没有找到数据库对象"
            description="可以调整筛选条件，或登记数据库来源与对象。"
          />
          <el-table
            v-else
            :data="data?.items ?? []"
            row-key="id"
            class="database-objects-table skh-data-table skh-data-table--dense"
            @row-click="openObject"
            @sort-change="handleSortChange"
          >
            <el-table-column prop="objectName" label="对象名称" min-width="156" sortable="custom">
              <template #default="scope">
                <button class="technical-text skh-table-link" type="button" @click.stop="openObject(scope.row)">{{ scope.row.schema }}.{{ scope.row.objectName }}</button>
                <small v-if="scope.row.matchedColumn" class="database-objects-table__matched">字段命中：{{ scope.row.matchedColumn.columnName }}</small>
              </template>
            </el-table-column>
            <el-table-column prop="schema" label="Schema" width="96" sortable="custom">
              <template #default="scope"><span class="technical-text">{{ scope.row.schema }}</span></template>
            </el-table-column>
            <el-table-column label="数据库来源" min-width="132">
              <template #default="scope"><span>{{ scope.row.databaseSource.name }}</span></template>
            </el-table-column>
            <el-table-column prop="objectType" label="类型" width="76">
              <template #default="scope">{{ objectTypeLabel(scope.row.objectType) }}</template>
            </el-table-column>
            <el-table-column prop="businessDescription" label="业务说明" min-width="190" show-overflow-tooltip>
              <template #default="scope"><span :class="{ 'text-muted': !scope.row.businessDescription }">{{ scope.row.businessDescription ?? '尚未记录' }}</span></template>
            </el-table-column>
            <el-table-column prop="estimatedRows" label="估算行数" width="92" align="right" sortable="custom">
              <template #default="scope">{{ formatRows(scope.row.estimatedRows) }}</template>
            </el-table-column>
            <el-table-column label="读写方式" width="88">
              <template #default="scope">{{ accessModeLabel(scope.row.accessMode) }}</template>
            </el-table-column>
            <el-table-column prop="relatedFunctionCount" label="关联功能" width="82" align="center" />
            <el-table-column prop="unknownCount" label="待确认" width="72" align="center" sortable="custom" />
            <el-table-column prop="knowledgeStatus" label="知识状态" width="94" sortable="custom">
              <template #default="scope"><KnowledgeStatusBadge :status="scope.row.knowledgeStatus" /></template>
            </el-table-column>
            <el-table-column width="34" align="right">
              <template #default><el-icon class="database-objects-table__next" title="查看对象详情"><ArrowRight /></el-icon></template>
            </el-table-column>
          </el-table>
          <footer v-if="data && data.total > 0" class="database-objects-pagination skh-pagination skh-pagination--split">
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
          <p v-if="error && data" class="database-objects-inline-error">刷新失败：{{ error }}</p>
        </section>
      </section>
    </section>
  </div>
</template>

<style src="../database-knowledge.css"></style>
