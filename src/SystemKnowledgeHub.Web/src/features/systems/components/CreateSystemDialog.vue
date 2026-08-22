<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ArrowLeft, DocumentChecked, Link, Memo } from '@element-plus/icons-vue'
import { ApiError } from '../../../api/errors/ApiError'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import { createSystem } from '../api/systemsApi'
import {
  systemLifecycleLabels,
  systemLifecycles,
  type CreateSystemResponse,
  type SystemLifecycle,
} from '../api/systemsContracts'

const emit = defineEmits<{ created: [system: CreateSystemResponse] }>()
const actorStore = useActorStore()
const overlayStore = useOverlayStore()
const formRef = ref<FormInstance>()
const submitting = ref(false)
const submitError = ref<string | null>(null)
const fieldErrors = reactive<Record<string, string>>({})
const form = reactive({
  name: '',
  displayName: '',
  systemType: '',
  lifecycle: 'Legacy' as SystemLifecycle,
  purpose: '',
})

const rules: FormRules<typeof form> = {
  name: [{ required: true, message: '请输入系统名称', trigger: 'blur' }],
  displayName: [{ required: true, message: '请输入显示名称', trigger: 'blur' }],
  systemType: [{ required: true, message: '请输入系统类型', trigger: 'blur' }],
  lifecycle: [{ required: true, message: '请选择生命周期', trigger: 'change' }],
}

const actorDescription = computed(() =>
  actorStore.role ? `${actorStore.displayName} · ${actorStore.role}` : actorStore.displayName,
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
  if (!valid || submitting.value) return

  submitting.value = true
  try {
    const created = await createSystem({
      name: form.name.trim(),
      displayName: form.displayName.trim(),
      systemType: form.systemType.trim(),
      lifecycle: form.lifecycle,
      purpose: form.purpose.trim() || null,
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
      submitError.value = error instanceof Error ? error.message : '系统创建失败。'
    }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <section class="create-system-dialog" aria-labelledby="create-system-title">
    <header class="authoring-header authoring-header--form">
      <div>
        <button class="authoring-back" type="button" @click="goBack">
          <el-icon><ArrowLeft /></el-icon> 选择知识类型
        </button>
        <h2 id="create-system-title">新增系统</h2>
        <p>先记录最小必要信息；创建后再补充技术、关系、证据和业务知识。</p>
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

    <div class="create-system-dialog__body">
      <el-form ref="formRef" :model="form" :rules="rules" label-position="top" @submit.prevent>
        <el-alert v-if="submitError" class="authoring-form-alert" type="error" :title="submitError" :closable="false" show-icon />
        <el-form-item label="系统名称" prop="name" :error="fieldErrors.name" required>
          <el-input v-model="form.name" maxlength="120" placeholder="例如 MES" class="technical-input" />
          <span class="form-help">稳定的技术名称，创建后不在当前 Slice 中编辑。</span>
        </el-form-item>
        <el-form-item label="显示名称" prop="displayName" :error="fieldErrors.displayName" required>
          <el-input v-model="form.displayName" maxlength="160" placeholder="例如 制造执行系统" />
        </el-form-item>
        <div class="create-system-dialog__row">
          <el-form-item label="系统类型" prop="systemType" :error="fieldErrors.systemType" required>
            <el-input v-model="form.systemType" maxlength="160" placeholder="例如 核心业务系统" />
          </el-form-item>
          <el-form-item label="生命周期" prop="lifecycle" :error="fieldErrors.lifecycle" required>
            <el-select v-model="form.lifecycle" placeholder="选择生命周期">
              <el-option
                v-for="lifecycle in systemLifecycles"
                :key="lifecycle"
                :label="systemLifecycleLabels[lifecycle]"
                :value="lifecycle"
              />
            </el-select>
          </el-form-item>
        </div>
        <el-form-item label="用途（可选）" prop="purpose" :error="fieldErrors.purpose">
          <el-input
            v-model="form.purpose"
            type="textarea"
            :rows="3"
            maxlength="500"
            show-word-limit
            placeholder="一句话说明该系统解决什么问题"
          />
        </el-form-item>
      </el-form>

      <aside class="create-system-dialog__next">
        <h3>创建后的下一步</h3>
        <div><el-icon><Link /></el-icon><span><strong>添加关系</strong><small>连接业务功能、数据库与集成关系</small></span></div>
        <div><el-icon><DocumentChecked /></el-icon><span><strong>添加证据</strong><small>说明为什么相信这条知识</small></span></div>
        <div><el-icon><Memo /></el-icon><span><strong>补充业务知识</strong><small>完善用途、技术与系统上下文</small></span></div>
        <section>
          <span>知识状态</span>
          <p>默认保持“未知”，不会因创建自动推进。</p>
          <strong class="knowledge-status-badge knowledge-status-badge--unknown">未知</strong>
        </section>
        <footer>创建人：{{ actorDescription }}</footer>
      </aside>
    </div>

    <footer class="authoring-actions">
      <p>创建不会自动添加证据、关系或待确认事项。</p>
      <div>
        <el-button @click="overlayStore.closeDialog">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submit">创建系统</el-button>
      </div>
    </footer>
  </section>
</template>
