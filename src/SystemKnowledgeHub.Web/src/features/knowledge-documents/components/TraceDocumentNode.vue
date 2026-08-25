<script setup lang="ts">
import { computed } from 'vue'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import { knowledgeStatusLabels } from '../../../api/contracts/knowledge'
import { documentTypeLabels, lifecycleLabels } from '../api/knowledgeDocumentContracts'
import type { TraceDocument, TraceRelationship } from '../api/traceabilityContracts'

const props = withDefaults(
  defineProps<{
    document: TraceDocument
    relationship?: TraceRelationship
    relationshipLabel?: string
  }>(),
  {
    relationship: undefined,
    relationshipLabel: undefined,
  },
)

const emit = defineEmits<{
  navigate: [document: TraceDocument]
  inspectRelationship: [relationship: TraceRelationship]
}>()

const confirmationCoverageText = computed(() => {
  const coverage = props.document.confirmationCoverage
  if (coverage.state === 'NoConfirmation') return '尚无人工确认'
  if (coverage.state === 'LegacyConfirmationUnknown') return '迁移前人工确认无法确定覆盖的修订。'
  if (coverage.state === 'CurrentRevisionConfirmed') {
    return `人工确认覆盖当前修订 ${coverage.lastConfirmedRevisionNumber}`
  }
  return '内容在最近一次确认后已修改'
})
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
    <div class="trace-document-node__tags">
      <span class="trace-document-node__type">{{ documentTypeLabels[document.documentType] }}</span>
      <el-tag size="small" effect="plain">{{ lifecycleLabels[document.lifecycleStatus] }}</el-tag>
      <KnowledgeStatusBadge :status="document.knowledgeStatus" />
    </div>
    <p class="trace-document-node__trust">
      证据 {{ document.evidenceCount }} · 人工确认 {{ document.humanConfirmationCount }} ·
      {{ confirmationCoverageText }}
    </p>
    <button
      v-if="relationship && relationshipLabel"
      class="trace-document-node__relationship"
      type="button"
      :aria-label="`查看关系详情：${relationshipLabel}`"
      @click="emit('inspectRelationship', relationship)"
    >
      {{ relationshipLabel }}
      <span>关系：{{ knowledgeStatusLabels[relationship.knowledgeStatus] }}</span>
      <span>证据 {{ relationship.evidenceCount }} · 人工确认 {{ relationship.humanConfirmationCount }}</span>
    </button>
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
.trace-document-node__relationship {
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
.trace-document-node__relationship:hover {
  text-decoration: underline;
}

.trace-document-node__title:focus-visible,
.trace-document-node__relationship:focus-visible {
  border-radius: var(--radius-sm);
  outline: 2px solid rgb(99 91 197 / 45%);
  outline-offset: 3px;
}

.trace-document-node__tags {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
  margin-top: var(--space-2);
}

.trace-document-node__type {
  padding: 2px 7px;
  border: 1px solid var(--color-border-strong);
  border-radius: 999px;
  color: var(--color-muted);
  font-size: 11px;
}

.trace-document-node__trust {
  margin: var(--space-2) 0 0;
  color: var(--color-muted);
  font-size: 11px;
  line-height: 1.5;
}

.trace-document-node__relationship {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  margin-top: var(--space-2);
  padding: 0;
  color: var(--color-text);
  font-size: 11px;
  line-height: 1.45;
}

.trace-document-node__relationship span {
  color: var(--color-muted);
}
</style>
