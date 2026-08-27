<script setup lang="ts">
import { computed } from 'vue'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import type { SystemKnowledgeView } from '../api/systemKnowledgeViewContracts'
import { formatDateTime } from '../../../app/formatters/dateTime'

const props = defineProps<{
  view: SystemKnowledgeView | null
  loading: boolean
  error: string | null
}>()
const emit = defineEmits<{
  openBusinessFunction: [id: number]
  openDatabaseObject: [id: number]
  openBusinessRule: [id: number]
  openIntegration: [id: number]
  openDocument: [id: number]
  openUnknownItem: [id: number]
}>()

const overviewCards = computed(() => props.view === null ? [] : [
  ['业务功能', props.view.overview.businessFunctionCount], ['数据库对象', props.view.overview.databaseObjectCount],
  ['业务规则', props.view.overview.businessRuleCount], ['集成', props.view.overview.integrationCount],
  ['知识内容', props.view.overview.documentCount], ['系统证据', props.view.overview.evidenceCount],
  ['待确认事项', props.view.overview.openUnknownItemCount],
])

</script>

<template>
  <section class="system-unified-view" aria-label="统一知识视图">
    <div class="system-section-heading"><div><h2>统一知识视图</h2><p>实时汇总当前系统已建立的结构化知识与关联知识。</p></div><span>只读投影</span></div>
    <LoadingState v-if="loading && !view" message="正在汇总系统知识…" />
    <div v-else-if="error && !view" class="system-unified-view__error">{{ error }}</div>
    <template v-else-if="view">
      <div v-if="error" class="system-unified-view__error">部分刷新失败：{{ error }}</div>
      <div class="system-unified-view__overview">
        <div v-for="card in overviewCards" :key="card[0]" class="system-unified-view__count"><strong>{{ card[1] }}</strong><span>{{ card[0] }}</span></div>
      </div>
      <div class="system-unified-view__sections">
        <section><header><h3>业务功能 <small>{{ view.overview.businessFunctionCount }}</small></h3></header><EmptyState v-if="!view.businessFunctions.length" title="暂无业务功能" /><button v-for="item in view.businessFunctions" :key="item.id" class="system-unified-view__row" @click="emit('openBusinessFunction', item.id)"><span><strong>{{ item.title }}</strong><small>{{ item.description ?? '尚未记录简述' }}</small></span><KnowledgeStatusBadge :status="item.knowledgeStatus" /></button></section>
        <section><header><h3>数据库知识 <small>{{ view.overview.databaseObjectCount }}</small></h3></header><EmptyState v-if="!view.databaseObjects.length" title="暂无数据库对象" /><button v-for="item in view.databaseObjects" :key="item.id" class="system-unified-view__row" @click="emit('openDatabaseObject', item.id)"><span><strong class="technical-text">{{ item.title }}</strong><small>{{ item.description ?? '尚未记录业务说明' }}</small></span><KnowledgeStatusBadge :status="item.knowledgeStatus" /></button></section>
        <section><header><h3>业务规则 <small>{{ view.overview.businessRuleCount }}</small></h3></header><EmptyState v-if="!view.businessRules.length" title="暂无业务规则" /><button v-for="item in view.businessRules" :key="item.id" class="system-unified-view__row" @click="emit('openBusinessRule', item.id)"><span><strong>{{ item.title }}</strong><small>{{ item.description ?? '尚未记录规则说明' }}</small></span><KnowledgeStatusBadge :status="item.knowledgeStatus" /></button></section>
        <section><header><h3>集成 <small>{{ view.overview.integrationCount }}</small></h3></header><EmptyState v-if="!view.integrations.length" title="暂无集成关系" /><button v-for="item in view.integrations" :key="item.id" class="system-unified-view__row" @click="emit('openIntegration', item.id)"><span><strong>{{ item.name }}</strong><small>{{ item.integrationType }} · {{ item.direction }} · {{ item.relatedParty }}</small></span><KnowledgeStatusBadge :status="item.knowledgeStatus" /></button></section>
        <section class="system-unified-view__section--wide"><header><h3>知识内容 <small>{{ view.overview.documentCount }}</small></h3><span>仅显示已建立关系的文档</span></header><EmptyState v-if="!view.documents.length" title="暂无关联知识内容" description="文档需通过明确 Relationship 与当前系统关联。" /><button v-for="item in view.documents" :key="item.id" class="system-unified-view__row" @click="emit('openDocument', item.id)"><span><strong>{{ item.title }}</strong><small>{{ item.documentType }} · {{ item.lifecycleStatus }} · {{ item.relationTypes.join(' / ') }} · 更新于 {{ formatDateTime(item.updatedAt) }}</small></span><KnowledgeStatusBadge :status="item.knowledgeStatus" /></button></section>
        <section><header><h3>系统关系 <small>{{ view.relationships.length }}</small></h3></header><EmptyState v-if="!view.relationships.length" title="暂无系统关系" /><div v-for="item in view.relationships" :key="item.id" class="system-unified-view__read-row"><span>{{ item.direction === 'Outgoing' ? '指向' : '来自' }} · {{ item.relationType }}</span><small>{{ item.relatedType }} #{{ item.relatedId }}</small><KnowledgeStatusBadge :status="item.knowledgeStatus" /></div></section>
        <section><header><h3>系统证据 <small>{{ view.overview.evidenceCount }}</small></h3><span>不包含文档证据</span></header><EmptyState v-if="!view.evidence.length" title="暂无系统级证据" /><div v-for="item in view.evidence" :key="item.id" class="system-unified-view__read-row"><span>{{ item.sourceTitle }}</span><small>{{ item.evidenceType }} · {{ item.summary ?? '未填写摘要' }}</small></div></section>
        <section class="system-unified-view__section--wide"><header><h3>待确认事项 <small>{{ view.overview.openUnknownItemCount }}</small></h3><span>未关闭优先</span></header><EmptyState v-if="!view.unknownItems.length" title="暂无待确认事项" /><button v-for="item in view.unknownItems" :key="item.id" class="system-unified-view__row" @click="emit('openUnknownItem', item.id)"><span><strong>{{ item.itemCode }} · {{ item.question }}</strong><small>{{ item.priority }} · {{ item.status }} · 更新于 {{ formatDateTime(item.updatedAt) }}</small></span></button></section>
      </div>
    </template>
  </section>
</template>
