<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ApiError } from '../../../api/errors/ApiError'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import { useOverlayStore } from '../../../app/stores/overlays'
import { getKnowledgeDocumentTraceability } from '../api/traceabilityApi'
import type {
  RequirementTraceabilityResponse,
  SpecificationTraceabilityResponse,
  TestCaseTraceabilityResponse,
  TraceDocument,
  TraceDocumentRelation,
  TraceRelationship,
  TraceabilityResponse,
} from '../api/traceabilityContracts'
import TraceDocumentNode from './TraceDocumentNode.vue'

const props = defineProps<{ documentId: number }>()

const router = useRouter()
const overlayStore = useOverlayStore()
const traceability = ref<TraceabilityResponse | null>(null)
const loading = ref(false)
const errorMessage = ref<string | null>(null)
let requestSequence = 0
let activeController: AbortController | null = null

const isArchivedRoot = computed(
  () => traceability.value?.coverage.eligibility === 'ExcludedArchived',
)
function isRequirementTrace(
  response: TraceabilityResponse | null,
): response is RequirementTraceabilityResponse {
  return response?.root.documentType === 'Requirement'
}
function isSpecificationTrace(
  response: TraceabilityResponse | null,
): response is SpecificationTraceabilityResponse {
  return response?.root.documentType === 'Specification'
}
function isTestCaseTrace(
  response: TraceabilityResponse | null,
): response is TestCaseTraceabilityResponse {
  return response?.root.documentType === 'TestCase'
}
const requirementTrace = computed<RequirementTraceabilityResponse | null>(() => {
  const response = traceability.value
  return isRequirementTrace(response) ? response : null
})
const specificationTrace = computed<SpecificationTraceabilityResponse | null>(() => {
  const response = traceability.value
  return isSpecificationTrace(response) ? response : null
})
const testCaseTrace = computed<TestCaseTraceabilityResponse | null>(() => {
  const response = traceability.value
  return isTestCaseTrace(response) ? response : null
})

function traceErrorMessage(reason: unknown): string {
  if (
    reason instanceof ApiError &&
    reason.status === 422 &&
    reason.response.code === 'reference_invalid'
  ) {
    return '可追溯关系中存在无效引用，无法安全展示该链路。'
  }
  return '当前无法读取可追溯关系，请稍后重新加载。'
}

function rootRevisionCoverageText(): string {
  const coverage = traceability.value?.root.confirmationCoverage
  if (!coverage) return '待确认'
  if (coverage.state === 'NoConfirmation') return '未人工确认'
  if (coverage.state === 'LegacyConfirmationUnknown') return '无法确认覆盖修订情况'
  if (coverage.state === 'CurrentRevisionConfirmed') {
    return `已人工确认 · 修订 ${coverage.lastConfirmedRevisionNumber}`
  }
  return '已确认后有更新'
}

function coverageSpecificationText(count: number): string {
  return count > 0 ? `规格说明 ${count}` : '规格说明 未关联'
}

function coverageTestDefinitionText(count: number): string {
  return count > 0 ? `测试定义 ${count}` : '测试定义 未关联'
}

async function load(): Promise<void> {
  const requestedId = props.documentId
  const sequence = ++requestSequence
  activeController?.abort()
  const controller = new AbortController()
  activeController = controller
  loading.value = true
  errorMessage.value = null
  try {
    const response = await getKnowledgeDocumentTraceability(requestedId, controller.signal)
    if (sequence === requestSequence && props.documentId === requestedId) {
      traceability.value = response
    }
  } catch (reason: unknown) {
    if (controller.signal.aborted) return
    if (sequence === requestSequence && props.documentId === requestedId) {
      traceability.value = null
      errorMessage.value = traceErrorMessage(reason)
    }
  } finally {
    if (sequence === requestSequence) loading.value = false
  }
}

function refresh(): void {
  void load()
}

function navigate(document: TraceDocument): void {
  void router.push({ name: 'knowledge-document-detail', params: { id: String(document.id) } })
}

function inspectRelationship(relationship: TraceRelationship): void {
  overlayStore.openDrawer({ kind: 'relationship', id: relationship.id, mode: 'read' })
}

function hasMissing(code: 'MissingSpecification' | 'MissingTestDefinition'): boolean {
  return traceability.value?.coverage.missingLinkCodes.includes(code) ?? false
}

function relationItems(items: readonly TraceDocumentRelation[]): readonly TraceDocumentRelation[] {
  return items
}

watch(
  () => props.documentId,
  () => {
    traceability.value = null
    errorMessage.value = null
    void load()
  },
  { immediate: true },
)

onBeforeUnmount(() => activeController?.abort())

defineExpose({ refresh })
</script>

<template>
  <section class="traceability-section" aria-labelledby="traceability-heading" :aria-busy="loading">
    <div class="traceability-section__heading">
      <div>
        <h2 id="traceability-heading">可追溯性</h2>
        <p>展示当前文档的结构关系与可信上下文。</p>
      </div>
      <span v-if="loading && traceability" class="traceability-section__refreshing" role="status"
        >正在更新…</span
      >
    </div>

    <LoadingState v-if="loading && !traceability" message="正在读取可追溯性…" />
    <ErrorState
      v-else-if="errorMessage"
      title="可追溯性加载失败"
      :message="errorMessage"
      @retry="refresh"
    />
    <template v-else-if="traceability">
      <div class="traceability-section__notices" aria-live="polite">
        <p
          v-if="isArchivedRoot"
          class="traceability-notice traceability-notice--info"
          role="status"
        >
          此文档已归档，不计入当前可追溯覆盖。
        </p>
        <p
          v-if="traceability.cycleDetected"
          class="traceability-notice traceability-notice--warning"
          role="status"
        >
          检测到循环关系，已停止继续展开。
        </p>
        <p
          v-if="traceability.isTruncated"
          class="traceability-notice traceability-notice--warning"
          role="status"
        >
          可追溯关系较多，当前仅显示部分结果。
        </p>
      </div>

      <div class="traceability-section__trust" role="status" aria-label="可信依据">
        <strong>可信依据</strong>
        <span>证据 {{ traceability.root.evidenceCount }}</span>
        <span aria-hidden="true">·</span>
        <span>人工确认 {{ traceability.root.humanConfirmationCount }}</span>
        <span aria-hidden="true">·</span>
        <span>当前修订：{{ rootRevisionCoverageText() }}</span>
      </div>

      <template v-if="!isArchivedRoot">
        <div v-if="requirementTrace" class="traceability-section__content">
          <div class="traceability-coverage" aria-label="结构覆盖概览">
            <strong>结构覆盖：</strong>
            <span>{{ coverageSpecificationText(requirementTrace.specifications.length) }}</span>
            <span aria-hidden="true">·</span>
            <span>{{
              coverageTestDefinitionText(
                requirementTrace.directTestCases.length +
                  requirementTrace.specifications.reduce(
                    (count, branch) => count + branch.testCases.length,
                    0,
                  ),
              )
            }}</span>
          </div>
          <div v-if="hasMissing('MissingSpecification')" class="traceability-missing" role="status">
            规格说明关系缺失
          </div>
          <section class="traceability-group" aria-labelledby="trace-specifications-heading">
            <h3 id="trace-specifications-heading">规格说明</h3>
            <div v-if="!requirementTrace.specifications.length" class="traceability-group__empty">
              暂无规格说明关系
            </div>
            <ul v-else class="traceability-list">
              <li
                v-for="branch in requirementTrace.specifications"
                :key="branch.relationship.id"
                class="traceability-branch"
              >
                <TraceDocumentNode
                  :document="branch.document"
                  :relationship="branch.relationship"
                  relationship-label="与当前需求的关系"
                  relationship-summary="该规格说明定义当前需求"
                  @navigate="navigate"
                  @inspect-relationship="inspectRelationship"
                />
                <section
                  class="traceability-branch__children"
                  :aria-labelledby="`trace-specification-tests-${branch.document.id}`"
                >
                  <h4 :id="`trace-specification-tests-${branch.document.id}`">测试定义</h4>
                  <div
                    v-if="
                      !branch.testCases.length &&
                      branch.coverage.missingLinkCodes.includes('MissingTestDefinition')
                    "
                    class="traceability-missing"
                    role="status"
                  >
                    测试定义关系缺失
                  </div>
                  <ul
                    v-else-if="branch.testCases.length"
                    class="traceability-list traceability-list--nested"
                  >
                    <TraceDocumentNode
                      v-for="item in relationItems(branch.testCases)"
                      :key="item.relationship.id"
                      :document="item.document"
                      :relationship="item.relationship"
                      relationship-label="与上级规格说明的关系"
                      relationship-summary="该测试用例验证该规格说明"
                      @navigate="navigate"
                      @inspect-relationship="inspectRelationship"
                    />
                  </ul>
                </section>
              </li>
            </ul>
          </section>
          <section class="traceability-group" aria-labelledby="trace-direct-tests-heading">
            <h3 id="trace-direct-tests-heading">直接关联的测试定义</h3>
            <p class="traceability-group__description">
              不经过规格说明、直接与当前需求建立验证关系的测试用例。
            </p>
            <p v-if="!requirementTrace.directTestCases.length" class="traceability-group__empty">
              暂无直接关联的测试定义
            </p>
            <ul v-else class="traceability-list">
              <TraceDocumentNode
                v-for="item in relationItems(requirementTrace.directTestCases)"
                :key="item.relationship.id"
                :document="item.document"
                :relationship="item.relationship"
                relationship-label="与当前需求的关系"
                relationship-summary="该测试用例验证当前需求"
                @navigate="navigate"
                @inspect-relationship="inspectRelationship"
              />
            </ul>
          </section>
        </div>

        <div v-else-if="specificationTrace" class="traceability-section__content">
          <section class="traceability-group" aria-labelledby="trace-upstream-requirements-heading">
            <h3 id="trace-upstream-requirements-heading">上游需求</h3>
            <EmptyState
              v-if="!specificationTrace.upstreamRequirements.length"
              title="暂无上游需求关系"
              description="当前规格说明尚未关联定义它的需求。"
            />
            <ul v-else class="traceability-list">
              <TraceDocumentNode
                v-for="item in relationItems(specificationTrace.upstreamRequirements)"
                :key="item.relationship.id"
                :document="item.document"
                :relationship="item.relationship"
                relationship-label="与上级需求的关系"
                relationship-summary="该需求定义当前规格说明"
                @navigate="navigate"
                @inspect-relationship="inspectRelationship"
              />
            </ul>
          </section>
          <section class="traceability-group" aria-labelledby="trace-specification-tests-heading">
            <h3 id="trace-specification-tests-heading">测试定义</h3>
            <div
              v-if="hasMissing('MissingTestDefinition')"
              class="traceability-missing"
              role="status"
            >
              测试定义关系缺失
            </div>
            <ul v-else-if="specificationTrace.testCases.length" class="traceability-list">
              <TraceDocumentNode
                v-for="item in relationItems(specificationTrace.testCases)"
                :key="item.relationship.id"
                :document="item.document"
                :relationship="item.relationship"
                relationship-label="与当前规格说明的关系"
                relationship-summary="该测试用例验证当前规格说明"
                @navigate="navigate"
                @inspect-relationship="inspectRelationship"
              />
            </ul>
          </section>
        </div>

        <div v-else-if="testCaseTrace" class="traceability-section__content">
          <section class="traceability-group" aria-labelledby="trace-verification-targets-heading">
            <h3 id="trace-verification-targets-heading">验证对象</h3>
            <EmptyState
              v-if="
                !testCaseTrace.directRequirements.length &&
                !testCaseTrace.upstreamSpecifications.length
              "
              title="暂无验证对象关系"
              description="当前测试定义尚未关联需求或规格说明。"
            />
            <template v-else>
              <section
                v-if="testCaseTrace.directRequirements.length"
                class="traceability-target-group"
                aria-labelledby="trace-requirement-targets-heading"
              >
                <h4 id="trace-requirement-targets-heading">需求</h4>
                <ul class="traceability-list">
                  <TraceDocumentNode
                    v-for="item in relationItems(testCaseTrace.directRequirements)"
                    :key="item.relationship.id"
                    :document="item.document"
                    :relationship="item.relationship"
                    relationship-label="与当前测试定义的关系"
                    relationship-summary="该需求定义当前测试定义"
                    @navigate="navigate"
                    @inspect-relationship="inspectRelationship"
                  />
                </ul>
              </section>
              <section
                v-if="testCaseTrace.upstreamSpecifications.length"
                class="traceability-target-group"
                aria-labelledby="trace-specification-targets-heading"
              >
                <h4 id="trace-specification-targets-heading">规格说明</h4>
                <ul class="traceability-list">
                  <li
                    v-for="branch in testCaseTrace.upstreamSpecifications"
                    :key="branch.relationship.id"
                    class="traceability-branch"
                  >
                    <TraceDocumentNode
                      :document="branch.document"
                      :relationship="branch.relationship"
                      relationship-label="与上级规格说明的关系"
                      relationship-summary="该测试定义关联该上级规格说明"
                      @navigate="navigate"
                      @inspect-relationship="inspectRelationship"
                    />
                    <div
                      v-if="branch.upstreamRequirements.length"
                      class="traceability-branch__children"
                    >
                      <h5>上游需求</h5>
                      <ul class="traceability-list traceability-list--nested">
                        <TraceDocumentNode
                          v-for="item in relationItems(branch.upstreamRequirements)"
                          :key="item.relationship.id"
                          :document="item.document"
                          :relationship="item.relationship"
                          relationship-label="与上级需求的关系"
                          relationship-summary="该需求定义了上级规格说明"
                          @navigate="navigate"
                          @inspect-relationship="inspectRelationship"
                        />
                      </ul>
                    </div>
                  </li>
                </ul>
              </section>
            </template>
          </section>
        </div>
      </template>

      <section
        v-if="
          traceability.lineage.incoming.length ||
          traceability.lineage.outgoing.length ||
          traceability.lineage.isTruncated
        "
        class="traceability-group traceability-lineage"
        aria-labelledby="trace-lineage-heading"
      >
        <h3 id="trace-lineage-heading">替代关系</h3>
        <p
          v-if="traceability.lineage.isTruncated"
          class="traceability-notice traceability-notice--warning"
          role="status"
        >
          仅显示部分替代关系。
        </p>
        <section
          v-if="traceability.lineage.outgoing.length"
          class="traceability-target-group"
          aria-labelledby="trace-lineage-outgoing-heading"
        >
          <h4 id="trace-lineage-outgoing-heading">此文档替代</h4>
          <ul class="traceability-list">
            <TraceDocumentNode
              v-for="item in relationItems(traceability.lineage.outgoing)"
              :key="item.relationship.id"
              :document="item.document"
              :relationship="item.relationship"
              relationship-label="与当前文档的关系"
              relationship-summary="当前文档替代该文档"
              @navigate="navigate"
              @inspect-relationship="inspectRelationship"
            />
          </ul>
        </section>
        <section
          v-if="traceability.lineage.incoming.length"
          class="traceability-target-group"
          aria-labelledby="trace-lineage-incoming-heading"
        >
          <h4 id="trace-lineage-incoming-heading">被以下文档替代</h4>
          <ul class="traceability-list">
            <TraceDocumentNode
              v-for="item in relationItems(traceability.lineage.incoming)"
              :key="item.relationship.id"
              :document="item.document"
              :relationship="item.relationship"
              relationship-label="与当前文档的关系"
              relationship-summary="该文档替代当前文档"
              @navigate="navigate"
              @inspect-relationship="inspectRelationship"
            />
          </ul>
        </section>
      </section>
    </template>
  </section>
</template>

<style scoped>
.traceability-section {
  margin-top: var(--space-6);
  padding: var(--space-5) 0;
  border-top: 1px solid var(--color-border);
  border-bottom: 1px solid var(--color-border);
}

.traceability-section__heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-3);
}

.traceability-section__heading h2,
.traceability-group h3 {
  margin: 0;
  color: var(--color-ink);
}

.traceability-section__heading h2 {
  font-size: 18px;
}
.traceability-section__heading p,
.traceability-group__description,
.traceability-group__empty {
  margin: var(--space-1) 0 0;
  color: var(--color-muted);
  font-size: 12px;
  line-height: 1.55;
}
.traceability-section__refreshing {
  color: var(--color-muted);
  font-size: 12px;
  white-space: nowrap;
}
.traceability-section__notices {
  display: grid;
  gap: var(--space-2);
  margin-top: var(--space-3);
}
.traceability-notice,
.traceability-missing {
  margin: 0;
  padding: var(--space-2) var(--space-3);
  border: 1px solid #e6c36a;
  border-radius: var(--radius-md);
  background: #fff8e6;
  color: #76520e;
  font-size: 12px;
  line-height: 1.5;
}
.traceability-notice--info {
  border-color: var(--color-border-strong);
  background: var(--color-surface-subtle);
  color: var(--color-muted);
}
.traceability-section__trust {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px var(--space-2);
  margin-top: var(--space-3);
  color: var(--color-muted);
  font-size: 12px;
  line-height: 1.5;
}
.traceability-section__trust strong {
  color: var(--color-text);
}
.traceability-section__content {
  display: grid;
  gap: var(--space-4);
  margin-top: var(--space-4);
}
.traceability-coverage {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px var(--space-2);
  color: var(--color-muted);
  font-size: 12px;
  line-height: 1.5;
}
.traceability-coverage strong {
  color: var(--color-text);
}
.traceability-group {
  min-width: 0;
}
.traceability-group h3 {
  font-size: 15px;
}
.traceability-list {
  display: grid;
  gap: 0;
  margin: var(--space-3) 0 0;
  padding: 0;
  list-style: none;
}
.traceability-list--nested {
  margin-top: var(--space-2);
}
.traceability-branch {
  min-width: 0;
  list-style: none;
}
.traceability-branch__children {
  margin: var(--space-1) 0 0 var(--space-4);
  padding-left: var(--space-2);
  border-left: 1px solid var(--color-border);
}
.traceability-branch__children h4,
.traceability-target-group h4,
.traceability-branch__children h5 {
  margin: 0;
  color: var(--color-muted);
  font-size: 12px;
  font-weight: 680;
}
.traceability-branch__children h5 {
  margin-top: var(--space-2);
}
.traceability-target-group + .traceability-target-group {
  margin-top: var(--space-4);
}
.traceability-missing {
  display: inline-flex;
  margin-top: var(--space-3);
}
.traceability-lineage {
  margin-top: var(--space-5);
  padding-top: var(--space-5);
  border-top: 1px solid var(--color-border);
}
.traceability-lineage .traceability-target-group {
  margin-top: var(--space-3);
}

@media (max-width: 720px) {
  .traceability-section__heading {
    align-items: stretch;
    flex-direction: column;
  }
  .traceability-branch__children {
    margin-left: var(--space-3);
  }
}
</style>
