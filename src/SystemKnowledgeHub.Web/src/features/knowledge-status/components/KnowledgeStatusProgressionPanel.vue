<script setup lang="ts">
import { computed } from 'vue'
import { DocumentChecked, Promotion } from '@element-plus/icons-vue'
import type { KnowledgeStatus } from '../../../api/contracts/knowledge'
import { useOverlayStore } from '../../../app/stores/overlays'
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
const nextLabel = computed(() => props.status === 'Unknown' ? '推进为推断' : props.status === 'Inferred' ? '推进为已确认' : null)
const requirementText = computed(() => {
  if (props.status === 'Unknown') return props.evidenceCount > 0 ? `已记录 ${props.evidenceCount} 条相关证据` : '需要至少 1 条可定位的相关证据'
  if (props.status === 'Inferred') return props.humanConfirmationCount > 0 ? `已记录 ${props.humanConfirmationCount} 条人工确认` : '需要至少 1 条完整的人工确认证据'
  return '当前知识已经明确确认；如有变化，可通过显式回退修正。'
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
</script>

<template>
  <section class="knowledge-status-panel">
    <div class="knowledge-status-panel__title">
      <div><el-icon><Promotion /></el-icon><span><strong>知识进展</strong><small>状态只通过明确操作改变</small></span></div>
      <el-button v-if="nextLabel && canChange" type="primary" plain size="small" @click="openChangeDialog">{{ nextLabel }}</el-button>
    </div>
    <KnowledgeProgression :status="status" />
    <p :class="{ 'is-ready': status === 'Unknown' ? evidenceCount > 0 : humanConfirmationCount > 0 }">
      <el-icon><DocumentChecked /></el-icon>{{ requirementText }}
    </p>
  </section>
</template>
