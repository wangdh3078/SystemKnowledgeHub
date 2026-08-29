<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { Close, Connection, Search } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { ApiError } from '../../../api/errors/ApiError'
import { useOverlayStore } from '../../../app/stores/overlays'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import { addRelationship, searchRelationshipTargets } from '../api/relationshipApi'
import { isRelationshipSourcePayload, relationTypeLabels, type KnowledgeTargetType, type RelationType, type TargetPreview } from '../api/relationshipContracts'
import { documentTypes, type DocumentType } from '../../knowledge-documents/api/knowledgeDocumentContracts'

const props = defineProps<{ payload: unknown }>()
const source = computed(() => isRelationshipSourcePayload(props.payload) ? props.payload : null)
const overlayStore = useOverlayStore()
const documentSource = computed(() => source.value?.source.type === 'KnowledgeDocument')
const sourceDocumentType = computed<DocumentType | null>(() => documentTypes.includes(source.value?.documentType as DocumentType)
  ? source.value!.documentType as DocumentType
  : null)
const relationType = ref<RelationType | null>(null)
const targetType = ref<KnowledgeTargetType | null>(null)
const query = ref('')
const candidates = ref<readonly TargetPreview[]>([])
const selected = ref<TargetPreview | null>(null)
const description = ref('')
const loading = ref(false)
const saving = ref(false)
const errorMessage = ref<string | null>(null)
let searchTimer: ReturnType<typeof setTimeout> | null = null
const relationOptions = computed<readonly RelationType[]>(() => documentSource.value
  ? documentRelationOptions(sourceDocumentType.value)
  : ['Calls','Reads','Writes','UsesField','AppliesRule','PublishesVia','ConsumesVia','UsesIntegration','DependsOn'])
const recommendedRelationOptions = computed<readonly RelationType[]>(() => {
  if (!documentSource.value) return []
  return sourceDocumentType.value === 'Requirement' ? ['AppliesTo', 'SpecifiedBy', 'VerifiedBy']
    : sourceDocumentType.value === 'Specification' ? ['VerifiedBy']
      : sourceDocumentType.value === 'Sop' || sourceDocumentType.value === 'Troubleshooting' ? ['AppliesTo']
        : sourceDocumentType.value === 'KnowledgeArticle' || sourceDocumentType.value === 'DesignNote' ? ['Documents']
          : []
})
const otherRelationOptions = computed(() => relationOptions.value.filter(type => !recommendedRelationOptions.value.includes(type)))
const relationHelper = computed(() => relationType.value === 'Documents' ? '说明表示该文档实质性描述目标对象；引用请使用“引用”。'
  : relationType.value === 'References' ? '引用仅表示指向或引证，不表示适用、验证、依赖或替代。'
    : relationType.value === 'AppliesTo' ? '适用于表示该需求、规程或排查指引的明确适用范围。'
      : null)
const typeOptions = computed<readonly KnowledgeTargetType[]>(() => {
  switch (relationType.value) {
    case 'Calls': return ['BusinessFunction']
    case 'Reads':
    case 'Writes': return ['DatabaseObject', 'DatabaseColumn']
    case 'UsesField': return ['DatabaseColumn']
    case 'AppliesRule': return ['BusinessRule']
    case 'PublishesVia':
    case 'ConsumesVia':
    case 'UsesIntegration': return ['Integration']
    case 'DependsOn': return ['System', 'DatabaseSource', 'DatabaseObject']
    case 'Documents': return ['System', 'BusinessFunction', 'DatabaseObject', 'BusinessRule', 'Integration']
    case 'References': return sourceDocumentType.value === 'DesignNote'
      ? ['KnowledgeDocument']
      : ['System', 'BusinessFunction', 'DatabaseObject', 'BusinessRule', 'Integration', 'KnowledgeDocument']
    case 'AppliesTo': return documentAppliesToTargets(sourceDocumentType.value)
    case 'SpecifiedBy':
    case 'VerifiedBy':
    case 'Supersedes': return ['KnowledgeDocument']
    default: return []
  }
})
const typeLabels: Readonly<Record<KnowledgeTargetType,string>> = {System:'系统',DatabaseSource:'数据库来源',BusinessFunction:'业务功能',DatabaseObject:'数据库对象',DatabaseColumn:'字段',BusinessRule:'业务规则',Integration:'集成关系',KnowledgeDocument:'知识文档'}
const visibleCandidates = computed(() => candidates.value.filter(item => item.target.type === targetType.value))

function documentRelationOptions(documentType: DocumentType | null): readonly RelationType[] {
  const common: RelationType[] = ['Documents', 'References', 'Supersedes']
  return documentType === 'Requirement' ? [...common, 'AppliesTo', 'SpecifiedBy', 'VerifiedBy']
    : documentType === 'Specification' ? [...common, 'VerifiedBy']
      : documentType === 'Sop' || documentType === 'Troubleshooting' ? [...common, 'AppliesTo']
        : common
}

function documentAppliesToTargets(documentType: DocumentType | null): readonly KnowledgeTargetType[] {
  return documentType === 'Requirement' ? ['System', 'BusinessFunction']
    : documentType === 'Sop' ? ['System', 'BusinessFunction', 'DatabaseObject', 'Integration']
      : documentType === 'Troubleshooting' ? ['System', 'DatabaseObject', 'Integration']
        : []
}

async function search(): Promise<void> {
  if (!source.value || !relationType.value || !targetType.value) return
  loading.value=true; errorMessage.value=null
  try { candidates.value= (await searchRelationshipTargets({systemId:source.value.systemId,sourceType:source.value.source.type,sourceId:source.value.source.id,relationType:relationType.value,q:query.value})).items }
  catch(error:unknown){ errorMessage.value=error instanceof Error?error.message:'目标对象查询失败。' }
  finally{loading.value=false}
}
async function save():Promise<void>{
  if(!source.value||!selected.value||!relationType.value||saving.value)return
  saving.value=true;errorMessage.value=null
  try{const created=await addRelationship({source:source.value.source,relationType:relationType.value,target:selected.value.target,description:description.value.trim()||null});window.dispatchEvent(new CustomEvent('relationship:changed'));ElMessage.success('关系已保存，知识状态保持“未知”。');overlayStore.openDrawer({kind:'relationship',id:created.id,mode:'read'})}
  catch(error:unknown){errorMessage.value=error instanceof ApiError?error.message:error instanceof Error?error.message:'关系保存失败。'}finally{saving.value=false}
}
watch([documentSource, sourceDocumentType],()=>{relationType.value=null;targetType.value=null;selected.value=null},{immediate:true})
watch(relationType,()=>{targetType.value=relationType.value?typeOptions.value[0]:null;selected.value=null;if(relationType.value)void search()},{immediate:true})
watch(targetType,()=>{selected.value=null})
watch(query,()=>{if(searchTimer!==null)clearTimeout(searchTimer);searchTimer=setTimeout(()=>void search(),300)})
onBeforeUnmount(()=>{if(searchTimer!==null)clearTimeout(searchTimer)})
</script>

<template>
  <div class="relationship-drawer add-relationship-drawer">
    <header class="skh-drawer-header"><el-button text circle :icon="Close" aria-label="关闭添加关系" @click="overlayStore.requestDrawerClose()"/><span>添加关系</span><h2>建立显式知识关系</h2><p v-if="source">源对象 · <template v-if="source.systemName">{{ source.systemName }} · </template><b>{{ source.title }}</b></p></header>
    <template v-if="source">
      <div class="relationship-steps"><strong><b>1</b>关系类型</strong><i></i><strong><b>2</b>目标对象</strong><i></i><span><b>3</b>确认</span></div>
      <section><h3>关系类型</h3><el-select v-model="relationType" class="relationship-full" placeholder="请选择关系类型"><template v-if="recommendedRelationOptions.length"><el-option-group label="推荐关系"><el-option v-for="type in recommendedRelationOptions" :key="type" :label="relationTypeLabels[type]" :value="type"/></el-option-group><el-option-group v-if="otherRelationOptions.length" label="其他合法关系"><el-option v-for="type in otherRelationOptions" :key="type" :label="relationTypeLabels[type]" :value="type"/></el-option-group></template><el-option-group v-else label="可用关系"><el-option v-for="type in relationOptions" :key="type" :label="relationTypeLabels[type]" :value="type"/></el-option-group></el-select><small>{{ relationHelper ?? '必须选择符合 Source / Target 端点矩阵的明确关系。' }}</small></section>
      <section><h3>目标对象类型</h3><el-select v-model="targetType" class="relationship-full" :disabled="!relationType" placeholder="请先选择关系类型"><el-option v-for="type in typeOptions" :key="type" :label="typeLabels[type]" :value="type"/></el-select><div class="relationship-search"><el-input v-model="query" :prefix-icon="Search" :disabled="!targetType" placeholder="搜索技术名称或业务说明" @keyup.enter="search"/><el-button :disabled="!targetType" :loading="loading" @click="search">搜索</el-button></div>
        <div v-if="visibleCandidates.length" class="relationship-candidates"><button v-for="item in visibleCandidates" :key="`${item.target.type}:${item.target.id}`" :class="{selected:selected?.target.id===item.target.id&&selected?.target.type===item.target.type}" @click="selected=item"><span><strong class="technical-text">{{ item.title }}</strong><small>{{ item.systemContext.map(x=>x.name).join(' / ') }} · {{ item.objectTypeLabel }}</small><em>{{ item.shortDescription ?? '尚无业务说明' }}</em></span><KnowledgeStatusBadge :status="item.knowledgeStatus"/></button></div>
        <div v-else-if="!loading" class="relationship-empty">当前条件下没有可选目标。</div>
      </section>
      <section v-if="selected" class="relationship-preview"><h3>目标对象预览</h3><strong class="technical-text">{{ selected.title }}</strong><p>{{ selected.systemContext.map(x=>x.name).join(' / ') }} · {{ selected.objectTypeLabel }}</p><small>{{ selected.shortDescription ?? '尚无业务说明' }}</small></section>
      <section><h3>关系说明（可选）</h3><el-input v-model="description" type="textarea" :rows="3" maxlength="500" show-word-limit placeholder="说明目标对象如何参与当前知识"/><div v-if="selected && relationType" class="relationship-path"><b>{{ source.title }}</b><span>— {{ relationTypeLabels[relationType] }} →</span><b>{{ selected.title }}</b></div></section>
      <p v-if="errorMessage" class="relationship-error">{{ errorMessage }}</p>
      <footer><p>保存后关系成为正式记录，初始知识状态为“未知”。</p><div><el-button @click="overlayStore.requestDrawerClose()">取消</el-button><el-button type="primary" :icon="Connection" :disabled="!selected || !relationType" :loading="saving" @click="save">保存关系</el-button></div></footer>
    </template>
  </div>
</template>

<style src="../relationships.css"></style>
