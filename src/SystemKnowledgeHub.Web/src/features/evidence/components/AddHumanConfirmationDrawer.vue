<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { Close, InfoFilled, UserFilled } from '@element-plus/icons-vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'
import { ApiError } from '../../../api/errors/ApiError'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import { addHumanConfirmation } from '../api/evidenceApi'
import { getKnowledgeDocument } from '../../knowledge-documents/api/knowledgeDocumentsApi'
import {
  confirmationMethods,
  isEvidenceSubjectPayload,
  type ConfirmationMethod,
  type EvidenceSubjectPayload,
} from '../api/evidenceContracts'

const props = defineProps<{ payload: unknown }>()
const actorStore = useActorStore()
const overlayStore = useOverlayStore()
const subject = computed<EvidenceSubjectPayload | null>(() =>
  isEvidenceSubjectPayload(props.payload) ? props.payload : null,
)
const activeRoles = computed(() =>
  actorStore.currentUser?.knowledgeRoles.filter((role) => role.isActive) ?? [],
)
const requiresRoleSelection = computed(() => activeRoles.value.length > 1)
const saving = ref(false)
const conflict = ref(false)
const reloadingDocument = ref(false)
const subjectRevisionNumber = ref<number | null>(null)
const errorMessage = ref<string | null>(null)
const fieldErrors = reactive<Record<string, string>>({})
const formRef = ref<FormInstance>()

function formatLocalDateTime(value: Date): string {
  const pad = (part: number): string => String(part).padStart(2, '0')
  return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())} ${pad(value.getHours())}:${pad(value.getMinutes())}:${pad(value.getSeconds())}`
}

const form = reactive({
  knowledgeRoleId: null as number | null,
  confirmationMethod: 'InSystem' as ConfirmationMethod,
  confirmedAt: formatLocalDateTime(new Date()),
  confirmationStatement: '',
  supportReason: '',
  sourceNote: '',
})

const rules: FormRules<typeof form> = {
  confirmationMethod: [{ required: true, message: '请选择确认方式', trigger: 'change' }],
  confirmedAt: [{ required: true, message: '请选择确认时间', trigger: 'change' }],
  confirmationStatement: [{ required: true, message: '请输入确认结论', trigger: 'blur' }],
  supportReason: [{ required: true, message: '请输入支持理由', trigger: 'blur' }],
}

const canSave = computed(() =>
  actorStore.canEdit
  && actorStore.currentUser !== null
  && (!requiresRoleSelection.value || form.knowledgeRoleId !== null)
  && !conflict.value,
)

watch(subject, (value) => {
  subjectRevisionNumber.value = value?.subjectRevisionNumber ?? null
  conflict.value = false
}, { immediate: true })

watch(activeRoles, (roles) => {
  if (roles.length <= 1 || !roles.some((role) => role.id === form.knowledgeRoleId)) {
    form.knowledgeRoleId = null
  }
}, { immediate: true })

function normalize(value: string): string | null {
  const result = value.trim()
  return result.length ? result : null
}

function clearFieldError(field: string): void {
  delete fieldErrors[field]
  if (!conflict.value) errorMessage.value = null
}

function toUtcIso(localDateTime: string): string {
  const value = new Date(localDateTime.replace(' ', 'T'))
  if (Number.isNaN(value.getTime())) throw new Error('确认时间无效。')
  return value.toISOString()
}

async function refreshCurrentUserAfterRoleError(): Promise<void> {
  await actorStore.refreshCurrentUser()
}

async function save(): Promise<void> {
  if (!subject.value || !canSave.value) return
  errorMessage.value = null
  for (const key of Object.keys(fieldErrors)) delete fieldErrors[key]
  if (requiresRoleSelection.value && form.knowledgeRoleId === null) {
    fieldErrors.knowledgeRoleId = '请选择本次确认使用的知识身份。'
    return
  }
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid || saving.value) return

  let confirmedAt: string
  try {
    confirmedAt = toUtcIso(form.confirmedAt)
  } catch (error: unknown) {
    fieldErrors.confirmedAt = error instanceof Error ? error.message : '确认时间无效。'
    return
  }

  saving.value = true
  try {
    const created = await addHumanConfirmation({
      subject: subject.value.subject,
      ...(subjectRevisionNumber.value === null
        ? {}
        : { subjectRevisionNumber: subjectRevisionNumber.value }),
      subjectDetailKey: subject.value.subjectDetailKey ?? null,
      knowledgeRoleId: requiresRoleSelection.value ? form.knowledgeRoleId : null,
      confirmationMethod: form.confirmationMethod,
      confirmedAt,
      confirmationStatement: form.confirmationStatement.trim(),
      supportReason: form.supportReason.trim(),
      sourceNote: normalize(form.sourceNote),
    })
    ElMessage.success('人工确认已记录；知识状态仍需单独推进。')
    window.dispatchEvent(new CustomEvent('evidence:changed'))
    window.dispatchEvent(new CustomEvent('human-confirmation:changed', {
      detail: { subject: created.subject },
    }))
    overlayStore.openDrawer({ kind: 'evidence', id: created.id, mode: 'read' })
  } catch (error: unknown) {
    if (error instanceof ApiError) {
      if (error.status === 409
        && error.response.code === 'conflict'
        && subject.value.subject.type === 'KnowledgeDocument') {
        conflict.value = true
        errorMessage.value = '当前修订已变化，请重新加载最新内容后再次明确确认。'
      } else {
        errorMessage.value = error.message
      }
      for (const [field, messages] of Object.entries(error.response.fieldErrors ?? {})) {
        const message = messages[0]
        if (message) fieldErrors[field] = message
      }
      if (error.status === 422
        && (error.response.code === 'invalid_state' || error.response.code === 'reference_invalid')) {
        await refreshCurrentUserAfterRoleError()
      }
    } else {
      errorMessage.value = error instanceof Error ? error.message : '人工确认保存失败。'
    }
  } finally {
    saving.value = false
  }
}

async function reloadLatestDocument(): Promise<void> {
  if (!subject.value
    || subject.value.subject.type !== 'KnowledgeDocument'
    || reloadingDocument.value) return
  reloadingDocument.value = true
  try {
    const document = await getKnowledgeDocument(subject.value.subject.id)
    subjectRevisionNumber.value = document.currentRevisionNumber
    conflict.value = false
    errorMessage.value = `已重新加载当前修订 ${document.currentRevisionNumber}，请再次明确确认最新内容。`
    window.dispatchEvent(new CustomEvent('knowledge-document:current-refreshed', {
      detail: { document },
    }))
  } catch (error: unknown) {
    errorMessage.value = error instanceof Error ? error.message : '重新加载当前文档失败。'
  } finally {
    reloadingDocument.value = false
  }
}

onMounted(() => void actorStore.initialize())
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
      <p v-if="subjectRevisionNumber !== null" class="human-confirmation-revision-context">
        本次人工确认将覆盖当前显示的修订 {{ subjectRevisionNumber }}。
      </p>

      <el-alert v-if="errorMessage" class="evidence-form-alert evidence-form-alert--outer" :type="conflict ? 'warning' : 'error'" :title="errorMessage" :closable="false" show-icon>
        <template v-if="conflict" #default>
          <el-button :loading="reloadingDocument" @click="reloadLatestDocument">重新加载最新内容</el-button>
        </template>
      </el-alert>

      <section class="evidence-current-user">
        <div class="evidence-current-user__heading">
          <div><small>当前操作者</small><strong>确认人身份由服务端根据 Current User 生成</strong></div>
        </div>

        <template v-if="actorStore.currentUser">
          <dl class="evidence-current-user__facts">
            <div><dt>姓名</dt><dd>{{ actorStore.currentUser.displayName }}</dd></div>
            <div><dt>工号</dt><dd>{{ actorStore.currentUser.employeeNo ?? '—' }}</dd></div>
            <div><dt>部门 / 团队</dt><dd>{{ actorStore.currentUser.departmentOrTeam ?? '—' }}</dd></div>
            <div><dt>职位</dt><dd>{{ actorStore.currentUser.jobTitle ?? '—' }}</dd></div>
          </dl>

          <div class="evidence-current-user__role">
            <template v-if="activeRoles.length === 0">
              <small>本次知识身份</small>
              <strong>知识提供者（未配置知识身份）</strong>
              <p>没有知识身份不会阻止人工确认；管理员可后续在用户管理中完善资料。</p>
            </template>
            <template v-else-if="activeRoles.length === 1">
              <small>本次知识身份</small>
              <strong>{{ activeRoles[0]?.name }}</strong>
              <p>唯一启用身份将由服务端自动采用。</p>
            </template>
            <el-form v-else label-position="top">
              <el-form-item label="本次知识身份" :error="fieldErrors.knowledgeRoleId" required>
                <el-select v-model="form.knowledgeRoleId" placeholder="选择本次确认使用的知识身份" @change="clearFieldError('knowledgeRoleId')">
                  <el-option v-for="role in activeRoles" :key="role.id" :label="role.name" :value="role.id" />
                </el-select>
              </el-form-item>
            </el-form>
          </div>
        </template>

        <el-alert v-else type="warning" title="当前认证身份不可用于人工确认，请重新登录或联系系统管理员。" :closable="false" show-icon />
      </section>

      <el-form ref="formRef" :model="form" :rules="rules" class="evidence-form" label-position="top" @submit.prevent>
        <section class="evidence-form-section evidence-form-section--confirmation">
          <h3>确认事实</h3>
          <div class="evidence-form__grid">
            <el-form-item label="确认方式" prop="confirmationMethod" :error="fieldErrors.confirmationMethod" required>
              <el-select v-model="form.confirmationMethod" placeholder="选择确认方式" @change="clearFieldError('confirmationMethod')">
                <el-option v-for="method in confirmationMethods" :key="method.value" :label="method.label" :value="method.value" />
              </el-select>
            </el-form-item>
            <el-form-item label="确认时间" prop="confirmedAt" :error="fieldErrors.confirmedAt" required>
              <el-date-picker v-model="form.confirmedAt" type="datetime" value-format="YYYY-MM-DD HH:mm:ss" format="YYYY-MM-DD HH:mm:ss" placeholder="选择本地确认时间" @change="clearFieldError('confirmedAt')" />
            </el-form-item>
          </div>
          <el-form-item label="确认结论" prop="confirmationStatement" :error="fieldErrors.confirmationStatement" required>
            <el-input v-model="form.confirmationStatement" type="textarea" :rows="4" placeholder="准确记录专家确认的知识内容" @input="clearFieldError('confirmationStatement')" />
          </el-form-item>
          <el-form-item label="为什么支持当前知识" prop="supportReason" :error="fieldErrors.supportReason" required>
            <el-input v-model="form.supportReason" type="textarea" :rows="3" placeholder="说明当前操作者的知识身份和上下文为什么足以支持这条知识" @input="clearFieldError('supportReason')" />
          </el-form-item>
          <el-form-item label="来源说明"><el-input v-model="form.sourceNote" placeholder="例如 现场评审会议" /></el-form-item>
        </section>
      </el-form>

      <section class="evidence-confirmation-impact">
        <el-icon><InfoFilled /></el-icon>
        <div><small>保存后的知识影响</small><strong>新增 HumanConfirmation Evidence</strong><p>Knowledge Status 保持当前状态；后续必须由明确操作推进。</p></div>
        <KnowledgeStatusBadge :status="subject.knowledgeStatus" />
      </section>

      <footer class="evidence-drawer__footer">
        <el-button @click="overlayStore.closeDrawer()">取消</el-button>
        <el-button type="primary" :icon="UserFilled" :loading="saving" :disabled="!canSave" @click="save">保存人工确认</el-button>
      </footer>
    </template>
  </div>
</template>

<style src="../evidence.css"></style>
