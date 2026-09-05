<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { Delete, DocumentChecked, EditPen, Search, UserFilled } from '@element-plus/icons-vue'
import { onBeforeRouteLeave, onBeforeRouteUpdate, useRoute, useRouter } from 'vue-router'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import { useOverlayStore } from '../../../app/stores/overlays'
import { useActorStore } from '../../../app/stores/actor'
import { getEvidenceList } from '../../evidence/api/evidenceApi'
import {
  evidenceTypeLabels,
  type EvidenceListItemResponse,
} from '../../evidence/api/evidenceContracts'
import KnowledgeStatusProgressionPanel from '../../knowledge-status/components/KnowledgeStatusProgressionPanel.vue'
import { deleteDatabaseObject, parseSafeApiId } from '../api/databaseKnowledgeApi'
import type { DatabaseColumnSummary } from '../api/databaseKnowledgeContracts'
import DatabaseObjectContextRail from '../components/DatabaseObjectContextRail.vue'
import RegisterDatabaseColumnDialog from '../components/RegisterDatabaseColumnDialog.vue'
import { useDatabaseObjectDetail } from '../composables/useDatabaseObjectDetail'
import { openDeleteDialog } from '../../soft-delete/deleteDialog'

const route = useRoute()
const router = useRouter()
const overlayStore = useOverlayStore()
const actorStore = useActorStore()
const filterText = ref('')
const objectEvidence = ref<readonly EvidenceListItemResponse[]>([])
const evidenceLoading = ref(false)
const evidenceError = ref<string | null>(null)
const {
  detail,
  loading,
  errorMessage,
  selectedColumnError,
  selectedColumnId,
  load,
  selectColumn,
  clearColumnSelection,
} = useDatabaseObjectDetail(() => parseSafeApiId(route.params.id))

const databaseObjectId = computed(() => parseSafeApiId(route.params.id))
const routeSelectedColumnId = computed(() => parseSafeApiId(route.query.selectedColumnId))
const humanConfirmationCount = computed(() => objectEvidence.value.filter(
  (item) => item.evidenceType === 'HumanConfirmation',
).length)

const filteredColumns = computed(() => {
  if (detail.value?.id !== databaseObjectId.value || !detail.value) return []
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

function openObjectKnowledgeEdit(): void {
  if (!actorStore.canEdit || detail.value?.id !== databaseObjectId.value || !detail.value) return
  overlayStore.openDrawer({ kind: 'edit-database-object', id: detail.value.id, mode: 'edit' })
}

function evidenceSubjectPayload() {
  if (detail.value?.id !== databaseObjectId.value || !detail.value) return null
  return {
    subject: { type: 'DatabaseObject', id: detail.value.id },
    title: detail.value.overview.qualifiedName,
    knowledgeStatus: detail.value.overview.knowledgeStatus,
  }
}

function openAddEvidence(): void {
  if (!actorStore.canEdit) return
  const payload = evidenceSubjectPayload()
  if (!payload) return
  overlayStore.openDrawer({ kind: 'add-evidence', id: null, mode: 'create', payload })
}

function openAddHumanConfirmation(): void {
  if (!actorStore.canEdit) return
  const payload = evidenceSubjectPayload()
  if (!payload) return
  overlayStore.openDrawer({ kind: 'human-confirmation', id: null, mode: 'create', payload })
}

function openEvidence(id: number): void {
  overlayStore.openDrawer({ kind: 'evidence', id, mode: 'read' })
}

function openRegisterColumn(): void {
  if (!actorStore.canEdit || detail.value?.id !== databaseObjectId.value || !detail.value) return
  const greatestOrdinal = Math.max(0, ...detail.value.columns.map((column) => column.ordinalPosition))
  overlayStore.openDialog({
    kind: 'register-database-column',
    id: detail.value.id,
    mode: 'create',
    payload: { concurrencyToken: detail.value.concurrencyToken, nextOrdinalPosition: greatestOrdinal + 1 },
  })
}

function handleColumnRegistered(): void {
  void loadRoute()
}

function databaseObjectChanged(): void {
  void loadRoute()
}

function knowledgeStatusChanged(): void {
  void loadRoute()
}

function evidenceChanged(): void {
  void loadRoute()
}

let evidenceRequest: AbortController | null = null
async function loadObjectEvidence(id: number): Promise<void> {
  evidenceRequest?.abort()
  const controller = new AbortController()
  evidenceRequest = controller
  const current = () => evidenceRequest === controller && !controller.signal.aborted && databaseObjectId.value === id
  objectEvidence.value = []
  evidenceLoading.value = true
  evidenceError.value = null
  try {
    const response = await getEvidenceList('DatabaseObject', id, controller.signal)
    if (current()) objectEvidence.value = response.items
  } catch (loadError: unknown) {
    if (!current()) return
    objectEvidence.value = []
    evidenceError.value = loadError instanceof Error ? loadError.message : '数据库对象证据加载失败。'
  } finally {
    if (current()) evidenceLoading.value = false
  }
}

async function loadRoute(): Promise<void> {
  if (databaseObjectId.value === null) return
  const requestedId = databaseObjectId.value
  await Promise.all([
    load(databaseObjectId.value, routeSelectedColumnId.value),
    loadObjectEvidence(databaseObjectId.value),
  ])
  if (requestedId !== databaseObjectId.value) return
  if (selectedColumnError.value) {
    const nextQuery = { ...route.query }
    delete nextQuery.selectedColumnId
    await router.replace({ query: nextQuery })
  }
}

function requestDelete(): void {
  if (!actorStore.canEdit || detail.value?.id !== databaseObjectId.value || !detail.value?.canDelete) return
  const current = detail.value
  openDeleteDialog(overlayStore, {
    objectTypeLabel: '数据库对象', actionLabel: '删除数据库对象', displayName: current.overview.qualifiedName,
    concurrencyToken: current.concurrencyToken,
    execute: async () => { if (loading.value || detail.value?.id !== current.id || parseSafeApiId(route.params.id) !== current.id) throw new Error('当前对象已变化，请重新加载。'); await deleteDatabaseObject(current.id, current.concurrencyToken) },
    onDeleted: () => router.push({ name: 'database-objects-list', query: { databaseSourceId: String(current.databaseSource.id) } }),
    onRefresh: loadRoute,
    onUnavailable: () => router.push({ name: 'database-objects-list' }),
  })
}

function openDatabaseBrowse(): void {
  void router.push({ name: 'database-objects-list' })
}

function openSystem(): void {
  if (detail.value?.id !== databaseObjectId.value || !detail.value) return
  void router.push({ name: 'system-detail', params: { id: String(detail.value.system.id) } })
}

function openDatabaseSource(): void {
  if (detail.value?.id !== databaseObjectId.value || !detail.value) return
  void router.push({
    name: 'database-objects-list',
    query: {
      systemId: String(detail.value.system.id),
      databaseSourceId: String(detail.value.databaseSource.id),
    },
  })
}

watch([databaseObjectId, routeSelectedColumnId], () => {
  void loadRoute()
}, { flush: 'sync' })

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
  window.addEventListener('database-object:changed', databaseObjectChanged)
  window.addEventListener('database-column:changed', databaseObjectChanged)
  window.addEventListener('knowledge-status:changed', knowledgeStatusChanged)
  window.addEventListener('evidence:changed', evidenceChanged)
})

onBeforeUnmount(() => {
  evidenceRequest?.abort()
  window.removeEventListener('database-object:changed', databaseObjectChanged)
  window.removeEventListener('database-column:changed', databaseObjectChanged)
  window.removeEventListener('knowledge-status:changed', knowledgeStatusChanged)
  window.removeEventListener('evidence:changed', evidenceChanged)
})
async function closeDetailOverlays(): Promise<boolean> {
  if (!await overlayStore.requestDrawerClose()) return false
  overlayStore.closeDialog()
  return true
}
onBeforeRouteUpdate((to, from) => to.params.id === from.params.id ? true : closeDetailOverlays())
onBeforeRouteLeave(closeDetailOverlays)
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
    <template v-else-if="detail && detail.id === databaseObjectId">
      <header class="database-object-header">
        <nav class="database-breadcrumb" aria-label="面包屑">
          <button type="button" @click="openDatabaseBrowse">数据库</button><b>/</b
          ><button type="button" @click="openSystem">{{ detail.system.name }}</button><b>/</b
          ><button type="button" @click="openDatabaseSource">{{ detail.databaseSource.name }}</button><b>/</b
          ><strong class="technical-text">{{ detail.overview.qualifiedName }}</strong>
        </nav>
        <div class="database-object-header__title">
          <div>
            <h1 class="technical-text">{{ detail.overview.qualifiedName }}</h1>
            <p>{{ detail.overview.businessDescription ?? '尚未记录业务说明' }}</p>
          </div>
          <div class="database-object-header__actions">
            <el-button v-if="actorStore.canEdit && detail.canDelete" type="danger" plain :icon="Delete" @click="requestDelete">删除数据库对象</el-button>
            <el-button v-if="actorStore.canEdit" text type="primary" :icon="EditPen" @click="openObjectKnowledgeEdit">编辑</el-button>
          </div>
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

      <section class="database-object-evidence-section" aria-labelledby="database-object-evidence-title">
        <div class="database-object-evidence-section__heading">
          <div>
            <h2 id="database-object-evidence-title">证据与人工确认</h2>
            <span>{{ objectEvidence.length }} 条对象级证据</span>
          </div>
          <div v-if="actorStore.canEdit" class="database-object-evidence-section__actions">
            <el-button class="skh-section-action skh-evidence-action" type="primary" :icon="DocumentChecked" @click="openAddEvidence">添加证据</el-button>
            <el-button class="skh-section-action skh-human-confirmation-action" plain :icon="UserFilled" @click="openAddHumanConfirmation">添加人工确认</el-button>
          </div>
        </div>
        <p class="database-object-evidence-section__scope">这里只显示并维护当前表或视图的对象级证据；字段证据继续在对应字段详情中独立维护。</p>
        <LoadingState v-if="evidenceLoading" message="正在读取数据库对象证据…" />
        <div v-else-if="evidenceError" class="database-inline-notice database-inline-notice--error">
          {{ evidenceError }}
          <el-button text type="primary" @click="loadObjectEvidence(detail.id)">重试</el-button>
        </div>
        <div v-else-if="objectEvidence.length" class="database-object-evidence-list">
          <button v-for="item in objectEvidence" :key="item.id" type="button" @click="openEvidence(item.id)">
            <el-icon><DocumentChecked /></el-icon>
            <span><small>{{ evidenceTypeLabels[item.evidenceType] }}</small><strong>{{ item.sourceTitle }}</strong></span>
            <p>{{ item.supportReason }}</p>
          </button>
        </div>
        <EmptyState v-else title="尚无对象级证据" description="添加可定位的证据或人工确认，为当前表或视图的知识状态提供依据。" />
      </section>

      <KnowledgeStatusProgressionPanel
        :id="detail.id"
        target-type="DatabaseObject"
        :title="detail.overview.qualifiedName"
        :status="detail.overview.knowledgeStatus"
        :concurrency-token="detail.concurrencyToken"
        :evidence-count="objectEvidence.length"
        :human-confirmation-count="humanConfirmationCount"
        :can-change="actorStore.canEdit && detail.availableActions.includes('ChangeKnowledgeStatus')"
      />

      <section class="database-columns-section" aria-labelledby="columns-title">
        <div class="database-columns-section__toolbar">
          <div><h2 id="columns-title">字段</h2><span>{{ detail.columns.length }} 个字段</span></div>
          <div class="database-columns-section__actions"><el-input v-model="filterText" clearable placeholder="筛选字段" :prefix-icon="Search" /><el-button v-if="actorStore.canEdit" type="primary" plain @click="openRegisterColumn">登记字段</el-button></div>
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

      <RegisterDatabaseColumnDialog
        :database-object-id="detail.id"
        :concurrency-token="detail.concurrencyToken"
        :next-ordinal-position="Math.max(0, ...detail.columns.map((column) => column.ordinalPosition)) + 1"
        @registered="handleColumnRegistered"
      />

    </template>
  </div>
</template>

<style src="../database-knowledge.css"></style>
<style src="../../knowledge-status/knowledge-status.css"></style>
