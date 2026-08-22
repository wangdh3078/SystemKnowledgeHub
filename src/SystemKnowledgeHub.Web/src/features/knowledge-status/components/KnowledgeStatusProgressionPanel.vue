<script setup lang="ts">
import { computed } from 'vue'
import { DocumentChecked, Promotion } from '@element-plus/icons-vue'
import type { KnowledgeStatus } from '../../../api/contracts/knowledge'
import { useOverlayStore } from '../../../app/stores/overlays'
import { useActorStore } from '../../../app/stores/actor'
import KnowledgeProgression from '../../../components/data-display/KnowledgeProgression.vue'

const props = defineProps<{
  targetType?: 'System' | 'BusinessFunction' | 'DatabaseObject' | 'DatabaseColumn' | 'BusinessRule' | 'Integration'
  id: number
  title: string
  status: KnowledgeStatus
  concurrencyToken: string
  evidenceCount: number
  humanConfirmationCount: number
  canChange: boolean
}>()

const overlayStore = useOverlayStore()
const actorStore = useActorStore()
const nextLabel = computed(() => props.status === 'Unknown' ? '推进为推断' : props.status === 'Inferred' ? '推进为已确认' : null)
const canAdvance = computed(() => props.status === 'Unknown'
  ? props.evidenceCount > 0
  : props.status === 'Inferred' ? props.humanConfirmationCount > 0 : false)
const requirementText = computed(() => {
  if (props.status === 'Unknown') {
    return props.evidenceCount > 0
      ? `该知识已登记，并已记录 ${props.evidenceCount} 条相关证据；可明确推进为“推断”。`
      : '该知识已登记，但尚未经过明确确认。请先添加至少 1 条可定位的相关证据。'
  }
  if (props.status === 'Inferred') {
    return props.humanConfirmationCount > 0
      ? `已有依据，并已记录 ${props.humanConfirmationCount} 条人工确认；可明确推进为“已确认”。`
      : '已有依据，但尚未完成人工确认。请先添加至少 1 条完整的人工确认证据。'
  }
  return '当前知识已确认；如知识发生变化，可通过填写原因的显式回退修正。'
})

function openChangeDialog(): void {
  if (!nextLabel.value) return
  overlayStore.openDialog({
    kind: 'change-knowledge-status',
    id: props.id,
    mode: 'edit',
    payload: {
      target: { type: props.targetType ?? 'BusinessFunction', id: props.id },
      title: props.title,
      knowledgeStatus: props.status,
      concurrencyToken: props.concurrencyToken,
      evidenceCount: props.evidenceCount,
      humanConfirmationCount: props.humanConfirmationCount,
    },
  })
}

function addHumanConfirmation(): void {
  overlayStore.openDrawer({
    kind: 'human-confirmation',
    id: null,
    mode: 'create',
    payload: {
      subject: { type: props.targetType ?? 'BusinessFunction', id: props.id },
      title: props.title,
      knowledgeStatus: props.status,
    },
  })
}
</script>

<template>
  <section class="knowledge-status-panel">
    <div class="knowledge-status-panel__title">
      <div><el-icon><Promotion /></el-icon><span><strong>知识进展</strong><small>状态只通过明确操作改变</small></span></div>
      <div class="knowledge-status-panel__actions">
        <el-button v-if="actorStore.canEdit && status === 'Inferred' && canChange && humanConfirmationCount === 0" plain size="small" @click="addHumanConfirmation">添加人工确认</el-button>
        <el-button v-if="actorStore.canEdit && nextLabel && canChange && canAdvance" type="primary" plain size="small" @click="openChangeDialog">{{ nextLabel }}</el-button>
      </div>
    </div>
    <KnowledgeProgression :status="status" />
    <p :class="{ 'is-ready': canAdvance }">
      <el-icon><DocumentChecked /></el-icon>{{ requirementText }}
    </p>
  </section>
</template>
