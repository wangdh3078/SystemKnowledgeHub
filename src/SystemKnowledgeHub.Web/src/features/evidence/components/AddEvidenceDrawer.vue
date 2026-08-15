<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { Close, DocumentAdd, InfoFilled } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { ElRadioButton, ElRadioGroup } from 'element-plus'
import { useOverlayStore } from '../../../app/stores/overlays'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import { addEvidence } from '../api/evidenceApi'
import {
  evidenceTypeLabels,
  isEvidenceSubjectPayload,
  type EvidenceConfidence,
  type EvidenceSubjectPayload,
  type OrdinaryEvidenceType,
} from '../api/evidenceContracts'
import { unknownItemsApi } from '../../unknown-items/api/unknownItemsApi'

const props = defineProps<{ payload: unknown }>()
const overlayStore = useOverlayStore()
const subject = computed<EvidenceSubjectPayload | null>(() =>
  isEvidenceSubjectPayload(props.payload) ? props.payload : null,
)
const investigation = computed<{ unknownItemId: number; concurrencyToken: string } | null>(() => {
  if (typeof props.payload !== 'object' || props.payload === null) return null
  const value = props.payload as Record<string, unknown>
  return typeof value.unknownItemId === 'number' && typeof value.concurrencyToken === 'string'
    ? { unknownItemId: value.unknownItemId, concurrencyToken: value.concurrencyToken }
    : null
})
const saving = ref(false)
const errorMessage = ref<string | null>(null)
const providerExpanded = ref(false)
const form = reactive({
  evidenceType: 'CodeReference' as OrdinaryEvidenceType,
  sourceTitle: '',
  sourceReference: '',
  repository: '',
  file: '',
  startLine: '',
  endLine: '',
  locatorJson: '',
  summary: '',
  supportReason: '',
  confidence: 'Medium' as EvidenceConfidence,
  providerName: '王敏',
  providerRole: '证据提供人',
  occurredAt: new Date().toISOString(),
  team: '制造系统组',
  providerExternalKey: '',
  providerSource: 'Manual',
  providerNote: '',
})

const ordinaryTypes: readonly OrdinaryEvidenceType[] = [
  'CodeReference', 'Sql', 'DatabaseSample', 'DatabaseComment', 'Api', 'MqMessage', 'ExistingDocument',
]

function normalize(value: string): string | null {
  const result = value.trim()
  return result.length ? result : null
}

function sourceLocator(): Readonly<Record<string, unknown>> | null {
  if (form.evidenceType !== 'CodeReference') {
    const raw = form.locatorJson.trim()
    if (!raw) return null
    const parsed: unknown = JSON.parse(raw)
    if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
      throw new Error('来源定位必须是 JSON Object。')
    }
    return parsed as Readonly<Record<string, unknown>>
  }
  const locator: Record<string, unknown> = {}
  if (normalize(form.repository)) locator.repository = form.repository.trim()
  if (normalize(form.file)) locator.file = form.file.trim()
  if (/^\d+$/.test(form.startLine)) locator.startLine = Number(form.startLine)
  if (/^\d+$/.test(form.endLine)) locator.endLine = Number(form.endLine)
  return Object.keys(locator).length ? locator : null
}

async function save(): Promise<void> {
  if (!subject.value) return
  errorMessage.value = null
  if (!form.sourceTitle.trim() || !form.supportReason.trim() || !form.providerName.trim() || !form.providerRole.trim()) {
    errorMessage.value = '请填写来源标题、支持理由和证据提供人信息。'
    return
  }
  let locator: Readonly<Record<string, unknown>> | null
  try {
    locator = sourceLocator()
  } catch (error: unknown) {
    errorMessage.value = error instanceof Error ? error.message : '来源定位格式无效。'
    return
  }
  if (!form.sourceReference.trim() && locator === null) {
    errorMessage.value = '来源引用与来源定位至少填写一项。'
    return
  }

  saving.value = true
  try {
    const request = {
      evidenceType: form.evidenceType,
      subject: subject.value.subject,
      subjectDetailKey: subject.value.subjectDetailKey ?? null,
      sourceTitle: form.sourceTitle.trim(),
      sourceReference: normalize(form.sourceReference),
      sourceLocator: locator,
      summary: normalize(form.summary),
      supportReason: form.supportReason.trim(),
      confidence: form.confidence,
      provider: {
        displayName: form.providerName.trim(),
        roleOrIdentity: form.providerRole.trim(),
        occurredAt: form.occurredAt,
        team: normalize(form.team),
        externalUserKey: normalize(form.providerExternalKey),
        source: normalize(form.providerSource),
        note: normalize(form.providerNote),
      },
    }
    if (investigation.value) {
      await unknownItemsApi.addEvidence(investigation.value.unknownItemId, {
          ...request,
          concurrencyToken: investigation.value.concurrencyToken,
        })
      ElMessage.success('证据已保存；知识状态保持不变。')
      window.dispatchEvent(new CustomEvent('evidence:changed'))
      window.dispatchEvent(new CustomEvent('unknown-item:changed'))
      overlayStore.closeDrawer()
    } else {
      const created = await addEvidence(request)
      ElMessage.success('证据已保存；知识状态保持不变。')
      window.dispatchEvent(new CustomEvent('evidence:changed'))
      overlayStore.openDrawer({ kind: 'evidence', id: created.id, mode: 'read' })
    }
  } catch (error: unknown) {
    errorMessage.value = error instanceof Error ? error.message : '证据保存失败。'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="evidence-drawer">
    <header class="evidence-drawer__header">
      <el-button text circle :icon="Close" aria-label="关闭添加证据" @click="overlayStore.closeDrawer()" />
      <span>添加证据</span>
      <h2>说明为什么相信这条知识</h2>
      <p>证据保存后不会自动改变知识状态。</p>
    </header>

    <div v-if="!subject" class="evidence-drawer__error">缺少有效的知识对象上下文，请关闭后重新进入。</div>
    <template v-else>
      <section class="evidence-subject-card">
        <div><small>支持对象</small><strong class="technical-text">{{ subject.title }}</strong></div>
        <KnowledgeStatusBadge :status="subject.knowledgeStatus" />
      </section>

      <section class="evidence-form-section">
        <h3>证据类型</h3>
        <el-radio-group v-model="form.evidenceType" class="evidence-type-grid">
          <el-radio-button v-for="type in ordinaryTypes" :key="type" :value="type">{{ evidenceTypeLabels[type] }}</el-radio-button>
        </el-radio-group>
      </section>

      <el-form class="evidence-form" label-position="top">
        <section class="evidence-form-section">
          <h3>来源定位</h3>
          <el-form-item label="来源标题 *"><el-input v-model="form.sourceTitle" placeholder="例如 EquipmentStatusService.cs : line 184" /></el-form-item>
          <el-form-item label="来源引用"><el-input v-model="form.sourceReference" placeholder="文件名、SQL 名、文档名或 Endpoint" /></el-form-item>
          <div v-if="form.evidenceType === 'CodeReference'" class="evidence-form__grid">
            <el-form-item label="Repository"><el-input v-model="form.repository" class="technical-input" /></el-form-item>
            <el-form-item label="File"><el-input v-model="form.file" class="technical-input" /></el-form-item>
            <el-form-item label="Start Line"><el-input v-model="form.startLine" class="technical-input" /></el-form-item>
            <el-form-item label="End Line"><el-input v-model="form.endLine" class="technical-input" /></el-form-item>
          </div>
          <el-form-item v-else label="来源定位（JSON Object）"><el-input v-model="form.locatorJson" type="textarea" :rows="3" class="technical-input" placeholder="例如 { &quot;endpoint&quot;: &quot;/api/equipment/status&quot; }" /></el-form-item>
        </section>

        <section class="evidence-form-section">
          <h3>知识支撑</h3>
          <el-form-item label="证据摘要"><el-input v-model="form.summary" type="textarea" :rows="2" /></el-form-item>
          <el-form-item label="为什么支持当前知识 *"><el-input v-model="form.supportReason" type="textarea" :rows="3" placeholder="说明这项来源如何支持当前含义或规则" /></el-form-item>
          <el-form-item label="可信度"><el-select v-model="form.confidence"><el-option label="高" value="High" /><el-option label="中" value="Medium" /><el-option label="低" value="Low" /></el-select></el-form-item>
        </section>

        <section class="evidence-form-section evidence-form-section--collapsible">
          <button type="button" @click="providerExpanded = !providerExpanded">
            <span>证据提供人</span><small>{{ form.providerName }} · {{ form.providerRole }}</small>
          </button>
          <div v-if="providerExpanded" class="evidence-person-grid">
            <el-form-item label="姓名 *"><el-input v-model="form.providerName" /></el-form-item>
            <el-form-item label="角色 / 身份 *"><el-input v-model="form.providerRole" /></el-form-item>
            <el-form-item label="团队"><el-input v-model="form.team" /></el-form-item>
            <el-form-item label="External User Key"><el-input v-model="form.providerExternalKey" class="technical-input" /></el-form-item>
            <el-form-item label="快照来源"><el-input v-model="form.providerSource" /></el-form-item>
            <el-form-item label="提供时间（UTC）"><el-input v-model="form.occurredAt" class="technical-input" /></el-form-item>
            <el-form-item label="备注"><el-input v-model="form.providerNote" /></el-form-item>
          </div>
        </section>
      </el-form>

      <div class="evidence-impact-note"><el-icon><InfoFilled /></el-icon><span><strong>知识状态保持 {{ subject.knowledgeStatus === 'Unknown' ? '未知' : subject.knowledgeStatus === 'Inferred' ? '推断' : '已确认' }}</strong><small>保存 Evidence 与推进 Knowledge Status 是两个明确操作。</small></span></div>
      <div v-if="errorMessage" class="evidence-drawer__error">{{ errorMessage }}</div>
      <footer class="evidence-drawer__footer">
        <el-button @click="overlayStore.closeDrawer()">取消</el-button>
        <el-button type="primary" :icon="DocumentAdd" :loading="saving" @click="save">保存证据</el-button>
      </footer>
    </template>
  </div>
</template>

<style src="../evidence.css"></style>
