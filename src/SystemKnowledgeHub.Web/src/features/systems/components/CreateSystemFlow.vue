<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { useOverlayStore } from '../../../app/stores/overlays'
import type { CreateSystemResponse } from '../api/systemsContracts'
import CreateSystemDialog from './CreateSystemDialog.vue'

const emit = defineEmits<{ created: [system: CreateSystemResponse] }>()
const overlayStore = useOverlayStore()

function handleCreated(system: CreateSystemResponse): void {
  overlayStore.closeDialog()
  ElMessage.success(`已创建系统 ${system.name}，知识状态保持“未知”。`)
  emit('created', system)
}
</script>

<template>
  <Teleport v-if="overlayStore.isDialogOpen" defer to="#dialog-feature-content">
    <CreateSystemDialog v-if="overlayStore.currentDialog?.kind === 'create-system'" @created="handleCreated" />
  </Teleport>
</template>
