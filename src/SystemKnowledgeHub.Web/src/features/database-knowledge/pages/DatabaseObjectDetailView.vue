<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { DocumentChecked, EditPen, Search } from '@element-plus/icons-vue'
import { useRoute, useRouter } from 'vue-router'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import { useOverlayStore } from '../../../app/stores/overlays'
import { parseSafeApiId } from '../api/databaseKnowledgeApi'
import type { DatabaseColumnSummary } from '../api/databaseKnowledgeContracts'
import DatabaseObjectContextRail from '../components/DatabaseObjectContextRail.vue'
import { useDatabaseObjectDetail } from '../composables/useDatabaseObjectDetail'

const route = useRoute()
const router = useRouter()
const overlayStore = useOverlayStore()
const filterText = ref('')
const {
  detail,
  loading,
  errorMessage,
  selectedColumnError,
  selectedColumnId,
  load,
  selectColumn,
  clearColumnSelection,
} = useDatabaseObjectDetail()

const databaseObjectId = computed(() => parseSafeApiId(route.params.id))
const routeSelectedColumnId = computed(() => parseSafeApiId(route.query.selectedColumnId))

const filteredColumns = computed(() => {
  if (!detail.value) return []
  const query = filterText.value.trim().toLocaleLowerCase()
  if (!query) return detail.value.columns
  return detail.value.columns.filter(
    (column) =>
      column.columnName.toLocaleLowerCase().includes(query) ||
      column.dataType.toLocaleLowerCase().includes(query) ||
      column.businessDescription?.toLocaleLowerCase().includes(query),
  )
})

function formatRows(value: number | null): string {
  if (value === null) return '—'
  return new Intl.NumberFormat('zh-CN', { notation: 'compact', maximumFractionDigits: 1 }).format(value)
}

function accessModeLabel(value: string): string {
  const labels: Readonly<Record<string, string>> = {
    Read: '只读',
    Write: '只写',
    ReadWrite: '读 / 写',
    Unknown: '待确认',
  }
  return labels[value] ?? value
}

function handleColumnClick(column: DatabaseColumnSummary): void {
  selectColumn(column.id)
  void router.replace({
    query: { ...route.query, selectedColumnId: String(column.id) },
  })
}

function rowClassName({ row }: { row: DatabaseColumnSummary }): string {
  return row.id === selectedColumnId.value ? 'database-column-row--selected' : ''
}

async function loadRoute(): Promise<void> {
  if (databaseObjectId.value === null) return
  await load(databaseObjectId.value, routeSelectedColumnId.value)
  if (selectedColumnError.value) {
    const nextQuery = { ...route.query }
    delete nextQuery.selectedColumnId
    await router.replace({ query: nextQuery })
  }
}

watch(
  () => overlayStore.currentDrawer,
  (drawer) => {
    if (drawer === null && selectedColumnId.value !== null) {
      clearColumnSelection()
      const nextQuery = { ...route.query }
      delete nextQuery.selectedColumnId
      void router.replace({ query: nextQuery })
    }
  },
)

onMounted(() => {
  void loadRoute()
})
</script>

<template>
  <div class="database-object-page">
    <ErrorState
      v-if="databaseObjectId === null"
      title="数据库对象地址无效"
      message="请从数据库对象列表重新进入。"
    />
    <LoadingState v-else-if="loading" message="正在读取数据库对象详情…" />
    <ErrorState
      v-else-if="errorMessage"
      title="数据库对象加载失败"
      :message="errorMessage"
      @retry="loadRoute"
    />
    <template v-else-if="detail">
      <header class="database-object-header">
        <nav class="database-breadcrumb" aria-label="面包屑">
          <span>数据库</span><b>/</b><span>{{ detail.system.name }}</span><b>/</b
          ><span>{{ detail.databaseSource.name }}</span><b>/</b
          ><strong class="technical-text">{{ detail.overview.qualifiedName }}</strong>
        </nav>
        <div class="database-object-header__title">
          <div>
            <h1 class="technical-text">{{ detail.overview.qualifiedName }}</h1>
            <p>{{ detail.overview.businessDescription ?? '尚未记录业务说明' }}</p>
          </div>
          <el-button text type="primary" :icon="EditPen" disabled>编辑</el-button>
        </div>
        <div class="database-object-header__tags">
          <span>{{ detail.overview.objectType === 'Table' ? '表' : '视图' }}</span>
          <span>{{ detail.databaseSource.engine }}</span>
          <KnowledgeStatusBadge :status="detail.overview.knowledgeStatus" />
        </div>
      </header>

      <div v-if="selectedColumnError" class="database-inline-notice">{{ selectedColumnError }}</div>

      <section class="database-metadata-strip" aria-label="数据库元数据摘要">
        <div><span>估算行数</span><strong>{{ formatRows(detail.metadata.estimatedRows) }}</strong></div>
        <div><span>访问方式</span><strong>{{ accessModeLabel(detail.overview.accessMode) }}</strong></div>
        <div><span>数据库来源</span><strong>{{ detail.databaseSource.name }}</strong></div>
        <div>
          <span>主键</span>
          <strong class="technical-text">{{ detail.metadata.primaryKeyColumns.join(' · ') || '—' }}</strong>
        </div>
        <div>
          <span>业务唯一键</span>
          <strong class="technical-text">{{ detail.metadata.businessKeyColumns.join(' · ') || '—' }}</strong>
        </div>
      </section>

      <section class="database-columns-section" aria-labelledby="columns-title">
        <div class="database-columns-section__toolbar">
          <div><h2 id="columns-title">字段</h2><span>{{ detail.columns.length }} 个字段</span></div>
          <el-input v-model="filterText" clearable placeholder="筛选字段" :prefix-icon="Search" />
        </div>

        <EmptyState v-if="detail.columns.length === 0" />
        <el-table
          v-else
          :data="filteredColumns"
          row-key="id"
          class="database-column-table"
          :row-class-name="rowClassName"
          @row-click="handleColumnClick"
        >
          <el-table-column prop="columnName" label="字段" min-width="120">
            <template #default="scope">
              <strong class="technical-text">{{ scope.row.columnName }}</strong>
            </template>
          </el-table-column>
          <el-table-column prop="dataType" label="数据类型" width="108">
            <template #default="scope"><code>{{ scope.row.dataType }}</code></template>
          </el-table-column>
          <el-table-column prop="nullable" label="允许为空" width="76">
            <template #default="scope">{{ scope.row.nullable ? '是' : '否' }}</template>
          </el-table-column>
          <el-table-column prop="businessDescription" label="业务说明" min-width="170">
            <template #default="scope">
              <span :class="{ 'text-muted': !scope.row.businessDescription }">
                {{ scope.row.businessDescription ?? '尚未记录' }}
              </span>
            </template>
          </el-table-column>
          <el-table-column prop="knowledgeStatus" label="知识状态" width="88">
            <template #default="scope">
              <KnowledgeStatusBadge :status="scope.row.knowledgeStatus" />
            </template>
          </el-table-column>
          <el-table-column label="证据" width="108">
            <template #default="scope">
              <span class="evidence-cell" :class="{ 'evidence-cell--empty': scope.row.evidenceCount === 0 }">
                <el-icon><DocumentChecked /></el-icon>
                {{ scope.row.evidenceCount }} 条证据
              </span>
            </template>
          </el-table-column>
        </el-table>

        <div v-if="detail.columns.length > 0 && filteredColumns.length === 0" class="table-filter-empty">
          没有匹配“{{ filterText }}”的字段。
        </div>
      </section>

      <Teleport defer to="#context-rail-content">
        <DatabaseObjectContextRail :detail="detail" />
      </Teleport>

    </template>
  </div>
</template>

<style src="../database-knowledge.css"></style>
