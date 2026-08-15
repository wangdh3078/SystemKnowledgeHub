<script setup lang="ts">
import { computed } from 'vue'
import { ElMessage } from 'element-plus'
import { useOverlayStore } from '../../../app/stores/overlays'
import CreateKnowledgeObjectChooser from '../../systems/components/CreateKnowledgeObjectChooser.vue'
import CreateSystemDialog from '../../systems/components/CreateSystemDialog.vue'
import type { CreateSystemResponse, SystemSummary } from '../../systems/api/systemsContracts'
import type { CreateBusinessFunctionResponse } from '../api/businessFunctionContracts'
import CreateBusinessFunctionDialog from './CreateBusinessFunctionDialog.vue'

const props = defineProps<{
  systems: readonly SystemSummary[]
  initialSystemId?: number
}>()
const emit = defineEmits<{
  created: [businessFunction: CreateBusinessFunctionResponse]
  systemCreated: [system: CreateSystemResponse]
}>()
const overlayStore = useOverlayStore()
const kind = computed(() => overlayStore.currentDialog?.kind)
const systemContext = computed(() => {
  const system = props.systems.find(item => item.id === props.initialSystemId)
  return system ? `${system.name} · ${system.displayName}` : '在下一步选择'
})

function handleBusinessFunctionCreated(businessFunction: CreateBusinessFunctionResponse): void {
  overlayStore.closeDialog()
  ElMessage.success(`已创建业务功能 ${businessFunction.name}，知识状态保持“未知”。`)
  emit('created', businessFunction)
}

function handleSystemCreated(system: CreateSystemResponse): void {
  overlayStore.closeDialog()
  ElMessage.success(`已创建系统 ${system.name}，知识状态保持“未知”。`)
  emit('systemCreated', system)
}
</script>

<template>
  <Teleport v-if="overlayStore.isDialogOpen" defer to="#dialog-feature-content">
    <CreateKnowledgeObjectChooser
      v-if="kind === 'create-knowledge-object'"
      :enabled-kinds="['system', 'business-function', 'business-rule']"
      :system-context="systemContext"
    />
    <CreateSystemDialog v-else-if="kind === 'create-system'" @created="handleSystemCreated" />
    <CreateBusinessFunctionDialog
      v-else-if="kind === 'create-business-function'"
      :systems="systems"
      :initial-system-id="initialSystemId"
      @created="handleBusinessFunctionCreated"
    />
  </Teleport>
</template>

<style src="../../systems/systems.css"></style>
