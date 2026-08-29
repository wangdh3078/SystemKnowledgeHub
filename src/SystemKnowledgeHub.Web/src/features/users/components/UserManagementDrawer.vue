<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'
import { ApiError } from '../../../api/errors/ApiError'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import { createUser, getKnowledgeRoles, getUser, updateUser } from '../api/usersApi'
import LoginIdentityManagementPanel from './LoginIdentityManagementPanel.vue'
import type { KnowledgeRole, UserDetail } from '../api/userContracts'

const props = defineProps<{ userId: number | null }>()
const emit = defineEmits<{ saved: [user: UserDetail] }>()
const actorStore = useActorStore()
const overlayStore = useOverlayStore()
const formRef = ref<FormInstance>()
const loading = ref(true)
const submitting = ref(false)
const loadError = ref<string | null>(null)
const submitError = ref<string | null>(null)
const conflict = ref(false)
const roles = ref<readonly KnowledgeRole[]>([])
const originalRoleIds = ref<ReadonlySet<number>>(new Set())
const concurrencyToken = ref('')
const form = reactive({
  displayName: '',
  employeeNo: '',
  email: '',
  departmentOrTeam: '',
  jobTitle: '',
  knowledgeRoleIds: [] as number[],
})
const fieldErrors = reactive<Record<string, string>>({})
const isEdit = computed(() => props.userId !== null)
const title = computed(() => isEdit.value ? '编辑用户' : '新增用户')
const rules: FormRules<typeof form> = {
  displayName: [{ required: true, message: '请输入姓名', trigger: 'blur' }],
  email: [{ type: 'email', message: '请输入有效邮箱地址', trigger: 'blur' }],
}

function assignUser(user: UserDetail): void {
  form.displayName = user.displayName
  form.employeeNo = user.employeeNo ?? ''
  form.email = user.email ?? ''
  form.departmentOrTeam = user.departmentOrTeam ?? ''
  form.jobTitle = user.jobTitle ?? ''
  form.knowledgeRoleIds = user.knowledgeRoles.map((role) => role.id)
  originalRoleIds.value = new Set(form.knowledgeRoleIds)
  concurrencyToken.value = user.concurrencyToken
}

async function load(): Promise<void> {
  loading.value = true
  loadError.value = null
  clearServerErrors()
  try {
    const [availableRoles, user] = await Promise.all([
      getKnowledgeRoles(),
      props.userId === null ? Promise.resolve(null) : getUser(props.userId),
    ])
    roles.value = availableRoles
    if (user) assignUser(user)
  } catch (error: unknown) {
    loadError.value = error instanceof Error ? error.message : '用户资料加载失败。'
  } finally {
    loading.value = false
  }
}

function roleDisabled(role: KnowledgeRole): boolean {
  return !role.isActive && !originalRoleIds.value.has(role.id)
}

function clearServerErrors(): void {
  submitError.value = null
  conflict.value = false
  for (const key of Object.keys(fieldErrors)) delete fieldErrors[key]
}

async function submit(): Promise<void> {
  clearServerErrors()
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid || submitting.value) return

  submitting.value = true
  const request = {
    displayName: form.displayName.trim(),
    employeeNo: form.employeeNo.trim() || null,
    email: form.email.trim() || null,
    departmentOrTeam: form.departmentOrTeam.trim() || null,
    jobTitle: form.jobTitle.trim() || null,
    knowledgeRoleIds: form.knowledgeRoleIds,
    actor: actorStore.actor,
  }
  try {
    const saved = props.userId === null
      ? await createUser(request)
      : await updateUser(props.userId, { ...request, concurrencyToken: concurrencyToken.value })
    ElMessage.success(props.userId === null ? '用户已创建。' : '用户资料已保存。')
    emit('saved', saved)
    overlayStore.closeDrawer()
  } catch (error: unknown) {
    if (error instanceof ApiError) {
      submitError.value = error.message
      conflict.value = error.status === 409
        && error.response.code === 'conflict'
        && error.response.details?.resourceType === 'User'
      if (error.response.fieldErrors) {
        for (const [field, messages] of Object.entries(error.response.fieldErrors)) {
          const message = messages[0]
          if (message) fieldErrors[field] = message
        }
      }
    } else {
      submitError.value = error instanceof Error ? error.message : '用户保存失败。'
    }
  } finally {
    submitting.value = false
  }
}

onMounted(() => void load())
</script>

<template>
  <section class="user-drawer" :aria-labelledby="'user-drawer-title'">
    <header class="user-drawer__header skh-drawer-header">
      <div>
        <span>ADMIN · USER PROFILE</span>
        <h2 id="user-drawer-title">{{ title }}</h2>
        <p>维护人员资料、KnowledgeRole 与 LoginIdentity 映射；AccessLevel 由独立安全操作管理。</p>
      </div>
      <el-tooltip content="关闭用户编辑" placement="bottom"><button class="skh-icon-action" type="button" aria-label="关闭用户编辑" @click="overlayStore.requestDrawerClose">×</button></el-tooltip>
    </header>

    <div v-if="loading" class="user-drawer__state">正在读取用户资料…</div>
    <div v-else-if="loadError" class="user-drawer__state user-drawer__state--error">
      <strong>加载失败</strong><p>{{ loadError }}</p><el-button @click="load">重试</el-button>
    </div>
    <template v-else>
      <el-alert
        v-if="submitError"
        class="user-drawer__alert"
        type="error"
        :title="conflict ? '资料已被其他操作修改' : submitError"
        :description="conflict ? '为避免覆盖其他修改，请重新载入最新资料后再编辑。' : undefined"
        :closable="false"
        show-icon
      >
        <template v-if="conflict" #default>
          <div class="user-conflict-message"><span>为避免覆盖其他修改，请重新载入最新资料后再编辑。</span><el-button size="small" @click="load">重新载入</el-button></div>
        </template>
      </el-alert>

      <el-form ref="formRef" :model="form" :rules="rules" label-position="top" @submit.prevent>
        <section class="user-drawer__section">
          <div class="user-drawer__section-title"><span>01</span><div><h3>基础资料</h3><p>姓名必填，其余字段可按现有信息渐进补充。</p></div></div>
          <el-form-item label="姓名" prop="displayName" :error="fieldErrors.displayName" required>
            <el-input v-model="form.displayName" maxlength="160" placeholder="例如 王敏" />
          </el-form-item>
          <div class="user-drawer__row">
            <el-form-item label="工号" prop="employeeNo" :error="fieldErrors.employeeNo">
              <el-input v-model="form.employeeNo" maxlength="80" placeholder="例如 EMP-001" class="technical-input" />
            </el-form-item>
            <el-form-item label="邮箱" prop="email" :error="fieldErrors.email">
              <el-input v-model="form.email" maxlength="240" placeholder="name@example.com" />
            </el-form-item>
          </div>
          <div class="user-drawer__row">
            <el-form-item label="部门 / 团队" prop="departmentOrTeam" :error="fieldErrors.departmentOrTeam">
              <el-input v-model="form.departmentOrTeam" maxlength="160" placeholder="例如 制造系统组" />
            </el-form-item>
            <el-form-item label="职位" prop="jobTitle" :error="fieldErrors.jobTitle">
              <el-input v-model="form.jobTitle" maxlength="160" placeholder="例如 Senior Engineer" />
            </el-form-item>
          </div>
        </section>

        <section class="user-drawer__section">
          <div class="user-drawer__section-title"><span>02</span><div><h3>知识身份</h3><p>只可新增启用中的身份；已停用的既有映射继续保留并明确标记。</p></div></div>
          <el-form-item label="Knowledge Roles" prop="knowledgeRoleIds" :error="fieldErrors.knowledgeRoleIds">
            <el-select v-model="form.knowledgeRoleIds" multiple filterable clearable placeholder="选择知识身份">
              <el-option
                v-for="role in roles"
                :key="role.id"
                :label="role.isActive ? role.name : `${role.name}（已停用）`"
                :value="role.id"
                :disabled="roleDisabled(role)"
              />
            </el-select>
            <span class="user-drawer__help">停用知识身份不会自动从已有用户移除。</span>
          </el-form-item>
        </section>

        <LoginIdentityManagementPanel v-if="userId !== null" :user-id="userId" />
      </el-form>

      <footer class="user-drawer__actions">
        <span>{{ isEdit ? '启用 / 停用请使用列表中的独立操作。' : '新用户创建后默认为启用。' }}</span>
        <div><el-button @click="overlayStore.requestDrawerClose">取消</el-button><el-button type="primary" :loading="submitting" @click="submit">{{ isEdit ? '保存修改' : '创建用户' }}</el-button></div>
      </footer>
    </template>
  </section>
</template>
