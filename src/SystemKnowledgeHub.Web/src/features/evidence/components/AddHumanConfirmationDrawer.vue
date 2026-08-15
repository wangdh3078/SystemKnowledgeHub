<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { Close, InfoFilled, UserFilled } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { useOverlayStore } from '../../../app/stores/overlays'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import { addHumanConfirmation } from '../api/evidenceApi'
import { isEvidenceSubjectPayload, type EvidenceSubjectPayload } from '../api/evidenceContracts'

const props = defineProps<{ payload: unknown }>()
const overlayStore = useOverlayStore()
const subject = computed<EvidenceSubjectPayload | null>(() =>
  isEvidenceSubjectPayload(props.payload) ? props.payload : null,
)
const saving = ref(false)
const errorMessage = ref<string | null>(null)
const form = reactive({
  displayName: '',
  roleOrIdentity: '',
  team: '',
  occurredAt: new Date().toISOString(),
  source: 'Human confirmation',
  confirmationStatement: '',
  supportReason: '',
  sourceNote: '',
  note: '',
})

function normalize(value: string): string | null {
  const result = value.trim()
  return result.length ? result : null
}

async function save(): Promise<void> {
  if (!subject.value) return
  errorMessage.value = null
  if (!form.displayName.trim() || !form.roleOrIdentity.trim() || !form.confirmationStatement.trim() || !form.supportReason.trim()) {
    errorMessage.value = '请填写确认人、角色 / 身份、确认结论和支持理由。'
    return
  }
  saving.value = true
  try {
    const created = await addHumanConfirmation({
      subject: subject.value.subject,
      subjectDetailKey: subject.value.subjectDetailKey ?? null,
      confirmationStatement: form.confirmationStatement.trim(),
      supportReason: form.supportReason.trim(),
      sourceNote: normalize(form.sourceNote),
      confirmer: {
        displayName: form.displayName.trim(),
        roleOrIdentity: form.roleOrIdentity.trim(),
        occurredAt: form.occurredAt,
        team: normalize(form.team),
        externalUserKey: null,
        source: normalize(form.source),
        note: normalize(form.note),
      },
    })
    ElMessage.success('人工确认已记录；知识状态仍需单独推进。')
    window.dispatchEvent(new CustomEvent('evidence:changed'))
    overlayStore.openDrawer({ kind: 'evidence', id: created.id, mode: 'read' })
  } catch (error: unknown) {
    errorMessage.value = error instanceof Error ? error.message : '人工确认保存失败。'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="evidence-drawer human-confirmation-drawer">
    <header class="evidence-drawer__header">
      <el-button text circle :icon="Close" aria-label="关闭人工确认" @click="overlayStore.closeDrawer()" />
      <span>添加人工确认</span>
      <h2>记录谁确认了这条知识</h2>
      <p>人工确认是 Evidence，不会自动推进知识状态。</p>
    </header>

    <div v-if="!subject" class="evidence-drawer__error">缺少有效的知识对象上下文，请关闭后重新进入。</div>
    <template v-else>
      <section class="evidence-subject-card">
        <div><small>确认对象</small><strong class="technical-text">{{ subject.title }}</strong></div>
        <KnowledgeStatusBadge :status="subject.knowledgeStatus" />
      </section>

      <el-form class="evidence-form" label-position="top">
        <section class="evidence-form-section">
          <h3>确认人快照</h3>
          <div class="evidence-person-grid">
            <el-form-item label="姓名 *"><el-input v-model="form.displayName" placeholder="例如 李工" /></el-form-item>
            <el-form-item label="角色 / 身份 *"><el-input v-model="form.roleOrIdentity" placeholder="例如 MES 业务专家" /></el-form-item>
            <el-form-item label="团队"><el-input v-model="form.team" /></el-form-item>
            <el-form-item label="确认方式"><el-input v-model="form.source" /></el-form-item>
            <el-form-item label="确认时间（UTC）"><el-input v-model="form.occurredAt" class="technical-input" /></el-form-item>
            <el-form-item label="人员快照备注"><el-input v-model="form.note" /></el-form-item>
          </div>
        </section>

        <section class="evidence-form-section evidence-form-section--confirmation">
          <h3>确认内容</h3>
          <el-form-item label="确认结论 *"><el-input v-model="form.confirmationStatement" type="textarea" :rows="4" placeholder="准确记录专家确认的知识内容" /></el-form-item>
          <el-form-item label="为什么支持当前知识 *"><el-input v-model="form.supportReason" type="textarea" :rows="3" placeholder="说明确认人的身份和上下文为什么足以支持这条知识" /></el-form-item>
          <el-form-item label="来源说明"><el-input v-model="form.sourceNote" placeholder="例如 现场评审会议" /></el-form-item>
        </section>
      </el-form>

      <section class="evidence-confirmation-impact">
        <el-icon><InfoFilled /></el-icon>
        <div><small>保存后的知识影响</small><strong>新增 HumanConfirmation Evidence</strong><p>Knowledge Status 保持当前状态；后续必须由明确操作推进。</p></div>
        <KnowledgeStatusBadge :status="subject.knowledgeStatus" />
      </section>

      <div v-if="errorMessage" class="evidence-drawer__error">{{ errorMessage }}</div>
      <footer class="evidence-drawer__footer">
        <el-button @click="overlayStore.closeDrawer()">取消</el-button>
        <el-button type="primary" :icon="UserFilled" :loading="saving" @click="save">保存人工确认</el-button>
      </footer>
    </template>
  </div>
</template>

<style src="../evidence.css"></style>
