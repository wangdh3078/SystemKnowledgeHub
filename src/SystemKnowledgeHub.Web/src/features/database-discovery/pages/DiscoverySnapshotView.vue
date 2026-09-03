<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Close } from '@element-plus/icons-vue'
import { useOverlayStore } from '../../../app/stores/overlays'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import SkhPagination from '../../../components/data-display/SkhPagination.vue'
import DiscoverySectionNav from '../components/DiscoverySectionNav.vue'
import {
  getSnapshotObjectReview,
  getSnapshotObjects,
  getSnapshotSchemas,
  getSnapshotSequences,
  getSnapshotSummary,
} from '../api/databaseDiscoveryApi'
import type {
  DiscoveryObjectType,
  SnapshotObject,
  SnapshotObjectHeaderData,
  SnapshotColumn,
  SnapshotConstraint,
  SnapshotIndex,
  SnapshotSchema,
  SnapshotSequence,
  SnapshotSummary,
} from '../api/databaseDiscoveryContracts'
import '../database-discovery.css'
const route = useRoute()
const overlayStore = useOverlayStore()
const id = computed(() => Number(route.params.id))
const summary = ref<SnapshotSummary | null>(null)
const schemas = ref<readonly SnapshotSchema[]>([])
const objects = ref<readonly SnapshotObject[]>([])
const sequences = ref<readonly SnapshotSequence[]>([])
const sequenceTotal = ref(0)
const sequencePage = ref(1)
const sequencePageSize = ref(20)
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
const schema = ref('')
const objectType = ref<DiscoveryObjectType | ''>('')
const search = ref('')
const detailObject = ref<SnapshotObjectHeaderData | null>(null)
const detailColumns = ref<readonly SnapshotColumn[]>([])
const detailConstraints = ref<readonly SnapshotConstraint[]>([])
const detailIndexes = ref<readonly SnapshotIndex[]>([])
const columnTotal = ref(0)
const constraintTotal = ref(0)
const indexTotal = ref(0)
const columnPage = ref(1)
const constraintPage = ref(1)
const indexPage = ref(1)
const detailPageSize = ref(20)
const selectedIdentity = ref('')
const loading = ref(false)
const error = ref('')
const detailLoading = ref(false)
const detailError = ref('')
let controller = new AbortController()
let detailController = new AbortController()
let requestId = 0
let detailRequestId = 0
const objectDrawerOpen = computed(
  () => overlayStore.currentDrawer?.kind === 'database-discovery-snapshot-object',
)
async function loadSchemaOptions(signal: AbortSignal): Promise<readonly SnapshotSchema[]> {
  const first = await getSnapshotSchemas(id.value, 1, '', signal)
  if (first.total <= first.items.length) return first.items
  const second = await getSnapshotSchemas(id.value, 2, '', signal)
  return [...first.items, ...second.items]
}
async function load(): Promise<void> {
  controller.abort()
  controller = new AbortController()
  const currentRequestId = ++requestId
  loading.value = true
  error.value = ''
  try {
    const [head, schemaItems, objectPage, sequenceResult] = await Promise.all([
      getSnapshotSummary(id.value, controller.signal),
      loadSchemaOptions(controller.signal),
      getSnapshotObjects(
        id.value,
        page.value,
        pageSize.value,
        schema.value,
        objectType.value,
        search.value,
        controller.signal,
      ),
      getSnapshotSequences(
        id.value,
        sequencePage.value,
        sequencePageSize.value,
        schema.value,
        search.value,
        controller.signal,
      ),
    ])
    if (currentRequestId !== requestId) return
    summary.value = head
    schemas.value = schemaItems
    objects.value = objectPage.items
    total.value = objectPage.total
    sequences.value = sequenceResult.items
    sequenceTotal.value = sequenceResult.total
  } catch (e) {
    if (!(e instanceof DOMException && e.name === 'AbortError') && currentRequestId === requestId) {
      error.value = '无法加载发现快照。'
      if (summary.value) ElMessage.error(error.value)
    }
  } finally {
    if (currentRequestId === requestId) loading.value = false
  }
}
async function open(item: SnapshotObject): Promise<void> {
  selectedIdentity.value = item.logicalIdentity
  detailObject.value = null
  detailColumns.value = []
  detailConstraints.value = []
  detailIndexes.value = []
  columnPage.value = 1
  constraintPage.value = 1
  indexPage.value = 1
  overlayStore.openDrawer({
    kind: 'database-discovery-snapshot-object',
    id: null,
    mode: 'read',
    payload: { logicalIdentity: item.logicalIdentity },
  })
  await loadDetail()
}
async function loadDetail(): Promise<void> {
  if (!selectedIdentity.value) return
  detailController.abort()
  detailController = new AbortController()
  const currentRequestId = ++detailRequestId
  const identity = selectedIdentity.value
  detailLoading.value = true
  detailError.value = ''
  try {
    const review = await getSnapshotObjectReview(
      id.value,
      identity,
      columnPage.value,
      constraintPage.value,
      indexPage.value,
      detailPageSize.value,
      detailController.signal,
    )
    if (selectedIdentity.value !== identity || currentRequestId !== detailRequestId) return
    detailObject.value = review.object
    detailColumns.value = review.columns.items
    columnTotal.value = review.columns.total
    detailConstraints.value = review.constraints.items
    constraintTotal.value = review.constraints.total
    detailIndexes.value = review.indexes.items
    indexTotal.value = review.indexes.total
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    if (currentRequestId === detailRequestId) detailError.value = '无法加载对象详情。'
  } finally {
    if (currentRequestId === detailRequestId) detailLoading.value = false
  }
}
const capabilityLabel: Record<string, string> = {
  Supported: '支持',
  NotSupported: '不支持',
  Unavailable: '不可用',
  NotApplicable: '不适用',
}
const capabilityNameLabel: Readonly<Record<string, string>> = {
  SupportsContainerDatabase: '容器数据库',
  SupportsFullDdl: '完整 DDL',
  SupportsIdentityColumns: '标识列',
  SupportsInvisibleColumns: '不可见列',
  SupportsMaterializedViews: '物化视图',
  SupportsPartitions: '分区',
  SupportsSequences: '序列',
  SupportsSynonyms: '同义词',
  SupportsTriggers: '触发器',
  SupportsComputedColumns: '计算列',
}
const capabilityReasonLabel: Readonly<Record<string, string>> = {
  CoreScopeExcluded: '当前范围不采集',
}
const providerLabel = (value: SnapshotSummary['providerType']) =>
  value === 'PostgreSql' ? 'PostgreSQL' : value === 'SqlServer' ? 'SQL Server' : 'Oracle'
const objectTypeLabel: Record<SnapshotObject['objectType'], string> = { Table: '表', View: '视图' }
const constraintLabel: Record<SnapshotConstraint['entityKind'], string> = {
  PrimaryKey: '主键',
  ForeignKey: '外键',
  UniqueConstraint: '唯一约束',
}
const snapshotObjectTypeLabel = (value: SnapshotObject['objectType']) => objectTypeLabel[value]
const snapshotConstraintLabel = (value: SnapshotConstraint['entityKind']) => constraintLabel[value]
const nullableBooleanLabel = (value: boolean | null) => (value === null ? '—' : value ? '是' : '否')
function applySnapshotFilters(): void {
  page.value = 1
  sequencePage.value = 1
  void load()
}
function objectPageSizeChanged(value: number): void {
  pageSize.value = value
  page.value = 1
  void load()
}
function sequencePageSizeChanged(value: number): void {
  sequencePageSize.value = value
  sequencePage.value = 1
  void load()
}
function detailPageSizeChanged(value: number): void {
  detailPageSize.value = value
  columnPage.value = 1
  constraintPage.value = 1
  indexPage.value = 1
  void loadDetail()
}
function closeObjectDrawer(): void {
  if (objectDrawerOpen.value) overlayStore.closeDrawer()
}
onMounted(load)
onBeforeUnmount(() => {
  requestId += 1
  detailRequestId += 1
  controller.abort()
  detailController.abort()
  closeObjectDrawer()
})
</script>
<template>
  <main class="discovery-page skh-page">
    <header class="discovery-page__header skh-page-header">
      <div>
        <nav class="discovery-breadcrumb" aria-label="面包屑">
          <RouterLink :to="{ name: 'database-discovery-runs' }">数据库发现</RouterLink>
          <span>/</span>
          <RouterLink :to="{ name: 'database-discovery-snapshots' }">发现快照</RouterLink>
          <span>/</span>
          <strong>快照 #{{ id }}</strong>
        </nav>
        <h1>快照 #{{ id }}</h1>
        <p>只读取有界摘要、分页列表和按需对象详情，不下载完整规范快照。</p>
      </div>
      <div class="skh-page-header__actions">
        <RouterLink class="el-button" :to="{ name: 'database-discovery-snapshots' }"
          >← 返回快照列表</RouterLink
        >
      </div>
    </header>
    <DiscoverySectionNav />
    <el-alert title="可见性提示" type="warning" :closable="false" show-icon>
      “完整”表示配置范围内的可见元数据完整，不表示账号能看到整个数据库（DBDISC-GAP-004）。
    </el-alert>
    <LoadingState v-if="loading && !summary" message="正在读取发现快照…" />
    <ErrorState
      v-else-if="error && !summary"
      title="发现快照加载失败"
      :message="error"
      @retry="load"
    />
    <section v-if="summary" class="discovery-summary">
      <div>
        <small>数据库类型</small
        ><strong>{{ providerLabel(summary.providerType) }} {{ summary.providerVersion }}</strong>
      </div>
      <div>
        <small>目标</small
        ><strong
          >{{ summary.currentDatabaseOrService
          }}<template v-if="summary.currentContainer">
            · {{ summary.currentContainer }}</template
          ></strong
        >
      </div>
      <div>
        <small>捕获时间</small
        ><strong>{{ new Date(summary.capturedAt).toLocaleString('zh-CN') }}</strong>
      </div>
      <div>
        <small>架构（Schema）/ 对象 / 字段</small
        ><strong
          >{{ summary.counts.schemas }} / {{ summary.counts.objects }} /
          {{ summary.counts.columns }}</strong
        >
      </div>
      <div>
        <small>约束 / 索引 / 序列</small
        ><strong
          >{{
            summary.counts.primaryKeys +
            summary.counts.foreignKeys +
            summary.counts.uniqueConstraints
          }}
          / {{ summary.counts.indexes }} / {{ summary.counts.sequences }}</strong
        >
      </div>
      <div>
        <small>格式 / 身份算法</small
        ><strong>v{{ summary.formatVersion }} / v{{ summary.identityAlgorithmVersion }}</strong>
      </div>
    </section>
    <section v-if="summary" class="discovery-panel">
      <header><h2>快照边界</h2></header>
      <dl class="discovery-metadata">
        <div>
          <dt>包含的架构</dt>
          <dd class="technical-text">{{ summary.includedSchemas.join(', ') }}</dd>
        </div>
        <div>
          <dt>内容 SHA-256</dt>
          <dd class="technical-text">{{ summary.contentSha256 }}</dd>
        </div>
        <div>
          <dt>范围代次</dt>
          <dd>{{ summary.scopeGenerationId }}</dd>
        </div>
        <div>
          <dt>范围指纹</dt>
          <dd class="technical-text">{{ summary.scopeFingerprint }}</dd>
        </div>
      </dl>
      <el-collapse class="discovery-capability-collapse">
        <el-collapse-item title="元数据采集能力" name="metadata-capabilities">
          <div class="discovery-capabilities">
            <el-tag
              v-for="item in summary.capabilities"
              :key="item.name"
              :type="
                item.state === 'Supported'
                  ? 'success'
                  : item.state === 'Unavailable'
                    ? 'warning'
                    : 'info'
              "
            >
              {{ capabilityNameLabel[item.name] ?? '其他能力' }}：{{
                item.reasonCode
                  ? (capabilityReasonLabel[item.reasonCode] ?? '当前不可采集')
                  : (capabilityLabel[item.state] ?? '未知')
              }}
            </el-tag>
            <span v-if="summary.capabilities.length === 0">当前数据库类型未报告可选采集能力。</span>
          </div>
        </el-collapse-item>
      </el-collapse>
    </section>
    <section v-if="summary" class="discovery-panel" :aria-busy="loading">
      <header>
        <h2>对象</h2>
        <div class="discovery-filters skh-filter-bar" aria-label="快照对象筛选">
          <el-select
            v-model="schema"
            clearable
            placeholder="架构（Schema）：全部"
            @change="applySnapshotFilters"
            ><el-option
              v-for="item in schemas"
              :key="item.logicalIdentity"
              :label="`${item.name} (${item.objectCount})`"
              :value="item.name" /></el-select
          ><el-select
            v-model="objectType"
            clearable
            placeholder="全部类型"
            @change="applySnapshotFilters"
            ><el-option label="表" value="Table" /><el-option
              label="视图"
              value="View" /></el-select
          ><el-input
            v-model="search"
            clearable
            placeholder="搜索对象"
            @keyup.enter="applySnapshotFilters"
          /><el-button @click="applySnapshotFilters">查询</el-button>
        </div>
      </header>
      <EmptyState
        v-if="objects.length === 0"
        title="没有找到对象"
        description="调整架构、对象类型或关键词筛选。"
      />
      <el-table
        v-else
        :data="objects"
        row-key="logicalIdentity"
        class="skh-data-table skh-data-table--dense"
        ><el-table-column prop="schemaName" label="架构（Schema）" /><el-table-column
          prop="name"
          label="对象"
          min-width="180"
        /><el-table-column label="类型"
          ><template #default="{ row }">{{
            snapshotObjectTypeLabel(row.objectType)
          }}</template></el-table-column
        ><el-table-column prop="columnCount" label="字段" /><el-table-column
          prop="constraintCount"
          label="约束"
        /><el-table-column prop="indexCount" label="索引" /><el-table-column label="操作"
          ><template #default="{ row }"
            ><el-button size="small" @click="open(row)">查看结构</el-button></template
          ></el-table-column
        ></el-table
      >
      <SkhPagination
        v-model:current-page="page"
        v-model:page-size="pageSize"
        class="discovery-pagination"
        :total="total"
        aria-label="快照对象分页"
        @current-change="load"
        @size-change="objectPageSizeChanged"
      />
      <p v-if="error" class="discovery-inline-error" role="alert">刷新失败：{{ error }}</p>
    </section>
    <section v-if="summary?.counts.sequences" class="discovery-panel">
      <header>
        <h2>序列</h2>
        <span>共 {{ sequenceTotal }} 个</span>
      </header>
      <el-table :data="sequences" class="skh-data-table skh-data-table--dense">
        <el-table-column prop="schemaName" label="架构（Schema）" min-width="140" />
        <el-table-column prop="name" label="名称" min-width="180" />
        <el-table-column prop="nativeDataType" label="数据库类型" min-width="140" />
        <el-table-column prop="startValue" label="起始值" />
        <el-table-column prop="incrementValue" label="步长" />
        <el-table-column prop="minimumValue" label="最小值" />
        <el-table-column prop="maximumValue" label="最大值" min-width="180" />
        <el-table-column prop="cacheSize" label="缓存" />
        <el-table-column label="循环"
          ><template #default="{ row }">{{
            nullableBooleanLabel(row.isCyclic)
          }}</template></el-table-column
        >
        <el-table-column label="有序"
          ><template #default="{ row }">{{
            nullableBooleanLabel(row.isOrdered)
          }}</template></el-table-column
        >
      </el-table>
      <SkhPagination
        v-model:current-page="sequencePage"
        v-model:page-size="sequencePageSize"
        class="discovery-pagination"
        :total="sequenceTotal"
        aria-label="快照序列分页"
        @current-change="load"
        @size-change="sequencePageSizeChanged"
      />
    </section>
    <Teleport v-if="objectDrawerOpen" defer to="#drawer-feature-content">
      <section class="discovery-drawer" aria-labelledby="database-discovery-object-title">
        <header class="discovery-drawer__header">
          <div>
            <small>发现快照</small>
            <h2 id="database-discovery-object-title">对象结构</h2>
          </div>
          <el-button
            text
            circle
            :icon="Close"
            aria-label="关闭对象结构"
            @click="closeObjectDrawer"
          />
        </header>
        <LoadingState v-if="detailLoading && !detailObject" message="正在读取对象结构…" />
        <ErrorState
          v-else-if="detailError && !detailObject"
          title="对象结构加载失败"
          :message="detailError"
          @retry="loadDetail"
        />
        <template v-else-if="detailObject">
          <h3 class="technical-text">
            {{ detailObject.schemaName }}.{{ detailObject.name }}
            <el-tag size="small">{{ snapshotObjectTypeLabel(detailObject.objectType) }}</el-tag>
          </h3>
          <p>{{ detailObject.databaseComment ?? '无数据库注释' }}</p>
          <section :aria-busy="detailLoading">
            <h3>字段</h3>
            <el-table :data="detailColumns" class="skh-data-table skh-data-table--dense"
              ><el-table-column prop="sourceOrdinal" label="#" width="60" /><el-table-column
                prop="name"
                label="字段" /><el-table-column label="类型" min-width="190"
                ><template #default="{ row }">{{
                  row.nativeDataType.declaration
                }}</template></el-table-column
              ><el-table-column label="可空"
                ><template #default="{ row }">{{
                  row.isNullable ? '是' : '否'
                }}</template></el-table-column
              ><el-table-column
                prop="defaultExpression"
                label="默认值"
                min-width="150"
                show-overflow-tooltip /><el-table-column
                prop="databaseComment"
                label="注释"
                min-width="160"
                show-overflow-tooltip
            /></el-table>
            <SkhPagination
              v-model:current-page="columnPage"
              :total="columnTotal"
              :page-size="detailPageSize"
              aria-label="对象字段分页"
              @current-change="loadDetail"
              @size-change="detailPageSizeChanged"
            />
            <h3>主键 / 外键 / 唯一约束</h3>
            <el-table :data="detailConstraints" class="skh-data-table skh-data-table--dense"
              ><el-table-column label="类型"
                ><template #default="{ row }">{{
                  snapshotConstraintLabel(row.entityKind)
                }}</template></el-table-column
              ><el-table-column
                prop="name"
                label="名称"
                min-width="190"
                show-overflow-tooltip
              /><el-table-column label="字段" min-width="170" show-overflow-tooltip
                ><template #default="{ row }">{{
                  row.columnNames.join(', ')
                }}</template></el-table-column
              ><el-table-column
                prop="referencedObjectName"
                label="引用对象"
                min-width="170"
                show-overflow-tooltip
              /><el-table-column label="更新 / 删除"
                ><template #default="{ row }"
                  >{{ row.updateRule ?? '—' }} / {{ row.deleteRule ?? '—' }}</template
                ></el-table-column
              ></el-table
            >
            <SkhPagination
              v-model:current-page="constraintPage"
              :total="constraintTotal"
              :page-size="detailPageSize"
              aria-label="对象约束分页"
              @current-change="loadDetail"
              @size-change="detailPageSizeChanged"
            />
            <h3>索引</h3>
            <el-table :data="detailIndexes" class="skh-data-table skh-data-table--dense"
              ><el-table-column
                prop="name"
                label="名称"
                min-width="180"
                show-overflow-tooltip /><el-table-column
                prop="nativeIndexKind"
                label="类型" /><el-table-column label="唯一"
                ><template #default="{ row }">{{
                  row.isUnique ? '是' : '否'
                }}</template></el-table-column
              ><el-table-column label="键字段" min-width="180" show-overflow-tooltip
                ><template #default="{ row }">{{
                  row.keyParts.join(', ') || '—'
                }}</template></el-table-column
              ><el-table-column label="包含字段" min-width="150" show-overflow-tooltip
                ><template #default="{ row }">{{
                  row.nonKeyParts.join(', ') || '—'
                }}</template></el-table-column
              ><el-table-column
                prop="nativePredicate"
                label="条件"
                min-width="160"
                show-overflow-tooltip
            /></el-table>
            <SkhPagination
              v-model:current-page="indexPage"
              :total="indexTotal"
              :page-size="detailPageSize"
              aria-label="对象索引分页"
              @current-change="loadDetail"
              @size-change="detailPageSizeChanged"
            />
            <p v-if="detailError" class="discovery-inline-error" role="alert">
              刷新失败：{{ detailError }}
            </p>
          </section>
        </template>
      </section>
    </Teleport>
  </main>
</template>
