<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { useRouter } from 'vue-router'
import { ApiError } from '../../../api/errors/ApiError'
import { useActorStore } from '../../../app/stores/actor'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import { getRunFilterOptions, listSnapshots } from '../api/databaseDiscoveryApi'
import type { RunFilterOptions, SnapshotHistoryItem } from '../api/databaseDiscoveryContracts'
import DiscoverySectionNav from '../components/DiscoverySectionNav.vue'
import '../database-discovery.css'

const actorStore = useActorStore()
const router = useRouter()
const items = ref<readonly SnapshotHistoryItem[]>([])
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
  value instanceof ApiError ? value.message : '无法加载发现快照历史。'
const format = (value: string) => new Date(value).toLocaleString('zh-CN')
const providerLabel = (value: SnapshotHistoryItem['providerType']) =>
  value === 'PostgreSql' ? 'PostgreSQL' : value === 'SqlServer' ? 'SQL Server' : 'Oracle'

async function load(): Promise<void> {
  controller.abort()
  controller = new AbortController()
  const currentRequestId = ++requestId
  loading.value = true
  error.value = ''
  try {
    const result = await listSnapshots(
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

function openSnapshot(item: SnapshotHistoryItem): void {
  void router.push({
    name: 'database-discovery-snapshot',
    params: { id: String(item.id) },
    query: item.differenceId ? { differenceId: String(item.differenceId) } : {},
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
        <h1>发现快照</h1>
        <p>直接查看每次成功发现生成的不可变数据库结构快照。</p>
      </div>
    </header>
    <DiscoverySectionNav />
    <el-alert title="可见性提示" type="warning" :closable="false" show-icon>
      快照只代表连接账号对配置范围的可见元数据，不证明不可见范围内没有其他对象（DBDISC-GAP-004）。
    </el-alert>
    <section class="discovery-filters skh-filter-bar" aria-label="发现快照筛选">
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

    <LoadingState v-if="loading && !initialized" message="正在读取发现快照…" />
    <ErrorState
      v-else-if="error && !initialized"
      title="发现快照加载失败"
      :message="error"
      @retry="load"
    />
    <section v-else class="discovery-table-section skh-table-section" :aria-busy="loading">
      <template v-if="items.length === 0">
        <EmptyState
          title="暂无发现快照"
          description="完成一次数据库发现后，可在这里查看发现的数据库结构。"
        />
        <div class="discovery-empty-actions">
          <el-button
            v-if="actorStore.isAdministrator"
            @click="router.push({ name: 'database-discovery-connections' })"
            >前往连接配置</el-button
          >
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
        <el-table-column prop="id" label="快照 ID" width="92" />
        <el-table-column prop="profileName" label="连接配置" min-width="150" />
        <el-table-column prop="databaseSourceName" label="数据库来源" min-width="150" />
        <el-table-column label="数据库类型" width="110">
          <template #default="{ row }">{{ providerLabel(row.providerType) }}</template>
        </el-table-column>
        <el-table-column label="捕获时间" min-width="170">
          <template #default="{ row }">{{ format(row.capturedAt) }}</template>
        </el-table-column>
        <el-table-column label="范围" min-width="210" show-overflow-tooltip>
          <template #default="{ row }">
            <span class="technical-text">{{ row.includedSchemas.join(', ') }}</span
            ><br />
            <small>范围代次 {{ row.scopeGenerationId }}</small>
          </template>
        </el-table-column>
        <el-table-column label="结构统计" min-width="150">
          <template #default="{ row }"
            >{{ row.counts.objects }} 对象 / {{ row.counts.columns }} 字段</template
          >
        </el-table-column>
        <el-table-column label="比较关系" min-width="190">
          <template #default="{ row }">
            <small
              >基线 {{ row.baseSnapshotId ?? '无' }} · 差异 {{ row.differenceId ?? '—' }}</small
            >
          </template>
        </el-table-column>
        <el-table-column label="操作" width="120" fixed="right">
          <template #default="{ row }">
            <el-button size="small" type="primary" plain @click="openSnapshot(row)"
              >查看快照</el-button
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
