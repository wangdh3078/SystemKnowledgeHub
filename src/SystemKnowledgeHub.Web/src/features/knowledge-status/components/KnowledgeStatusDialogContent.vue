<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { CircleCheck, DocumentChecked, Warning } from '@element-plus/icons-vue'
import { ApiError } from '../../../api/errors/ApiError'
import { knowledgeStatusLabels, type KnowledgeStatus } from '../../../api/contracts/knowledge'
import { useOverlayStore } from '../../../app/stores/overlays'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import { changeKnowledgeStatus } from '../api/knowledgeStatusApi'
import { isKnowledgeStatusDialogPayload } from '../api/knowledgeStatusContracts'

const overlayStore = useOverlayStore()
const submitting = ref(false)
const errorMessage = ref<string | null>(null)
const conflict = ref(false)
const reason = ref('')
const payload = computed(() => {
  const current = overlayStore.currentDialog
  return current?.kind === 'change-knowledge-status' && isKnowledgeStatusDialogPayload(current.payload)
    ? current.payload
    : null
})
const targetStatus = computed<KnowledgeStatus>(() => payload.value?.knowledgeStatus === 'Unknown' ? 'Inferred' : 'Confirmed')
const requirementMet = computed(() => {
  if (!payload.value) return false
  return payload.value.knowledgeStatus === 'Unknown'
    ? payload.value.evidenceCount > 0
    : payload.value.humanConfirmationCount > 0
})
const requirementText = computed(() => payload.value?.knowledgeStatus === 'Unknown'
  ? '至少一条与当前知识对象明确相关且可定位的证据'
  : '至少一条包含完整确认人快照的人工确认证据')

watch(payload, () => {
  errorMessage.value = null
  conflict.value = false
  reason.value = ''
})

async function submit(): Promise<void> {
  if (!payload.value || !requirementMet.value || submitting.value) return
  submitting.value = true
  errorMessage.value = null
  conflict.value = false
  try {
    await changeKnowledgeStatus({
      target: payload.value.target,
      targetStatus: targetStatus.value,
      reason: reason.value.trim() || null,
      concurrencyToken: payload.value.concurrencyToken,
    })
    overlayStore.closeDialog()
    window.dispatchEvent(new Event('knowledge-status:changed'))
  } catch (error: unknown) {
    if (error instanceof ApiError) {
      conflict.value = error.status === 409
      errorMessage.value = error.message
      const missing = error.response.details?.missingRequirement
      if (typeof missing === 'string') errorMessage.value = missing
    } else {
      errorMessage.value = error instanceof Error ? error.message : '知识状态修改失败。'
    }
  } finally {
    submitting.value = false
  }
}

function reload(): void {
  overlayStore.closeDialog()
  window.dispatchEvent(new Event('knowledge-status:changed'))
}
</script>

<template>
  <section v-if="payload" class="knowledge-status-dialog" aria-labelledby="knowledge-status-dialog-title">
      <header>
        <div><small>知识状态修改</small><h2 id="knowledge-status-dialog-title">确认推进知识状态</h2><p class="technical-text">{{ payload.title }}</p></div>
        <button type="button" aria-label="关闭" @click="overlayStore.closeDialog">×</button>
      </header>
      <div class="knowledge-status-dialog__transition">
        <div><span>当前状态</span><KnowledgeStatusBadge :status="payload.knowledgeStatus" /></div>
        <strong>→</strong>
        <div><span>目标状态</span><KnowledgeStatusBadge :status="targetStatus" /></div>
      </div>
      <section class="knowledge-status-dialog__requirement" :class="{ 'is-met': requirementMet }">
        <el-icon><CircleCheck v-if="requirementMet" /><Warning v-else /></el-icon>
        <div><strong>{{ requirementMet ? '推进条件已满足' : '暂时不能推进' }}</strong><p>{{ requirementText }}</p><small>{{ requirementMet ? '服务端将在保存时再次校验关联性和证据完整性。' : '请先添加所需证据；保存证据不会自动改变知识状态。' }}</small></div>
      </section>
      <label class="knowledge-status-dialog__reason"><span>修改说明（可选）</span><el-input v-model="reason" type="textarea" :rows="3" maxlength="500" show-word-limit placeholder="记录本次状态判断的简要说明" /></label>
      <p v-if="errorMessage" class="knowledge-status-dialog__error" role="alert">{{ errorMessage }}</p>
      <footer>
        <p><el-icon><DocumentChecked /></el-icon>状态变化是显式操作，不会由证据自动触发。</p>
        <div><el-button @click="overlayStore.closeDialog">取消</el-button><el-button v-if="conflict" @click="reload">重新加载</el-button><el-button type="primary" :disabled="!requirementMet || conflict" :loading="submitting" @click="submit">确认推进为{{ knowledgeStatusLabels[targetStatus] }}</el-button></div>
      </footer>
  </section>
</template>
