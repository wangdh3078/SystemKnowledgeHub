<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { ArrowRight, Close, DocumentChecked, EditPen, Refresh } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { ApiError } from '../../../api/errors/ApiError'
import type { KnowledgeStatus } from '../../../api/contracts/knowledge'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import { formatDateTime } from '../../../app/formatters/dateTime'
import KnowledgeProgression from '../../../components/data-display/KnowledgeProgression.vue'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import { evidenceTypeLabels, evidenceTypes } from '../../evidence/api/evidenceContracts'
import { changeRelationshipStatus, getRelationshipDetail, updateRelationshipDescription } from '../api/relationshipApi'
import { relationTypeLabels, type RelationshipDetailResponse } from '../api/relationshipContracts'

const props=defineProps<{relationshipId:number|null}>()
const overlayStore=useOverlayStore();const actorStore=useActorStore()
const detail=ref<RelationshipDetailResponse|null>(null);const loading=ref(false);const saving=ref(false);const editing=ref(false);const statusConfirming=ref(false);const description=ref('');const errorMessage=ref<string|null>(null);const conflict=ref(false)
const humanCount=computed(()=>detail.value?.evidence.filter(x=>x.evidenceType==='HumanConfirmation').length??0)
const nextStatus=computed<KnowledgeStatus|null>(()=>detail.value?.knowledgeStatus==='Unknown'?'Inferred':detail.value?.knowledgeStatus==='Inferred'?'Confirmed':null)
const requirementMet=computed(()=>detail.value?.knowledgeStatus==='Unknown'?(detail.value?.evidence.length??0)>0:humanCount.value>0)
function evidenceTypeLabel(value: unknown): string { const type=evidenceTypes.find(item=>item===value); return type?evidenceTypeLabels[type]:'未知证据类型' }
async function load(){if(props.relationshipId===null)return;loading.value=true;errorMessage.value=null;try{detail.value=await getRelationshipDetail(props.relationshipId);description.value=detail.value.description??'';editing.value=false;statusConfirming.value=false;conflict.value=false}catch(e:unknown){errorMessage.value=e instanceof Error?e.message:'关系详情加载失败。'}finally{loading.value=false}}
async function saveDescription(){if(!actorStore.canEdit||!detail.value||saving.value)return;saving.value=true;errorMessage.value=null;try{const result=await updateRelationshipDescription(detail.value.id,{description:description.value.trim()||null,concurrencyToken:detail.value.concurrencyToken});detail.value={...detail.value,description:result.description,concurrencyToken:result.concurrencyToken};editing.value=false;window.dispatchEvent(new CustomEvent('relationship:changed'));ElMessage.success('关系说明已更新。')}catch(e:unknown){conflict.value=e instanceof ApiError&&e.status===409;errorMessage.value=e instanceof Error?e.message:'关系说明保存失败。'}finally{saving.value=false}}
async function changeStatus(){if(!actorStore.canEdit||!detail.value||!nextStatus.value||!requirementMet.value||saving.value)return;saving.value=true;errorMessage.value=null;try{await changeRelationshipStatus(detail.value.id,{targetStatus:nextStatus.value,reason:null,concurrencyToken:detail.value.concurrencyToken});await load();window.dispatchEvent(new CustomEvent('relationship:changed'));ElMessage.success('关系知识状态已明确推进。')}catch(e:unknown){conflict.value=e instanceof ApiError&&e.status===409;errorMessage.value=e instanceof Error?e.message:'知识状态修改失败。'}finally{saving.value=false}}
function addEvidence(){if(!actorStore.canEdit||!detail.value)return;overlayStore.openDrawer({kind:'add-evidence',id:detail.value.id,mode:'create',payload:{subject:{type:'KnowledgeRelation',id:detail.value.id},title:`${detail.value.source.title} → ${detail.value.target.title}`,knowledgeStatus:detail.value.knowledgeStatus,subjectDetailKey:null}})}
function openEvidence(id:number){overlayStore.openDrawer({kind:'evidence',id,mode:'read'})}
watch(()=>props.relationshipId,()=>void load());onMounted(()=>void load())
</script>

<template><div class="relationship-drawer relationship-detail-drawer"><LoadingState v-if="loading&&!detail" message="正在读取关系详情…"/><ErrorState v-else-if="errorMessage&&!detail" title="关系详情加载失败" :message="errorMessage" @retry="load"/><template v-else-if="detail">
  <header class="skh-drawer-header"><el-button text circle :icon="Close" aria-label="关闭关系详情" @click="overlayStore.requestDrawerClose()"/><span>关系详情</span><h2>{{ relationTypeLabels[detail.relationType] }}</h2><p>显式知识关系</p></header>
  <section class="relationship-overview"><h3>关系概览</h3><article><small>源对象</small><strong class="technical-text">{{ detail.source.title }}</strong><em>{{ detail.source.systemContext }}</em></article><div class="relationship-overview__line"><span>{{ relationTypeLabels[detail.relationType] }}</span><el-icon><ArrowRight/></el-icon></div><article><small>目标对象</small><strong class="technical-text">{{ detail.target.title }}</strong><em>{{ detail.target.systemContext }}</em></article></section>
  <section><div class="relationship-heading"><h3>关系说明</h3><el-button v-if="actorStore.canEdit&&!editing" text type="primary" :icon="EditPen" @click="editing=true">编辑</el-button></div><p v-if="!editing" class="relationship-description">{{ detail.description??'尚未记录关系说明。' }}</p><template v-else><el-input v-model="description" type="textarea" :rows="3" maxlength="500"/><div class="relationship-edit-actions"><el-button @click="editing=false;description=detail.description??''">取消</el-button><el-button type="primary" :loading="saving" @click="saveDescription">保存说明</el-button></div></template></section>
  <section class="relationship-status"><div class="relationship-heading"><h3>知识状态</h3><KnowledgeStatusBadge :status="detail.knowledgeStatus"/></div><KnowledgeProgression :status="detail.knowledgeStatus"/><p>证据是关系依据，保存后不会自动改变状态。</p><el-button v-if="actorStore.canEdit&&nextStatus&&!statusConfirming" type="primary" plain size="small" @click="statusConfirming=true">推进为{{ nextStatus==='Inferred'?'推断':'已确认' }}</el-button><div v-if="actorStore.canEdit&&statusConfirming" class="relationship-status-confirm"><strong>{{ requirementMet?'推进条件已满足':'暂时不能推进' }}</strong><p>{{ detail.knowledgeStatus==='Unknown'?'需要至少一条可定位的关系证据。':'需要至少一条完整的人工确认证据。' }}</p><div><el-button size="small" @click="statusConfirming=false">取消</el-button><el-button size="small" type="primary" :disabled="!requirementMet" :loading="saving" @click="changeStatus">明确推进</el-button></div></div></section>
  <section><div class="relationship-heading"><h3>证据 <b>{{ detail.evidence.length }}</b></h3><el-button v-if="actorStore.canEdit" class="skh-section-action skh-evidence-action" type="primary" :icon="DocumentChecked" @click="addEvidence">添加证据</el-button></div><div v-if="detail.evidence.length" class="relationship-evidence"><button v-for="item in detail.evidence" :key="item.id" @click="openEvidence(item.id)"><el-icon><DocumentChecked/></el-icon><span><small>{{ evidenceTypeLabel(item.evidenceType) }}</small><strong>{{ item.sourceTitle }}</strong></span><el-icon><ArrowRight/></el-icon></button></div><div v-else class="relationship-empty"><el-icon><DocumentChecked/></el-icon> 尚未添加支持这条关系的证据。</div></section>
  <section><div class="relationship-heading"><h3>待确认事项 <b>0</b></h3></div><div class="relationship-empty">当前没有与这条关系直接关联的待确认事项。</div></section>
  <section><div class="relationship-heading"><h3>关系与记录</h3></div><dl class="relationship-record"><div><dt>创建人</dt><dd>{{ detail.created.displayName }}<small>{{ detail.created.roleOrIdentity??'—' }}</small></dd></div><div><dt>创建于</dt><dd class="technical-text">{{ formatDateTime(detail.created.occurredAt) }}</dd></div><div><dt>状态最近修改</dt><dd>{{ detail.statusChanged.displayName }}<small>{{ detail.statusChanged.roleOrIdentity??'—' }}</small></dd></div><div><dt>修改于</dt><dd class="technical-text">{{ formatDateTime(detail.statusChanged.occurredAt) }}</dd></div></dl></section>
  <p v-if="errorMessage" class="relationship-error">{{ errorMessage }} <el-button v-if="conflict" text :icon="Refresh" @click="load">重新加载</el-button></p>
  <footer><p>本阶段不支持修改源对象、目标对象和关系类型。</p><el-button @click="overlayStore.requestDrawerClose()">关闭</el-button></footer>
</template></div></template>
<style src="../relationships.css"></style>
