<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { Check, DocumentAdd, Lock, Plus, Refresh, Search, VideoPlay } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { onBeforeRouteLeave, onBeforeRouteUpdate, useRoute, useRouter } from 'vue-router'
import { parseSafeApiId } from '../../../api/contracts/id'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import { formatDateTime } from '../../../app/formatters/dateTime'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import HistoricalTargetLabel from '../../../components/data-display/HistoricalTargetLabel.vue'
import { getDatabaseColumnDetail } from '../../database-knowledge/api/databaseKnowledgeApi'
import { getBusinessFunctionDetail } from '../../business-functions/api/businessFunctionsApi'
import { businessRulesApi } from '../../business-rules/api/businessRulesApi'
import type { BusinessRuleInputData } from '../../business-rules/api/businessRuleContracts'
import { integrationsApi } from '../../integrations/api/integrationsApi'
import type { IntegrationOverviewInput, IntegrationType, FlowDirection } from '../../integrations/api/integrationContracts'
import { evidenceTypeLabels, type EvidenceType } from '../../evidence/api/evidenceContracts'
import { priorityLabels, targetTypeLabels, unknownItemStatusLabels, type Finding, type HistoricalTargetIdentity, type KnowledgeUpdate, type KnowledgeUpdateDraft } from '../api/unknownItemContracts'
import { unknownItemsApi } from '../api/unknownItemsApi'
import UnknownItemContextRail from '../components/UnknownItemContextRail.vue'
import { useUnknownItemDetail } from '../composables/useUnknownItemDetail'

const route = useRoute(); const router = useRouter(); const actorStore = useActorStore(); const overlays = useOverlayStore()
const itemId = computed(() => parseSafeApiId(route.params.id)); const findingText = ref(''); const resolutionText = ref('')
const findingError = ref<string | null>(null)
const draftTargetKey = ref(''); const draftAction = ref<'AddColumnKnownValue' | 'UpdateDatabaseColumnKnowledge' | 'UpdateBusinessRule'>('AddColumnKnownValue')
const draftValue = ref(''); const draftMeaning = ref(''); const draftDescription = ref('')
const draftRuleName=ref('');const draftRuleDescription=ref('');const draftRuleCondition=ref('');const draftRuleResult=ref('');const draftRuleInputData=ref('[]')
const draftIntegrationBase=ref<IntegrationOverviewInput|null>(null);const draftIntegrationName=ref('');const draftIntegrationPurpose=ref('')
const { detail, loading, saving, error, load, run, person } = useUnknownItemDetail(() => parseSafeApiId(route.params.id))
const statusSteps = ['Open', 'Investigating', 'ConclusionConfirmed', 'Closed'] as const
const can = (action: string) => actorStore.canEdit && detail.value?.id === parseSafeApiId(route.params.id) && detail.value?.availableActions.includes(action) === true
const evidenceTypeLabel = (value: string) => evidenceTypeLabels[value as EvidenceType] ?? value
const columnTargets = computed(() => detail.value?.relatedObjects.filter(item => item.target.type === 'DatabaseColumn') ?? [])
const ruleTargets = computed(() => detail.value?.relatedObjects.filter(item => item.target.type === 'BusinessRule') ?? [])
const integrationTargets = computed(() => detail.value?.relatedObjects.filter(item => item.target.type === 'Integration') ?? [])
const editableTargets = computed(() => [...columnTargets.value, ...ruleTargets.value, ...integrationTargets.value].filter(item => item.identity?.isDeleted !== true && item.identity?.isNavigable !== false))
const primaryRelatedObject = computed(() => detail.value?.relatedObjects.find(item => item.primary) ?? null)
const systemIdentity = computed<HistoricalTargetIdentity | null>(() => detail.value ? {
  id: detail.value.system.id,
  targetType: detail.value.system.targetType ?? 'System',
  displayName: detail.value.system.displayName ?? detail.value.system.name,
  isDeleted: detail.value.system.isDeleted ?? false,
  isNavigable: detail.value.system.isNavigable ?? true,
} : null)
function relatedIdentity(display: string, target: { type: string; id: number }, identity?: HistoricalTargetIdentity): HistoricalTargetIdentity {
  return identity ?? { id: target.id, targetType: target.type, displayName: display, isDeleted: false, isNavigable: true }
}
const selectedTarget = computed(() => editableTargets.value.find(item => `${item.target.type}:${item.target.id}` === draftTargetKey.value) ?? null)
const latestKnowledgeApplyActivity = computed(() => detail.value?.activity.find(item => item.type === 'KnowledgeUpdateApplied') ?? null)
async function reload(): Promise<void> {
  if (itemId.value !== null) {
    if (!await load(itemId.value)) return
    resolutionText.value = detail.value?.resolution?.conclusion ?? ''
    if (!draftTargetKey.value && editableTargets.value[0]) draftTargetKey.value = `${editableTargets.value[0].target.type}:${editableTargets.value[0].target.id}`
    await loadSelectedDraftTarget()
  }
}
function integrationInput(value:Awaited<ReturnType<typeof integrationsApi.detail>>):IntegrationOverviewInput{const endpoint:Record<string,string|null>=value.header.integrationType==='HttpApi'?{url:value.endpoint.url,method:value.endpoint.method}:value.header.integrationType==='RabbitMq'?{exchange:value.endpoint.exchange,topic:value.endpoint.topic,queue:value.endpoint.queue}:value.header.integrationType==='FileExchange'?{filePath:value.endpoint.filePath}:{};return{name:value.header.name,integrationType:value.header.integrationType,sourceParty:value.sourceParty,targetParty:value.targetParty,flowDirection:value.flowDirection,purpose:value.purpose,endpoint,databaseSourceId:value.databaseSourceId,databaseObjectId:value.databaseObjectId}}
async function loadSelectedDraftTarget(): Promise<void> {
  const subject = detail.value
  const target = selectedTarget.value
  draftIntegrationBase.value = null
  if (!subject || subject.id !== itemId.value || !target) return
  const current = () => detail.value === subject && itemId.value === subject.id && selectedTarget.value === target
  try {
    if (target.target.type === 'BusinessRule') {
      const rule = await businessRulesApi.detail(target.target.id)
      if (!current()) return
      draftAction.value = 'UpdateBusinessRule'
      draftRuleName.value = rule.header.name
      draftRuleDescription.value = rule.description
      draftRuleCondition.value = rule.condition ?? ''
      draftRuleResult.value = rule.result ?? ''
      draftRuleInputData.value = JSON.stringify(rule.inputData, null, 2)
    } else if (target.target.type === 'Integration') {
      const integration = integrationInput(await integrationsApi.detail(target.target.id))
      if (!current()) return
      draftIntegrationBase.value = integration
      draftIntegrationName.value = integration.name
      draftIntegrationPurpose.value = integration.purpose ?? ''
    }
  } catch (cause: unknown) {
    if (current()) error.value = cause instanceof Error ? cause.message : '更新目标加载失败。'
  }
}
function parseRuleInputData():BusinessRuleInputData[]|null{try{const value:unknown=JSON.parse(draftRuleInputData.value);if(!Array.isArray(value))return null;const rows:BusinessRuleInputData[]=[];for(const item of value){if(typeof item!=='object'||item===null||Array.isArray(item))return null;const row=item as Record<string,unknown>;if(typeof row.name!=='string'||!row.name.trim()||(row.description!==null&&row.description!==undefined&&typeof row.description!=='string'))return null;rows.push({name:row.name.trim(),description:typeof row.description==='string'&&row.description.trim()?row.description.trim():null})}return rows}catch{return null}}
async function start(): Promise<void> {
  if (detail.value?.id !== parseSafeApiId(route.params.id) || !detail.value) return
  if (await run(() => unknownItemsApi.start(detail.value!.id, person(actorStore.displayName, '调查人'), detail.value!.concurrencyToken))) ElMessage.success('已开始调查。')
}
async function addFinding(): Promise<void> {
  if (detail.value?.id !== parseSafeApiId(route.params.id) || !detail.value) return
  if (!findingText.value.trim()) { findingError.value = '请先记录调查发现。'; return }
  findingError.value = null
  if (await run(() => unknownItemsApi.addFinding(detail.value!.id, findingText.value.trim(), person(actorStore.displayName, '调查人'), detail.value!.concurrencyToken))) { findingText.value = ''; ElMessage.success('调查发现已记录。') }
}
async function saveResolution(): Promise<void> {
  if (detail.value?.id !== parseSafeApiId(route.params.id) || !detail.value || !resolutionText.value.trim()) return
  const subject = detail.value
  const draftTarget = selectedTarget.value
  const drafts: KnowledgeUpdateDraft[] = []
  if (selectedTarget.value?.target.type === 'DatabaseColumn') {
    const column = await getDatabaseColumnDetail(selectedTarget.value.target.id)
    if (draftAction.value === 'AddColumnKnownValue' && draftValue.value.trim() && draftMeaning.value.trim()) {
      drafts.push({ id: null, target: { type: 'DatabaseColumn', id: column.id }, subjectDetailKey: `KnownValues:${draftValue.value.trim()}`,
        applyAction: 'AddColumnKnownValue', changeSummary: `新增 ${draftValue.value.trim()} 的业务含义`, before: null,
        after: { value: draftValue.value.trim(), meaning: draftMeaning.value.trim() }, knowledgeStatusBefore: null, knowledgeStatusAfter: null })
    } else if (draftAction.value === 'UpdateDatabaseColumnKnowledge' && draftDescription.value.trim()) {
      drafts.push({ id: null, target: { type: 'DatabaseColumn', id: column.id }, subjectDetailKey: 'BusinessDescription',
        applyAction: 'UpdateDatabaseColumnKnowledge', changeSummary: '更新字段业务含义',
        before: { businessDescription: column.businessKnowledge.description }, after: { businessDescription: draftDescription.value.trim() },
        knowledgeStatusBefore: null, knowledgeStatusAfter: null })
    }
  } else if(selectedTarget.value?.target.type==='BusinessRule'){
    const rule=await businessRulesApi.detail(selectedTarget.value.target.id);const inputData=parseRuleInputData();if(inputData===null){ElMessage.error('输入数据必须是由 name / description 组成的 JSON 数组。');return}const before={name:rule.header.name,description:rule.description,condition:rule.condition,result:rule.result,inputData:rule.inputData};const after={name:draftRuleName.value.trim(),description:draftRuleDescription.value.trim(),condition:draftRuleCondition.value.trim()||null,result:draftRuleResult.value.trim()||null,inputData};if(after.name&&after.description)drafts.push({id:null,target:{type:'BusinessRule',id:rule.id},subjectDetailKey:null,applyAction:'UpdateBusinessRule',changeSummary:'更新业务规则定义',before,after,knowledgeStatusBefore:null,knowledgeStatusAfter:null})
  } else if(selectedTarget.value?.target.type==='Integration'&&draftIntegrationBase.value){const before=draftIntegrationBase.value;const after={...before,name:draftIntegrationName.value.trim(),purpose:draftIntegrationPurpose.value.trim()||null};if(after.name)drafts.push({id:null,target:{type:'Integration',id:selectedTarget.value.target.id},subjectDetailKey:null,applyAction:'UpdateIntegration',changeSummary:'更新集成关系概览',before,after,knowledgeStatusBefore:null,knowledgeStatusAfter:null})}
  if (detail.value !== subject || itemId.value !== subject.id || selectedTarget.value !== draftTarget) return
  if (await run(() => unknownItemsApi.saveResolution(subject.id, resolutionText.value.trim(), drafts,
    person(actorStore.displayName, '调查人'), subject.concurrencyToken))) {
    draftValue.value = ''; draftMeaning.value = ''; draftDescription.value = ''
    ElMessage.success('结论草稿与知识更新预览已保存；正式知识尚未改变。')
  }
}
function record(value: unknown): Record<string, unknown> | null { return typeof value === 'object' && value !== null && !Array.isArray(value) ? value as Record<string, unknown> : null }
function text(value: unknown): string { return typeof value === 'string' ? value : '' }
function updateAction(update: KnowledgeUpdate): string {
  if (update.target.type === 'DatabaseColumn') return update.subjectDetailKey?.startsWith('KnownValues:') ? 'AddColumnKnownValue' : 'UpdateDatabaseColumnKnowledge'
  if (update.target.type === 'BusinessFunction') return 'UpdateBusinessFunction'
  if (update.target.type === 'BusinessRule') return 'UpdateBusinessRule'
  if (update.target.type === 'Integration') return 'UpdateIntegration'
  return 'Unsupported'
}
function nullableText(value: unknown): string | null { return typeof value === 'string' ? value : null }
function nullableId(value: unknown): number | null { return value === null ? null : typeof value === 'number' && Number.isSafeInteger(value) && value > 0 ? value : null }
function integrationSnapshot(value: Record<string, unknown>): IntegrationOverviewInput | null {
  const type = text(value.integrationType) as IntegrationType; const flowDirection = text(value.flowDirection) as FlowDirection
  if (!['HttpApi','RabbitMq','FileExchange','DatabaseDependency'].includes(type) || !['OneWay','Bidirectional'].includes(flowDirection)) return null
  const source = record(value.sourceParty); const target = record(value.targetParty); const endpoint = record(value.endpoint)
  if (!source || !target || !endpoint || !text(value.name).trim() || !text(source.displayName).trim() || !text(target.displayName).trim()) return null
  const sourceId = nullableId(source.systemId); const targetId = nullableId(target.systemId)
  if (sourceId === null && source.systemId !== null || targetId === null && target.systemId !== null || sourceId === null && targetId === null) return null
  const endpointValues: Record<string, string | null> = {}; for (const key of ['url','method','exchange','topic','queue','filePath']) { const current=endpoint[key]; if(current!==undefined&&current!==null&&typeof current!=='string') return null; if(current!==undefined) endpointValues[key]=nullableText(current) }
  return {name:text(value.name).trim(),integrationType:type,sourceParty:{systemId:sourceId,displayName:text(source.displayName).trim()},targetParty:{systemId:targetId,displayName:text(target.displayName).trim()},flowDirection,purpose:nullableText(value.purpose),endpoint:endpointValues,databaseSourceId:nullableId(value.databaseSourceId),databaseObjectId:nullableId(value.databaseObjectId)}
}
function targetDisplay(update: KnowledgeUpdate): string { return detail.value?.relatedObjects.find(item => item.target.type === update.target.type && item.target.id === update.target.id)?.display ?? `${update.target.type} #${update.target.id}` }
function updateTargetIdentity(update: KnowledgeUpdate): HistoricalTargetIdentity {
  return update.targetIdentity ?? relatedIdentity(targetDisplay(update), update.target)
}
async function applyUpdate(update: KnowledgeUpdate): Promise<void> {
  if (detail.value?.id !== parseSafeApiId(route.params.id) || !detail.value || update.status !== 'Proposed') return
  const subject = detail.value
  const proposed = record(update.after); if (!proposed) { ElMessage.error('知识更新预览结构无效。'); return }
  try { await ElMessageBox.confirm(`将修改正式知识：${targetDisplay(update)}\n${update.changeSummary}\n应用后不会自动确认结论或关闭事项。`, '确认应用知识更新', { confirmButtonText: '应用知识更新', cancelButtonText: '取消', type: 'warning' }) } catch { return }
  if (detail.value !== subject || itemId.value !== subject.id) return
  const applier = person(actorStore.displayName, '知识更新执行人')
  const action = updateAction(update)
  let task: () => Promise<unknown>
  if (action === 'AddColumnKnownValue') {
    const column = await getDatabaseColumnDetail(update.target.id)
    task = () => unknownItemsApi.applyColumnKnownValue(subject.id, update.id, { columnId: update.target.id,
      value: text(proposed.value), meaning: text(proposed.meaning), sortOrder: 0, knowledgeStatusChange: null,
      applier, concurrencyToken: subject.concurrencyToken, targetConcurrencyToken: column.concurrencyToken })
  } else if (action === 'UpdateDatabaseColumnKnowledge') {
    const column = await getDatabaseColumnDetail(update.target.id)
    task = () => unknownItemsApi.applyColumnKnowledge(subject.id, update.id, { columnId: update.target.id,
      businessDescription: text(proposed.businessDescription), knowledgeStatusChange: null, applier,
      concurrencyToken: subject.concurrencyToken, targetConcurrencyToken: column.concurrencyToken })
  } else if (action === 'UpdateBusinessFunction') {
    const fn = await getBusinessFunctionDetail(update.target.id)
    task = () => unknownItemsApi.applyBusinessFunction(subject.id, update.id, { businessFunctionId: update.target.id,
      overview: proposed, knowledgeStatusChange: null, applier, concurrencyToken: subject.concurrencyToken,
      targetConcurrencyToken: fn.concurrencyToken })
  } else if(action==='UpdateBusinessRule'){
    const rule=await businessRulesApi.detail(update.target.id)
    task=()=>unknownItemsApi.applyBusinessRule(subject.id,update.id,{businessRuleId:update.target.id,rule:proposed,knowledgeStatusChange:null,applier,concurrencyToken:subject.concurrencyToken,targetConcurrencyToken:rule.concurrencyToken})
  } else if(action==='UpdateIntegration'){
    const integration=integrationSnapshot(proposed);if(!integration){ElMessage.error('集成关系更新预览结构无效。');return}const current=await integrationsApi.detail(update.target.id)
    task=()=>unknownItemsApi.applyIntegration(subject.id,update.id,{integrationId:update.target.id,integration,knowledgeStatusChange:null,applier,concurrencyToken:subject.concurrencyToken,targetConcurrencyToken:current.concurrencyToken})
  } else { ElMessage.warning('该目标 Feature 尚未落地，当前不能应用此更新。'); return }
  if (detail.value !== subject || itemId.value !== subject.id) return
  if (await run(task)) ElMessage.success('知识更新已原子应用；结论仍需单独确认。')
}
async function confirmConclusion(): Promise<void> {
  if (detail.value?.id !== parseSafeApiId(route.params.id) || !detail.value) return
  const subject = detail.value
  try { await ElMessageBox.confirm('确认当前调查结论成立？此操作不会应用知识更新，也不会自动关闭事项。', '确认调查结论', { confirmButtonText: '确认结论', cancelButtonText: '取消', type: 'warning' }) } catch { return }
  if (detail.value !== subject || itemId.value !== subject.id) return
  if (await run(() => unknownItemsApi.confirmConclusion(subject.id, person(actorStore.displayName, '结论确认人'), subject.concurrencyToken))) ElMessage.success('结论已确认，事项仍保持开放以供关闭。')
}
async function closeItem(): Promise<void> {
  if (detail.value?.id !== parseSafeApiId(route.params.id) || !detail.value) return
  const subject = detail.value
  try { await ElMessageBox.confirm('关闭后事项进入只读状态；已应用知识不会再次改变。', '关闭待确认事项', { confirmButtonText: '关闭事项', cancelButtonText: '取消', type: 'warning' }) } catch { return }
  if (detail.value !== subject || itemId.value !== subject.id) return
  if (await run(() => unknownItemsApi.close(subject.id, '结论与知识更新已核对。', person(actorStore.displayName, '调查人'), subject.concurrencyToken))) ElMessage.success('待确认事项已关闭。')
}
async function reopenItem(): Promise<void> {
  if (detail.value?.id !== parseSafeApiId(route.params.id) || !detail.value) return
  const subject = detail.value
  let result: { value: string }
  try { result = await ElMessageBox.prompt('重新打开不会回滚已应用知识。请说明继续调查的原因。', '重新打开待确认事项', { confirmButtonText: '重新打开', cancelButtonText: '取消', inputValidator: value => value.trim().length > 0 || '必须填写重新打开原因' }) } catch { return }
  if (detail.value !== subject || itemId.value !== subject.id) return
  if (await run(() => unknownItemsApi.reopen(subject.id, result.value.trim(), person(actorStore.displayName, '调查人'), subject.concurrencyToken))) ElMessage.success('事项已重新进入调查中；历史知识更新完整保留。')
}
function addEvidence(finding?: Finding): void {
  if (detail.value?.id !== parseSafeApiId(route.params.id) || !detail.value) return
  const subject = finding ? { type: 'Finding' as const, id: finding.id } : { type: 'UnknownItem' as const, id: detail.value.id }
  overlays.openDrawer({ kind: 'add-investigation-evidence', id: subject.id, mode: 'create', payload: {
    subject, title: finding ? `调查发现 · ${finding.content}` : `${detail.value.itemCode} · ${detail.value.question.text}`,
    knowledgeStatus: 'Unknown', subjectDetailKey: null, unknownItemId: detail.value.id, concurrencyToken: detail.value.concurrencyToken,
  } })
}
watch(() => route.params.id, () => {
  findingText.value = ''; findingError.value = null; resolutionText.value = ''
  draftTargetKey.value = ''; draftValue.value = ''; draftMeaning.value = ''; draftDescription.value = ''
  draftRuleName.value = ''; draftRuleDescription.value = ''; draftRuleCondition.value = ''; draftRuleResult.value = ''; draftRuleInputData.value = '[]'
  draftIntegrationBase.value = null; draftIntegrationName.value = ''; draftIntegrationPurpose.value = ''
  void reload()
}, { flush: 'sync' })
onMounted(() => { void reload(); window.addEventListener('unknown-item:changed', reload) })
onUnmounted(() => window.removeEventListener('unknown-item:changed', reload))
// Existing overlays belong to the current detail; preserve the dirty-drawer decision before navigation.
async function closeDetailOverlays(): Promise<boolean> {
  if (!await overlays.requestDrawerClose()) return false
  overlays.closeDialog()
  return true
}
onBeforeRouteUpdate(closeDetailOverlays)
onBeforeRouteLeave(closeDetailOverlays)
</script>

<template>
  <main class="unknown-detail-page">
    <ErrorState v-if="itemId === null" title="待确认事项地址无效" message="请从列表重新进入。" />
    <LoadingState v-else-if="loading && !detail" message="正在读取调查上下文…" />
    <ErrorState v-else-if="error && !detail" title="详情加载失败" :message="error" @retry="reload" />
    <template v-else-if="detail && detail.id === parseSafeApiId(route.params.id)">
      <header class="unknown-detail-header"><nav><button @click="router.push({ name: 'unknown-items-list' })">待确认事项</button><b>/</b><span class="technical-text">{{ detail.itemCode }}</span></nav><div><span :class="`priority priority--${detail.question.priority.toLowerCase()}`">{{ priorityLabels[detail.question.priority] }}优先级</span><span :class="`unknown-status unknown-status--${detail.question.status.toLowerCase()}`">{{ unknownItemStatusLabels[detail.question.status] }}</span><el-button v-if="can('StartInvestigation')" type="primary" :icon="VideoPlay" :loading="saving" @click="start">开始调查</el-button><el-button v-if="can('CloseUnknownItem')" type="primary" :icon="Lock" :loading="saving" @click="closeItem">关闭待确认事项</el-button><el-button v-if="can('ReopenUnknownItem')" type="primary" plain :icon="Refresh" :loading="saving" @click="reopenItem">重新打开</el-button></div><h1>{{ detail.question.text }}</h1><p>{{ detail.question.context ?? '尚未补充问题上下文。' }}</p><small>所属系统 <HistoricalTargetLabel v-if="systemIdentity" :identity="systemIdentity" /> · 更新于 {{ formatDateTime(detail.question.updatedAt) }}</small></header>

      <section class="workflow-progression" aria-label="事项状态"><div v-for="(step, index) in statusSteps" :key="step" :class="{ active: step === detail.question.status, done: statusSteps.indexOf(detail.question.status) > index }"><b>{{ index + 1 }}</b><span>{{ unknownItemStatusLabels[step] }}</span></div></section>
      <p v-if="error" class="unknown-inline-error">{{ error }}</p>

      <section class="unknown-section"><header><div><small>问题</small><h2>问题上下文</h2></div></header><dl class="unknown-metadata"><div><dt>主要对象</dt><dd class="technical-text"><HistoricalTargetLabel v-if="primaryRelatedObject" :identity="relatedIdentity(primaryRelatedObject.display, primaryRelatedObject.target, primaryRelatedObject.identity)" /></dd></div><div><dt>对象类型</dt><dd>{{ primaryRelatedObject ? targetTypeLabels[primaryRelatedObject.target.type] : '—' }}</dd></div><div><dt>创建时间</dt><dd>{{ formatDateTime(detail.question.createdAt) }}</dd></div></dl></section>

<section class="unknown-section"><header><div><small>调查发现</small><h2>调查发现</h2></div><span>{{ detail.findings.length }} 条</span></header><div v-if="detail.findings.length" class="finding-list"><article v-for="finding in detail.findings" :key="finding.id"><p>{{ finding.content }}</p><footer><span>{{ finding.recordedBy.displayName }} · {{ finding.recordedBy.roleOrIdentity }}</span><button v-if="actorStore.canEdit" @click="addEvidence(finding)">为此发现添加证据</button></footer></article></div><div v-else class="unknown-empty">调查发现用于记录调查过程中的发现，不等于最终结论。</div><el-form v-if="can('AddFinding')" class="finding-composer" label-position="top"><el-form-item label="调查发现" :error="findingError ?? undefined" required><el-input v-model="findingText" type="textarea" :rows="3" placeholder="记录一条可核查的调查发现…" @input="findingError = null" /></el-form-item><el-button type="primary" :icon="Plus" :loading="saving" @click="addFinding">添加调查发现</el-button></el-form></section>

      <section class="unknown-section evidence-section"><header><div><small>证据</small><h2>证据</h2></div><el-button v-if="can('AddEvidenceToInvestigation')" text type="primary" :icon="DocumentAdd" @click="addEvidence()">添加证据</el-button></header><div v-if="detail.evidence.length" class="investigation-evidence"><button v-for="item in detail.evidence" :key="item.id" @click="overlays.openDrawer({ kind: 'evidence', id: item.id, mode: 'read' })"><span>{{ evidenceTypeLabel(item.evidenceType) }}</span><strong class="technical-text">{{ item.sourceTitle }}</strong><small>支持 {{ targetTypeLabels[item.subject.type] }} #{{ item.subject.id }}</small></button></div><div v-else class="unknown-empty">尚无证据。证据用于说明为什么相信调查发现或结论。</div></section>

      <section class="unknown-section resolution-section"><header><div><small>结论</small><h2>{{ detail.question.status === 'Investigating' ? '结论草稿' : '最终结论' }}</h2></div><span>调查发现不等于结论</span></header><template v-if="can('SaveResolutionDraft')"><el-input v-model="resolutionText" type="textarea" :rows="4" placeholder="记录当前调查结论…" /><div v-if="editableTargets.length" class="knowledge-draft-editor"><h3>知识更新预览（可选）</h3><div class="knowledge-draft-grid"><el-select v-model="draftTargetKey" placeholder="选择知识对象" @change="loadSelectedDraftTarget"><el-option v-for="item in editableTargets" :key="`${item.target.type}:${item.target.id}`" :label="item.display" :value="`${item.target.type}:${item.target.id}`" /></el-select><el-select v-if="selectedTarget?.target.type==='DatabaseColumn'" v-model="draftAction"><el-option label="新增字段已知值" value="AddColumnKnownValue" /><el-option label="更新字段业务含义" value="UpdateDatabaseColumnKnowledge" /></el-select><el-input v-else :model-value="selectedTarget?.target.type==='Integration'?'更新集成关系概览':'更新业务规则'" disabled /></div><div v-if="selectedTarget?.target.type==='BusinessRule'" class="rule-resolution-editor"><el-input v-model="draftRuleName" placeholder="规则名称"/><el-input v-model="draftRuleDescription" type="textarea" :rows="2" placeholder="规则描述"/><el-input v-model="draftRuleCondition" class="technical-input" type="textarea" :rows="2" placeholder="条件"/><el-input v-model="draftRuleResult" type="textarea" :rows="2" placeholder="结果"/><label>输入数据（明确的 name / description JSON 数组）</label><el-input v-model="draftRuleInputData" class="technical-input" type="textarea" :rows="5"/></div><div v-else-if="selectedTarget?.target.type==='Integration'" class="rule-resolution-editor"><el-input v-model="draftIntegrationName" class="technical-input" placeholder="集成名称"/><el-input v-model="draftIntegrationPurpose" type="textarea" :rows="2" placeholder="更新后的用途"/><small>参与方、方向与端点保持当前预览值；保存后仍需明确应用，不会自动修改集成关系。</small></div><template v-else><div v-if="draftAction === 'AddColumnKnownValue'" class="knowledge-draft-grid"><el-input v-model="draftValue" placeholder="新值，例如 30" /><el-input v-model="draftMeaning" placeholder="业务含义，例如 未知 / 离线" /></div><el-input v-else v-model="draftDescription" placeholder="更新后的字段业务含义" /></template></div><footer><p>保存只记录结论与待应用预览，不会修改正式知识。</p><el-button type="primary" :icon="Search" :loading="saving" @click="saveResolution">保存结论与预览</el-button></footer></template><div v-else class="confirmed-resolution"><strong>{{ detail.resolution?.conclusion ?? '当前状态尚无结论。' }}</strong><small v-if="detail.resolution?.confirmedBy">确认人：{{ detail.resolution.confirmedBy.displayName }} · {{ detail.resolution.confirmedBy.roleOrIdentity }} · {{ formatDateTime(detail.resolution.confirmedAt) }}</small></div></section>

      <section class="unknown-section knowledge-update-section"><header><div><small>知识更新</small><h2>知识更新</h2></div><span>结论不等于知识更新</span></header><div v-if="detail.knowledgeUpdates.length" class="knowledge-update-list"><article v-for="update in detail.knowledgeUpdates" :key="update.id" :class="{ applied: update.status === 'Applied' }"><header><div><HistoricalTargetLabel :identity="updateTargetIdentity(update)" /><small>{{ update.subjectDetailKey ?? '对象级知识' }}</small></div><span :class="`update-status update-status--${update.status.toLowerCase()}`">{{ update.status === 'Applied' ? '已应用' : '待应用' }}</span></header><p>{{ update.changeSummary }}</p><dl><div><dt>更新前</dt><dd class="technical-text">{{ JSON.stringify(update.before) }}</dd></div><div><dt>更新后</dt><dd class="technical-text">{{ JSON.stringify(update.after) }}</dd></div></dl><footer><span>{{ update.status === 'Applied' ? '已写入 SQLite；重新打开不会回滚' : '尚未修改正式知识' }}</span><el-button v-if="update.status === 'Proposed' && !updateTargetIdentity(update).isDeleted && updateTargetIdentity(update).isNavigable" type="primary" :loading="saving" @click="applyUpdate(update)">应用知识更新</el-button></footer></article></div><div v-else class="unknown-empty">当前结论不要求修改正式知识，也可以直接确认结论。</div><div v-if="latestKnowledgeApplyActivity && detail.knowledgeUpdates.some(item => item.status === 'Applied')" class="knowledge-update-applied-by"><strong>最近应用记录</strong><span>{{ latestKnowledgeApplyActivity.summary }}</span><time>{{ formatDateTime(latestKnowledgeApplyActivity.occurredAt) }}</time></div><div class="resolution-actions"><p>应用操作不会自动确认结论；确认结论也不会自动关闭。</p><el-button v-if="can('ConfirmConclusion')" type="primary" :icon="Check" :loading="saving" :disabled="detail.knowledgeUpdates.some(item => item.status === 'Proposed')" @click="confirmConclusion">确认调查结论</el-button></div></section>

      <section class="unknown-section activity-section"><header><div><small>活动记录</small><h2>活动记录</h2></div></header><ol><li v-for="item in detail.activity" :key="`${item.occurredAt}-${item.type}`"><i></i><div><strong>{{ item.summary }}</strong><small>{{ formatDateTime(item.occurredAt) }}</small></div></li></ol></section>
      <Teleport defer to="#context-rail-content"><UnknownItemContextRail :detail="detail" /></Teleport>
    </template>
  </main>
</template>
<style src="../unknown-items.css"></style>
