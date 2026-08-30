<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'
import { ApiError } from '../../../api/errors/ApiError'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import {
  createUser,
  getKnowledgeRoles,
  getUser,
  getUserLoginMethods,
  getUserLoginSetupOptions,
  updateUser,
} from '../api/usersApi'
import LoginIdentityManagementPanel from './LoginIdentityManagementPanel.vue'
import type {
  KnowledgeRole,
  LoginSetup,
  UserDetail,
  UserLoginMethods,
  UserLoginSetupOptions,
} from '../api/userContracts'

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
const loginSetupOptions = ref<UserLoginSetupOptions | null>(null)
const loginMethods = ref<UserLoginMethods | null>(null)
const originalRoleIds = ref<ReadonlySet<number>>(new Set())
const concurrencyToken = ref('')
const form = reactive({
  displayName: '',
  employeeNo: '',
  email: '',
  departmentOrTeam: '',
  jobTitle: '',
  knowledgeRoleIds: [] as number[],
  loginSetupType: '' as '' | LoginSetup['type'],
  loginUsername: '',
  initialPassword: '',
  confirmPassword: '',
  oidcProvider: '',
  oidcSubject: '',
})
const fieldErrors = reactive<Record<string, string>>({})
const isEdit = computed(() => props.userId !== null)
const title = computed(() => isEdit.value ? '编辑用户' : '新增用户')
const rules: FormRules<typeof form> = {
  displayName: [{ required: true, message: '请输入姓名', trigger: 'blur' }],
  email: [{ type: 'email', message: '请输入有效邮箱地址', trigger: 'blur' }],
}
const passwordMismatch = computed(() =>
  !isEdit.value
  && form.loginSetupType === 'local'
  && form.confirmPassword.length > 0
  && form.initialPassword !== form.confirmPassword,
)

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
    const [availableRoles, user, setupOptions, methods] = await Promise.all([
      getKnowledgeRoles(),
      props.userId === null ? Promise.resolve(null) : getUser(props.userId),
      props.userId === null ? getUserLoginSetupOptions() : Promise.resolve(null),
      props.userId === null ? Promise.resolve(null) : getUserLoginMethods(props.userId),
    ])
    roles.value = availableRoles
    loginSetupOptions.value = setupOptions
    loginMethods.value = methods
    form.oidcProvider = setupOptions?.approvedOidcProvider ?? ''
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

function selectLoginSetup(type: LoginSetup['type']): void {
  clearServerErrors()
  if (type === 'local' && !form.loginUsername && form.employeeNo.trim()) {
    form.loginUsername = form.employeeNo.trim()
  }
  if (type === 'oidc') {
    form.oidcProvider = loginSetupOptions.value?.approvedOidcProvider ?? ''
  }
}

function validateLoginSetup(): boolean {
  if (isEdit.value) return true
  if (!form.loginSetupType) {
    fieldErrors['loginSetup.type'] = '请选择登录方式。'
    return false
  }
  if (form.loginSetupType === 'local') {
    if (!form.loginUsername.trim()) fieldErrors['loginSetup.username'] = '请输入登录用户名。'
    if (form.initialPassword.length < 8 || form.initialPassword.length > 128) {
      fieldErrors['loginSetup.initialPassword'] = '初始密码长度必须为 8～128 个字符。'
    }
    if (!form.confirmPassword) fieldErrors.confirmPassword = '请再次输入初始密码。'
    else if (form.initialPassword !== form.confirmPassword) fieldErrors.confirmPassword = '两次输入的密码不一致。'
  }
  if (form.loginSetupType === 'oidc') {
    if (!loginSetupOptions.value?.oidcSetupAvailable || !form.oidcProvider) {
      fieldErrors['loginSetup.provider'] = '当前服务器未配置可用的身份提供方。'
    }
    if (!form.oidcSubject.trim()) fieldErrors['loginSetup.subject'] = '请输入 Subject / sub。'
  }
  return Object.keys(fieldErrors).length === 0
}

function buildLoginSetup(): LoginSetup {
  if (form.loginSetupType === 'local') {
    return { type: 'local', username: form.loginUsername, initialPassword: form.initialPassword }
  }
  if (form.loginSetupType === 'oidc') {
    return { type: 'oidc', provider: form.oidcProvider, subject: form.oidcSubject }
  }
  return { type: 'none' }
}

async function submit(): Promise<void> {
  clearServerErrors()
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid || !validateLoginSetup() || submitting.value) return

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
      ? await createUser({ ...request, loginSetup: buildLoginSetup() })
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
        <span>用户管理</span>
        <h2 id="user-drawer-title">{{ title }}</h2>
        <p>维护人员资料、知识身份与登录身份映射；访问级别由独立安全操作管理。</p>
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
          <el-form-item label="知识身份" prop="knowledgeRoleIds" :error="fieldErrors.knowledgeRoleIds">
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

        <section class="user-drawer__section user-login-setup">
          <div class="user-drawer__section-title"><span>03</span><div><h3>登录方式</h3><p>{{ isEdit ? '查看当前可用的登录方式；具体维护操作按独立安全流程执行。' : '必须明确选择一种初始登录方式。' }}</p></div></div>

          <template v-if="!isEdit">
            <el-form-item prop="loginSetupType" :error="fieldErrors['loginSetup.type']" required>
              <el-radio-group v-model="form.loginSetupType" class="user-login-setup__choices" @change="selectLoginSetup">
                <el-radio value="local">本地账号</el-radio>
                <el-radio value="oidc" :disabled="!loginSetupOptions?.oidcSetupAvailable">企业统一登录（OIDC / SSO）</el-radio>
                <el-radio value="none">暂不配置登录</el-radio>
              </el-radio-group>
            </el-form-item>

            <template v-if="form.loginSetupType === 'local'">
              <el-alert v-if="loginSetupOptions && !loginSetupOptions.localGloballyEnabled" type="warning" title="当前部署未启用本地登录" :closable="false" show-icon />
              <el-form-item label="登录用户名" prop="loginUsername" :error="fieldErrors['loginSetup.username']" required>
                <el-input v-model="form.loginUsername" maxlength="64" autocomplete="username" class="technical-input" placeholder="例如 EMP-001" />
                <span class="user-drawer__help">可从工号带出，但保存后与工号相互独立。</span>
              </el-form-item>
              <div class="user-drawer__row">
                <el-form-item label="初始密码" prop="initialPassword" :error="fieldErrors['loginSetup.initialPassword']" required>
                  <el-input v-model="form.initialPassword" type="password" show-password maxlength="128" autocomplete="new-password" />
                </el-form-item>
                <el-form-item label="确认密码" prop="confirmPassword" :error="fieldErrors.confirmPassword || (passwordMismatch ? '两次输入的密码不一致。' : '')" required>
                  <el-input v-model="form.confirmPassword" type="password" show-password maxlength="128" autocomplete="new-password" />
                  <span v-if="passwordMismatch" class="user-login-setup__field-error">两次输入的密码不一致。</span>
                </el-form-item>
              </div>
              <el-checkbox :model-value="true" disabled>首次登录必须修改密码</el-checkbox>
              <p class="user-drawer__help">密码必须为 8～128 个字符；空格与大小写均按原样保留。</p>
            </template>

            <template v-else-if="form.loginSetupType === 'oidc'">
              <el-alert v-if="loginSetupOptions && !loginSetupOptions.oidcGloballyEnabled" type="warning" title="当前部署未启用企业统一登录" :closable="false" show-icon />
              <el-form-item label="身份提供方" prop="oidcProvider" :error="fieldErrors['loginSetup.provider']" required>
                <el-input v-model="form.oidcProvider" readonly class="technical-input" />
              </el-form-item>
              <el-form-item label="Subject / sub" prop="oidcSubject" :error="fieldErrors['loginSetup.subject']" required>
                <el-input v-model="form.oidcSubject" maxlength="240" class="technical-input" placeholder="由身份提供方提供的稳定标识" />
              </el-form-item>
            </template>

            <el-alert
              v-else-if="form.loginSetupType === 'none'"
              type="warning"
              title="该用户当前无法登录系统。"
              description="仍可作为知识提供者、负责人或历史业务人员使用。"
              :closable="false"
              show-icon
            />

            <p v-if="!loginSetupOptions?.oidcSetupAvailable" class="user-drawer__help">服务器未配置可用的身份提供方，因此不能选择企业统一登录。</p>
          </template>

          <template v-else-if="loginMethods">
            <el-alert
              v-if="!loginMethods.local.exists && loginMethods.oidc.length === 0"
              type="warning"
              title="该用户当前无法登录系统。"
              :closable="false"
              show-icon
            />
            <div v-else class="user-login-methods">
              <article v-if="loginMethods.local.exists">
                <div><strong>本地账号</strong><el-tag size="small" :type="loginMethods.local.isActive ? 'success' : 'info'">{{ loginMethods.local.isActive ? '已启用' : '已停用' }}</el-tag></div>
                <p class="technical-text">{{ loginMethods.local.username }}</p>
                <small v-if="!loginMethods.local.globallyEnabled">当前部署未启用本地登录</small>
                <small v-else-if="loginMethods.local.mustChangePassword">首次登录后必须修改密码</small>
              </article>
              <article v-for="identity in loginMethods.oidc" :key="`${identity.provider}:${identity.subject}`">
                <div><strong>企业统一登录（OIDC / SSO）</strong><el-tag size="small" :type="identity.isActive ? 'success' : 'info'">{{ identity.isActive ? '已启用' : '已停用' }}</el-tag></div>
                <p><span class="technical-text">{{ identity.provider }}</span> · <span class="technical-text">{{ identity.subject }}</span></p>
                <small v-if="!identity.globallyEnabled">当前部署未启用此身份提供方</small>
              </article>
            </div>
            <LoginIdentityManagementPanel :user-id="userId!" />
          </template>
        </section>
      </el-form>

      <footer class="user-drawer__actions">
        <span>{{ isEdit ? '启用 / 停用请使用列表中的独立操作。' : '新用户创建后默认为启用。' }}</span>
        <div><el-button @click="overlayStore.requestDrawerClose">取消</el-button><el-button type="primary" :loading="submitting" :disabled="passwordMismatch" @click="submit">{{ isEdit ? '保存修改' : '创建用户' }}</el-button></div>
      </footer>
    </template>
  </section>
</template>
