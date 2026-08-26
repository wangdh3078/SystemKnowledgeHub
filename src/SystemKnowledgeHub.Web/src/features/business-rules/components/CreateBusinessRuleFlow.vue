<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useRouter } from 'vue-router'
import { useOverlayStore } from '../../../app/stores/overlays'
import { getSystemsList } from '../../systems/api/systemsApi'
import type { SystemSummary } from '../../systems/api/systemsContracts'
import type { BusinessRuleWriteResponse } from '../api/businessRuleContracts'
import CreateBusinessRuleDialog from './CreateBusinessRuleDialog.vue'
const overlays=useOverlayStore();const router=useRouter();const systems=ref<readonly SystemSummary[]>([]);const kind=computed(()=>overlays.currentDialog?.kind)
async function loadSystems():Promise<void>{try{systems.value=(await getSystemsList({page:1,pageSize:100,sort:'name:asc'})).items}catch{systems.value=[]}}
watch(kind,(value,previous)=>{if(value==='create-business-rule'&&previous!=='create-business-rule'){systems.value=[];void loadSystems()}},{immediate:true})
async function created(rule:BusinessRuleWriteResponse){overlays.closeDialog();ElMessage.success(`已创建业务规则 ${rule.header.name}，知识状态保持“未知”。`);await router.push({name:'business-rule-detail',params:{id:String(rule.id)}})}
</script>
<template><Teleport v-if="overlays.isDialogOpen" defer to="#dialog-feature-content"><CreateBusinessRuleDialog v-if="kind==='create-business-rule'" :systems="systems" @created="created"/></Teleport></template>
