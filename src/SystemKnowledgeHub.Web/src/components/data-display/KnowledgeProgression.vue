<script setup lang="ts">
import type { KnowledgeStatus } from '../../api/contracts/knowledge'

defineProps<{ status: KnowledgeStatus }>()

const steps: readonly { status: KnowledgeStatus; label: string }[] = [
  { status: 'Unknown', label: '未知' },
  { status: 'Inferred', label: '推断' },
  { status: 'Confirmed', label: '已确认' },
]

const order: Readonly<Record<KnowledgeStatus, number>> = { Unknown: 0, Inferred: 1, Confirmed: 2 }

function stepState(step: KnowledgeStatus, current: KnowledgeStatus): 'complete' | 'current' | 'future' {
  if (order[step] < order[current]) return 'complete'
  if (step === current) return 'current'
  return 'future'
}
</script>

<template>
  <div class="knowledge-progression" aria-label="知识进展：未知到推断再到已确认">
    <template v-for="(step, index) in steps" :key="step.status">
      <div class="knowledge-progression__step" :data-state="stepState(step.status, status)">
        <span class="knowledge-progression__node" aria-hidden="true"></span>
        <span>{{ step.label }}</span>
      </div>
      <span v-if="index < steps.length - 1" class="knowledge-progression__line" aria-hidden="true"></span>
    </template>
  </div>
</template>

<style scoped>
.knowledge-progression { display: flex; align-items: center; width: 100%; }
.knowledge-progression__step { display: flex; align-items: center; gap: 7px; color: var(--color-muted); font-size: 12px; font-weight: 650; white-space: nowrap; }
.knowledge-progression__node { width: 9px; height: 9px; border: 2px solid var(--color-border-strong); border-radius: 50%; background: var(--color-surface); box-sizing: border-box; }
.knowledge-progression__line { flex: 1; min-width: 22px; height: 1px; margin: 0 8px; background: var(--color-border); }
.knowledge-progression__step[data-state='complete'] { color: #25635b; }
.knowledge-progression__step[data-state='complete'] .knowledge-progression__node { border-color: #3c8b80; background: #3c8b80; }
.knowledge-progression__step[data-state='current'] { color: var(--color-primary); }
.knowledge-progression__step[data-state='current'] .knowledge-progression__node { border-color: var(--color-primary); background: var(--color-primary); box-shadow: 0 0 0 3px var(--color-primary-soft); }
</style>
