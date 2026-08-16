<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { useRoute, useRouter } from 'vue-router'
import { useOverlayStore } from '../../../app/stores/overlays'
import { getSystemsList } from '../../systems/api/systemsApi'
import type { SystemSummary } from '../../systems/api/systemsContracts'
import CreateKnowledgeObjectChooser from '../../systems/components/CreateKnowledgeObjectChooser.vue'
import type { BusinessRuleWriteResponse } from '../api/businessRuleContracts'
import CreateBusinessRuleDialog from './CreateBusinessRuleDialog.vue'
const overlays=useOverlayStore();const route=useRoute();const router=useRouter();const systems=ref<readonly SystemSummary[]>([]);const kind=computed(()=>overlays.currentDialog?.kind);const routeOwnsChooser=computed(()=>route.name==='systems-list'||route.name==='business-functions-list')
onMounted(async()=>{try{systems.value=(await getSystemsList({page:1,pageSize:100,sort:'name:asc'})).items}catch{systems.value=[]}})
async function created(rule:BusinessRuleWriteResponse){overlays.closeDialog();ElMessage.success(`已创建业务规则 ${rule.header.name}，知识状态保持“未知”。`);await router.push({name:'business-rule-detail',params:{id:String(rule.id)}})}
</script>
<template><Teleport v-if="overlays.isDialogOpen" defer to="#dialog-feature-content"><CreateKnowledgeObjectChooser v-if="kind==='create-knowledge-object'&&!routeOwnsChooser" :enabled-kinds="['business-rule','integration']" system-context="在下一步选择"/><CreateBusinessRuleDialog v-else-if="kind==='create-business-rule'" :systems="systems" @created="created"/></Teleport></template>
