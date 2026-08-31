<script setup lang="ts">
import { computed } from 'vue'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import { documentTypeLabels, lifecycleLabels } from '../api/knowledgeDocumentContracts'
import type { TraceDocument, TraceRelationship } from '../api/traceabilityContracts'

const props = withDefaults(
  defineProps<{
    document: TraceDocument
    relationship?: TraceRelationship
    relationshipLabel?: string
    relationshipSummary?: string
  }>(),
  {
    relationship: undefined,
    relationshipLabel: undefined,
    relationshipSummary: undefined,
  },
)

const emit = defineEmits<{
  navigate: [document: TraceDocument]
  inspectRelationship: [relationship: TraceRelationship]
}>()

const confirmationCoverageText = computed(() => {
  const coverage = props.document.confirmationCoverage
  if (coverage.state === 'NoConfirmation') return '当前修订未人工确认'
  if (coverage.state === 'LegacyConfirmationUnknown') return '当前修订确认范围未知'
  if (coverage.state === 'CurrentRevisionConfirmed') {
    return `当前修订已确认 · 修订 ${coverage.lastConfirmedRevisionNumber}`
  }
  return '当前修订在确认后有更新'
})

const hasReadableRelationship = computed(
  () => !!props.relationshipLabel || !!props.relationshipSummary,
)
const readableRelationshipLabel = computed(() => props.relationshipLabel ?? '')
const readableRelationshipSummary = computed(() => props.relationshipSummary ?? '')
const relationshipButtonLabel = computed(() =>
  readableRelationshipLabel.value
    ? `查看关系详情：${readableRelationshipLabel.value}`
    : props.relationship
      ? '查看关系详情'
      : '',
)
</script>

<template>
  <li class="trace-document-node">
    <button
      class="trace-document-node__title"
      type="button"
      :aria-label="`打开${documentTypeLabels[document.documentType]}：${document.title}`"
      @click="emit('navigate', document)"
    >
      {{ document.title }}
    </button>
    <div class="trace-document-node__meta">
      <p>
        <span class="trace-document-node__meta-label">类型：</span>
        {{ documentTypeLabels[document.documentType] }}
      </p>
      <p>
        <span class="trace-document-node__meta-label">生命周期：</span>
        {{ lifecycleLabels[document.lifecycleStatus] }}
      </p>
      <p class="trace-document-node__status">
        <span class="trace-document-node__meta-label">知识状态：</span>
        <KnowledgeStatusBadge :status="document.knowledgeStatus" />
      </p>
    </div>
    <div v-if="hasReadableRelationship" class="trace-document-node__relationship">
      <p class="trace-document-node__relationship-summary">
        <span v-if="readableRelationshipLabel" class="trace-document-node__relationship-label">
          {{ readableRelationshipLabel }}：
        </span>
        <span v-if="readableRelationshipSummary">{{ readableRelationshipSummary }}</span>
        <button
          v-if="relationship"
          type="button"
          class="trace-document-node__relationship-link"
          :aria-label="relationshipButtonLabel"
          @click="emit('inspectRelationship', relationship!)"
        >
          查看关系详情
        </button>
      </p>
    </div>
    <div class="trace-document-node__trust">
      <span class="trace-document-node__trust-label">可信依据：</span>
      <span
        >证据 {{ document.evidenceCount }} · 人工确认 {{ document.humanConfirmationCount }} ·
        {{ confirmationCoverageText }}</span
      >
    </div>
  </li>
</template>

<style scoped>
.trace-document-node {
  position: relative;
  min-width: 0;
  padding: var(--space-3) 0 var(--space-3) var(--space-4);
  border-left: 1px solid var(--color-border);
}

.trace-document-node__title,
.trace-document-node__relationship-link {
  border: 0;
  background: transparent;
  cursor: pointer;
  font: inherit;
  text-align: left;
}

.trace-document-node__title {
  min-width: 0;
  padding: 0;
  color: var(--color-primary);
  font-size: 14px;
  font-weight: 680;
  line-height: 1.45;
}

.trace-document-node__title:hover,
.trace-document-node__relationship-link:hover {
  text-decoration: underline;
}

.trace-document-node__title:focus-visible,
.trace-document-node__relationship-link:focus-visible {
  border-radius: var(--radius-sm);
  outline: 2px solid rgb(99 91 197 / 45%);
  outline-offset: 3px;
}

.trace-document-node__meta {
  display: grid;
  gap: var(--space-1);
  margin-top: var(--space-2);
  color: var(--color-muted);
  font-size: 11px;
  line-height: 1.5;
}

.trace-document-node__meta p {
  margin: 0;
}

.trace-document-node__status {
  display: flex;
  align-items: center;
  gap: 6px;
}

.trace-document-node__status :deep(.knowledge-status-badge) {
  min-height: 0;
  padding: 1px 7px;
  cursor: default;
  pointer-events: none;
}

.trace-document-node__meta-label {
  color: var(--color-subtle);
}

.trace-document-node__trust {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-3);
  margin: var(--space-2) 0 0;
  color: var(--color-muted);
  font-size: 11px;
  line-height: 1.5;
}

.trace-document-node__trust span {
  display: inline-flex;
  flex-wrap: wrap;
  gap: 4px;
}

.trace-document-node__relationship {
  margin-top: var(--space-2);
  color: var(--color-muted);
  font-size: 11px;
  line-height: 1.5;
}

.trace-document-node__relationship-label {
  color: var(--color-ink);
  font-weight: 680;
}

.trace-document-node__relationship-summary {
  margin: 0;
}

.trace-document-node__relationship-link {
  margin-left: var(--space-2);
  padding: 0;
  color: var(--color-muted);
  font-size: 10px;
}

.trace-document-node__trust-label {
  color: var(--color-subtle);
}
</style>
