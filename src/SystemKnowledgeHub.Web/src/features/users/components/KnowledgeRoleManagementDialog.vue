<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ApiError } from '../../../api/errors/ApiError'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import { createKnowledgeRole, getKnowledgeRoles, setKnowledgeRoleActiveState, updateKnowledgeRole } from '../api/usersApi'
import type { KnowledgeRole } from '../api/userContracts'

const emit = defineEmits<{ changed: [] }>()
const actorStore = useActorStore()
const overlayStore = useOverlayStore()
const formRef = ref<FormInstance>()
const roles = ref<readonly KnowledgeRole[]>([])
const loading = ref(true)
const submitting = ref(false)
const actionId = ref<number | null>(null)
const error = ref<string | null>(null)
const conflict = ref(false)
const editingRole = ref<KnowledgeRole | null>(null)
const form = reactive({ name: '', description: '' })
const fieldErrors = reactive<Record<string, string>>({})
const rules: FormRules<typeof form> = {
  name: [{ required: true, message: '请输入知识身份名称', trigger: 'blur' }],
}
const formTitle = computed(() => editingRole.value ? '编辑知识身份' : '新增知识身份')

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  conflict.value = false
  try {
    roles.value = await getKnowledgeRoles()
  } catch (requestError: unknown) {
    error.value = requestError instanceof Error ? requestError.message : '知识身份加载失败。'
  } finally {
    loading.value = false
  }
}

function clearForm(): void {
  editingRole.value = null
  form.name = ''
  form.description = ''
  error.value = null
  conflict.value = false
  for (const key of Object.keys(fieldErrors)) delete fieldErrors[key]
  formRef.value?.clearValidate()
}

function edit(role: KnowledgeRole): void {
  editingRole.value = role
  form.name = role.name
  form.description = role.description ?? ''
  error.value = null
  conflict.value = false
  for (const key of Object.keys(fieldErrors)) delete fieldErrors[key]
}

function handleApiError(requestError: unknown, fallback: string): void {
  if (requestError instanceof ApiError) {
    error.value = requestError.message
    conflict.value = requestError.status === 409
      && requestError.response.code === 'conflict'
      && requestError.response.details?.resourceType === 'KnowledgeRole'
    if (requestError.response.fieldErrors) {
      for (const [field, messages] of Object.entries(requestError.response.fieldErrors)) {
        const message = messages[0]
        if (message) fieldErrors[field] = message
      }
    }
  } else {
    error.value = requestError instanceof Error ? requestError.message : fallback
  }
}

async function submit(): Promise<void> {
  error.value = null
  conflict.value = false
  for (const key of Object.keys(fieldErrors)) delete fieldErrors[key]
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid || submitting.value) return
  submitting.value = true
  const request = {
    name: form.name.trim(),
    description: form.description.trim() || null,
    actor: actorStore.actor,
  }
  try {
    if (editingRole.value) {
      await updateKnowledgeRole(editingRole.value.id, {
        ...request,
        concurrencyToken: editingRole.value.concurrencyToken,
      })
      ElMessage.success('知识身份已更新。')
    } else {
      await createKnowledgeRole(request)
      ElMessage.success('知识身份已创建。')
    }
    clearForm()
    await load()
    emit('changed')
  } catch (requestError: unknown) {
    handleApiError(requestError, '知识身份保存失败。')
  } finally {
    submitting.value = false
  }
}

async function toggleActive(role: KnowledgeRole): Promise<void> {
  const nextActive = !role.isActive
  const action = nextActive ? '启用' : '停用'
  try {
    await ElMessageBox.confirm(
      nextActive
        ? `确认启用“${role.name}”？启用后可重新分配给用户。`
        : `确认停用“${role.name}”？已有用户映射会保留，但不能再新增分配。`,
      `${action}知识身份`,
      { confirmButtonText: action, cancelButtonText: '取消', type: nextActive ? 'info' : 'warning' },
    )
  } catch {
    return
  }

  actionId.value = role.id
  error.value = null
  conflict.value = false
  try {
    await setKnowledgeRoleActiveState(role.id, nextActive, role.concurrencyToken, actorStore.actor)
    ElMessage.success(`知识身份已${action}。`)
    if (editingRole.value?.id === role.id) clearForm()
    await load()
    emit('changed')
  } catch (requestError: unknown) {
    handleApiError(requestError, `知识身份${action}失败。`)
  } finally {
    actionId.value = null
  }
}

onMounted(() => void load())
</script>

<template>
  <section class="role-dialog" aria-labelledby="role-dialog-title">
    <header class="role-dialog__header">
      <div><span>ADMIN · KNOWLEDGE ROLE</span><h2 id="role-dialog-title">知识身份管理</h2><p>知识身份表达专业背景，不授予任何系统权限。</p></div>
      <el-tooltip content="关闭知识身份管理" placement="bottom"><button class="skh-icon-action" type="button" aria-label="关闭知识身份管理" @click="overlayStore.closeDialog">×</button></el-tooltip>
    </header>

    <el-alert
      v-if="error"
      class="role-dialog__alert"
      type="error"
      :title="conflict ? '知识身份已被其他操作修改' : error"
      :description="conflict ? '请重新载入最新列表后再继续，系统不会静默覆盖其他修改。' : undefined"
      :closable="false"
      show-icon
    >
      <template v-if="conflict" #default>
        <div class="user-conflict-message"><span>请重新载入最新列表后再继续，系统不会静默覆盖其他修改。</span><el-button size="small" @click="load">重新载入</el-button></div>
      </template>
    </el-alert>

    <div class="role-dialog__body">
      <section class="role-dialog__list" aria-label="知识身份列表">
        <header><h3>Knowledge Roles</h3><span>{{ roles.length }} 项</span></header>
        <div v-if="loading" class="role-dialog__loading">正在读取知识身份…</div>
        <div v-else-if="roles.length === 0" class="role-dialog__empty">尚未创建知识身份。</div>
        <button v-for="role in roles" v-else :key="role.id" type="button" :class="{ 'is-selected': editingRole?.id === role.id }" @click="edit(role)">
          <span><strong>{{ role.name }}</strong><small>{{ role.description ?? '暂无说明' }}</small></span>
          <el-tag :type="role.isActive ? 'success' : 'info'" effect="plain" size="small">{{ role.isActive ? '启用' : '停用' }}</el-tag>
        </button>
      </section>

      <section class="role-dialog__editor">
        <header><div><h3>{{ formTitle }}</h3><p>{{ editingRole ? '修改名称或说明；状态使用下方独立操作。' : '创建后默认为启用，可立即分配给用户。' }}</p></div><el-button v-if="editingRole" text type="primary" @click="clearForm">新增</el-button></header>
        <el-form ref="formRef" :model="form" :rules="rules" label-position="top" @submit.prevent>
          <el-form-item label="名称" prop="name" :error="fieldErrors.name" required><el-input v-model="form.name" maxlength="160" placeholder="例如 MES 业务专家" /></el-form-item>
          <el-form-item label="说明" prop="description" :error="fieldErrors.description"><el-input v-model="form.description" type="textarea" :rows="4" maxlength="500" show-word-limit placeholder="说明该身份代表的知识范围（可选）" /></el-form-item>
        </el-form>
        <div class="role-dialog__editor-actions">
          <el-button v-if="editingRole" :type="editingRole.isActive ? 'danger' : 'success'" plain :loading="actionId === editingRole.id" @click="toggleActive(editingRole)">{{ editingRole.isActive ? '停用知识身份' : '启用知识身份' }}</el-button>
          <el-button type="primary" :loading="submitting" @click="submit">{{ editingRole ? '保存修改' : '创建知识身份' }}</el-button>
        </div>
      </section>
    </div>

    <footer class="role-dialog__footer"><p>停用不会删除角色，也不会清除已有 UserKnowledgeRole 映射。</p><el-button @click="overlayStore.closeDialog">完成</el-button></footer>
  </section>
</template>
