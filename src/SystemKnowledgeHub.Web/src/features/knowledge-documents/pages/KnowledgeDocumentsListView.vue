<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { Plus, Search } from '@element-plus/icons-vue'
import { useRoute, useRouter } from 'vue-router'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import SkhPagination from '../../../components/data-display/SkhPagination.vue'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import { useActorStore } from '../../../app/stores/actor'
import { formatDateTime } from '../../../app/formatters/dateTime'
import { getKnowledgeDocuments } from '../api/knowledgeDocumentsApi'
import {
  documentLifecycleStatuses,
  documentTypeLabels,
  documentTypes,
  lifecycleLabels,
  type DocumentLifecycleStatus,
  type DocumentType,
  type KnowledgeDocumentListItem,
  type KnowledgeDocumentsListResponse,
} from '../api/knowledgeDocumentContracts'
import CreateKnowledgeDocumentDialog from '../components/CreateKnowledgeDocumentDialog.vue'

const route = useRoute()
const router = useRouter()
const actorStore = useActorStore()
const data = ref<KnowledgeDocumentsListResponse | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)
const createOpen = ref(false)
const query = ref(typeof route.query.query === 'string' ? route.query.query : '')
const documentType = ref<DocumentType | undefined>(
  documentTypes.includes(route.query.documentType as DocumentType)
    ? (route.query.documentType as DocumentType)
    : undefined,
)
const lifecycleStatus = ref<DocumentLifecycleStatus | undefined>(
  documentLifecycleStatuses.includes(route.query.lifecycleStatus as DocumentLifecycleStatus)
    ? (route.query.lifecycleStatus as DocumentLifecycleStatus)
    : undefined,
)
const knowledgeStatus = ref<'Unknown' | 'Inferred' | 'Confirmed' | undefined>(
  ['Unknown', 'Inferred', 'Confirmed'].includes(route.query.knowledgeStatus as string)
    ? (route.query.knowledgeStatus as 'Unknown' | 'Inferred' | 'Confirmed')
    : undefined,
)
const page = ref(Number(route.query.page) > 0 ? Number(route.query.page) : 1)
const pageSize = ref(Number(route.query.pageSize) > 0 ? Number(route.query.pageSize) : 20)
let timer: ReturnType<typeof setTimeout> | null = null
const hasFilters = computed(() =>
  Boolean(query.value || documentType.value || lifecycleStatus.value || knowledgeStatus.value),
)
const canEdit = computed(() => actorStore.canEdit)

function updateRoute(): void {
  void router.replace({
    query: {
      ...(query.value ? { query: query.value } : {}),
      ...(documentType.value ? { documentType: documentType.value } : {}),
      ...(lifecycleStatus.value ? { lifecycleStatus: lifecycleStatus.value } : {}),
      ...(knowledgeStatus.value ? { knowledgeStatus: knowledgeStatus.value } : {}),
      ...(page.value > 1 ? { page: String(page.value) } : {}),
      ...(pageSize.value !== 20 ? { pageSize: String(pageSize.value) } : {}),
    },
  })
}
async function load(): Promise<void> {
  loading.value = true
  error.value = null
  updateRoute()
  try {
    data.value = await getKnowledgeDocuments({
      query: query.value || undefined,
      documentType: documentType.value,
      lifecycleStatus: lifecycleStatus.value,
      knowledgeStatus: knowledgeStatus.value,
      page: page.value,
      pageSize: pageSize.value,
    })
  } catch (reason: unknown) {
    error.value = reason instanceof Error ? reason.message : '无法读取知识内容列表。'
  } finally {
    loading.value = false
  }
}
function resetAndLoad(): void {
  page.value = 1
  void load()
}
function clearFilters(): void {
  query.value = ''
  documentType.value = undefined
  lifecycleStatus.value = undefined
  knowledgeStatus.value = undefined
  resetAndLoad()
}
function openDetail(id: number): void {
  void router.push({ name: 'knowledge-document-detail', params: { id: String(id) } })
}
function openRow(row: KnowledgeDocumentListItem): void {
  openDetail(row.id)
}
function documentTypeLabel(value: DocumentType): string {
  return documentTypeLabels[value]
}
function lifecycleLabel(value: DocumentLifecycleStatus): string {
  return lifecycleLabels[value]
}
function handlePageChange(next: number): void {
  page.value = next
  void load()
}
function handlePageSizeChange(next: number): void {
  pageSize.value = next
  resetAndLoad()
}
watch(query, () => {
  if (timer) clearTimeout(timer)
  timer = setTimeout(resetAndLoad, 280)
})
onMounted(() => void load())
</script>

<template>
  <div class="knowledge-documents-page skh-page">
    <header class="knowledge-documents-page__header skh-page-header">
      <div>
        <h1>知识内容</h1>
        <p>集中浏览需求、规格说明、测试用例、操作规程与沉淀知识。</p>
      </div>
      <div>
        <span v-if="data">共 {{ data.total }} 篇</span
        ><el-button
          v-if="canEdit"
          class="skh-page-primary-action"
          type="primary"
          :icon="Plus"
          @click="createOpen = true"
          >新增知识内容</el-button
        >
      </div>
    </header>
    <section class="knowledge-documents-filter-bar skh-filter-bar" aria-label="知识内容筛选">
      <el-input
        v-model="query"
        clearable
        :prefix-icon="Search"
        placeholder="搜索标题或摘要"
      /><el-select v-model="documentType" clearable placeholder="类型：全部" @change="resetAndLoad"
        ><el-option
          v-for="value in documentTypes"
          :key="value"
          :label="documentTypeLabels[value]"
          :value="value" /></el-select
      ><el-select
        v-model="lifecycleStatus"
        clearable
        placeholder="生命周期：当前"
        @change="resetAndLoad"
        ><el-option
          v-for="value in documentLifecycleStatuses"
          :key="value"
          :label="lifecycleLabels[value]"
          :value="value" /></el-select
      ><el-select
        v-model="knowledgeStatus"
        clearable
        placeholder="知识状态：全部"
        @change="resetAndLoad"
        ><el-option label="未知" value="Unknown" /><el-option
          label="推断"
          value="Inferred" /><el-option label="已确认" value="Confirmed" /></el-select
      ><el-button v-if="hasFilters" text type="primary" @click="clearFilters">清除筛选</el-button>
    </section>
    <LoadingState v-if="loading && !data" message="正在读取知识内容…" /><ErrorState
      v-else-if="error && !data"
      title="知识内容列表加载失败"
      :message="error"
      @retry="load"
    />
    <section v-else class="knowledge-documents-table-section">
      <EmptyState
        v-if="data && data.items.length === 0"
        title="还没有知识内容"
        description="可调整筛选条件，或由编辑者创建第一篇草稿。"
      /><el-table
        v-else
        :data="data?.items ?? []"
        row-key="id"
        class="knowledge-documents-table skh-data-table"
        @row-click="openRow"
        ><el-table-column label="标题" min-width="220"
          ><template #default="scope"
            ><button
              class="knowledge-document-link skh-table-link"
              type="button"
              @click.stop="openDetail(scope.row.id)"
            >
              {{ scope.row.title }}</button
            ><small v-if="scope.row.summary">{{ scope.row.summary }}</small></template
          ></el-table-column
        ><el-table-column label="类型" width="112"
          ><template #default="scope">{{
            documentTypeLabel(scope.row.documentType)
          }}</template></el-table-column
        ><el-table-column label="生命周期" width="92"
          ><template #default="scope"
            ><el-tag size="small" effect="plain">{{
              lifecycleLabel(scope.row.lifecycleStatus)
            }}</el-tag></template
          ></el-table-column
        ><el-table-column label="知识状态" width="94"
          ><template #default="scope"
            ><KnowledgeStatusBadge
              :status="scope.row.knowledgeStatus" /></template></el-table-column
        ><el-table-column prop="updatedByDisplayName" label="更新人" width="112" /><el-table-column
          label="更新于"
          width="156"
          ><template #default="scope">{{
            formatDateTime(scope.row.updatedAt)
          }}</template></el-table-column
        ></el-table
      >
      <SkhPagination
        v-if="data"
        class="knowledge-documents-pagination"
        :total="data.total"
        :current-page="data.page"
        :page-size="data.pageSize"
        aria-label="知识内容列表分页"
        @current-change="handlePageChange"
        @size-change="handlePageSizeChange"
      />
      <p v-if="error && data" class="knowledge-document-error">刷新失败：{{ error }}</p>
    </section>
    <CreateKnowledgeDocumentDialog
      :open="createOpen"
      @close="createOpen = false"
      @created="resetAndLoad"
    />
  </div>
</template>

<style src="../knowledge-documents.css"></style>
