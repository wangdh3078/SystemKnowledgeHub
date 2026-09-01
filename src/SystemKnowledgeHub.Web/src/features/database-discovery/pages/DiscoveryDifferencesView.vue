<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { useRouter } from 'vue-router'
import { ApiError } from '../../../api/errors/ApiError'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import { getRunFilterOptions, listDifferences } from '../api/databaseDiscoveryApi'
import type { DifferenceHistoryItem, RunFilterOptions } from '../api/databaseDiscoveryContracts'
import DiscoverySectionNav from '../components/DiscoverySectionNav.vue'
import '../database-discovery.css'

const router = useRouter()
const items = ref<readonly DifferenceHistoryItem[]>([])
const page = ref(1)
const total = ref(0)
const profileId = ref<number | undefined>()
const databaseSourceId = ref<number | undefined>()
const filterOptions = ref<RunFilterOptions>({ profiles: [], databaseSources: [] })
const loading = ref(false)
const initialized = ref(false)
const error = ref('')
let controller = new AbortController()
let requestId = 0

const message = (value: unknown) =>
  value instanceof ApiError ? value.message : '无法加载发现差异历史。'
const format = (value: string) => new Date(value).toLocaleString('zh-CN')
const providerLabel = (value: DifferenceHistoryItem['providerType']) =>
  value === 'PostgreSql' ? 'PostgreSQL' : value === 'SqlServer' ? 'SQL Server' : 'Oracle'

async function load(): Promise<void> {
  controller.abort()
  controller = new AbortController()
  const currentRequestId = ++requestId
  loading.value = true
  error.value = ''
  try {
    const result = await listDifferences(
      page.value,
      20,
      profileId.value,
      databaseSourceId.value,
      controller.signal,
    )
    if (currentRequestId !== requestId) return
    items.value = result.items
    total.value = result.total
  } catch (value) {
    if (!(value instanceof DOMException && value.name === 'AbortError')) {
      error.value = message(value)
      if (initialized.value) ElMessage.error(error.value)
    }
  } finally {
    if (currentRequestId === requestId) {
      loading.value = false
      initialized.value = true
    }
  }
}

function handleFilterChange(): void {
  page.value = 1
  void load()
}

function openDifference(item: DifferenceHistoryItem): void {
  void router.push({
    name: 'database-discovery-difference',
    params: { id: String(item.id) },
  })
}

async function initialize(): Promise<void> {
  try {
    filterOptions.value = await getRunFilterOptions()
  } catch (value) {
    ElMessage.error(message(value))
  }
  await load()
}

onMounted(initialize)
onBeforeUnmount(() => {
  requestId += 1
  controller.abort()
})
</script>

<template>
  <main class="discovery-page skh-page">
    <header class="discovery-page__header skh-page-header">
      <div>
        <small class="discovery-eyebrow">数据库 / 数据库发现</small>
        <h1>差异审查</h1>
        <p>直接进入任一兼容快照比较结果，审查外部数据库结构变化。</p>
      </div>
    </header>
    <DiscoverySectionNav />
    <el-alert title="同步边界" type="info" :closable="false" show-icon>
      当前仅用于审查外部数据库结构变化，尚未同步到数据库知识。结构同步将在手工同步流程中完成。
    </el-alert>
    <section class="discovery-filters skh-filter-bar" aria-label="发现差异筛选">
      <el-select
        v-model="profileId"
        clearable
        placeholder="连接配置：全部"
        @change="handleFilterChange"
      >
        <el-option
          v-for="item in filterOptions.profiles"
          :key="item.id"
          :label="item.name"
          :value="item.id"
        />
      </el-select>
      <el-select
        v-model="databaseSourceId"
        clearable
        placeholder="数据库来源：全部"
        @change="handleFilterChange"
      >
        <el-option
          v-for="item in filterOptions.databaseSources"
          :key="item.id"
          :label="item.name"
          :value="item.id"
        />
      </el-select>
    </section>

    <LoadingState v-if="loading && !initialized" message="正在读取发现差异…" />
    <ErrorState
      v-else-if="error && !initialized"
      title="发现差异加载失败"
      :message="error"
      @retry="load"
    />
    <section v-else class="discovery-table-section skh-table-section" :aria-busy="loading">
      <template v-if="items.length === 0">
        <EmptyState
          title="暂无可审查差异"
          description="首次发现会建立基线；后续兼容范围的发现完成后，才会产生可比较差异。"
        />
        <div class="discovery-empty-actions">
          <el-button @click="router.push({ name: 'database-discovery-runs' })"
            >前往发现运行</el-button
          >
        </div>
      </template>
      <el-table
        v-else
        :data="items"
        row-key="id"
        class="discovery-table skh-data-table skh-data-table--comfortable"
      >
        <el-table-column prop="id" label="差异 ID" width="90" />
        <el-table-column prop="profileName" label="连接配置" min-width="150" />
        <el-table-column prop="databaseSourceName" label="数据库来源" min-width="150" />
        <el-table-column label="数据库类型" width="110">
          <template #default="{ row }">{{ providerLabel(row.providerType) }}</template>
        </el-table-column>
        <el-table-column label="快照比较" min-width="190">
          <template #default="{ row }">
            <small>基线 {{ row.baseSnapshotId ?? '无' }} → 目标 {{ row.targetSnapshotId }}</small>
          </template>
        </el-table-column>
        <el-table-column label="创建时间" min-width="170">
          <template #default="{ row }">{{ format(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column label="新增" width="78">
          <template #default="{ row }">{{ row.summaryCounts.added }}</template>
        </el-table-column>
        <el-table-column label="变化" width="78">
          <template #default="{ row }">{{ row.summaryCounts.changed }}</template>
        </el-table-column>
        <el-table-column label="来源中未发现" width="120">
          <template #default="{ row }">{{ row.summaryCounts.missingFromSource }}</template>
        </el-table-column>
        <el-table-column label="未变化" width="88">
          <template #default="{ row }">{{ row.summaryCounts.unchanged }}</template>
        </el-table-column>
        <el-table-column label="操作" width="120" fixed="right">
          <template #default="{ row }">
            <el-button size="small" type="primary" plain @click="openDifference(row)"
              >查看差异</el-button
            >
          </template>
        </el-table-column>
      </el-table>
      <footer v-if="total > 0" class="discovery-pagination skh-pagination">
        <span>{{ (page - 1) * 20 + 1 }}–{{ Math.min(page * 20, total) }} / {{ total }}</span>
        <el-pagination
          v-model:current-page="page"
          :total="total"
          :page-size="20"
          background
          layout="prev,pager,next"
          @current-change="load"
        />
      </footer>
      <p v-if="error" class="discovery-inline-error" role="alert">刷新失败：{{ error }}</p>
    </section>
  </main>
</template>
