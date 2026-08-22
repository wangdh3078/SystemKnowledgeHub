<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'
import { ElRadioButton, ElRadioGroup } from 'element-plus'
import { useRouter } from 'vue-router'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import { getSystemsList } from '../../systems/api/systemsApi'
import type { SystemSummary } from '../../systems/api/systemsContracts'
import { ApiError } from '../../../api/errors/ApiError'
import { unknownItemsApi } from '../api/unknownItemsApi'
import type { UnknownItemPriority, UnknownTarget, UnknownTargetType } from '../api/unknownItemContracts'

type Payload = { systemId: number; systemName: string; target: UnknownTarget; title: string }
const emit = defineEmits<{ created: [] }>()
const overlays = useOverlayStore(); const actorStore = useActorStore(); const router = useRouter()
const systems = ref<SystemSummary[]>([]); const saving = ref(false); const error = ref<string | null>(null)
const formRef = ref<FormInstance>()
const fieldErrors = reactive<Record<string, string>>({})
const payload = computed<Payload | null>(() => {
  const value = overlays.currentDialog?.payload
  if (typeof value !== 'object' || value === null) return null
  const item = value as Record<string, unknown>; const target = item.target
  if (typeof item.systemId !== 'number' || typeof item.systemName !== 'string' || typeof item.title !== 'string' || typeof target !== 'object' || target === null) return null
  const ref = target as Record<string, unknown>
  return typeof ref.id === 'number' && typeof ref.type === 'string'
    ? { systemId: item.systemId, systemName: item.systemName, title: item.title, target: { id: ref.id, type: ref.type as UnknownTargetType } } : null
})
const form = reactive({ systemId: payload.value?.systemId ?? 0, question: '', context: '', priority: 'Medium' as UnknownItemPriority })
const rules: FormRules<typeof form> = {
  systemId: [{ required: true, type: 'number', min: 1, message: '请选择所属系统', trigger: 'change' }],
  question: [{ required: true, message: '请输入问题', trigger: 'blur' }],
}
const hasFieldErrors = computed(() => Object.keys(fieldErrors).length > 0)

function clearFieldError(field: string): void {
  delete fieldErrors[field]
  if (hasFieldErrors.value === false) error.value = null
}

watch(payload, (value) => {
  if (value) form.systemId = value.systemId
})

onMounted(async () => {
  if (payload.value) return
  try {
    const response = await getSystemsList({ page: 1, pageSize: 100, sort: 'name:asc' })
    systems.value = [...response.items]; if (!form.systemId && systems.value[0]) form.systemId = systems.value[0].id
  } catch (cause: unknown) { error.value = cause instanceof Error ? cause.message : '系统列表加载失败。' }
})

async function save(): Promise<void> {
  error.value = null
  for (const field of Object.keys(fieldErrors)) delete fieldErrors[field]
  const valid = await formRef.value?.validate().catch(() => false)
  if (!payload.value && !form.systemId) fieldErrors.systemId = '请选择所属系统。'
  if (!valid || Object.keys(fieldErrors).length > 0 || !form.systemId) return
  saving.value = true; error.value = null
  try {
    const primaryTarget: UnknownTarget = payload.value?.target ?? { type: 'System', id: form.systemId }
    const created = await unknownItemsApi.create({
      systemId: form.systemId, question: form.question.trim(), context: form.context.trim() || null,
      priority: form.priority, primaryTarget, relatedTargets: [],
      creator: { displayName: actorStore.displayName, roleOrIdentity: '创建人', occurredAt: new Date().toISOString(), team: null, externalUserKey: null, source: 'Manual', note: null },
    })
    overlays.closeDialog(); ElMessage.success('待确认事项已创建，当前状态为“待处理”。'); emit('created')
    await router.push({ name: 'unknown-item-detail', params: { id: String(created.id) } })
  } catch (cause: unknown) {
    if (cause instanceof ApiError) {
      error.value = cause.message
      for (const [field, messages] of Object.entries(cause.response.fieldErrors ?? {})) {
        const message = messages[0]
        if (message) fieldErrors[field] = message
      }
    } else error.value = cause instanceof Error ? cause.message : '创建失败。'
  }
  finally { saving.value = false }
}
</script>

<template>
  <Teleport v-if="overlays.currentDialog?.kind === 'create-unknown-item'" defer to="#dialog-feature-content">
    <section class="create-unknown-dialog">
      <header><div><small>渐进式记录</small><h2>新增待确认事项</h2><p>先记录问题与上下文，调查发现、证据和结论可后续补充。</p></div><button aria-label="关闭" @click="overlays.closeDialog">×</button></header>
      <div class="create-unknown-dialog__context">
        <template v-if="payload"><span>关联对象</span><strong class="technical-text">{{ payload.systemName }} · {{ payload.title }}</strong></template>
        <el-form v-else :model="form" :rules="rules" label-position="top"><el-form-item label="所属系统" prop="systemId" :error="fieldErrors.systemId" required><el-select v-model="form.systemId" filterable @change="clearFieldError('systemId')"><el-option v-for="item in systems" :key="item.id" :label="`${item.name} · ${item.displayName}`" :value="item.id" /></el-select></el-form-item></el-form>
      </div>
      <el-form ref="formRef" :model="form" :rules="rules" label-position="top" @submit.prevent>
        <el-alert v-if="error && !hasFieldErrors" class="authoring-form-alert" type="error" :title="error" :closable="false" show-icon />
        <el-form-item label="问题" prop="question" :error="fieldErrors.question" required><el-input v-model="form.question" maxlength="240" placeholder="例如 STATE_FLAG=30 具体表示什么？" @input="clearFieldError('question')" /></el-form-item>
        <el-form-item label="问题上下文"><el-input v-model="form.context" type="textarea" :rows="3" placeholder="说明在哪里发现问题、为什么需要确认" /></el-form-item>
        <el-form-item label="优先级"><el-radio-group v-model="form.priority"><el-radio-button value="High">高</el-radio-button><el-radio-button value="Medium">中</el-radio-button><el-radio-button value="Low">低</el-radio-button></el-radio-group></el-form-item>
      </el-form>
      <footer><span>创建后保持“待处理”，不会自动开始调查。</span><div><el-button @click="overlays.closeDialog">取消</el-button><el-button type="primary" :loading="saving" @click="save">创建事项</el-button></div></footer>
    </section>
  </Teleport>
</template>
