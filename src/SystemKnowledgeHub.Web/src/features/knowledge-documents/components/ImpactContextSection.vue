<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ApiError } from '../../../api/errors/ApiError'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import { getKnowledgeDocumentImpact } from '../api/impactApi'
import type {
  ImpactItem,
  ImpactMeaning,
  ImpactResponse,
  ImpactPathKind,
  ImpactTarget,
  ImpactTargetType,
} from '../api/impactContracts'
import SkhPagination from '../../../components/data-display/SkhPagination.vue'

const props = defineProps<{ documentId: number }>()
const router = useRouter()
const pageSize = ref(20)
const page = ref(1)
const impact = ref<ImpactResponse | null>(null)
const loading = ref(false)
const errorMessage = ref<string | null>(null)
let requestSequence = 0
let activeController: AbortController | null = null

const meaningOrder: readonly ImpactMeaning[] = [
  'ExplicitRequirementScope',
  'DocumentedByRequirement',
  'DocumentedBySpecification',
  'DocumentedByTestCase',
  'UpstreamRequirementScope',
  'UpstreamRequirementDocumentedContext',
  'VerifiedRequirementScope',
  'VerifiedSpecificationDocumentedContext',
]

const meaningCopy: Readonly<Record<ImpactMeaning, { title: string; description: string }>> = {
  ExplicitRequirementScope: {
    title: '直接关联上下文',
    description: '当前需求明确声明“适用于”该对象。',
  },
  DocumentedByRequirement: {
    title: '直接关联上下文',
    description: '当前需求通过“说明”关系直接关联该对象。',
  },
  DocumentedBySpecification: {
    title: '直接关联上下文',
    description: '当前规格说明通过“说明”关系直接关联该对象。',
  },
  DocumentedByTestCase: {
    title: '直接关联上下文',
    description: '当前测试用例通过“说明”关系直接关联该对象。',
  },
  UpstreamRequirementScope: {
    title: '间接关联上下文',
    description: '关联的上游需求声明“适用于”该对象，因此作为当前规格说明的间接复核上下文显示。',
  },
  UpstreamRequirementDocumentedContext: {
    title: '间接关联上下文',
    description: '关联的上游需求说明了该对象，因此作为当前规格说明的间接复核上下文显示。',
  },
  VerifiedRequirementScope: {
    title: '间接关联上下文',
    description: '当前测试用例验证的需求声明“适用于”该对象，因此作为间接复核上下文显示。',
  },
  VerifiedSpecificationDocumentedContext: {
    title: '间接关联上下文',
    description: '当前测试用例验证的规格说明说明了该对象，因此作为间接复核上下文显示。',
  },
}

const targetTypeLabels: Readonly<Record<ImpactTargetType, string>> = {
  System: '系统',
  BusinessFunction: '业务功能',
  DatabaseObject: '数据库对象',
  BusinessRule: '业务规则',
  Integration: '集成',
}

const groups = computed(() => {
  const items = impact.value?.items ?? []
  return meaningOrder
    .map((meaning) => {
      const groupedItems = items.filter((item) => item.meaning === meaning)
      const copy =
        meaning === 'DocumentedBySpecification' &&
        groupedItems.some((item) => item.pathKind === 'ViaSpecificationDocuments')
          ? {
              title: '间接关联上下文',
              description:
                '由当前需求关联的规格说明进一步说明了该对象，因此作为当前需求的间接复核上下文显示。',
            }
          : meaningCopy[meaning]
      return { meaning, copy, items: groupedItems }
    })
    .filter((group) => group.items.length > 0)
})

function impactErrorMessage(reason: unknown): string {
  if (
    reason instanceof ApiError &&
    reason.status === 422 &&
    reason.response.code === 'reference_invalid'
  )
    return '影响上下文中存在无法安全解析的引用。'
  return '当前无法读取影响上下文，请稍后重试。'
}

async function load(): Promise<void> {
  const requestedId = props.documentId
  const requestedPage = page.value
  const sequence = ++requestSequence
  activeController?.abort()
  const controller = new AbortController()
  activeController = controller
  loading.value = true
  errorMessage.value = null
  try {
    const response = await getKnowledgeDocumentImpact(
      requestedId,
      requestedPage,
      pageSize.value,
      controller.signal,
    )
    if (
      sequence === requestSequence &&
      props.documentId === requestedId &&
      page.value === requestedPage
    ) {
      impact.value = response
    }
  } catch (reason: unknown) {
    if (controller.signal.aborted) return
    if (
      sequence === requestSequence &&
      props.documentId === requestedId &&
      page.value === requestedPage
    ) {
      impact.value = null
      errorMessage.value = impactErrorMessage(reason)
    }
  } finally {
    if (sequence === requestSequence) loading.value = false
  }
}

function refresh(): void {
  void load()
}

function changePage(nextPage: number): void {
  page.value = nextPage
  void load()
}
function changePageSize(nextPageSize: number): void {
  pageSize.value = nextPageSize
  page.value = 1
  void load()
}

function navigate(target: ImpactTarget): void {
  const routes: Readonly<Record<ImpactTargetType, string>> = {
    System: 'system-detail',
    BusinessFunction: 'business-function-detail',
    DatabaseObject: 'database-object-detail',
    BusinessRule: 'business-rule-detail',
    Integration: 'integration-detail',
  }
  void router.push({ name: routes[target.type], params: { id: String(target.id) } })
}

function pathText(item: ImpactItem): string {
  switch (item.pathKind) {
    case 'DirectAppliesTo':
      return `当前需求 → 适用于 → ${item.target.title}`
    case 'DirectDocuments':
      return `当前文档 → 说明 → ${item.target.title}`
    case 'ViaSpecificationDocuments':
      return `当前需求 → 规格说明 → 说明 → ${item.target.title}`
    case 'ViaRequirementAppliesTo':
      return `当前规格说明 ← 定义需求 ← 上游需求 → 适用于 → ${item.target.title}`
    case 'ViaRequirementDocuments':
      return `当前规格说明 ← 定义需求 ← 上游需求 → 说明 → ${item.target.title}`
    case 'ViaVerifiedRequirementAppliesTo':
      return `当前测试用例 ← 定义验证方式 ← 需求 → 适用于 → ${item.target.title}`
    case 'ViaVerifiedSpecificationDocuments':
      return `当前测试用例 ← 定义验证方式 ← 规格说明 → 说明 → ${item.target.title}`
  }
}

function relationNature(pathKind: ImpactPathKind): string {
  return pathKind.startsWith('Via') ? '间接' : '直接'
}

function objectLabel(pathKind: ImpactPathKind): string {
  return pathKind.startsWith('Via') ? '上下文对象：' : '影响对象：'
}

function isIndirect(pathKind: ImpactPathKind): boolean {
  return pathKind.startsWith('Via')
}

watch(
  () => props.documentId,
  () => {
    page.value = 1
    impact.value = null
    errorMessage.value = null
    void load()
  },
  { immediate: true },
)

onBeforeUnmount(() => activeController?.abort())

defineExpose({ refresh })
</script>

<template>
  <section
    class="impact-context-section"
    aria-labelledby="impact-context-heading"
    :aria-busy="loading"
  >
    <div class="impact-context-section__heading">
      <div>
        <h2 id="impact-context-heading">影响上下文</h2>
        <p>基于已支持的显式关系提示可能需要人工复核的结构化上下文，不代表实际或必然影响。</p>
      </div>
      <span v-if="loading && impact" class="impact-context-section__refreshing" role="status"
        >正在更新…</span
      >
    </div>

    <LoadingState v-if="loading && !impact" message="正在读取影响上下文…" />
    <ErrorState
      v-else-if="errorMessage"
      title="影响上下文加载失败"
      :message="errorMessage"
      @retry="refresh"
    />
    <template v-else-if="impact">
      <EmptyState
        v-if="impact.total === 0"
        title="暂无影响上下文"
        description="当前没有通过已支持关系表达的影响上下文。"
      />
      <div v-else class="impact-context-section__groups" aria-live="polite">
        <section
          v-for="group in groups"
          :key="group.meaning"
          class="impact-context-group"
          :aria-labelledby="`impact-context-${group.meaning}`"
        >
          <header>
            <h3 :id="`impact-context-${group.meaning}`">{{ group.copy.title }}</h3>
            <p>{{ group.copy.description }}</p>
          </header>
          <ul class="impact-context-list">
            <li
              v-for="item in group.items"
              :key="`${item.pathKind}-${item.target.type}-${item.target.id}-${item.path.map((segment) => segment.relationshipId).join('-')}`"
            >
              <p class="impact-context-item__field">
                <span>{{ objectLabel(item.pathKind) }}</span>
                <button
                  type="button"
                  class="impact-context-item__target"
                  :aria-label="`打开${targetTypeLabels[item.target.type]} ${item.target.title}`"
                  @click="navigate(item.target)"
                >
                  {{ item.target.title }}
                </button>
              </p>
              <p class="impact-context-item__field">
                <span>类型：</span>
                {{ targetTypeLabels[item.target.type] }}
              </p>
              <p class="impact-context-item__field">
                <span>为什么显示：</span>
                {{ group.copy.description }}
              </p>
              <p class="impact-context-item__field">
                <span>关系性质：</span>
                <span
                  class="impact-context-item__nature"
                  :class="`impact-context-item__nature--${isIndirect(item.pathKind) ? 'indirect' : 'direct'}`"
                >
                  {{ relationNature(item.pathKind) }}
                </span>
              </p>
              <p
                v-if="!isIndirect(item.pathKind)"
                class="impact-context-item__field impact-context-item__path"
              >
                <span>关系路径：</span>
                {{ pathText(item) }}
              </p>
              <details v-else class="impact-context-item__path-details">
                <summary>查看关系路径</summary>
                <p class="impact-context-item__field impact-context-item__path">
                  <span>关系路径：</span>
                  {{ pathText(item) }}
                </p>
              </details>
              <p v-if="isIndirect(item.pathKind)" class="impact-context-item__notice">
                仅用于辅助人工复核，不表示当前文档一定直接影响该对象。
              </p>
              <p
                v-if="item.target.type !== 'System' && item.target.systemContext.length"
                class="impact-context-item__system"
              >
                所属系统：{{ item.target.systemContext.map((system) => system.name).join('、') }}
              </p>
            </li>
          </ul>
        </section>
      </div>
      <SkhPagination
        class="impact-context-pagination"
        :total="impact.total"
        :current-page="impact.page"
        :page-size="impact.pageSize"
        aria-label="影响上下文分页"
        @current-change="changePage"
        @size-change="changePageSize"
      />
    </template>
  </section>
</template>

<style scoped>
.impact-context-section {
  padding: var(--space-5) 0;
  border-bottom: 1px solid var(--color-border);
}

.impact-context-section__heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-3);
}

.impact-context-section__heading h2,
.impact-context-group h3 {
  margin: 0;
  color: var(--color-ink);
}

.impact-context-section__heading h2 {
  font-size: 18px;
}
.impact-context-section__heading p,
.impact-context-group header p {
  margin: var(--space-1) 0 0;
  color: var(--color-muted);
  font-size: 12px;
  line-height: 1.55;
}
.impact-context-section__refreshing {
  color: var(--color-muted);
  font-size: 12px;
  white-space: nowrap;
}
.impact-context-section__groups {
  display: grid;
  gap: var(--space-4);
  margin-top: var(--space-4);
}
.impact-context-group {
  min-width: 0;
}
.impact-context-group h3 {
  font-size: 15px;
}
.impact-context-list {
  display: grid;
  gap: 0;
  margin: var(--space-3) 0 0;
  padding: 0;
  list-style: none;
}
.impact-context-list li {
  min-width: 0;
  padding: var(--space-2) 0 var(--space-3);
  border-top: 1px solid var(--color-border);
}
.impact-context-item__metadata {
  display: grid;
  gap: 4px;
  margin-top: var(--space-2);
}
.impact-context-item__field,
.impact-context-item__system,
.impact-context-item__notice {
  margin: var(--space-1) 0 0;
  overflow-wrap: anywhere;
  color: var(--color-muted);
  font-size: 11px;
  line-height: 1.55;
}
.impact-context-item__field > span,
.impact-context-item__system > span {
  color: var(--color-subtle);
}
.impact-context-item__field {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin: var(--space-1) 0 0;
}
.impact-context-item__nature {
  display: inline-flex;
  align-items: center;
  padding: 1px 7px;
  border: 1px solid var(--color-border);
  border-radius: 999px;
  background: var(--color-surface-subtle);
  color: var(--color-muted);
  cursor: default;
  font-size: 10px;
  font-weight: 680;
  line-height: 1.4;
}
.impact-context-item__nature--indirect {
  border-style: dashed;
}
.impact-context-item__target {
  min-width: 0;
  padding: 0;
  border: 0;
  background: transparent;
  color: var(--color-primary);
  font: inherit;
  font-weight: 680;
  text-align: left;
  cursor: pointer;
}
.impact-context-item__target:hover {
  text-decoration: underline;
}
.impact-context-item__target:focus-visible {
  outline: 2px solid var(--color-primary);
  outline-offset: 3px;
  border-radius: 2px;
}
.impact-context-item__path {
  margin: var(--space-1) 0 0;
}
.impact-context-item__path-details {
  margin-top: var(--space-1);
  color: var(--color-muted);
  font-size: 11px;
}
.impact-context-item__path-details summary {
  width: fit-content;
  cursor: pointer;
  color: var(--color-muted);
}
.impact-context-item__path-details summary:hover {
  color: var(--color-primary);
}
.impact-context-item__path-details[open] summary {
  margin-bottom: 2px;
}
.impact-context-item__system {
  color: var(--color-text-secondary, var(--color-muted));
}
.impact-context-item__notice {
  color: var(--color-subtle);
}
.impact-context-pagination {
  margin-top: var(--space-4);
}

@media (max-width: 720px) {
  .impact-context-section__heading {
    align-items: stretch;
    flex-direction: column;
  }
}
</style>
