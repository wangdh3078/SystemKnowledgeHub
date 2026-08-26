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
  ImpactTarget,
  ImpactTargetType,
} from '../api/impactContracts'

const props = defineProps<{ documentId: number }>()
const router = useRouter()
const pageSize = 20
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
    title: '明确适用范围',
    description: '当前需求通过“适用于”明确声明的范围。',
  },
  DocumentedByRequirement: {
    title: '需求直接文档化的上下文',
    description: '当前需求通过“说明”关系文档化的对象。',
  },
  DocumentedBySpecification: {
    title: '本规格直接文档化的对象',
    description: '当前规格说明通过“说明”关系文档化的对象。',
  },
  DocumentedByTestCase: {
    title: '测试用例直接文档化的对象',
    description: '当前测试用例通过“说明”关系文档化的对象。',
  },
  UpstreamRequirementScope: {
    title: '上游需求声明的适用范围',
    description: '来自定义当前规格说明的上游需求，不表示规格说明自身声明了适用关系。',
  },
  UpstreamRequirementDocumentedContext: {
    title: '上游需求文档化的上下文',
    description: '来自定义当前规格说明的上游需求。',
  },
  VerifiedRequirementScope: {
    title: '直接验证需求的适用范围',
    description: '来自当前测试用例直接定义验证方式的需求。',
  },
  VerifiedSpecificationDocumentedContext: {
    title: '所验证规格说明文档化的上下文',
    description: '来自当前测试用例直接定义验证方式的规格说明。',
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
      const copy = meaning === 'DocumentedBySpecification'
        && groupedItems.some((item) => item.pathKind === 'ViaSpecificationDocuments')
        ? {
            title: '由规格说明带入的上下文',
            description: '来自定义当前需求的规格说明，不表示需求自身直接文档化了该对象。',
          }
        : meaningCopy[meaning]
      return { meaning, copy, items: groupedItems }
    })
    .filter((group) => group.items.length > 0)
})

const visibleRange = computed(() => {
  if (!impact.value || impact.value.total === 0) return '0 / 0'
  const start = (impact.value.page - 1) * impact.value.pageSize + 1
  const end = Math.min(impact.value.page * impact.value.pageSize, impact.value.total)
  return `${start}–${end} / ${impact.value.total}`
})

function impactErrorMessage(reason: unknown): string {
  if (reason instanceof ApiError && reason.status === 422 && reason.response.code === 'reference_invalid')
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
      pageSize,
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
  if (nextPage === page.value) return
  page.value = nextPage
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
      return `当前需求 → 由规格说明定义 → 规格说明 → 说明 → ${item.target.title}`
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
  <section class="impact-context-section" aria-labelledby="impact-context-heading" :aria-busy="loading">
    <div class="impact-context-section__heading">
      <div>
        <h2 id="impact-context-heading">影响上下文</h2>
        <p>基于已支持的显式关系提示可能需要人工复核的结构化上下文，不代表实际或必然影响。</p>
      </div>
      <span v-if="loading && impact" class="impact-context-section__refreshing" role="status">正在更新…</span>
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
            <li v-for="item in group.items" :key="`${item.pathKind}-${item.target.type}-${item.target.id}-${item.path.map((segment) => segment.relationshipId).join('-')}`">
              <div class="impact-context-item__primary">
                <button
                  type="button"
                  class="impact-context-item__target"
                  :aria-label="`打开${targetTypeLabels[item.target.type]} ${item.target.title}`"
                  @click="navigate(item.target)"
                >
                  {{ item.target.title }}
                </button>
                <span>{{ targetTypeLabels[item.target.type] }}</span>
              </div>
              <p class="impact-context-item__path">{{ pathText(item) }}</p>
              <p v-if="item.target.systemContext.length" class="impact-context-item__system">
                系统上下文：{{ item.target.systemContext.map((system) => system.name).join('、') }}
              </p>
            </li>
          </ul>
        </section>
      </div>
      <footer v-if="impact.total > 0" class="impact-context-pagination skh-pagination">
        <span aria-live="polite">当前 {{ visibleRange }}</span>
        <el-pagination
          v-if="impact.total > impact.pageSize"
          background
          layout="prev, pager, next"
          :current-page="impact.page"
          :page-size="impact.pageSize"
          :total="impact.total"
          aria-label="影响上下文分页"
          @current-change="changePage"
        />
      </footer>
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

.impact-context-section__heading h2 { font-size: 18px; }
.impact-context-section__heading p,
.impact-context-group header p {
  margin: var(--space-1) 0 0;
  color: var(--color-muted);
  font-size: 12px;
  line-height: 1.55;
}
.impact-context-section__refreshing { color: var(--color-muted); font-size: 12px; white-space: nowrap; }
.impact-context-section__groups { display: grid; gap: var(--space-5); margin-top: var(--space-5); }
.impact-context-group { min-width: 0; }
.impact-context-group h3 { font-size: 15px; }
.impact-context-list { display: grid; gap: 0; margin: var(--space-3) 0 0; padding: 0; list-style: none; }
.impact-context-list li { min-width: 0; padding: var(--space-3) 0; border-top: 1px solid var(--color-border); }
.impact-context-item__primary { display: flex; flex-wrap: wrap; align-items: center; gap: var(--space-2); }
.impact-context-item__primary > span { color: var(--color-muted); font-size: 11px; }
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
.impact-context-item__target:hover { text-decoration: underline; }
.impact-context-item__target:focus-visible { outline: 2px solid var(--color-primary); outline-offset: 3px; border-radius: 2px; }
.impact-context-item__path,
.impact-context-item__system { margin: var(--space-1) 0 0; overflow-wrap: anywhere; color: var(--color-muted); font-size: 11px; line-height: 1.55; }
.impact-context-item__system { color: var(--color-text-secondary, var(--color-muted)); }
.impact-context-pagination { margin-top: var(--space-4); }

@media (max-width: 720px) {
  .impact-context-section__heading { align-items: stretch; flex-direction: column; }
}
</style>
