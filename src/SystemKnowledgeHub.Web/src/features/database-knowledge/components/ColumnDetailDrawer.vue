<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, toRef } from 'vue'
import { Close, Delete, DocumentChecked, EditPen, Plus, QuestionFilled } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ApiError } from '../../../api/errors/ApiError'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import { addColumnKnownValue, deleteDatabaseColumn, removeColumnKnownValue, updateDatabaseColumnKnowledge } from '../api/databaseKnowledgeApi'
import { useDatabaseColumnDetail } from '../composables/useDatabaseColumnDetail'
import KnowledgeStatusProgressionPanel from '../../knowledge-status/components/KnowledgeStatusProgressionPanel.vue'
import { evidenceTypeLabels, type EvidenceType } from '../../evidence/api/evidenceContracts'
import { unknownItemStatusLabels, type UnknownItemStatus } from '../../unknown-items/api/unknownItemContracts'
import { openDeleteDialog } from '../../soft-delete/deleteDialog'
import {
  confirmDrawerDiscard,
  markDrawerDirty,
  resetDrawerDirty,
} from '../../../layouts/drawerDirtyState'

const props = defineProps<{ columnId: number | null }>()
const overlayStore = useOverlayStore()
const actorStore = useActorStore()
const { detail, loading, errorMessage, reload } = useDatabaseColumnDetail(toRef(props, 'columnId'))
const activeSections = ref<string[]>(['businessKnowledge', 'evidence', 'unknownItems'])
const editing = ref(false)
const saving = ref(false)
const editError = ref<string | null>(null)
const businessDescription = ref('')
const knownValueForm = reactive({ value: '', meaning: '', sortOrder: 0 })
const knownValueSubmitting = ref(false)
const evidenceTypeLabel = (value: string) => evidenceTypeLabels[value as EvidenceType] ?? value
const unknownStatusLabel = (value: string) => unknownItemStatusLabels[value as UnknownItemStatus] ?? value

function notifyColumnChanged(): void { window.dispatchEvent(new Event('database-column:changed')) }

function startEditing(): void {
  if (!actorStore.canEdit || !detail.value) return
  businessDescription.value = detail.value.businessKnowledge.description ?? ''
  knownValueForm.value = ''
  knownValueForm.meaning = ''
  knownValueForm.sortOrder = detail.value.knownValues.length === 0 ? 0 : Math.max(...detail.value.knownValues.map((item) => Number(item.value) || 0)) + 1
  editError.value = null
  editing.value = true
  if (!activeSections.value.includes('knownValues')) activeSections.value.push('knownValues')
}

async function stopEditing(): Promise<void> {
  if (!(await confirmDrawerDiscard())) return
  resetDrawerDirty()
  editing.value = false
  editError.value = null
}
function mutationError(error: unknown, fallback: string): string { return error instanceof ApiError ? error.message : error instanceof Error ? error.message : fallback }

async function saveBusinessKnowledge(): Promise<void> {
  if (!actorStore.canEdit || !detail.value || saving.value) return
  saving.value = true; editError.value = null
  try {
    await updateDatabaseColumnKnowledge(detail.value.id, { businessDescription: businessDescription.value.trim() || null, actor: actorStore.actor, concurrencyToken: detail.value.concurrencyToken })
    await reload(); notifyColumnChanged(); editing.value = false; resetDrawerDirty()
    ElMessage.success('字段业务知识已保存，知识状态未自动改变。')
  } catch (error: unknown) { editError.value = mutationError(error, '字段业务知识保存失败。') } finally { saving.value = false }
}

async function addKnownValue(): Promise<void> {
  if (!actorStore.canEdit || !detail.value || knownValueSubmitting.value) return
  if (!knownValueForm.value.trim() || !knownValueForm.meaning.trim()) { editError.value = '请填写已知值及其业务含义。'; return }
  knownValueSubmitting.value = true; editError.value = null
  try {
    await addColumnKnownValue(detail.value.id, { value: knownValueForm.value.trim(), meaning: knownValueForm.meaning.trim(), sortOrder: knownValueForm.sortOrder, actor: actorStore.actor, concurrencyToken: detail.value.concurrencyToken })
    await reload(); notifyColumnChanged(); knownValueForm.value = ''; knownValueForm.meaning = ''; resetDrawerDirty()
    if (businessDescription.value !== (detail.value?.businessKnowledge.description ?? '')) markDrawerDirty()
    ElMessage.success('已添加字段已知值，知识状态未自动改变。')
  } catch (error: unknown) { editError.value = mutationError(error, '添加已知值失败。') } finally { knownValueSubmitting.value = false }
}

async function removeKnownValue(knownValueId: number, value: string): Promise<void> {
  if (!actorStore.canEdit || !detail.value || knownValueSubmitting.value) return
  try { await ElMessageBox.confirm(`确认移除已知值“${value}”吗？如果它已被证据或开放待确认事项精确引用，系统会阻止移除。`, '移除已知值', { confirmButtonText: '确认移除', cancelButtonText: '取消', type: 'warning' }) } catch { return }
  knownValueSubmitting.value = true; editError.value = null
  try {
    await removeColumnKnownValue(detail.value.id, knownValueId, { confirmed: true, actor: actorStore.actor, concurrencyToken: detail.value.concurrencyToken })
    await reload(); notifyColumnChanged(); ElMessage.success('已移除未被引用的已知值。')
  } catch (error: unknown) { editError.value = mutationError(error, '移除已知值失败。') } finally { knownValueSubmitting.value = false }
}

function addEvidence(): void {
  if (!actorStore.canEdit || !detail.value) return
  overlayStore.openDrawer({ kind: 'add-evidence', id: null, mode: 'create', payload: { subject: { type: 'DatabaseColumn', id: detail.value.id }, title: `${detail.value.parent.qualifiedName}.${detail.value.databaseMetadata.columnName}`, knowledgeStatus: detail.value.businessKnowledge.knowledgeStatus } })
}
function openEvidence(evidenceId: number): void { overlayStore.openDrawer({ kind: 'evidence', id: evidenceId, mode: 'read' }) }
function evidenceChanged(): void { void reload(); notifyColumnChanged() }
onMounted(() => window.addEventListener('evidence:changed', evidenceChanged))
onBeforeUnmount(() => window.removeEventListener('evidence:changed', evidenceChanged))

function createUnknownItem(): void {
  if (!actorStore.canEdit || !detail.value) return
  overlayStore.openDialog({ kind: 'create-unknown-item', id: null, mode: 'create', payload: { systemId: detail.value.system.id, systemName: detail.value.system.name, target: { type: 'DatabaseColumn', id: detail.value.id }, title: `${detail.value.parent.qualifiedName}.${detail.value.databaseMetadata.columnName}` } })
}

function requestDelete(): void {
  if (!actorStore.canEdit || !detail.value?.canDelete) return
  const current = detail.value
  openDeleteDialog(overlayStore, {
    objectTypeLabel: '数据库字段', actionLabel: '删除数据库字段',
    displayName: `${current.parent.qualifiedName}.${current.databaseMetadata.columnName}`,
    concurrencyToken: current.concurrencyToken,
    execute: () => deleteDatabaseColumn(current.id, current.concurrencyToken),
    onDeleted: () => { overlayStore.closeDialog(); notifyColumnChanged() },
    onRefresh: reload,
    onUnavailable: () => { overlayStore.closeDialog(); notifyColumnChanged() },
  })
}

const metadataRows = computed(() => !detail.value ? [] : [
  ['字段名', detail.value.databaseMetadata.columnName], ['数据类型', detail.value.databaseMetadata.dataType], ['允许为空', detail.value.databaseMetadata.nullable ? '是' : '否'], ['默认值', detail.value.databaseMetadata.defaultValue ?? '—'], ['字段顺序', String(detail.value.databaseMetadata.ordinalPosition)],
] as const)
const humanConfirmationCount = computed(() => detail.value?.evidence.filter(
  (item) => item.evidenceType === 'HumanConfirmation',
).length ?? 0)
</script>

<template>
  <div class="column-drawer">
    <LoadingState v-if="loading" message="正在读取字段详情…" />
    <ErrorState v-else-if="errorMessage" title="字段详情加载失败" :message="errorMessage" @retry="reload" />
    <template v-else-if="detail">
      <header class="column-drawer__header skh-drawer-header"><el-button class="column-drawer__close" text circle :icon="Close" aria-label="关闭字段详情" @click="overlayStore.requestDrawerClose()" /><span class="column-drawer__eyebrow">字段详情</span><h2 class="technical-text">{{ detail.databaseMetadata.columnName }}</h2><p><span class="technical-text">{{ detail.parent.qualifiedName }}</span> · <span class="technical-text">{{ detail.databaseMetadata.dataType }}</span></p></header>
      <KnowledgeStatusProgressionPanel
        :id="detail.id"
        target-type="DatabaseColumn"
        :title="`${detail.parent.qualifiedName}.${detail.databaseMetadata.columnName}`"
        :status="detail.businessKnowledge.knowledgeStatus"
        :concurrency-token="detail.concurrencyToken"
        :evidence-count="detail.evidence.length"
        :human-confirmation-count="humanConfirmationCount"
        :can-change="actorStore.canEdit && detail.availableActions.includes('ChangeKnowledgeStatus')"
      />
      <el-collapse v-model="activeSections" class="column-drawer__sections">
        <el-collapse-item name="businessKnowledge"><template #title><div class="drawer-collapse-title"><span>业务知识</span><el-button v-if="actorStore.canEdit && !editing" text type="primary" :icon="EditPen" @click.stop="startEditing">编辑</el-button><span v-else-if="editing" class="drawer-editing-label">正在编辑</span></div></template><el-form v-if="editing" label-position="top" class="drawer-edit-form" @submit.prevent><el-form-item label="业务说明"><el-input v-model="businessDescription" type="textarea" :rows="3" maxlength="500" show-word-limit placeholder="说明字段的业务含义" /></el-form-item><p class="drawer-section-note">数据库元数据、知识状态、证据和关系不会在此修改。</p><div class="drawer-edit-actions"><el-button @click="stopEditing">取消</el-button><el-button type="primary" :loading="saving" @click="saveBusinessKnowledge">保存业务知识</el-button></div></el-form><template v-else><dl class="drawer-facts"><div><dt>描述</dt><dd>{{ detail.businessKnowledge.description ?? '尚未记录业务含义' }}</dd></div><div><dt>知识状态</dt><dd><KnowledgeStatusBadge :status="detail.businessKnowledge.knowledgeStatus" /></dd></div></dl><p class="drawer-section-note">业务含义与支撑它的证据分开保存。</p></template></el-collapse-item>
        <el-collapse-item name="evidence"><template #title><div class="drawer-collapse-title"><span>证据 <b>{{ detail.evidence.length }}</b></span><el-button v-if="actorStore.canEdit" class="skh-section-action skh-evidence-action" type="primary" :icon="DocumentChecked" @click.stop="addEvidence">添加证据</el-button></div></template><div v-if="detail.evidence.length" class="drawer-evidence-list"><article v-for="item in detail.evidence" :key="item.id" role="button" tabindex="0" @click="openEvidence(item.id)" @keydown.enter="openEvidence(item.id)"><el-icon><DocumentChecked /></el-icon><div><small>{{ evidenceTypeLabel(item.evidenceType) }}</small><strong>{{ item.sourceTitle }}</strong><p>{{ item.supportReason }}</p></div></article></div><div v-else class="drawer-empty-state"><el-icon><DocumentChecked /></el-icon><div><strong>尚无字段级证据</strong><p>添加代码、SQL、数据库样本或人工确认，说明为什么相信这条知识。</p></div></div></el-collapse-item>
        <el-collapse-item name="unknownItems"><template #title><div class="drawer-collapse-title"><span>待确认事项 <b>{{ detail.unknownItems.length }}</b></span><el-button v-if="actorStore.canEdit" text type="primary" :icon="Plus" @click.stop="createUnknownItem">添加</el-button></div></template><div v-if="detail.unknownItems.length" class="drawer-unknown-list"><article v-for="item in detail.unknownItems" :key="item.id"><el-icon><QuestionFilled /></el-icon><div><strong>{{ item.question }}</strong><span>{{ unknownStatusLabel(item.status) }}</span></div></article></div><div v-else class="drawer-empty-state drawer-empty-state--compact"><p>当前字段没有开放待确认事项。</p></div></el-collapse-item>
        <el-collapse-item name="databaseMetadata" title="数据库元数据"><dl class="drawer-facts drawer-facts--metadata"><div v-for="row in metadataRows" :key="row[0]"><dt>{{ row[0] }}</dt><dd class="technical-text">{{ row[1] }}</dd></div></dl></el-collapse-item>
        <el-collapse-item name="knownValues"><template #title><div class="drawer-collapse-title"><span>已知值 <b>{{ detail.knownValues.length }}</b></span><el-button v-if="editing" text type="primary" :icon="Plus" @click.stop="addKnownValue">添加值</el-button></div></template><div v-if="detail.knownValues.length" class="known-values-list"><div v-for="item in detail.knownValues" :key="item.id"><code>{{ item.value }}</code><span>{{ item.meaning }}</span><el-button v-if="editing" text type="danger" :icon="Delete" :disabled="knownValueSubmitting" @click="removeKnownValue(item.id, item.value)">移除</el-button></div></div><div v-else class="drawer-empty-state drawer-empty-state--compact"><p>尚无已知值。</p></div><el-form v-if="editing" class="known-value-editor" inline @submit.prevent><el-form-item label="值"><el-input v-model="knownValueForm.value" class="technical-input" placeholder="例如 30" /></el-form-item><el-form-item label="业务含义"><el-input v-model="knownValueForm.meaning" placeholder="例如 Unknown / Offline" /></el-form-item><el-form-item label="排序"><el-input-number v-model="knownValueForm.sortOrder" :min="0" :precision="0" controls-position="right" /></el-form-item><el-button type="primary" plain :loading="knownValueSubmitting" @click="addKnownValue">添加</el-button></el-form></el-collapse-item>
        <el-collapse-item name="relations"><template #title><span class="drawer-title-with-count">字段级关系 <b>{{ detail.relations.length }}</b></span></template><div v-if="detail.relations.length" class="drawer-relation-list"><div v-for="item in detail.relations" :key="item.id"><span>{{ item.relationType }}</span><strong>{{ item.otherObject.title }}</strong></div></div><div v-else class="drawer-empty-state drawer-empty-state--compact"><p>尚未建立字段级关系。</p></div></el-collapse-item>
      </el-collapse>
      <p v-if="editError" class="authoring-error column-drawer__mutation-error" role="alert">{{ editError }}</p>
      <footer class="column-drawer__footer"><el-button v-if="actorStore.canEdit && detail.canDelete && !editing" type="danger" plain :icon="Delete" @click="requestDelete">删除数据库字段</el-button><el-button v-if="actorStore.canEdit && !editing" type="primary" :icon="EditPen" @click="startEditing">编辑字段知识</el-button><el-button v-else-if="editing" @click="stopEditing">结束编辑</el-button><el-button v-if="actorStore.canEdit" class="skh-section-action skh-evidence-action" type="primary" :icon="DocumentChecked" @click="addEvidence">添加证据</el-button><el-button v-if="actorStore.canEdit" :icon="QuestionFilled" @click="createUnknownItem">新建待确认事项</el-button></footer>
    </template>
  </div>
</template>

<style scoped>
.drawer-editing-label { color: var(--el-color-primary); font-size: 12px; font-weight: 600; }
.drawer-edit-form { padding: 2px 2px 8px; }
.drawer-edit-actions { display: flex; justify-content: flex-end; gap: 8px; }
.known-value-editor { display: grid; grid-template-columns: 92px minmax(0, 1fr) 86px auto; gap: 0 8px; align-items: end; margin-top: 12px; padding-top: 12px; border-top: 1px solid var(--color-border); }
.known-value-editor :deep(.el-form-item) { margin-bottom: 0; }
.known-value-editor :deep(.el-input-number) { width: 86px; }
.known-values-list > div { grid-template-columns: 88px minmax(0, 1fr) auto; }
.column-drawer__mutation-error { margin: 0 24px 12px; }
@media (max-width: 620px) { .known-value-editor { grid-template-columns: 1fr; } }
</style>
