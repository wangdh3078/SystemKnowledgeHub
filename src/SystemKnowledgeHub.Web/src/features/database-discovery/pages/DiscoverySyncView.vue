<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ApiError } from '../../../api/errors/ApiError'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import { useActorStore } from '../../../app/stores/actor'
import { getRunFilterOptions } from '../api/databaseDiscoveryApi'
import {
  applySyncPlan,
  confirmSyncPlan,
  createSyncPlan,
  listSyncPlans,
  previewSyncPlan,
  queryReconciliationObjectChildren,
  queryReconciliationObjectGroups,
  setWholeObjectSelection,
} from '../api/databaseDiscoverySyncApi'
import type { RunFilterOptions } from '../api/databaseDiscoveryContracts'
import type {
  ReconciliationCandidate,
  ReconciliationChild,
  ReconciliationObjectChildrenPage,
  ReconciliationObjectGroup,
  ReconciliationObjectGroupPage,
  SyncActionType,
  SyncPlan,
  SyncSelection,
} from '../api/databaseDiscoverySyncContracts'
import DiscoverySectionNav from '../components/DiscoverySectionNav.vue'
import '../database-discovery.css'

const actorStore = useActorStore()
const profiles = ref<RunFilterOptions>({ profiles: [], databaseSources: [] })
const profileId = ref<number>()
const category = ref('')
const search = ref('')
const page = ref(1)
const pageSize = ref(50)
const reconciliation = ref<ReconciliationObjectGroupPage>()
const selected = ref<readonly SyncSelection[]>([])
const expandedObjects = ref<ReadonlySet<string>>(new Set())
const childPages = ref<ReadonlyMap<string, ReconciliationObjectChildrenPage>>(new Map())
const childPageSizes = ref<ReadonlyMap<string, number>>(new Map())
const childLoading = ref<ReadonlySet<string>>(new Set())
const plans = ref<readonly SyncPlan[]>([])
const plansPage = ref(1)
const plansPageSize = ref(20)
const plansTotal = ref(0)
const activePlan = ref<SyncPlan>()
const confirmationChecked = ref(false)
const loading = ref(false)
const mutating = ref(false)
const error = ref('')
let controller = new AbortController()
let plansController = new AbortController()
const actionLimitMessage = '该选择将超过单个同步计划允许的最大操作数，请减少选择范围。'

const message = (value: unknown) =>
  value instanceof ApiError ? value.message : '手工同步操作失败。'
const canSelect = (row: ReconciliationCandidate) =>
  row.status === 'Applicable' && row.suggestedAction !== null
const actionLabels: Record<SyncActionType, string> = {
  CreateDatabaseObject: '创建对象',
  LinkExistingDatabaseObject: '链接对象',
  CreateDatabaseColumn: '创建字段',
  LinkExistingDatabaseColumn: '链接字段',
  UpdateDatabaseObjectStructure: '更新对象结构',
  UpdateDatabaseColumnStructure: '更新字段结构',
  MarkObjectSourceMissing: '标记对象来源未发现',
  ClearObjectSourceMissing: '清除对象来源未发现',
  MarkColumnSourceMissing: '标记字段来源未发现',
  ClearColumnSourceMissing: '清除字段来源未发现',
}
const statusLabels = {
  Applicable: '可处理',
  NoAction: '无需操作',
  Conflict: '冲突',
  Unsupported: '仅审查',
} as const
const objectActionTypes = new Set<SyncActionType>([
  'CreateDatabaseObject',
  'LinkExistingDatabaseObject',
  'UpdateDatabaseObjectStructure',
  'MarkObjectSourceMissing',
  'ClearObjectSourceMissing',
])
const selectedCount = computed(() => selected.value.length)
const selectedObjectCount = computed(
  () => selected.value.filter((item) => objectActionTypes.has(item.actionType)).length,
)
const selectedColumnCount = computed(() => selected.value.length - selectedObjectCount.value)

const selectionKey = (selection: SyncSelection): string =>
  `${selection.actionType}\u001f${selection.logicalIdentity}\u001f${selection.targetId ?? ''}`

function statusType(
  status: ReconciliationCandidate['status'],
): 'success' | 'info' | 'danger' | 'warning' {
  return status === 'Applicable'
    ? 'success'
    : status === 'Conflict'
      ? 'danger'
      : status === 'Unsupported'
        ? 'warning'
        : 'info'
}
function actionLabel(value: unknown): string {
  return typeof value === 'string' && value in actionLabels
    ? actionLabels[value as SyncActionType]
    : '—'
}
function reconciliationStatusLabel(value: unknown): string {
  return typeof value === 'string' && value in statusLabels
    ? statusLabels[value as ReconciliationCandidate['status']]
    : '未知'
}
function selectionFor(row: ReconciliationCandidate): SyncSelection {
  return {
    actionType: row.suggestedAction!,
    logicalIdentity: row.logicalIdentity,
    targetId: row.targetId,
  }
}
function replaceSelections(actions: readonly SyncSelection[]): void {
  selected.value = [...actions]
}
function groupChecked(group: ReconciliationObjectGroup): boolean {
  return group.selectableCount > 0 && group.selectedCount === group.selectableCount
}
function groupIndeterminate(group: ReconciliationObjectGroup): boolean {
  return group.selectedCount > 0 && group.selectedCount < group.selectableCount
}
function childChecked(child: ReconciliationChild): boolean {
  return child.selectableCount > 0 && child.selectedCount === child.selectableCount
}
function childIndeterminate(child: ReconciliationChild): boolean {
  return child.selectedCount > 0 && child.selectedCount < child.selectableCount
}
function groupStatusLabel(group: ReconciliationObjectGroup): string {
  if (
    group.selectableCount > 0 &&
    group.conflictCount + group.unsupportedCount + group.noActionCount > 0
  )
    return '部分可处理'
  return reconciliationStatusLabel(group.status)
}
function candidateActions(candidates: readonly ReconciliationCandidate[]): string {
  const labels = candidates
    .filter(canSelect)
    .map((item) => actionLabel(item.suggestedAction))
    .filter((item, index, items) => items.indexOf(item) === index)
  return labels.length === 0 ? '—' : labels.join('、')
}
function groupActionLabel(group: ReconciliationObjectGroup): string {
  const objectActions = candidateActions(group.objectCandidates)
  if (objectActions !== '—') return objectActions
  return group.selectableColumnCount > 0 ? `${group.selectableColumnCount} 个字段待同步` : '—'
}
function setChildLoading(identity: string, value: boolean): void {
  const next = new Set(childLoading.value)
  if (value) next.add(identity)
  else next.delete(identity)
  childLoading.value = next
}

async function load(resetChildren = true): Promise<void> {
  if (!profileId.value) return
  controller.abort()
  plansController.abort()
  controller = new AbortController()
  plansController = new AbortController()
  loading.value = true
  error.value = ''
  try {
    const [result, planResult] = await Promise.all([
      queryReconciliationObjectGroups(
        profileId.value,
        reconciliation.value?.profileId === profileId.value
          ? reconciliation.value.targetSnapshotId
          : null,
        category.value,
        search.value,
        page.value,
        pageSize.value,
        selected.value,
        controller.signal,
      ),
      listSyncPlans(plansPage.value, plansPageSize.value, profileId.value, plansController.signal),
    ])
    reconciliation.value = result
    plans.value = planResult.items
    plansTotal.value = planResult.total
    if (resetChildren) {
      expandedObjects.value = new Set()
      childPages.value = new Map()
      childLoading.value = new Set()
    }
  } catch (value) {
    if (!(value instanceof DOMException && value.name === 'AbortError'))
      error.value = message(value)
  } finally {
    loading.value = false
  }
}
function filterChanged(): void {
  page.value = 1
  void load()
}
function pageSizeChanged(value: number): void {
  pageSize.value = value
  page.value = 1
  void load()
}
function profileChanged(): void {
  page.value = 1
  plansPage.value = 1
  reconciliation.value = undefined
  replaceSelections([])
  childPageSizes.value = new Map()
  void load()
}
function clearSelections(): void {
  replaceSelections([])
  void load()
}
function childPageSize(identity: string): number {
  return childPageSizes.value.get(identity) ?? 50
}
async function loadChildren(
  group: ReconciliationObjectGroup,
  childPage = 1,
  requestedPageSize = childPageSize(group.objectLogicalIdentity),
): Promise<void> {
  if (!reconciliation.value || !profileId.value) return
  setChildLoading(group.objectLogicalIdentity, true)
  try {
    const result = await queryReconciliationObjectChildren(
      profileId.value,
      reconciliation.value.targetSnapshotId,
      group.objectLogicalIdentity,
      category.value,
      search.value,
      childPage,
      requestedPageSize,
      selected.value,
      controller.signal,
    )
    const next = new Map(childPages.value)
    next.set(group.objectLogicalIdentity, result)
    childPages.value = next
  } catch (value) {
    if (!(value instanceof DOMException && value.name === 'AbortError'))
      ElMessage.error(message(value))
  } finally {
    setChildLoading(group.objectLogicalIdentity, false)
  }
}
function childPageSizeChanged(group: ReconciliationObjectGroup, value: number): void {
  const next = new Map(childPageSizes.value)
  next.set(group.objectLogicalIdentity, value)
  childPageSizes.value = next
  void loadChildren(group, 1, value)
}
async function toggleExpanded(group: ReconciliationObjectGroup): Promise<void> {
  const next = new Set(expandedObjects.value)
  if (next.has(group.objectLogicalIdentity)) {
    next.delete(group.objectLogicalIdentity)
    expandedObjects.value = next
    return
  }
  next.add(group.objectLogicalIdentity)
  expandedObjects.value = next
  await loadChildren(group, childPages.value.get(group.objectLogicalIdentity)?.page ?? 1)
}
async function refreshSelectionState(group: ReconciliationObjectGroup): Promise<void> {
  if (!profileId.value || !reconciliation.value) return
  const currentExpanded = new Set(expandedObjects.value)
  const result = await queryReconciliationObjectGroups(
    profileId.value,
    reconciliation.value.targetSnapshotId,
    category.value,
    search.value,
    page.value,
    pageSize.value,
    selected.value,
  )
  reconciliation.value = result
  expandedObjects.value = currentExpanded
  if (currentExpanded.has(group.objectLogicalIdentity)) {
    const refreshedGroup = result.items.find(
      (item) => item.objectLogicalIdentity === group.objectLogicalIdentity,
    )
    if (refreshedGroup)
      await loadChildren(
        refreshedGroup,
        childPages.value.get(group.objectLogicalIdentity)?.page ?? 1,
        childPageSize(group.objectLogicalIdentity),
      )
  }
}
async function toggleWholeObject(
  group: ReconciliationObjectGroup,
  checked: boolean,
): Promise<void> {
  if (!profileId.value || !reconciliation.value || !actorStore.canEdit) return
  mutating.value = true
  try {
    const result = await setWholeObjectSelection(
      profileId.value,
      reconciliation.value.targetSnapshotId,
      group.objectLogicalIdentity,
      checked,
      selected.value,
    )
    replaceSelections(result.actions)
    await refreshSelectionState(group)
  } catch (value) {
    ElMessage.error(message(value))
  } finally {
    mutating.value = false
  }
}
async function toggleChild(
  group: ReconciliationObjectGroup,
  child: ReconciliationChild,
  checked: boolean,
): Promise<void> {
  if (!actorStore.canEdit || child.selectableCount === 0) return
  const next = new Map(selected.value.map((item) => [selectionKey(item), item]))
  const childSelections = child.candidates.filter(canSelect).map(selectionFor)
  for (const item of childSelections) {
    if (checked) next.set(selectionKey(item), item)
    else next.delete(selectionKey(item))
  }
  if (checked && group.requiredParentAction)
    next.set(selectionKey(group.requiredParentAction), group.requiredParentAction)
  if (next.size > (reconciliation.value?.maximumSyncPlanActions ?? 0)) {
    ElMessage.error(actionLimitMessage)
    return
  }
  replaceSelections([...next.values()])
  try {
    await refreshSelectionState(group)
  } catch (value) {
    ElMessage.error(message(value))
  }
}

async function createAndPreview(): Promise<void> {
  if (!reconciliation.value || selected.value.length === 0) return
  mutating.value = true
  try {
    let plan = await createSyncPlan(
      reconciliation.value.profileId,
      reconciliation.value.targetSnapshotId,
      selected.value,
    )
    plan = await previewSyncPlan(plan)
    activePlan.value = plan
    confirmationChecked.value = false
    plansPage.value = 1
    await loadPlanHistory()
    ElMessage.success('同步计划预览已生成，请核对后显式确认。')
  } catch (value) {
    ElMessage.error(message(value))
  } finally {
    mutating.value = false
  }
}
async function confirmPlan(): Promise<void> {
  if (!activePlan.value?.preview || !confirmationChecked.value) return
  mutating.value = true
  try {
    activePlan.value = await confirmSyncPlan(activePlan.value)
    plans.value = plans.value.map((item) =>
      item.id === activePlan.value!.id ? activePlan.value! : item,
    )
    ElMessage.success('当前预览哈希已确认。')
  } catch (value) {
    ElMessage.error(message(value))
  } finally {
    mutating.value = false
  }
}
async function applyPlan(): Promise<void> {
  if (!activePlan.value || activePlan.value.status !== 'Ready') return
  try {
    await ElMessageBox.confirm(
      '应用将在一个短事务中写入已确认的结构操作，且不会覆盖人工知识字段。是否继续？',
      '应用同步计划',
      { confirmButtonText: '应用计划', cancelButtonText: '取消', type: 'warning' },
    )
  } catch {
    return
  }
  mutating.value = true
  try {
    activePlan.value = await applySyncPlan(activePlan.value)
    plans.value = plans.value.map((item) =>
      item.id === activePlan.value!.id ? activePlan.value! : item,
    )
    ElMessage.success('同步计划已原子应用。')
    replaceSelections([])
    await load()
  } catch (value) {
    ElMessage.error(message(value))
  } finally {
    mutating.value = false
  }
}
function openPlan(plan: SyncPlan): void {
  activePlan.value = plan
  confirmationChecked.value = false
}

async function loadPlanHistory(): Promise<void> {
  if (!profileId.value) return
  plansController.abort()
  plansController = new AbortController()
  try {
    const result = await listSyncPlans(
      plansPage.value,
      plansPageSize.value,
      profileId.value,
      plansController.signal,
    )
    plans.value = result.items
    plansTotal.value = result.total
  } catch (value) {
    if (!(value instanceof DOMException && value.name === 'AbortError'))
      ElMessage.error(message(value))
  }
}
function plansPageSizeChanged(value: number): void {
  plansPageSize.value = value
  plansPage.value = 1
  void loadPlanHistory()
}

async function initialize(): Promise<void> {
  try {
    profiles.value = await getRunFilterOptions()
    profileId.value = profiles.value.profiles[0]?.id
    if (profileId.value) await load()
  } catch (value) {
    error.value = message(value)
  }
}
onMounted(initialize)
onBeforeUnmount(() => {
  controller.abort()
  plansController.abort()
})
</script>

<template>
  <main class="discovery-page skh-page">
    <header class="discovery-page__header skh-page-header">
      <div>
        <small class="discovery-eyebrow">数据库 / 数据库发现</small>
        <h1>手工同步</h1>
        <p>基于最新完整快照选择结构操作；预览、显式确认后才会原子应用。</p>
      </div>
    </header>
    <DiscoverySectionNav />
    <el-alert title="人工知识保护" type="info" :closable="false" show-icon>
      同步只写外部来源拥有的结构字段与 typed
      binding，不会覆盖业务说明、业务键、访问模式、知识状态或人工证据。
    </el-alert>

    <section class="discovery-filters skh-filter-bar" aria-label="手工同步筛选">
      <el-select v-model="profileId" placeholder="选择连接配置" @change="profileChanged">
        <el-option
          v-for="item in profiles.profiles"
          :key="item.id"
          :label="item.name"
          :value="item.id"
        />
      </el-select>
      <el-select v-model="category" clearable placeholder="分类：全部" @change="filterChanged">
        <el-option label="新增/链接" value="New" /><el-option
          label="结构变化"
          value="StructuralChange"
        />
        <el-option label="来源未发现" value="MissingFromSource" /><el-option
          label="重新出现"
          value="Reappeared"
        />
        <el-option label="超出当前范围" value="OutOfScope" /><el-option
          label="需要重建基线"
          value="RebaselineRequired"
        />
        <el-option label="冲突" value="Conflict" /><el-option label="仅审查" value="Unsupported" />
      </el-select>
      <el-input
        v-model="search"
        clearable
        placeholder="搜索 schema / 对象 / 字段"
        @keyup.enter="filterChanged"
        @clear="filterChanged"
      />
      <el-button @click="filterChanged">查询</el-button>
    </section>

    <LoadingState v-if="loading && !reconciliation" message="正在生成 Reconciliation…" />
    <ErrorState
      v-else-if="error && !reconciliation"
      title="手工同步加载失败"
      :message="error"
      @retry="load"
    />
    <template v-else-if="reconciliation">
      <section class="discovery-summary" aria-label="同步上下文">
        <div>
          <small>数据库来源</small><strong>{{ reconciliation.databaseSourceName }}</strong>
        </div>
        <div>
          <small>最新完整快照</small><strong>#{{ reconciliation.targetSnapshotId }}</strong>
        </div>
        <div>
          <small>Scope Generation</small><strong>#{{ reconciliation.scopeGenerationId }}</strong>
        </div>
      </section>
      <section class="discovery-table-section skh-table-section" :aria-busy="loading">
        <div v-if="actorStore.canEdit" class="discovery-selection-bar">
          <strong
            >已选择：{{ selectedObjectCount }} 对象 · {{ selectedColumnCount }} 字段 ·
            {{ selectedCount }} 个操作</strong
          >
          <div class="discovery-selection-bar__actions">
            <el-button v-if="selectedCount > 0" @click="clearSelections">清除选择</el-button>
            <el-button
              type="primary"
              :disabled="selectedCount === 0"
              :loading="mutating"
              @click="createAndPreview"
              >生成计划并预览</el-button
            >
          </div>
        </div>
        <EmptyState
          v-if="reconciliation.items.length === 0"
          title="当前筛选没有数据库对象"
          description="可调整分类或搜索条件。"
        />
        <div v-else class="discovery-object-groups" role="treegrid" aria-label="数据库对象同步候选">
          <div class="discovery-object-groups__header" role="row">
            <span role="columnheader">选择</span>
            <span role="columnheader">对象</span>
            <span role="columnheader">判断</span>
            <span role="columnheader">当前知识库</span>
            <span role="columnheader">建议操作</span>
            <span role="columnheader">字段</span>
            <span role="columnheader">冲突/仅审查</span>
          </div>
          <article
            v-for="group in reconciliation.items"
            :key="group.key"
            class="discovery-object-group"
            role="rowgroup"
          >
            <div
              class="discovery-object-group__row"
              role="row"
              :aria-expanded="expandedObjects.has(group.objectLogicalIdentity)"
            >
              <div class="discovery-object-group__selection" role="gridcell">
                <el-checkbox
                  v-if="actorStore.canEdit"
                  :model-value="groupChecked(group)"
                  :indeterminate="groupIndeterminate(group)"
                  :disabled="group.selectableCount === 0 || mutating"
                  :aria-label="`选择对象 ${group.schemaName}.${group.objectName} 的全部可处理操作`"
                  :aria-checked="groupIndeterminate(group) ? 'mixed' : groupChecked(group)"
                  @change="(checked: boolean) => toggleWholeObject(group, checked)"
                />
                <button
                  class="discovery-object-group__toggle"
                  type="button"
                  :aria-label="`${expandedObjects.has(group.objectLogicalIdentity) ? '收起' : '展开'} ${group.schemaName}.${group.objectName} 字段`"
                  :aria-expanded="expandedObjects.has(group.objectLogicalIdentity)"
                  @click="toggleExpanded(group)"
                >
                  <span aria-hidden="true">{{
                    expandedObjects.has(group.objectLogicalIdentity) ? '⌄' : '›'
                  }}</span>
                </button>
              </div>
              <div class="discovery-object-group__identity" role="gridcell">
                <strong>{{ group.schemaName }}.{{ group.objectName }}</strong>
                <small>{{ group.objectType }}</small>
              </div>
              <div role="gridcell">
                <el-tag :type="statusType(group.status)" effect="plain">{{
                  groupStatusLabel(group)
                }}</el-tag>
              </div>
              <div class="discovery-object-group__fact" role="gridcell">
                <span>{{ group.targetId ? `已关联 #${group.targetId}` : '未关联' }}</span>
              </div>
              <div class="discovery-object-group__action" role="gridcell" :title="group.summary">
                <span>{{ groupActionLabel(group) }}</span>
                <small>{{ group.summary }}</small>
              </div>
              <div class="discovery-object-group__counts" role="gridcell">
                <strong
                  >{{ group.selectableColumnCount }}/{{ group.totalColumnCount }} 可处理</strong
                >
                <small>已选 {{ group.selectedCount }}/{{ group.selectableCount }} 个操作</small>
              </div>
              <div class="discovery-object-group__review" role="gridcell">
                <span v-if="group.conflictCount + group.unsupportedCount > 0">
                  冲突 {{ group.conflictCount }} · 仅审查 {{ group.unsupportedCount }}
                </span>
                <span v-else>—</span>
              </div>
            </div>

            <div
              v-if="expandedObjects.has(group.objectLogicalIdentity)"
              class="discovery-object-children"
              role="rowgroup"
            >
              <LoadingState
                v-if="childLoading.has(group.objectLogicalIdentity)"
                message="正在加载字段…"
              />
              <template v-else-if="childPages.get(group.objectLogicalIdentity)">
                <div
                  v-for="child in childPages.get(group.objectLogicalIdentity)!.items"
                  :key="child.key"
                  class="discovery-object-child"
                  role="row"
                >
                  <div class="discovery-object-child__selection" role="gridcell">
                    <el-checkbox
                      v-if="actorStore.canEdit"
                      :model-value="childChecked(child)"
                      :indeterminate="childIndeterminate(child)"
                      :disabled="child.selectableCount === 0"
                      :aria-label="`选择 ${child.name ?? child.logicalIdentity} 的可处理操作`"
                      :aria-checked="childIndeterminate(child) ? 'mixed' : childChecked(child)"
                      @change="(checked: boolean) => toggleChild(group, child, checked)"
                    />
                    <span v-else class="discovery-object-child__viewer-space" aria-hidden="true" />
                  </div>
                  <div class="discovery-object-child__identity" role="gridcell">
                    <strong>{{ child.name ?? child.logicalIdentity }}</strong>
                    <small>{{ child.entityKind === 'Column' ? '字段' : '仅审查结构' }}</small>
                  </div>
                  <div role="gridcell">
                    <el-tag :type="statusType(child.status)" effect="plain">{{
                      reconciliationStatusLabel(child.status)
                    }}</el-tag>
                  </div>
                  <span role="gridcell">—</span>
                  <span
                    class="discovery-object-child__summary"
                    role="gridcell"
                    :title="child.summary"
                    >{{ candidateActions(child.candidates) }}</span
                  >
                  <span role="gridcell">—</span>
                  <small
                    class="discovery-object-child__block"
                    role="gridcell"
                    :title="
                      child.blockCodes.length > 0 ? child.blockCodes.join('、') : child.summary
                    "
                  >
                    {{ child.blockCodes.length > 0 ? child.blockCodes.join('、') : '—' }}
                  </small>
                </div>
                <footer
                  v-if="
                    childPages.get(group.objectLogicalIdentity)!.total >
                    childPageSize(group.objectLogicalIdentity)
                  "
                  class="discovery-pagination skh-pagination"
                >
                  <el-pagination
                    :current-page="childPages.get(group.objectLogicalIdentity)!.page"
                    :total="childPages.get(group.objectLogicalIdentity)!.total"
                    :page-size="childPageSize(group.objectLogicalIdentity)"
                    :page-sizes="[50, 100, 200]"
                    background
                    layout="total, sizes, prev, pager, next, jumper"
                    @current-change="(childPage: number) => loadChildren(group, childPage)"
                    @size-change="(value: number) => childPageSizeChanged(group, value)"
                  />
                </footer>
              </template>
            </div>
          </article>
          <el-alert
            v-if="reconciliation.ungroupedReviewOnlyCount > 0"
            :title="`另有 ${reconciliation.ungroupedReviewOnlyCount} 项序列等仅审查结构，请在快照中查看。`"
            type="info"
            :closable="false"
          />
        </div>
        <footer v-if="reconciliation.total > 0" class="discovery-pagination skh-pagination">
          <el-pagination
            v-model:current-page="page"
            v-model:page-size="pageSize"
            :total="reconciliation.total"
            :page-sizes="[50, 100, 200]"
            background
            layout="total, sizes, prev, pager, next, jumper"
            @current-change="load"
            @size-change="pageSizeChanged"
          />
        </footer>
      </section>
    </template>

    <section
      v-if="activePlan?.preview"
      class="discovery-panel discovery-sync-preview"
      aria-label="同步计划预览"
    >
      <header>
        <div>
          <small>计划 #{{ activePlan.id }}</small>
          <h2>预览与确认</h2>
        </div>
        <el-tag>{{ activePlan.status }}</el-tag>
      </header>
      <el-alert
        v-for="warning in activePlan.preview.warnings"
        :key="warning"
        :title="warning"
        type="warning"
        :closable="false"
        show-icon
      />
      <p class="discovery-hash">
        PreviewHash：<code>{{ activePlan.preview.previewHash }}</code>
      </p>
      <el-table
        :data="activePlan.preview.actions"
        :row-key="
          (row: { actionType: string; logicalIdentity: string }) =>
            `${row.actionType}:${row.logicalIdentity}`
        "
        max-height="420"
      >
        <el-table-column label="操作" width="160"
          ><template #default="{ row }">{{
            actionLabel(row.actionType)
          }}</template></el-table-column
        >
        <el-table-column prop="summary" label="范围" min-width="180" />
        <el-table-column label="变更前" min-width="220"
          ><template #default="{ row }">
            <pre>{{ row.before ?? '—' }}</pre>
          </template></el-table-column
        >
        <el-table-column label="变更后" min-width="220"
          ><template #default="{ row }">
            <pre>{{ row.after ?? '—' }}</pre>
          </template></el-table-column
        >
      </el-table>
      <div
        v-if="actorStore.canEdit && activePlan.status === 'Draft'"
        class="discovery-confirmation"
      >
        <el-checkbox v-model="confirmationChecked"
          >我已核对当前 PreviewHash、before/after 与人工知识保护边界</el-checkbox
        >
        <el-button
          type="primary"
          :disabled="!confirmationChecked"
          :loading="mutating"
          @click="confirmPlan"
          >确认当前预览</el-button
        >
      </div>
      <div
        v-if="actorStore.canEdit && activePlan.status === 'Ready'"
        class="discovery-confirmation"
      >
        <span>预览已确认；应用前服务端将再次验证最新快照、binding 与目标并发令牌。</span>
        <el-button type="danger" :loading="mutating" @click="applyPlan">应用已确认计划</el-button>
      </div>
      <template v-if="activePlan.status === 'Applied' && activePlan.result">
        <el-result
          icon="success"
          title="同步计划已应用"
          sub-title="所有操作已在一个 SQLite 事务中提交。"
        />
        <dl class="discovery-sync-result" aria-label="应用结果">
          <div>
            <dt>创建对象</dt>
            <dd>{{ activePlan.result.createdObjects }}</dd>
          </div>
          <div>
            <dt>链接对象</dt>
            <dd>{{ activePlan.result.linkedObjects }}</dd>
          </div>
          <div>
            <dt>更新对象</dt>
            <dd>{{ activePlan.result.updatedObjects }}</dd>
          </div>
          <div>
            <dt>创建字段</dt>
            <dd>{{ activePlan.result.createdColumns }}</dd>
          </div>
          <div>
            <dt>链接字段</dt>
            <dd>{{ activePlan.result.linkedColumns }}</dd>
          </div>
          <div>
            <dt>更新字段</dt>
            <dd>{{ activePlan.result.updatedColumns }}</dd>
          </div>
          <div>
            <dt>标记来源未发现</dt>
            <dd>{{ activePlan.result.markedMissing }}</dd>
          </div>
          <div>
            <dt>清除来源未发现</dt>
            <dd>{{ activePlan.result.clearedMissing }}</dd>
          </div>
        </dl>
        <a
          class="el-button el-button--primary"
          :href="`/database-objects?databaseSourceId=${activePlan.databaseSourceId}`"
          >查看数据库对象</a
        >
      </template>
    </section>

    <section class="discovery-panel">
      <header>
        <div>
          <small>历史</small>
          <h2>同步计划</h2>
        </div>
      </header>
      <EmptyState
        v-if="plans.length === 0"
        title="暂无同步计划"
        description="选择可处理项并生成第一份计划。"
      />
      <el-table v-else :data="plans" row-key="id">
        <el-table-column prop="id" label="计划 ID" width="90" /><el-table-column
          prop="profileName"
          label="连接配置"
          min-width="150"
        />
        <el-table-column prop="targetSnapshotId" label="快照" width="90" /><el-table-column
          prop="status"
          label="状态"
          width="110"
        />
        <el-table-column label="操作" width="110"
          ><template #default="{ row }"
            ><el-button size="small" @click="openPlan(row)">查看计划</el-button></template
          ></el-table-column
        >
      </el-table>
      <footer v-if="plansTotal > 0" class="discovery-pagination skh-pagination">
        <el-pagination
          v-model:current-page="plansPage"
          v-model:page-size="plansPageSize"
          :total="plansTotal"
          :page-sizes="[20, 50, 100]"
          background
          layout="total, sizes, prev, pager, next, jumper"
          @current-change="loadPlanHistory"
          @size-change="plansPageSizeChanged"
        />
      </footer>
    </section>
  </main>
</template>
