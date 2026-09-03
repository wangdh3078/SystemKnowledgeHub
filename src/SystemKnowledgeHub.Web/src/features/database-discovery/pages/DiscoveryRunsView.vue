<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useRoute } from 'vue-router'
import { useActorStore } from '../../../app/stores/actor'
import { ApiError } from '../../../api/errors/ApiError'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import SkhPagination from '../../../components/data-display/SkhPagination.vue'
import DiscoverySectionNav from '../components/DiscoverySectionNav.vue'
import { cancelRun, getRunFilterOptions, listRuns } from '../api/databaseDiscoveryApi'
import type { DiscoveryRun, RunFilterOptions } from '../api/databaseDiscoveryContracts'
import '../database-discovery.css'
const actorStore = useActorStore()
const route = useRoute()
const items = ref<readonly DiscoveryRun[]>([])
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const profileId = ref<number | undefined>(
  typeof route.query.profileId === 'string' ? Number(route.query.profileId) : undefined,
)
const databaseSourceId = ref<number | undefined>()
const filterOptions = ref<RunFilterOptions>({ profiles: [], databaseSources: [] })
const loading = ref(false)
const initialized = ref(false)
const error = ref('')
let timer: number | undefined
let controller: AbortController | undefined
let disposed = false
let requestId = 0
const hasActive = computed(() =>
  items.value.some((x) => x.status === 'Queued' || x.status === 'Running'),
)
const msg = (e: unknown) => (e instanceof ApiError ? e.message : '无法加载发现运行。')
const format = (v: string | null) => (v ? new Date(v).toLocaleString('zh-CN') : '—')
const statusLabel: Record<DiscoveryRun['status'], string> = {
  Queued: '排队中',
  Running: '运行中',
  Succeeded: '成功',
  Failed: '失败',
  Cancelled: '已取消',
}
const runStatusLabel = (value: DiscoveryRun['status']) => statusLabel[value]
const providerLabel = (value: DiscoveryRun['providerType']) =>
  value === 'PostgreSql' ? 'PostgreSQL' : value === 'SqlServer' ? 'SQL Server' : 'Oracle'
const progressLabel = (run: DiscoveryRun) =>
  run.status === 'Queued'
    ? '等待后台发现任务处理'
    : run.status === 'Running'
      ? '正在发现数据库结构'
      : '—'
const duration = (run: DiscoveryRun) => {
  if (!run.startedAt) return '—'
  const end = run.completedAt ? new Date(run.completedAt).getTime() : Date.now()
  return `${Math.max(0, Math.round((end - new Date(run.startedAt).getTime()) / 1000))} 秒`
}
async function load(): Promise<void> {
  if (disposed) return
  if (timer) window.clearTimeout(timer)
  controller?.abort()
  controller = new AbortController()
  const currentRequestId = ++requestId
  loading.value = true
  error.value = ''
  try {
    const result = await listRuns(
      page.value,
      pageSize.value,
      profileId.value,
      databaseSourceId.value,
      controller.signal,
    )
    if (currentRequestId !== requestId || disposed) return
    items.value = result.items
    total.value = result.total
  } catch (e) {
    if (!(e instanceof DOMException && e.name === 'AbortError') && currentRequestId === requestId) {
      error.value = msg(e)
      if (initialized.value) ElMessage.error(error.value)
    }
  } finally {
    if (currentRequestId === requestId && !disposed) {
      loading.value = false
      initialized.value = true
      schedule()
    }
  }
}
function schedule(): void {
  if (timer) window.clearTimeout(timer)
  if (!disposed && hasActive.value) timer = window.setTimeout(load, 2500)
}
function handleFilterChange(): void {
  page.value = 1
  void load()
}
function handlePageSizeChange(value: number): void {
  pageSize.value = value
  page.value = 1
  void load()
}
async function cancel(run: DiscoveryRun): Promise<void> {
  try {
    await ElMessageBox.confirm(
      '确认取消该发现任务？运行取消后不会产生成功快照或差异。',
      '取消发现',
      { type: 'warning' },
    )
    await cancelRun(run)
    ElMessage.success('取消请求已提交。')
    await load()
  } catch (e) {
    if (e !== 'cancel' && e !== 'close') ElMessage.error(msg(e))
  }
}
function rowClassName({ row }: { row: DiscoveryRun }): string {
  return String(row.id) === route.query.runId ? 'discovery-row--focused' : ''
}
async function initialize(): Promise<void> {
  try {
    filterOptions.value = await getRunFilterOptions()
  } catch (e) {
    ElMessage.error(msg(e))
  }
  await load()
}
onMounted(initialize)
onBeforeUnmount(() => {
  disposed = true
  requestId += 1
  controller?.abort()
  if (timer) window.clearTimeout(timer)
})
</script>
<template>
  <main class="discovery-page skh-page">
    <header class="discovery-page__header skh-page-header">
      <div>
        <small class="discovery-eyebrow">数据库 / 数据库发现</small>
        <h1>发现运行</h1>
        <p>队列与后台任务状态每 2.5 秒自动刷新，进入终态后停止轮询。</p>
      </div>
    </header>
    <DiscoverySectionNav />
    <el-alert title="可见性提示" type="warning" :closable="false" show-icon>
      发现结果只代表当前账号的可见范围；权限变化可能影响差异解释（DBDISC-GAP-004）。
    </el-alert>
    <section class="discovery-filters skh-filter-bar" aria-label="发现运行筛选">
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

    <LoadingState v-if="loading && !initialized" message="正在读取发现运行…" />
    <ErrorState
      v-else-if="error && !initialized"
      title="发现运行加载失败"
      :message="error"
      @retry="load"
    />
    <section v-else class="discovery-table-section skh-table-section" :aria-busy="loading">
      <EmptyState
        v-if="items.length === 0"
        title="没有找到发现运行"
        description="调整筛选条件，或由管理员从连接配置开始一次发现。"
      />
      <el-table
        v-else
        :data="items"
        row-key="id"
        class="discovery-table skh-data-table skh-data-table--comfortable"
        :row-class-name="rowClassName"
      >
        <el-table-column prop="id" label="运行 ID" width="86" />
        <el-table-column
          prop="profileName"
          label="连接配置"
          min-width="150"
          show-overflow-tooltip
        />
        <el-table-column label="数据库类型" width="108"
          ><template #default="{ row }">{{
            providerLabel(row.providerType)
          }}</template></el-table-column
        >
        <el-table-column
          prop="databaseSourceName"
          label="数据库来源"
          min-width="140"
          show-overflow-tooltip
        />
        <el-table-column label="状态" width="104">
          <template #default="{ row }"
            ><el-tag
              :type="
                row.status === 'Succeeded'
                  ? 'success'
                  : row.status === 'Failed'
                    ? 'danger'
                    : row.status === 'Cancelled'
                      ? 'info'
                      : 'warning'
              "
              >{{ runStatusLabel(row.status) }}</el-tag
            ></template
          >
        </el-table-column>
        <el-table-column label="时间 / 耗时" min-width="220"
          ><template #default="{ row }"
            >排队 {{ format(row.queuedAt) }}<br /><small
              >开始 {{ format(row.startedAt) }} · 完成 {{ format(row.completedAt) }}</small
            ><br /><small>耗时 {{ duration(row) }}</small></template
          ></el-table-column
        >
        <el-table-column label="运行信息" min-width="190"
          ><template #default="{ row }"
            >{{ progressLabel(row) }}<br /><small
              >范围代次 {{ row.scopeGenerationId ?? '—' }}</small
            ></template
          ></el-table-column
        >
        <el-table-column label="产物" min-width="220"
          ><template #default="{ row }"
            ><small
              >基线 {{ row.baseSnapshotId ?? '—' }} · 快照 {{ row.snapshotId ?? '—' }} · 差异
              {{ row.differenceId ?? '—' }}</small
            ></template
          ></el-table-column
        >
        <el-table-column label="对象统计" min-width="150"
          ><template #default="{ row }">{{
            row.objectCounts
              ? `${row.objectCounts.objects} 对象 / ${row.objectCounts.columns} 字段`
              : '—'
          }}</template></el-table-column
        >
        <el-table-column label="结果" min-width="220" show-overflow-tooltip
          ><template #default="{ row }"
            ><span v-if="row.errorSummary" class="discovery-error"
              >{{ row.errorCode }} · {{ row.errorSummary }}</span
            ><span v-else>—</span></template
          ></el-table-column
        >
        <el-table-column
          v-if="actorStore.isAdministrator && hasActive"
          label="操作"
          width="90"
          fixed="right"
        >
          <template #default="{ row }">
            <el-button
              v-if="
                actorStore.isAdministrator && (row.status === 'Queued' || row.status === 'Running')
              "
              size="small"
              plain
              @click="cancel(row)"
              >取消</el-button
            >
          </template>
        </el-table-column>
      </el-table>
      <SkhPagination
        v-model:current-page="page"
        v-model:page-size="pageSize"
        class="discovery-pagination"
        :total="total"
        aria-label="发现运行分页"
        @current-change="load"
        @size-change="handlePageSizeChange"
      />
      <p v-if="error" class="discovery-inline-error" role="alert">刷新失败：{{ error }}</p>
    </section>
  </main>
</template>
