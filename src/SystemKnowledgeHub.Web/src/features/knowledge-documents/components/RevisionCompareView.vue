<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import {
  lifecycleLabels,
  type DocumentLifecycleStatus,
  type KnowledgeDocumentRevisionDetail,
  type KnowledgeDocumentRevisionListItem,
  type RevisionOrigin,
} from '../api/knowledgeDocumentContracts'
import { getKnowledgeDocumentRevision } from '../api/knowledgeDocumentsApi'
import {
  compareRevisionSnapshots,
  type FieldComparison,
  type FieldComparisonStatus,
  type RevisionComparison,
} from '../compare/revisionCompare'
import type { LineDiffKind } from '../compare/myersLineDiff'

const props = defineProps<{
  documentId: number
  revisionCount: number
  initialToRevisionNumber: number
  initialSnapshot?: KnowledgeDocumentRevisionDetail | null
}>()
const emit = defineEmits<{ return: [] }>()

const originLabels: Readonly<Record<RevisionOrigin, string>> = {
  Created: '创建',
  ContentSave: '内容保存',
  Restore: '历史恢复',
  MigrationBaseline: '迁移基线',
}
const fieldStatusLabels: Readonly<Record<FieldComparisonStatus, string>> = {
  unchanged: '未变化',
  added: '新增',
  removed: '删除',
  changed: '已修改',
}
const diffLabels: Readonly<Record<LineDiffKind, string>> = {
  unchanged: '未变化',
  added: '新增',
  removed: '删除',
}
const diffPrefixes: Readonly<Record<LineDiffKind, string>> = {
  unchanged: ' ',
  added: '+',
  removed: '-',
}

const initialTo = Math.min(Math.max(props.initialToRevisionNumber, 1), props.revisionCount)
const fromRevisionNumber = ref(initialTo > 1 ? initialTo - 1 : 1)
const toRevisionNumber = ref(initialTo)
const fromDetail = ref<KnowledgeDocumentRevisionDetail | null>(null)
const toDetail = ref<KnowledgeDocumentRevisionDetail | null>(null)
const comparison = ref<RevisionComparison | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)
const normalizedNotice = ref<string | null>(null)
const snapshotCache = new Map<number, KnowledgeDocumentRevisionDetail>()
let activeRequest: AbortController | null = null
let requestSequence = 0

if (
  props.initialSnapshot
  && props.initialSnapshot.knowledgeDocumentId === props.documentId
  && props.initialSnapshot.revisionNumber === initialTo
) {
  snapshotCache.set(initialTo, props.initialSnapshot)
}

const revisionOptions = computed(() =>
  Array.from({ length: props.revisionCount }, (_, index) => index + 1),
)
const samePair = computed(() => fromRevisionNumber.value === toRevisionNumber.value)
const earliestPair = computed(() => samePair.value && toRevisionNumber.value === 1)
const metadataCards = computed(() => {
  if (!fromDetail.value || !toDetail.value) return []
  return [
    { direction: '从', snapshot: fromDetail.value },
    { direction: '到', snapshot: toDetail.value },
  ] as const
})

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
function displayValue(value: string | null): string {
  return value ?? '（空）'
}
function fieldStatusLabel(field: FieldComparison): string {
  return fieldStatusLabels[field.status]
}
function setNormalizedPair(first: number, second: number): void {
  if (!Number.isSafeInteger(first) || first < 1 || first > props.revisionCount) return
  if (!Number.isSafeInteger(second) || second < 1 || second > props.revisionCount) return
  const from = Math.min(first, second)
  const to = Math.max(first, second)
  normalizedNotice.value = first > second ? '已按较早到较新修订调整比较方向。' : null
  fromRevisionNumber.value = from
  toRevisionNumber.value = to
}
function handleFromChange(value: number): void {
  setNormalizedPair(value, toRevisionNumber.value)
}
function handleToChange(value: number): void {
  setNormalizedPair(fromRevisionNumber.value, value)
}
async function loadPair(): Promise<void> {
  const sequence = ++requestSequence
  activeRequest?.abort()
  activeRequest = null
  error.value = null
  comparison.value = null
  fromDetail.value = null
  toDetail.value = null
  loading.value = false
  if (samePair.value) return

  const request = new AbortController()
  activeRequest = request
  loading.value = true
  const requestedNumbers = [...new Set([fromRevisionNumber.value, toRevisionNumber.value])]
  const missingNumbers = requestedNumbers.filter((number) => !snapshotCache.has(number))
  try {
    const loaded = await Promise.all(
      missingNumbers.map(async (revisionNumber) => ({
        revisionNumber,
        snapshot: await getKnowledgeDocumentRevision(
          props.documentId,
          revisionNumber,
          request.signal,
        ),
      })),
    )
    if (sequence !== requestSequence) return
    for (const item of loaded) snapshotCache.set(item.revisionNumber, item.snapshot)
    const from = snapshotCache.get(fromRevisionNumber.value)
    const to = snapshotCache.get(toRevisionNumber.value)
    if (!from || !to) throw new Error('无法读取完整的修订组合。')
    fromDetail.value = from
    toDetail.value = to
    comparison.value = compareRevisionSnapshots(from, to)
  } catch (reason: unknown) {
    if (sequence === requestSequence && !isAbort(reason)) {
      error.value = reason instanceof Error ? reason.message : '无法比较所选修订。'
    }
  } finally {
    if (sequence === requestSequence) {
      loading.value = false
      activeRequest = null
    }
  }
}
function retry(): void {
  void loadPair()
}

watch([fromRevisionNumber, toRevisionNumber], () => void loadPair(), { immediate: true })
onBeforeUnmount(() => {
  requestSequence += 1
  activeRequest?.abort()
})
</script>

<template>
  <section class="knowledge-document-compare" aria-labelledby="revision-compare-heading">
    <header class="knowledge-document-history__header">
      <div>
        <h2 id="revision-compare-heading">比较修订</h2>
        <p>从较早修订到较新修订比较不可变快照；正文按行显示纯文本差异。</p>
      </div>
      <el-button type="primary" plain @click="emit('return')">返回修订历史</el-button>
    </header>

    <div class="knowledge-document-compare__selectors" aria-label="比较方向">
      <label for="compare-from-revision">从</label>
      <el-select
        id="compare-from-revision"
        :model-value="fromRevisionNumber"
        aria-label="从修订"
        @change="handleFromChange"
      >
        <el-option
          v-for="revisionNumber in revisionOptions"
          :key="`from-${revisionNumber}`"
          :label="`修订 ${revisionNumber}`"
          :value="revisionNumber"
        />
      </el-select>
      <span aria-hidden="true">→</span>
      <label for="compare-to-revision">到</label>
      <el-select
        id="compare-to-revision"
        :model-value="toRevisionNumber"
        aria-label="到修订"
        @change="handleToChange"
      >
        <el-option
          v-for="revisionNumber in revisionOptions"
          :key="`to-${revisionNumber}`"
          :label="`修订 ${revisionNumber}`"
          :value="revisionNumber"
        />
      </el-select>
      <strong v-if="!samePair">从 修订 {{ fromRevisionNumber }} 到 修订 {{ toRevisionNumber }}</strong>
      <strong v-else>请选择两个不同的修订</strong>
    </div>
    <p v-if="normalizedNotice" class="knowledge-document-compare__notice" role="status">
      {{ normalizedNotice }}
    </p>

    <div v-if="earliestPair" class="knowledge-document-history__empty" role="status">
      <strong>这是最早的修订，没有更早版本可比较</strong>
      <p>可在上方选择其他两个修订。</p>
    </div>
    <div v-else-if="samePair" class="knowledge-document-history__empty" role="status">
      <strong>两个修订相同，没有可比较的变化</strong>
      <p>请选择两个不同的修订。</p>
    </div>
    <LoadingState v-else-if="loading" message="正在加载两个修订快照…" />
    <ErrorState
      v-else-if="error"
      title="修订比较加载失败"
      :message="error"
      @retry="retry"
    />
    <template v-else-if="fromDetail && toDetail && comparison">
      <div class="knowledge-document-compare__metadata" aria-label="修订元数据">
        <article v-for="card in metadataCards" :key="card.direction">
          <header>
            <strong>{{ card.direction }} · 修订 {{ card.snapshot.revisionNumber }}</strong>
            <span v-if="card.snapshot.isCurrent" class="knowledge-document-history__marker">当前版本</span>
            <span
              v-if="card.snapshot.isLatestPublished"
              class="knowledge-document-history__marker is-published"
            >最近发布</span>
          </header>
          <dl>
            <div><dt>来源</dt><dd>{{ originLabels[card.snapshot.revisionOrigin] }}</dd></div>
            <div><dt>作者快照</dt><dd>{{ authorLabel(card.snapshot) }}</dd></div>
            <div><dt>{{ capturedAtLabel(card.snapshot) }}</dt><dd>{{ formatDate(card.snapshot.createdAt) }}</dd></div>
            <div><dt>修订生成时生命周期</dt><dd>{{ lifecycleLabel(card.snapshot.lifecycleContext) }}</dd></div>
            <div v-if="card.snapshot.restoredFromRevisionNumber"><dt>恢复来源</dt><dd>从修订 {{ card.snapshot.restoredFromRevisionNumber }} 恢复</dd></div>
            <div v-if="card.snapshot.restoreReason"><dt>恢复原因</dt><dd>{{ card.snapshot.restoreReason }}</dd></div>
          </dl>
        </article>
      </div>

      <div v-if="comparison.kind === 'oversized'" class="knowledge-document-compare__oversized" role="alert">
        <strong>该版本组合超出比较限制，未生成差异结果</strong>
        <p>这两个修订内容过大，无法在页面内比较。请返回修订历史分别查看修订内容。</p>
      </div>
      <div v-else class="knowledge-document-compare__result">
        <p v-if="comparison.identical" class="knowledge-document-compare__identical" role="status">
          两个修订的标题、摘要和正文内容一致。
        </p>
        <section class="knowledge-document-compare__field" aria-labelledby="title-diff-heading">
          <header>
            <h3 id="title-diff-heading">标题变化</h3>
            <span>{{ fieldStatusLabel(comparison.title) }}</span>
          </header>
          <pre v-if="comparison.title.status === 'unchanged'">{{ displayValue(comparison.title.to) }}</pre>
          <div v-else class="knowledge-document-compare__field-values">
            <div><strong>旧版本</strong><pre>{{ displayValue(comparison.title.from) }}</pre></div>
            <div><strong>新版本</strong><pre>{{ displayValue(comparison.title.to) }}</pre></div>
          </div>
        </section>
        <section class="knowledge-document-compare__field" aria-labelledby="summary-diff-heading">
          <header>
            <h3 id="summary-diff-heading">摘要变化</h3>
            <span>{{ fieldStatusLabel(comparison.summary) }}</span>
          </header>
          <pre v-if="comparison.summary.status === 'unchanged'">{{ displayValue(comparison.summary.to) }}</pre>
          <div v-else class="knowledge-document-compare__field-values">
            <div><strong>旧版本</strong><pre>{{ displayValue(comparison.summary.from) }}</pre></div>
            <div><strong>新版本</strong><pre>{{ displayValue(comparison.summary.to) }}</pre></div>
          </div>
        </section>
        <section class="knowledge-document-compare__body" aria-labelledby="body-diff-heading">
          <header>
            <h3 id="body-diff-heading">正文变化</h3>
            <div class="knowledge-document-compare__legend" aria-label="差异图例">
              <span><b>+</b> 新增</span>
              <span><b>-</b> 删除</span>
              <span><b>&nbsp;</b> 未变化</span>
            </div>
          </header>
          <p v-if="comparison.body.length === 0" class="text-muted">正文未变化（均为空）。</p>
          <div v-else class="knowledge-document-compare__lines" aria-label="正文逐行差异">
            <template v-for="(segment, segmentIndex) in comparison.body" :key="segmentIndex">
              <div
                v-for="(line, lineIndex) in segment.lines"
                :key="`${segmentIndex}-${lineIndex}`"
                :class="['knowledge-document-compare__line', `is-${segment.kind}`]"
                :aria-label="`${diffLabels[segment.kind]}：${line}`"
              >
                <span aria-hidden="true">{{ diffPrefixes[segment.kind] }}</span><code>{{ line }}</code>
              </div>
            </template>
          </div>
        </section>
      </div>
    </template>
  </section>
</template>
