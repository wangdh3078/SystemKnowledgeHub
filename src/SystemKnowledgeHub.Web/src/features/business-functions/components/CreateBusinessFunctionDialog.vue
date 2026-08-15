<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ArrowLeft, Connection, DocumentChecked, List } from '@element-plus/icons-vue'
import { ApiError } from '../../../api/errors/ApiError'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import type { SystemSummary } from '../../systems/api/systemsContracts'
import { createBusinessFunction } from '../api/businessFunctionsApi'
import {
  functionTypeLabels,
  type CreateBusinessFunctionResponse,
} from '../api/businessFunctionContracts'

const props = defineProps<{
  systems: readonly SystemSummary[]
  initialSystemId?: number
}>()
const emit = defineEmits<{ created: [businessFunction: CreateBusinessFunctionResponse] }>()
const actorStore = useActorStore()
const overlayStore = useOverlayStore()
const formRef = ref<FormInstance>()
const submitting = ref(false)
const submitError = ref<string | null>(null)
const fieldErrors = reactive<Record<string, string>>({})
const form = reactive({
  systemId: props.initialSystemId,
  name: '',
  displayName: '',
  functionType: 'Query',
  purpose: '',
})

const rules: FormRules<typeof form> = {
  systemId: [{ required: true, message: '请选择所属系统', trigger: 'change' }],
  name: [{ required: true, message: '请输入业务功能名称', trigger: 'blur' }],
  functionType: [{ required: true, message: '请选择功能类型', trigger: 'change' }],
}
const functionTypes = Object.entries(functionTypeLabels).map(([value, label]) => ({ value, label }))
const actorDescription = computed(() =>
  actorStore.role ? `${actorStore.displayName} · ${actorStore.role}` : actorStore.displayName,
)

watch(
  () => [props.initialSystemId, props.systems] as const,
  () => {
    if (form.systemId) return
    form.systemId = props.initialSystemId ?? props.systems[0]?.id
  },
  { immediate: true },
)

function goBack(): void {
  overlayStore.openDialog({ kind: 'create-knowledge-object', id: null, mode: 'create' })
}

function clearServerErrors(): void {
  submitError.value = null
  for (const key of Object.keys(fieldErrors)) delete fieldErrors[key]
}

async function submit(): Promise<void> {
  clearServerErrors()
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid || submitting.value || !form.systemId) return

  submitting.value = true
  try {
    const created = await createBusinessFunction({
      systemId: form.systemId,
      name: form.name.trim(),
      displayName: form.displayName.trim() || null,
      functionType: form.functionType,
      purpose: form.purpose.trim() || null,
      rewriteStatus: 'Unknown',
      actor: actorStore.actor,
    })
    emit('created', created)
  } catch (error: unknown) {
    if (error instanceof ApiError) {
      submitError.value = error.message
      if (error.response.fieldErrors) {
        for (const [field, messages] of Object.entries(error.response.fieldErrors)) {
          const message = messages[0]
          if (message) fieldErrors[field] = message
        }
      }
    } else {
      submitError.value = error instanceof Error ? error.message : '业务功能创建失败。'
    }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <section class="create-business-function-dialog create-system-dialog" aria-labelledby="create-business-function-title">
    <header class="authoring-header authoring-header--form">
      <div>
        <button class="authoring-back" type="button" @click="goBack">
          <el-icon><ArrowLeft /></el-icon> 选择知识类型
        </button>
        <h2 id="create-business-function-title">新增业务功能</h2>
        <p>先记录最小必要信息；业务流程、关系、证据和业务知识可以创建后逐步补充。</p>
      </div>
      <button class="authoring-close" type="button" aria-label="关闭" @click="overlayStore.closeDialog">×</button>
    </header>

    <div class="authoring-progression" aria-label="知识完善路径">
      <span class="authoring-progression__step authoring-progression__step--active"><b>1</b>基本信息（当前）</span>
      <span class="authoring-progression__line"></span>
      <span class="authoring-progression__step"><b>2</b>关系（创建后）</span>
      <span class="authoring-progression__line"></span>
      <span class="authoring-progression__step"><b>3</b>证据（创建后）</span>
    </div>

    <div class="create-business-function-dialog__body create-system-dialog__body">
      <el-form ref="formRef" :model="form" :rules="rules" label-position="top" @submit.prevent>
        <el-form-item label="所属系统" prop="systemId" :error="fieldErrors.systemId">
          <el-select v-model="form.systemId" filterable placeholder="选择系统">
            <el-option
              v-for="system in systems"
              :key="system.id"
              :label="`${system.name} · ${system.displayName}`"
              :value="system.id"
            />
          </el-select>
          <span class="form-help">系统上下文会用于后续关系与证据。</span>
        </el-form-item>
        <el-form-item label="功能名称" prop="name" :error="fieldErrors.name">
          <el-input v-model="form.name" maxlength="160" placeholder="例如 Equipment Status Query" class="technical-input" />
        </el-form-item>
        <div class="create-business-function-dialog__row create-system-dialog__row">
          <el-form-item label="显示名称（可选）" prop="displayName" :error="fieldErrors.displayName">
            <el-input v-model="form.displayName" maxlength="160" placeholder="例如 设备状态查询" />
          </el-form-item>
          <el-form-item label="功能类型" prop="functionType" :error="fieldErrors.functionType">
            <el-select v-model="form.functionType" placeholder="选择功能类型">
              <el-option v-for="item in functionTypes" :key="item.value" :label="item.label" :value="item.value" />
            </el-select>
          </el-form-item>
        </div>
        <el-form-item label="用途（可选）" prop="purpose" :error="fieldErrors.purpose">
          <el-input v-model="form.purpose" type="textarea" :rows="3" maxlength="500" show-word-limit placeholder="一句话说明该业务功能解决什么问题" />
        </el-form-item>
        <p v-if="submitError" class="authoring-error" role="alert">{{ submitError }}</p>
      </el-form>

      <aside class="create-business-function-dialog__next create-system-dialog__next">
        <h3>创建后的下一步</h3>
        <div><el-icon><List /></el-icon><span><strong>补充业务流程</strong><small>用简单有序步骤描述处理过程</small></span></div>
        <div><el-icon><Connection /></el-icon><span><strong>添加关系</strong><small>连接数据、规则与集成关系</small></span></div>
        <div><el-icon><DocumentChecked /></el-icon><span><strong>添加证据</strong><small>说明为什么相信这条知识</small></span></div>
        <section>
          <span>知识状态</span>
          <p>默认保持“未知”，不会因创建自动推进。</p>
          <strong class="knowledge-status-badge knowledge-status-badge--unknown">未知</strong>
        </section>
        <footer>创建人：{{ actorDescription }}</footer>
      </aside>
    </div>

    <footer class="authoring-actions">
      <p>创建不会自动添加业务流程、关系、证据或待确认事项。</p>
      <div>
        <el-button @click="overlayStore.closeDialog">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submit">创建业务功能</el-button>
      </div>
    </footer>
  </section>
</template>
