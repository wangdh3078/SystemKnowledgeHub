<script setup lang="ts">
/* eslint-disable vue/no-v-html -- Historical Markdown uses the shared HTML-disabled renderer. */
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import {
  getKnowledgeDocumentRevision,
  listKnowledgeDocumentRevisions,
} from '../api/knowledgeDocumentsApi'
import {
  lifecycleLabels,
  type DocumentLifecycleStatus,
  type KnowledgeDocumentRevisionDetail,
  type KnowledgeDocumentRevisionListItem,
  type RevisionOrigin,
} from '../api/knowledgeDocumentContracts'
import { renderMarkdown } from '../markdown/renderMarkdown'
import RevisionCompareView from './RevisionCompareView.vue'

const props = defineProps<{
  documentId: number
  currentRevisionNumber: number
}>()
const emit = defineEmits<{ return: [] }>()

const pageSize = 20
const page = ref(1)
const total = ref(0)
const items = ref<readonly KnowledgeDocumentRevisionListItem[]>([])
const selectedRevisionNumber = ref<number | null>(null)
const detail = ref<KnowledgeDocumentRevisionDetail | null>(null)
const listLoading = ref(false)
const detailLoading = ref(false)
const listError = ref<string | null>(null)
const detailError = ref<string | null>(null)
const compareMode = ref(false)
const compareInitialRevisionNumber = ref<number | null>(null)
const compareInitialSnapshot = ref<KnowledgeDocumentRevisionDetail | null>(null)
let listRequest: AbortController | null = null
let detailRequest: AbortController | null = null

const originLabels: Readonly<Record<RevisionOrigin, string>> = {
  Created: '创建',
  ContentSave: '内容保存',
  Restore: '历史恢复',
  MigrationBaseline: '迁移基线',
}
const renderedBody = computed(() => (detail.value ? renderMarkdown(detail.value.bodyMarkdown) : ''))

function isAbort(reason: unknown): boolean {
  return reason instanceof DOMException && reason.name === 'AbortError'
}
function formatDate(value: string): string {
  return value.replace('T', ' ').slice(0, 16)
}
function authorLabel(item: KnowledgeDocumentRevisionListItem): string {
  return item.revisionOrigin === 'MigrationBaseline'
    ? '历史作者未知'
    : (item.authorDisplayName ?? '历史作者未知')
}
function capturedAtLabel(item: KnowledgeDocumentRevisionListItem): string {
  return item.revisionOrigin === 'MigrationBaseline' ? '捕获于' : '生成于'
}
function lifecycleLabel(value: DocumentLifecycleStatus): string {
  return lifecycleLabels[value]
}
async function selectRevision(item: KnowledgeDocumentRevisionListItem): Promise<void> {
  selectedRevisionNumber.value = item.revisionNumber
  detailRequest?.abort()
  const request = new AbortController()
  detailRequest = request
  detailLoading.value = true
  detailError.value = null
  detail.value = null
  try {
    detail.value = await getKnowledgeDocumentRevision(
      props.documentId,
      item.revisionNumber,
      request.signal,
    )
  } catch (reason: unknown) {
    if (!isAbort(reason)) {
      detailError.value = reason instanceof Error ? reason.message : '无法读取该修订。'
    }
  } finally {
    if (detailRequest === request) {
      detailLoading.value = false
      detailRequest = null
    }
  }
}
async function loadList(): Promise<void> {
  listRequest?.abort()
  detailRequest?.abort()
  const request = new AbortController()
  listRequest = request
  listLoading.value = true
  listError.value = null
  detailError.value = null
  detail.value = null
  selectedRevisionNumber.value = null
  try {
    const response = await listKnowledgeDocumentRevisions(
      props.documentId,
      page.value,
      pageSize,
      request.signal,
    )
    if (listRequest !== request) return
    items.value = response.items
    total.value = response.total
    if (response.items.length > 0) await selectRevision(response.items[0])
  } catch (reason: unknown) {
    if (!isAbort(reason)) {
      items.value = []
      total.value = 0
      listError.value = reason instanceof Error ? reason.message : '无法加载修订历史。'
    }
  } finally {
    if (listRequest === request) {
      listLoading.value = false
      listRequest = null
    }
  }
}
function handlePageChange(nextPage: number): void {
  if (nextPage === page.value || listLoading.value) return
  page.value = nextPage
  void loadList()
}
function retryDetail(): void {
  const item = items.value.find((candidate) =>
    candidate.revisionNumber === selectedRevisionNumber.value)
  if (item) void selectRevision(item)
}
function enterCompare(): void {
  if (selectedRevisionNumber.value === null) return
  compareInitialRevisionNumber.value = selectedRevisionNumber.value
  compareInitialSnapshot.value = detail.value?.revisionNumber === selectedRevisionNumber.value
    ? detail.value
    : null
  compareMode.value = true
}
function returnToHistory(): void {
  compareMode.value = false
}

onMounted(() => void loadList())
onBeforeUnmount(() => {
  listRequest?.abort()
  detailRequest?.abort()
})
</script>

<template>
  <RevisionCompareView
    v-if="compareMode && compareInitialRevisionNumber !== null"
    :document-id="documentId"
    :revision-count="currentRevisionNumber"
    :initial-to-revision-number="compareInitialRevisionNumber"
    :initial-snapshot="compareInitialSnapshot"
    @return="returnToHistory"
  />
  <section v-else class="knowledge-document-history" aria-labelledby="revision-history-heading">
    <header class="knowledge-document-history__header">
      <div>
        <h2 id="revision-history-heading">修订历史（{{ currentRevisionNumber }}）</h2>
        <p>查看不可变的历史快照；生命周期表示该修订生成时的文档状态。</p>
      </div>
      <div class="knowledge-document-history__header-actions">
        <el-button
          :disabled="selectedRevisionNumber === null"
          type="primary"
          @click="enterCompare"
        >比较修订</el-button>
        <el-button type="primary" plain @click="emit('return')">返回当前内容</el-button>
      </div>
    </header>

    <LoadingState
      v-if="listLoading && items.length === 0"
      message="正在加载修订历史…"
    />
    <ErrorState
      v-else-if="listError && items.length === 0"
      title="修订历史加载失败"
      :message="listError"
      @retry="loadList"
    />
    <div v-else-if="items.length === 0" class="knowledge-document-history__empty" role="status">
      <strong>{{ currentRevisionNumber > 0 ? '无法加载修订历史' : '暂无修订历史' }}</strong>
      <p>未返回可显示的修订；当前内容不会被伪造为历史快照。</p>
      <el-button text type="primary" @click="loadList">重试</el-button>
    </div>
    <template v-else>
      <div class="knowledge-document-history__layout">
        <aside class="knowledge-document-history__list" aria-label="修订列表">
          <button
            v-for="item in items"
            :key="item.id"
            type="button"
            :class="['knowledge-document-history__item', { 'is-selected': selectedRevisionNumber === item.revisionNumber }]"
            :aria-current="selectedRevisionNumber === item.revisionNumber ? 'true' : undefined"
            :aria-label="`查看修订 ${item.revisionNumber}`"
            @click="selectRevision(item)"
          >
            <span class="knowledge-document-history__item-heading">
              <strong>修订 {{ item.revisionNumber }}</strong>
              <span class="knowledge-document-history__markers">
                <span v-if="item.isCurrent" class="knowledge-document-history__marker">当前版本</span>
                <span v-if="item.isLatestPublished" class="knowledge-document-history__marker is-published">最近发布</span>
              </span>
            </span>
            <span class="knowledge-document-history__item-line">
              {{ originLabels[item.revisionOrigin] }} · {{ authorLabel(item) }}
            </span>
            <span class="knowledge-document-history__item-line">
              {{ capturedAtLabel(item) }} {{ formatDate(item.createdAt) }}
            </span>
            <span class="knowledge-document-history__item-line">
              修订生成时生命周期：{{ lifecycleLabel(item.lifecycleContext) }}
            </span>
            <span v-if="item.changeSummary" class="knowledge-document-history__item-note">
              修订说明：{{ item.changeSummary }}
            </span>
            <span v-if="item.restoredFromRevisionNumber" class="knowledge-document-history__item-note">
              从修订 {{ item.restoredFromRevisionNumber }} 恢复
            </span>
            <span v-if="item.restoreReason" class="knowledge-document-history__item-note">
              原因：{{ item.restoreReason }}
            </span>
          </button>
        </aside>

        <main class="knowledge-document-history__preview" aria-live="polite">
          <LoadingState v-if="detailLoading" message="正在读取历史快照…" />
          <ErrorState
            v-else-if="detailError"
            title="历史快照加载失败"
            :message="detailError"
            @retry="retryDetail"
          />
          <article v-else-if="detail" class="knowledge-document-history__detail">
            <header>
              <div class="knowledge-document-history__detail-kicker">
                <span>修订 {{ detail.revisionNumber }}</span>
                <span>{{ originLabels[detail.revisionOrigin] }}</span>
                <span v-if="detail.isCurrent">当前版本</span>
                <span v-if="detail.isLatestPublished">最近发布</span>
              </div>
              <h2>{{ detail.title }}</h2>
              <p v-if="detail.summary">{{ detail.summary }}</p>
            </header>
            <dl class="knowledge-document-history__metadata">
              <div><dt>作者快照</dt><dd>{{ authorLabel(detail) }}</dd></div>
              <div><dt>{{ capturedAtLabel(detail) }}</dt><dd>{{ formatDate(detail.createdAt) }}</dd></div>
              <div><dt>修订生成时生命周期</dt><dd>{{ lifecycleLabel(detail.lifecycleContext) }}</dd></div>
              <div v-if="detail.changeSummary"><dt>修订说明</dt><dd>{{ detail.changeSummary }}</dd></div>
              <div v-if="detail.restoredFromRevisionNumber"><dt>恢复来源</dt><dd>从修订 {{ detail.restoredFromRevisionNumber }} 恢复</dd></div>
              <div v-if="detail.restoreReason"><dt>恢复原因</dt><dd>{{ detail.restoreReason }}</dd></div>
            </dl>
            <section class="knowledge-document-history__body">
              <h3>历史正文</h3>
              <p v-if="!detail.bodyMarkdown.trim()" class="text-muted">该修订暂无正文。</p>
              <div v-else class="knowledge-document-markdown" v-html="renderedBody"></div>
            </section>
          </article>
        </main>
      </div>
      <footer v-if="total > pageSize" class="knowledge-document-history__pagination">
        <span>{{ (page - 1) * pageSize + 1 }}–{{ Math.min(page * pageSize, total) }} / {{ total }}</span>
        <el-pagination
          background
          layout="prev, pager, next"
          :current-page="page"
          :page-size="pageSize"
          :total="total"
          aria-label="修订历史分页"
          @current-change="handlePageChange"
        />
      </footer>
    </template>
  </section>
</template>
