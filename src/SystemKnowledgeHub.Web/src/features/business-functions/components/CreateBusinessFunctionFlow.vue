<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useOverlayStore } from '../../../app/stores/overlays'
import { getSystemsList } from '../../systems/api/systemsApi'
import type { SystemSummary } from '../../systems/api/systemsContracts'
import type { CreateBusinessFunctionResponse } from '../api/businessFunctionContracts'
import CreateBusinessFunctionDialog from './CreateBusinessFunctionDialog.vue'

const props = defineProps<{
  systems?: readonly SystemSummary[]
  initialSystemId?: number
}>()
const emit = defineEmits<{
  created: [businessFunction: CreateBusinessFunctionResponse]
}>()
const overlayStore = useOverlayStore()
const kind = computed(() => overlayStore.currentDialog?.kind)
const loadedSystems = ref<readonly SystemSummary[]>([])
const systemOptions = computed(() => props.systems?.length ? props.systems : loadedSystems.value)

function handleBusinessFunctionCreated(businessFunction: CreateBusinessFunctionResponse): void {
  overlayStore.closeDialog()
  ElMessage.success(`已创建业务功能 ${businessFunction.name}，知识状态保持“未知”。`)
  emit('created', businessFunction)
}

async function loadSystemsWhenNeeded(): Promise<void> {
  if (props.systems?.length || loadedSystems.value.length) return
  try {
    loadedSystems.value = (await getSystemsList({ page: 1, pageSize: 100, sort: 'name:asc' })).items
  } catch {
    loadedSystems.value = []
  }
}

watch(kind, (nextKind) => {
  if (nextKind === 'create-business-function') void loadSystemsWhenNeeded()
})
</script>

<template>
  <Teleport v-if="overlayStore.isDialogOpen" defer to="#dialog-feature-content">
    <CreateBusinessFunctionDialog
      v-if="kind === 'create-business-function'"
      :systems="systemOptions"
      :initial-system-id="initialSystemId"
      @created="handleBusinessFunctionCreated"
    />
  </Teleport>
</template>

<style src="../../systems/systems.css"></style>
