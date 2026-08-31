<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Close } from '@element-plus/icons-vue'
import { useOverlayStore } from '../../../app/stores/overlays'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import DiscoverySectionNav from '../components/DiscoverySectionNav.vue'
import { getDifference, getDifferenceEntries } from '../api/databaseDiscoveryApi'
import type {
  DifferenceEntry,
  DifferenceScalarValue,
  DifferenceState,
  DifferenceSummary,
  DiscoveryEntityKind,
} from '../api/databaseDiscoveryContracts'
import '../database-discovery.css'
const route = useRoute()
const router = useRouter()
const overlayStore = useOverlayStore()
const id = computed(() => Number(route.params.id))
const summary = ref<DifferenceSummary | null>(null)
const entries = ref<readonly DifferenceEntry[]>([])
const total = ref(0)
const page = ref(1)
const state = ref<DifferenceState>('Changed')
const entityKind = ref<DiscoveryEntityKind | ''>('')
const schema = ref('')
const search = ref('')
const selected = ref<DifferenceEntry | null>(null)
const selectedIdentities = ref<ReadonlySet<string>>(new Set())
const loading = ref(false)
const error = ref('')
let controller = new AbortController()
let requestId = 0
const differenceDrawerOpen = computed(
  () => overlayStore.currentDrawer?.kind === 'database-discovery-difference-entry',
)
async function load(): Promise<void> {
  controller.abort()
  controller = new AbortController()
  const currentRequestId = ++requestId
  loading.value = true
  error.value = ''
  try {
    if (!summary.value) {
      const head = await getDifference(id.value, controller.signal)
      if (currentRequestId !== requestId) return
      summary.value = head
      if (head.summaryCounts.changed === 0) {
        if (head.summaryCounts.added > 0) state.value = 'Added'
        else if (head.summaryCounts.missingFromSource > 0) state.value = 'MissingFromSource'
        else if (head.summaryCounts.unchanged > 0) state.value = 'Unchanged'
      }
    }
    const result = await getDifferenceEntries(
      id.value,
      state.value,
      page.value,
      entityKind.value,
      schema.value,
      search.value,
      controller.signal,
    )
    if (currentRequestId !== requestId) return
    entries.value = result.items
    total.value = result.total
  } catch (e) {
    if (!(e instanceof DOMException && e.name === 'AbortError') && currentRequestId === requestId) {
      error.value = '无法加载发现差异。'
      if (summary.value) ElMessage.error(error.value)
    }
  } finally {
    if (currentRequestId === requestId) loading.value = false
  }
}
function selectState(value: DifferenceState): void {
  state.value = value
  page.value = 1
  void load()
}
function applyFilters(): void {
  page.value = 1
  void load()
}
function open(item: DifferenceEntry): void {
  selected.value = item
  overlayStore.openDrawer({
    kind: 'database-discovery-difference-entry',
    id: item.id,
    mode: 'read',
    payload: { logicalIdentity: item.logicalIdentity },
  })
}
const stateLabel: Record<DifferenceState, string> = {
  Added: '新增',
  Changed: '已变化',
  MissingFromSource: '来源中未发现',
  Unchanged: '未变化',
}
const entityKindLabel: Record<DiscoveryEntityKind, string> = {
  Schema: '架构',
  DatabaseObject: '数据库对象',
  Column: '字段',
  PrimaryKey: '主键',
  ForeignKey: '外键',
  UniqueConstraint: '唯一约束',
  Index: '索引',
  Sequence: '序列',
}
const entityKindOptions = (Object.keys(entityKindLabel) as DiscoveryEntityKind[]).map((value) => ({
  value,
  label: entityKindLabel[value],
}))
const differenceStateLabel = (value: DifferenceState) => stateLabel[value]
const differenceEntityLabel = (value: DiscoveryEntityKind) => entityKindLabel[value]
const differenceFieldLabels: Readonly<Record<string, string>> = {
  name: '名称',
  schemaName: '架构',
  objectName: '对象',
  objectType: '对象类型',
  databaseComment: '注释',
  sourceOrdinal: '字段顺序',
  nativeDataType: '数据类型',
  isNullable: '可空',
  defaultExpression: '默认值',
  isPrimaryKey: '主键',
  columnLogicalIdentities: '字段',
  referencedObjectLogicalIdentity: '引用对象',
  referencedColumnLogicalIdentities: '引用字段',
  updateRule: '更新规则',
  deleteRule: '删除规则',
  nativeIndexKind: '索引类型',
  isUnique: '唯一',
  keyParts: '键字段',
  nonKeyParts: '包含字段',
  nativePredicate: '筛选条件',
  backingConstraintLogicalIdentity: '支撑约束',
  incrementValue: '步长',
  minimumValue: '最小值',
  maximumValue: '最大值',
  cacheSize: '缓存大小',
  isCyclic: '循环',
  isOrdered: '有序',
  startValue: '起始值',
  referenceOnly: '仅引用',
  受保护的内部字段: '受保护的内部字段',
}
const differenceFieldLabel = (value: string) => differenceFieldLabels[value] ?? '其他字段'
const selectedCount = computed(() => selectedIdentities.value.size)
function isSelected(item: DifferenceEntry): boolean {
  return selectedIdentities.value.has(item.logicalIdentity)
}
function toggleSelection(item: DifferenceEntry, checked: string | number | boolean): void {
  const next = new Set(selectedIdentities.value)
  if (checked) next.add(item.logicalIdentity)
  else next.delete(item.logicalIdentity)
  selectedIdentities.value = next
}
function showPlanBoundary(): void {
  ElMessage.info('已保留当前审查选择；手工同步计划将在后续任务提供，本页不会应用任何变更。')
}
function closeDifferenceDrawer(): void {
  if (differenceDrawerOpen.value) overlayStore.closeDrawer()
}
function formatValue(value: DifferenceScalarValue | unknown): string {
  if (value === null) return '无'
  if (typeof value === 'boolean') return value ? '是' : '否'
  if (typeof value === 'string') return value
  if (typeof value === 'number' && Number.isFinite(value)) return String(value)
  return '已隐藏'
}
onMounted(load)
onBeforeUnmount(() => {
  requestId += 1
  controller.abort()
  closeDifferenceDrawer()
})
</script>
<template>
  <main class="discovery-page skh-page">
    <header class="discovery-page__header skh-page-header">
      <div>
        <nav class="discovery-breadcrumb" aria-label="面包屑">
          <RouterLink :to="{ name: 'database-discovery-runs' }">数据库发现</RouterLink>
          <span>/</span>
          <RouterLink :to="{ name: 'database-discovery-differences' }">差异审查</RouterLink>
          <span>/</span>
          <strong>Difference #{{ id }}</strong>
        </nav>
        <h1>差异 #{{ id }}</h1>
        <p>审查结构变化；本页面不会应用或同步任何变更。</p>
      </div>
      <div class="skh-page-header__actions">
        <el-button
          v-if="summary"
          @click="
            router.push({
              name: 'database-discovery-snapshot',
              params: { id: String(summary.targetSnapshotId) },
            })
          "
          >打开目标快照</el-button
        >
      </div>
    </header>
    <DiscoverySectionNav />
    <el-alert title="重要解释" type="warning" :closable="false" show-icon
      >“来源中未发现”仅表示目标快照在当前可见范围内未发现该身份，不等同于已删除；权限变化可能造成假缺失（DBDISC-GAP-004）。</el-alert
    >
    <LoadingState v-if="loading && !summary" message="正在读取发现差异…" />
    <ErrorState
      v-else-if="error && !summary"
      title="发现差异加载失败"
      :message="error"
      @retry="load"
    />
    <section v-if="summary" class="discovery-diff-cards" aria-label="差异状态筛选">
      <button
        :class="{ 'is-active': state === 'Added' }"
        :aria-pressed="state === 'Added'"
        @click="selectState('Added')"
      >
        <small>新增</small><strong>{{ summary.summaryCounts.added }}</strong>
      </button>
      <button
        :class="{ 'is-active': state === 'Changed' }"
        :aria-pressed="state === 'Changed'"
        @click="selectState('Changed')"
      >
        <small>已变化</small><strong>{{ summary.summaryCounts.changed }}</strong>
      </button>
      <button
        :class="{ 'is-active': state === 'MissingFromSource' }"
        :aria-pressed="state === 'MissingFromSource'"
        @click="selectState('MissingFromSource')"
      >
        <small>来源中未发现</small><strong>{{ summary.summaryCounts.missingFromSource }}</strong>
      </button>
      <button
        :class="{ 'is-active': state === 'Unchanged' }"
        :aria-pressed="state === 'Unchanged'"
        @click="selectState('Unchanged')"
      >
        <small>未变化</small><strong>{{ summary.summaryCounts.unchanged }}</strong>
      </button>
    </section>
    <dl v-if="summary" class="discovery-metadata discovery-panel">
      <div>
        <dt>基线快照</dt>
        <dd>{{ summary.baseSnapshotId ?? '无兼容基线' }}</dd>
      </div>
      <div>
        <dt>目标快照</dt>
        <dd>{{ summary.targetSnapshotId }}</dd>
      </div>
      <div>
        <dt>范围代次</dt>
        <dd>{{ summary.scopeGenerationId }}</dd>
      </div>
      <div>
        <dt>差异算法</dt>
        <dd>v{{ summary.algorithmVersion }}</dd>
      </div>
      <div>
        <dt>内容 SHA-256</dt>
        <dd class="technical-text">{{ summary.contentSha256 }}</dd>
      </div>
    </dl>
    <p v-if="summary?.baseSnapshotId === null" class="discovery-result">
      这是当前兼容范围的第一份快照，没有可安全比较的基线；核心身份按“新增”展示。
    </p>
    <p v-if="state === 'Added'" class="discovery-hint">
      新增表示来源数据库发现了新的结构身份，不表示必须新增到知识库。
    </p>
    <p v-if="state === 'MissingFromSource'" class="discovery-hint">
      来源中未发现不表示已经删除，也不会自动删除、归档或修改知识库对象。
    </p>
    <section v-if="summary" class="discovery-panel" :aria-busy="loading">
      <header>
        <h2>{{ stateLabel[state] }}</h2>
        <div class="discovery-filters skh-filter-bar" aria-label="差异条目筛选">
          <el-select
            v-model="entityKind"
            clearable
            placeholder="实体类型：全部"
            @change="applyFilters"
            ><el-option
              v-for="kind in entityKindOptions"
              :key="kind.value"
              :label="kind.label"
              :value="kind.value" /></el-select
          ><el-input
            v-model="schema"
            placeholder="架构（Schema）"
            clearable
            @keyup.enter="applyFilters"
          /><el-input
            v-model="search"
            placeholder="搜索名称"
            clearable
            @keyup.enter="applyFilters"
          /><el-button @click="applyFilters">查询</el-button>
        </div>
      </header>
      <div class="discovery-selection-bar">
        <span
          >已选择 <strong>{{ selectedCount }}</strong> 项；选择仅保留在当前审查会话。</span
        ><el-button type="primary" plain :disabled="selectedCount === 0" @click="showPlanBoundary"
          >下一步：手工同步计划</el-button
        >
      </div>
      <EmptyState
        v-if="entries.length === 0"
        title="当前状态下没有差异条目"
        description="可切换状态或调整实体、架构与关键词筛选。"
      />
      <el-table
        v-else
        :data="entries"
        row-key="logicalIdentity"
        class="skh-data-table skh-data-table--dense"
      >
        <el-table-column label="选择" width="62" align="center"
          ><template #default="{ row }"
            ><el-checkbox
              :model-value="isSelected(row)"
              :disabled="row.state === 'Unchanged'"
              :aria-label="`选择 ${row.displayName}`"
              @change="toggleSelection(row, $event)" /></template
        ></el-table-column>
        <el-table-column label="实体" width="120"
          ><template #default="{ row }">{{
            differenceEntityLabel(row.entityKind)
          }}</template></el-table-column
        >
        <el-table-column
          prop="schemaName"
          label="架构（Schema）"
          min-width="130"
          show-overflow-tooltip
        /><el-table-column
          prop="objectName"
          label="对象"
          min-width="150"
          show-overflow-tooltip
        /><el-table-column
          prop="childName"
          label="子项"
          min-width="150"
          show-overflow-tooltip
        /><el-table-column
          prop="displayName"
          label="显示名称"
          min-width="180"
          show-overflow-tooltip
        />
        <el-table-column label="状态" width="130"
          ><template #default="{ row }"
            ><el-tag
              :type="
                row.state === 'Added'
                  ? 'success'
                  : row.state === 'Changed'
                    ? 'warning'
                    : row.state === 'MissingFromSource'
                      ? 'danger'
                      : 'info'
              "
              >{{ differenceStateLabel(row.state) }}</el-tag
            ></template
          ></el-table-column
        >
        <el-table-column label="操作" width="130"
          ><template #default="{ row }"
            ><el-button size="small" @click="open(row)">查看前后值</el-button></template
          ></el-table-column
        >
      </el-table>
      <footer v-if="total > 0" class="discovery-pagination skh-pagination">
        <span>{{ (page - 1) * 50 + 1 }}–{{ Math.min(page * 50, total) }} / {{ total }}</span
        ><el-pagination
          v-model:current-page="page"
          :page-size="50"
          :total="total"
          background
          layout="prev,pager,next"
          @current-change="load"
        />
      </footer>
      <p v-if="error" class="discovery-inline-error" role="alert">刷新失败：{{ error }}</p>
    </section>
    <Teleport v-if="differenceDrawerOpen && selected" defer to="#drawer-feature-content">
      <section class="discovery-drawer" aria-labelledby="database-discovery-difference-title">
        <header class="discovery-drawer__header">
          <div>
            <small
              >{{ differenceEntityLabel(selected.entityKind) }} ·
              {{ differenceStateLabel(selected.state) }}</small
            >
            <h2 id="database-discovery-difference-title">{{ selected.displayName }}</h2>
            <p>
              {{ selected.schemaName ?? '—' }} · {{ selected.objectName ?? '—' }} ·
              {{ selected.childName ?? selected.displayName }}
            </p>
          </div>
          <el-button
            text
            circle
            :icon="Close"
            aria-label="关闭字段级差异"
            @click="closeDifferenceDrawer"
          />
        </header>
        <EmptyState
          v-if="selected.changes.length === 0"
          title="没有字段级变化"
          description="该结构身份本身未发生可展示的字段变化。"
        />
        <el-table v-else :data="selected.changes" class="skh-data-table skh-data-table--comfortable"
          ><el-table-column label="字段" min-width="160" show-overflow-tooltip
            ><template #default="{ row }">{{
              differenceFieldLabel(row.field)
            }}</template></el-table-column
          ><el-table-column label="之前" min-width="230" show-overflow-tooltip
            ><template #default="{ row }"
              ><span class="technical-text">{{ formatValue(row.before) }}</span></template
            ></el-table-column
          ><el-table-column label="之后" min-width="230" show-overflow-tooltip
            ><template #default="{ row }"
              ><span class="technical-text">{{ formatValue(row.after) }}</span></template
            ></el-table-column
          ></el-table
        >
      </section>
    </Teleport>
  </main>
</template>
