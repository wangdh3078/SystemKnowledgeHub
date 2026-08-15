<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { Close, Connection, Search } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { ApiError } from '../../../api/errors/ApiError'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import { addRelationship, searchRelationshipTargets } from '../api/relationshipApi'
import { isRelationshipSourcePayload, relationTypeLabels, type KnowledgeTargetType, type RelationType, type TargetPreview } from '../api/relationshipContracts'

const props = defineProps<{ payload: unknown }>()
const source = computed(() => isRelationshipSourcePayload(props.payload) ? props.payload : null)
const actorStore = useActorStore()
const overlayStore = useOverlayStore()
const relationType = ref<RelationType>('Reads')
const targetType = ref<KnowledgeTargetType>('DatabaseObject')
const query = ref('')
const candidates = ref<readonly TargetPreview[]>([])
const selected = ref<TargetPreview | null>(null)
const description = ref('')
const loading = ref(false)
const saving = ref(false)
const errorMessage = ref<string | null>(null)
const relationOptions: readonly RelationType[] = ['Calls','Reads','Writes','UsesField','AppliesRule','PublishesVia','ConsumesVia','UsesIntegration','DependsOn']
const typeOptions = computed<readonly KnowledgeTargetType[]>(() => ({
  Calls:['BusinessFunction'], Reads:['DatabaseObject','DatabaseColumn'], Writes:['DatabaseObject','DatabaseColumn'],
  UsesField:['DatabaseColumn'], AppliesRule:['BusinessRule'], PublishesVia:['Integration'], ConsumesVia:['Integration'],
  UsesIntegration:['Integration'], DependsOn:['System','DatabaseSource','DatabaseObject'],
} as const)[relationType.value])
const typeLabels: Readonly<Record<KnowledgeTargetType,string>> = {System:'系统',DatabaseSource:'数据库来源',BusinessFunction:'业务功能',DatabaseObject:'数据库对象',DatabaseColumn:'字段',BusinessRule:'业务规则',Integration:'集成关系'}
const visibleCandidates = computed(() => candidates.value.filter(item => item.target.type === targetType.value))

async function search(): Promise<void> {
  if (!source.value) return
  loading.value=true; errorMessage.value=null
  try { candidates.value= (await searchRelationshipTargets({systemId:source.value.systemId,sourceType:source.value.source.type,sourceId:source.value.source.id,relationType:relationType.value,q:query.value})).items }
  catch(error:unknown){ errorMessage.value=error instanceof Error?error.message:'目标对象查询失败。' }
  finally{loading.value=false}
}
async function save():Promise<void>{
  if(!source.value||!selected.value||saving.value)return
  saving.value=true;errorMessage.value=null
  try{const created=await addRelationship({source:source.value.source,relationType:relationType.value,target:selected.value.target,description:description.value.trim()||null,actor:actorStore.actor});window.dispatchEvent(new CustomEvent('relationship:changed'));ElMessage.success('关系已保存，知识状态保持“未知”。');overlayStore.openDrawer({kind:'relationship',id:created.id,mode:'read'})}
  catch(error:unknown){errorMessage.value=error instanceof ApiError?error.message:error instanceof Error?error.message:'关系保存失败。'}finally{saving.value=false}
}
watch(relationType,()=>{targetType.value=typeOptions.value[0];selected.value=null;void search()})
watch(targetType,()=>{selected.value=null})
onMounted(()=>void search())
</script>

<template>
  <div class="relationship-drawer add-relationship-drawer">
    <header><el-button text circle :icon="Close" aria-label="关闭添加关系" @click="overlayStore.closeDrawer()"/><span>添加关系</span><h2>建立显式知识关系</h2><p v-if="source">源对象 · {{ source.systemName }} · <b>{{ source.title }}</b></p></header>
    <template v-if="source">
      <div class="relationship-steps"><strong><b>1</b>关系类型</strong><i></i><strong><b>2</b>目标对象</strong><i></i><span><b>3</b>确认</span></div>
      <section><h3>关系类型</h3><el-select v-model="relationType" class="relationship-full"><el-option v-for="type in relationOptions" :key="type" :label="relationTypeLabels[type]" :value="type"/></el-select><small>必须选择符合 Source / Target 端点矩阵的明确关系。</small></section>
      <section><h3>目标对象类型</h3><el-select v-model="targetType" class="relationship-full"><el-option v-for="type in typeOptions" :key="type" :label="typeLabels[type]" :value="type"/></el-select><div class="relationship-search"><el-input v-model="query" :prefix-icon="Search" placeholder="搜索技术名称或业务说明" @keyup.enter="search"/><el-button :loading="loading" @click="search">搜索</el-button></div>
        <div v-if="visibleCandidates.length" class="relationship-candidates"><button v-for="item in visibleCandidates" :key="`${item.target.type}:${item.target.id}`" :class="{selected:selected?.target.id===item.target.id&&selected?.target.type===item.target.type}" @click="selected=item"><span><strong class="technical-text">{{ item.title }}</strong><small>{{ item.systemContext.map(x=>x.name).join(' / ') }} · {{ item.objectTypeLabel }}</small><em>{{ item.shortDescription ?? '尚无业务说明' }}</em></span><KnowledgeStatusBadge :status="item.knowledgeStatus"/></button></div>
        <div v-else-if="!loading" class="relationship-empty">当前条件下没有可选目标。</div>
      </section>
      <section v-if="selected" class="relationship-preview"><h3>目标对象预览</h3><strong class="technical-text">{{ selected.title }}</strong><p>{{ selected.systemContext.map(x=>x.name).join(' / ') }} · {{ selected.objectTypeLabel }}</p><small>{{ selected.shortDescription ?? '尚无业务说明' }}</small></section>
      <section><h3>关系说明（可选）</h3><el-input v-model="description" type="textarea" :rows="3" maxlength="500" show-word-limit placeholder="说明目标对象如何参与当前知识"/><div v-if="selected" class="relationship-path"><b>{{ source.title }}</b><span>— {{ relationTypeLabels[relationType] }} →</span><b>{{ selected.title }}</b></div></section>
      <p v-if="errorMessage" class="relationship-error">{{ errorMessage }}</p>
      <footer><p>保存后关系成为正式记录，初始知识状态为“未知”。</p><div><el-button @click="overlayStore.closeDrawer()">取消</el-button><el-button type="primary" :icon="Connection" :disabled="!selected" :loading="saving" @click="save">保存关系</el-button></div></footer>
    </template>
  </div>
</template>

<style src="../relationships.css"></style>
