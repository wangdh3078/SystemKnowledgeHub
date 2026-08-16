<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { useRouter } from 'vue-router'
import { useOverlayStore } from '../../../app/stores/overlays'
import { getSystemsList } from '../../systems/api/systemsApi'
import type { SystemSummary } from '../../systems/api/systemsContracts'
import CreateIntegrationDialog from './CreateIntegrationDialog.vue'
import type { IntegrationWriteResponse } from '../api/integrationContracts'
const overlays=useOverlayStore();const router=useRouter();const systems=ref<readonly SystemSummary[]>([]);const kind=computed(()=>overlays.currentDialog?.kind)
onMounted(async()=>{try{systems.value=(await getSystemsList({page:1,pageSize:100,sort:'name:asc'})).items}catch{systems.value=[]}})
async function created(item:IntegrationWriteResponse){overlays.closeDialog();ElMessage.success(`已创建集成关系 ${item.name}，知识状态保持“未知”。`);await router.push({name:'integration-detail',params:{id:String(item.id)}})}
</script>
<template><Teleport v-if="overlays.isDialogOpen" defer to="#dialog-feature-content"><CreateIntegrationDialog v-if="kind==='create-integration'" :systems="systems" @created="created"/></Teleport></template>
