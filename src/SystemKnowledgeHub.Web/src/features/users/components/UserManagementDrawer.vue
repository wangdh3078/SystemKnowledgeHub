<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'
import { useRouter } from 'vue-router'
import { ApiError } from '../../../api/errors/ApiError'
import { formatDateTime } from '../../../app/formatters/dateTime'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import {
  createUser,
  createUserLocalCredential,
  getKnowledgeRoles,
  getUser,
  getUserLoginMethods,
  getUserLoginSetupOptions,
  resetUserLocalPassword,
  setLocalCredentialActiveState,
  setUserAccessLevel,
  updateUser,
} from '../api/usersApi'
import LoginIdentityManagementPanel from './LoginIdentityManagementPanel.vue'
import type {
  AccessLevel,
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
const router = useRouter()
const formRef = ref<FormInstance>()
const loading = ref(true)
const submitting = ref(false)
const loadError = ref<string | null>(null)
const submitError = ref<string | null>(null)
const conflict = ref(false)
const roles = ref<readonly KnowledgeRole[]>([])
const loginSetupOptions = ref<UserLoginSetupOptions | null>(null)
const loginMethods = ref<UserLoginMethods | null>(null)
const userActive = ref<boolean | null>(null)
const localCredentialFormVisible = ref(false)
const localCredentialSubmitting = ref(false)
const localCredentialError = ref<string | null>(null)
const localCredentialFieldErrors = reactive<Record<string, string>>({})
const localCredentialForm = reactive({ username: '', initialPassword: '', confirmPassword: '' })
const resetPasswordFormVisible = ref(false)
const resetPasswordSubmitting = ref(false)
const resetPasswordError = ref<string | null>(null)
const resetPasswordFieldErrors = reactive<Record<string, string>>({})
const resetPasswordForm = reactive({ newPassword: '', confirmPassword: '' })
const originalRoleIds = ref<ReadonlySet<number>>(new Set())
const concurrencyToken = ref('')
const loadedUser = ref<UserDetail | null>(null)
const originalAccessLevel = ref<AccessLevel>('Viewer')
const accessLevelSubmitting = ref(false)
const accessLevelError = ref<string | null>(null)
const accessLevelConflict = ref(false)
const form = reactive({
  displayName: '',
  employeeNo: '',
  email: '',
  departmentOrTeam: '',
  jobTitle: '',
  accessLevel: 'Viewer' as AccessLevel,
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
const title = computed(() => (isEdit.value ? '编辑用户' : '新增用户'))
const accessLevelChanged = computed(() => form.accessLevel !== originalAccessLevel.value)
const rules: FormRules<typeof form> = {
  displayName: [{ required: true, message: '请输入姓名', trigger: 'blur' }],
  email: [{ type: 'email', message: '请输入有效邮箱地址', trigger: 'blur' }],
}
const passwordMismatch = computed(
  () =>
    !isEdit.value &&
    form.loginSetupType === 'local' &&
    form.confirmPassword.length > 0 &&
    form.initialPassword !== form.confirmPassword,
)
const localCredentialPasswordMismatch = computed(
  () =>
    localCredentialForm.confirmPassword.length > 0 &&
    localCredentialForm.initialPassword !== localCredentialForm.confirmPassword,
)
const resetPasswordMismatch = computed(
  () =>
    resetPasswordForm.confirmPassword.length > 0 &&
    resetPasswordForm.newPassword !== resetPasswordForm.confirmPassword,
)
const localLoginAvailability = computed<{ type: 'success' | 'warning'; title: string } | null>(
  () => {
    const local = loginMethods.value?.local
    if (!local?.exists || userActive.value === null) return null
    if (!userActive.value && local.isActive) {
      return { type: 'warning', title: '本地登录方式已启用，但用户当前已停用，因此无法登录系统。' }
    }
    if (!userActive.value) {
      return { type: 'warning', title: '用户当前已停用，因此无法登录系统。' }
    }
    if (!local.isActive) {
      return {
        type: 'warning',
        title: '用户当前启用，但本地登录方式已停用，因此无法通过本地账号登录。',
      }
    }
    if (!local.globallyEnabled) {
      return { type: 'warning', title: '用户和本地登录方式均已启用，但当前部署未启用本地登录。' }
    }
    if (local.lockedUntil) {
      return { type: 'warning', title: `本地登录临时锁定至 ${formatDateTime(local.lockedUntil)}。` }
    }
    if (local.mustChangePassword) {
      return { type: 'success', title: '当前可使用临时密码登录；登录后必须先修改密码。' }
    }
    return { type: 'success', title: '当前可通过本地账号登录。' }
  },
)

function assignUser(user: UserDetail): void {
  loadedUser.value = user
  form.displayName = user.displayName
  form.employeeNo = user.employeeNo ?? ''
  form.email = user.email ?? ''
  form.departmentOrTeam = user.departmentOrTeam ?? ''
  form.jobTitle = user.jobTitle ?? ''
  form.accessLevel = user.accessLevel
  originalAccessLevel.value = user.accessLevel
  form.knowledgeRoleIds = user.knowledgeRoles.map((role) => role.id)
  originalRoleIds.value = new Set(form.knowledgeRoleIds)
  concurrencyToken.value = user.concurrencyToken
  userActive.value = user.isActive
}

async function load(): Promise<void> {
  loading.value = true
  loadError.value = null
  accessLevelError.value = null
  accessLevelConflict.value = false
  clearServerErrors()
  try {
    const [availableRoles, user, setupOptions, methods] = await Promise.all([
      getKnowledgeRoles(),
      props.userId === null ? Promise.resolve(null) : getUser(props.userId),
      getUserLoginSetupOptions(),
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

async function reloadLoginMethods(): Promise<void> {
  if (props.userId === null) return
  loginMethods.value = await getUserLoginMethods(props.userId)
}

function clearLocalCredentialErrors(): void {
  localCredentialError.value = null
  for (const key of Object.keys(localCredentialFieldErrors)) delete localCredentialFieldErrors[key]
}

function openLocalCredentialForm(): void {
  clearLocalCredentialErrors()
  localCredentialForm.username = form.employeeNo.trim()
  localCredentialForm.initialPassword = ''
  localCredentialForm.confirmPassword = ''
  localCredentialFormVisible.value = true
}

async function createLocalCredential(): Promise<void> {
  clearLocalCredentialErrors()
  if (!localCredentialForm.username.trim())
    localCredentialFieldErrors.username = '请输入登录用户名。'
  if (
    localCredentialForm.initialPassword.length < 8 ||
    localCredentialForm.initialPassword.length > 128
  ) {
    localCredentialFieldErrors.initialPassword = '初始密码长度必须为 8～128 个字符。'
  }
  if (!localCredentialForm.confirmPassword)
    localCredentialFieldErrors.confirmPassword = '请再次输入初始密码。'
  else if (localCredentialPasswordMismatch.value)
    localCredentialFieldErrors.confirmPassword = '两次输入的密码不一致。'
  if (
    Object.keys(localCredentialFieldErrors).length > 0 ||
    localCredentialSubmitting.value ||
    props.userId === null
  )
    return

  localCredentialSubmitting.value = true
  try {
    await createUserLocalCredential(
      props.userId,
      localCredentialForm.username,
      localCredentialForm.initialPassword,
    )
    localCredentialForm.initialPassword = ''
    localCredentialForm.confirmPassword = ''
    localCredentialFormVisible.value = false
    await reloadLoginMethods()
    ElMessage.success('本地账号已添加；首次登录必须修改密码。')
  } catch (error: unknown) {
    if (error instanceof ApiError) {
      localCredentialError.value = error.message
      for (const [field, messages] of Object.entries(error.response.fieldErrors ?? {})) {
        const message = messages[0]
        if (message) localCredentialFieldErrors[field] = message
      }
    } else {
      localCredentialError.value = error instanceof Error ? error.message : '本地账号添加失败。'
    }
  } finally {
    localCredentialSubmitting.value = false
  }
}

async function toggleLocalCredential(): Promise<void> {
  if (
    props.userId === null ||
    !loginMethods.value?.local.exists ||
    loginMethods.value.local.isActive === null
  )
    return
  localCredentialSubmitting.value = true
  localCredentialError.value = null
  const wasActive = loginMethods.value.local.isActive
  try {
    await setLocalCredentialActiveState(props.userId, loginMethods.value.local, !wasActive)
    await reloadLoginMethods()
    ElMessage.success(wasActive ? '本地账号已停用。' : '本地账号已启用。')
  } catch (error: unknown) {
    localCredentialError.value = error instanceof Error ? error.message : '本地账号状态更新失败。'
    if (error instanceof ApiError && error.status === 409) await reloadLoginMethods()
  } finally {
    localCredentialSubmitting.value = false
  }
}

function clearResetPasswordErrors(): void {
  resetPasswordError.value = null
  for (const key of Object.keys(resetPasswordFieldErrors)) delete resetPasswordFieldErrors[key]
}

function openResetPasswordForm(): void {
  clearResetPasswordErrors()
  resetPasswordForm.newPassword = ''
  resetPasswordForm.confirmPassword = ''
  resetPasswordFormVisible.value = true
}

async function resetLocalPassword(): Promise<void> {
  clearResetPasswordErrors()
  if (resetPasswordForm.newPassword.length < 8 || resetPasswordForm.newPassword.length > 128) {
    resetPasswordFieldErrors.newPassword = '新临时密码长度必须为 8～128 个字符。'
  }
  if (!resetPasswordForm.confirmPassword)
    resetPasswordFieldErrors.confirmPassword = '请再次输入新临时密码。'
  else if (resetPasswordMismatch.value)
    resetPasswordFieldErrors.confirmPassword = '两次输入的密码不一致。'
  const local = loginMethods.value?.local
  if (
    Object.keys(resetPasswordFieldErrors).length > 0 ||
    resetPasswordSubmitting.value ||
    props.userId === null ||
    !local?.exists
  )
    return

  resetPasswordSubmitting.value = true
  try {
    await resetUserLocalPassword(props.userId, local, resetPasswordForm.newPassword)
    resetPasswordForm.newPassword = ''
    resetPasswordForm.confirmPassword = ''
    resetPasswordFormVisible.value = false
    await reloadLoginMethods()
    ElMessage.success('本地密码已重置；用户下次使用临时密码登录后必须修改密码。')
  } catch (error: unknown) {
    if (error instanceof ApiError) {
      resetPasswordError.value = error.message
      for (const [field, messages] of Object.entries(error.response.fieldErrors ?? {})) {
        const message = messages[0]
        if (message) resetPasswordFieldErrors[field] = message
      }
      if (error.status === 409) await reloadLoginMethods()
    } else {
      resetPasswordError.value = error instanceof Error ? error.message : '本地密码重置失败。'
    }
  } finally {
    resetPasswordSubmitting.value = false
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
    else if (form.initialPassword !== form.confirmPassword)
      fieldErrors.confirmPassword = '两次输入的密码不一致。'
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

async function saveAccessLevel(): Promise<void> {
  if (props.userId === null || !accessLevelChanged.value || accessLevelSubmitting.value) return

  accessLevelSubmitting.value = true
  accessLevelError.value = null
  accessLevelConflict.value = false
  try {
    const result = await setUserAccessLevel(props.userId, form.accessLevel, concurrencyToken.value)
    const changesCurrentActor = actorStore.currentUser?.id === props.userId
    concurrencyToken.value = result.concurrencyToken
    originalAccessLevel.value = result.accessLevel
    if (loadedUser.value) {
      loadedUser.value = {
        ...loadedUser.value,
        accessLevel: result.accessLevel,
        concurrencyToken: result.concurrencyToken,
      }
      if (!changesCurrentActor) emit('saved', loadedUser.value)
    }
    ElMessage.success('系统权限已更新。')

    if (changesCurrentActor) {
      await actorStore.refreshCurrentUser()
      if (!actorStore.isAdministrator) {
        overlayStore.closeDrawer()
        await router.replace({ name: 'dashboard' })
      }
    }
  } catch (error: unknown) {
    if (error instanceof ApiError) {
      accessLevelConflict.value = error.status === 409 && error.response.code === 'conflict'
      accessLevelError.value =
        error.status === 422 &&
        error.response.code === 'business_rule_violation' &&
        error.response.details?.reason === 'last_usable_administrator'
          ? '系统必须保留至少一个可登录的启用管理员。'
          : error.message
    } else {
      accessLevelError.value = error instanceof Error ? error.message : '系统权限更新失败。'
    }
  } finally {
    accessLevelSubmitting.value = false
  }
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
    const saved =
      props.userId === null
        ? await createUser({
            ...request,
            accessLevel: form.accessLevel,
            loginSetup: buildLoginSetup(),
          })
        : await updateUser(props.userId, { ...request, concurrencyToken: concurrencyToken.value })
    ElMessage.success(props.userId === null ? '用户已创建。' : '用户资料已保存。')
    emit('saved', saved)
    overlayStore.closeDrawer()
  } catch (error: unknown) {
    if (error instanceof ApiError) {
      submitError.value = error.message
      conflict.value =
        error.status === 409 &&
        error.response.code === 'conflict' &&
        error.response.details?.resourceType === 'User'
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
        <p>维护人员资料、系统权限、知识身份与登录方式；系统权限与知识身份相互独立。</p>
      </div>
      <el-tooltip content="关闭用户编辑" placement="bottom"
        ><button
          class="skh-icon-action"
          type="button"
          aria-label="关闭用户编辑"
          @click="overlayStore.requestDrawerClose"
        >
          ×
        </button></el-tooltip
      >
    </header>

    <div v-if="loading" class="user-drawer__state">正在读取用户资料…</div>
    <div v-else-if="loadError" class="user-drawer__state user-drawer__state--error">
      <strong>加载失败</strong>
      <p>{{ loadError }}</p>
      <el-button @click="load">重试</el-button>
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
          <div class="user-conflict-message">
            <span>为避免覆盖其他修改，请重新载入最新资料后再编辑。</span
            ><el-button size="small" @click="load">重新载入</el-button>
          </div>
        </template>
      </el-alert>

      <el-form ref="formRef" :model="form" :rules="rules" label-position="top" @submit.prevent>
        <section class="user-drawer__section">
          <div class="user-drawer__section-title">
            <span>01</span>
            <div>
              <h3>基础资料</h3>
              <p>姓名必填，其余字段可按现有信息渐进补充。</p>
            </div>
          </div>
          <el-form-item label="姓名" prop="displayName" :error="fieldErrors.displayName" required>
            <el-input v-model="form.displayName" maxlength="160" placeholder="例如 王敏" />
          </el-form-item>
          <div class="user-drawer__row">
            <el-form-item label="工号" prop="employeeNo" :error="fieldErrors.employeeNo">
              <el-input
                v-model="form.employeeNo"
                maxlength="80"
                placeholder="例如 EMP-001"
                class="technical-input"
              />
            </el-form-item>
            <el-form-item label="邮箱" prop="email" :error="fieldErrors.email">
              <el-input v-model="form.email" maxlength="240" placeholder="name@example.com" />
            </el-form-item>
          </div>
          <div class="user-drawer__row">
            <el-form-item
              label="部门 / 团队"
              prop="departmentOrTeam"
              :error="fieldErrors.departmentOrTeam"
            >
              <el-input
                v-model="form.departmentOrTeam"
                maxlength="160"
                placeholder="例如 制造系统组"
              />
            </el-form-item>
            <el-form-item label="职位" prop="jobTitle" :error="fieldErrors.jobTitle">
              <el-input
                v-model="form.jobTitle"
                maxlength="160"
                placeholder="例如 Senior Engineer"
              />
            </el-form-item>
          </div>
          <div class="user-access-level">
            <el-form-item
              label="系统权限"
              prop="accessLevel"
              :error="fieldErrors.accessLevel"
              required
            >
              <el-radio-group v-model="form.accessLevel" class="user-access-level__choices">
                <el-radio value="Viewer"
                  ><span><strong>查看者</strong><small>只读查看</small></span></el-radio
                >
                <el-radio value="Editor"
                  ><span
                    ><strong>编辑者</strong><small>可查看、新增和编辑业务内容</small></span
                  ></el-radio
                >
                <el-radio value="Administrator"
                  ><span><strong>管理员</strong><small>系统管理</small></span></el-radio
                >
              </el-radio-group>
              <span class="user-drawer__help"
                >系统权限控制功能访问；知识身份仅描述知识归属，两者不会互相提升。</span
              >
            </el-form-item>
            <template v-if="isEdit">
              <el-alert
                v-if="accessLevelError"
                type="error"
                :title="accessLevelConflict ? '系统权限已被其他操作修改' : accessLevelError"
                :description="
                  accessLevelConflict ? '系统未覆盖较新的修改；请重新载入后再选择。' : undefined
                "
                :closable="false"
                show-icon
              />
              <div class="user-access-level__actions">
                <el-button v-if="accessLevelConflict" @click="load">重新载入</el-button>
                <el-button
                  type="primary"
                  plain
                  :loading="accessLevelSubmitting"
                  :disabled="!accessLevelChanged"
                  @click="saveAccessLevel"
                  >保存系统权限</el-button
                >
              </div>
            </template>
          </div>
        </section>

        <section class="user-drawer__section">
          <div class="user-drawer__section-title">
            <span>02</span>
            <div>
              <h3>知识身份</h3>
              <p>只可新增启用中的身份；已停用的既有映射继续保留并明确标记。</p>
            </div>
          </div>
          <el-form-item
            label="知识身份"
            prop="knowledgeRoleIds"
            :error="fieldErrors.knowledgeRoleIds"
          >
            <el-select
              v-model="form.knowledgeRoleIds"
              multiple
              filterable
              clearable
              placeholder="选择知识身份"
            >
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
          <div class="user-drawer__section-title">
            <span>03</span>
            <div>
              <h3>登录方式</h3>
              <p>
                {{
                  isEdit
                    ? '查看当前可用的登录方式；具体维护操作按独立安全流程执行。'
                    : '必须明确选择一种初始登录方式。'
                }}
              </p>
            </div>
          </div>

          <template v-if="!isEdit">
            <el-form-item prop="loginSetupType" :error="fieldErrors['loginSetup.type']" required>
              <el-radio-group
                v-model="form.loginSetupType"
                class="user-login-setup__choices"
                @change="selectLoginSetup"
              >
                <el-radio value="local">本地账号</el-radio>
                <el-radio value="oidc" :disabled="!loginSetupOptions?.oidcSetupAvailable"
                  >企业统一登录（OIDC / SSO）</el-radio
                >
                <el-radio value="none">暂不配置登录</el-radio>
              </el-radio-group>
            </el-form-item>

            <template v-if="form.loginSetupType === 'local'">
              <el-alert
                v-if="loginSetupOptions && !loginSetupOptions.localGloballyEnabled"
                type="warning"
                title="当前部署未启用本地登录"
                :closable="false"
                show-icon
              />
              <el-form-item
                label="登录用户名"
                prop="loginUsername"
                :error="fieldErrors['loginSetup.username']"
                required
              >
                <el-input
                  v-model="form.loginUsername"
                  maxlength="64"
                  autocomplete="username"
                  class="technical-input"
                  placeholder="例如 EMP-001"
                />
                <span class="user-drawer__help">可从工号带出，但保存后与工号相互独立。</span>
              </el-form-item>
              <div class="user-drawer__row">
                <el-form-item
                  label="初始密码"
                  prop="initialPassword"
                  :error="fieldErrors['loginSetup.initialPassword']"
                  required
                >
                  <el-input
                    v-model="form.initialPassword"
                    type="password"
                    show-password
                    maxlength="128"
                    autocomplete="new-password"
                  />
                </el-form-item>
                <el-form-item
                  label="确认密码"
                  prop="confirmPassword"
                  :error="
                    fieldErrors.confirmPassword ||
                    (passwordMismatch ? '两次输入的密码不一致。' : '')
                  "
                  required
                >
                  <el-input
                    v-model="form.confirmPassword"
                    type="password"
                    show-password
                    maxlength="128"
                    autocomplete="new-password"
                  />
                  <span v-if="passwordMismatch" class="user-login-setup__field-error"
                    >两次输入的密码不一致。</span
                  >
                </el-form-item>
              </div>
              <el-checkbox :model-value="true" disabled>首次登录必须修改密码</el-checkbox>
              <p class="user-drawer__help">密码必须为 8～128 个字符；空格与大小写均按原样保留。</p>
            </template>

            <template v-else-if="form.loginSetupType === 'oidc'">
              <el-alert
                v-if="loginSetupOptions && !loginSetupOptions.oidcGloballyEnabled"
                type="warning"
                title="当前部署未启用企业统一登录"
                :closable="false"
                show-icon
              />
              <el-form-item
                label="身份提供方"
                prop="oidcProvider"
                :error="fieldErrors['loginSetup.provider']"
                required
              >
                <el-input v-model="form.oidcProvider" readonly class="technical-input" />
              </el-form-item>
              <el-form-item
                label="Subject / sub"
                prop="oidcSubject"
                :error="fieldErrors['loginSetup.subject']"
                required
              >
                <el-input
                  v-model="form.oidcSubject"
                  maxlength="240"
                  class="technical-input"
                  placeholder="由身份提供方提供的稳定标识"
                />
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

            <p v-if="!loginSetupOptions?.oidcSetupAvailable" class="user-drawer__help">
              服务器未配置可用的身份提供方，因此不能选择企业统一登录。
            </p>
          </template>

          <template v-else-if="loginMethods">
            <el-alert
              v-if="!loginMethods.local.exists && loginMethods.oidc.length === 0"
              type="warning"
              title="该用户当前无法登录系统。"
              :closable="false"
              show-icon
            />
            <div class="user-login-methods">
              <article>
                <div class="user-login-methods__heading">
                  <div><strong>本地账号</strong><small>用户名与密码登录</small></div>
                  <el-tag
                    v-if="loginMethods.local.exists"
                    size="small"
                    :type="loginMethods.local.isActive ? 'success' : 'info'"
                    >{{ loginMethods.local.isActive ? '本地登录：启用' : '本地登录：停用' }}</el-tag
                  >
                  <el-tag v-else size="small" type="info">未配置</el-tag>
                </div>

                <template v-if="loginMethods.local.exists">
                  <dl class="user-login-methods__details">
                    <div>
                      <dt>用户状态</dt>
                      <dd>{{ userActive ? '用户启用' : '用户停用' }}</dd>
                    </div>
                    <div>
                      <dt>用户名</dt>
                      <dd class="technical-text">{{ loginMethods.local.username }}</dd>
                    </div>
                    <div>
                      <dt>本地登录状态</dt>
                      <dd>{{ loginMethods.local.isActive ? '启用' : '停用' }}</dd>
                    </div>
                    <div>
                      <dt>首次登录需修改密码</dt>
                      <dd>{{ loginMethods.local.mustChangePassword ? '是' : '否' }}</dd>
                    </div>
                    <div>
                      <dt>最近密码变更时间</dt>
                      <dd>{{ formatDateTime(loginMethods.local.lastPasswordChangedAt) }}</dd>
                    </div>
                    <div>
                      <dt>全局本地登录</dt>
                      <dd>{{ loginMethods.local.globallyEnabled ? '已启用' : '未启用' }}</dd>
                    </div>
                    <div v-if="loginMethods.local.lockedUntil">
                      <dt>临时锁定至</dt>
                      <dd>{{ formatDateTime(loginMethods.local.lockedUntil) }}</dd>
                    </div>
                  </dl>
                  <el-alert
                    v-if="localLoginAvailability"
                    :type="localLoginAvailability.type"
                    :title="localLoginAvailability.title"
                    :closable="false"
                    show-icon
                  />
                  <el-alert
                    v-if="localCredentialError"
                    type="error"
                    :title="localCredentialError"
                    :closable="false"
                    show-icon
                  />
                  <div class="user-login-methods__actions">
                    <el-button
                      plain
                      :loading="resetPasswordSubmitting"
                      @click="openResetPasswordForm"
                      >重置密码</el-button
                    >
                    <el-button
                      :type="loginMethods.local.isActive ? 'danger' : 'success'"
                      plain
                      :loading="localCredentialSubmitting"
                      @click="toggleLocalCredential"
                      >{{ loginMethods.local.isActive ? '停用' : '启用' }}</el-button
                    >
                  </div>
                  <div
                    v-if="resetPasswordFormVisible"
                    class="local-credential-create local-credential-reset"
                  >
                    <el-alert
                      type="warning"
                      title="重置后，该用户现有本地登录会话将全部失效，下次使用临时密码登录后必须修改密码。"
                      :closable="false"
                      show-icon
                    />
                    <el-alert
                      v-if="!loginMethods.local.isActive"
                      type="info"
                      title="本地登录当前停用；重置密码不会自动启用该登录方式。"
                      :closable="false"
                      show-icon
                    />
                    <el-alert
                      v-if="resetPasswordError"
                      type="error"
                      :title="resetPasswordError"
                      :closable="false"
                      show-icon
                    />
                    <div class="user-drawer__row">
                      <el-form-item
                        label="新临时密码"
                        :error="resetPasswordFieldErrors.newPassword"
                        required
                      >
                        <el-input
                          v-model="resetPasswordForm.newPassword"
                          type="password"
                          show-password
                          maxlength="128"
                          autocomplete="new-password"
                        />
                      </el-form-item>
                      <el-form-item
                        label="确认临时密码"
                        :error="
                          resetPasswordFieldErrors.confirmPassword ||
                          (resetPasswordMismatch ? '两次输入的密码不一致。' : '')
                        "
                        required
                      >
                        <el-input
                          v-model="resetPasswordForm.confirmPassword"
                          type="password"
                          show-password
                          maxlength="128"
                          autocomplete="new-password"
                        />
                      </el-form-item>
                    </div>
                    <p class="user-drawer__help">
                      确认临时密码只在当前页面校验，不会发送到服务器。
                    </p>
                    <div class="user-login-methods__actions">
                      <el-button @click="resetPasswordFormVisible = false">取消</el-button>
                      <el-button
                        type="primary"
                        :loading="resetPasswordSubmitting"
                        :disabled="resetPasswordMismatch"
                        @click="resetLocalPassword"
                        >确认重置</el-button
                      >
                    </div>
                  </div>
                </template>

                <template v-else>
                  <p class="user-drawer__help">
                    尚未配置本地账号；添加后将要求用户首次登录修改初始密码。
                  </p>
                  <el-button
                    v-if="!localCredentialFormVisible"
                    type="primary"
                    plain
                    @click="openLocalCredentialForm"
                    >添加本地账号</el-button
                  >
                  <div v-else class="local-credential-create">
                    <el-alert
                      v-if="localCredentialError"
                      type="error"
                      :title="localCredentialError"
                      :closable="false"
                      show-icon
                    />
                    <el-alert
                      v-if="!loginMethods.local.globallyEnabled"
                      type="warning"
                      title="当前部署未启用本地登录；可以预先配置账号"
                      :closable="false"
                      show-icon
                    />
                    <el-form-item
                      label="登录用户名"
                      :error="localCredentialFieldErrors.username"
                      required
                    >
                      <el-input
                        v-model="localCredentialForm.username"
                        maxlength="64"
                        autocomplete="username"
                        class="technical-input"
                      />
                    </el-form-item>
                    <div class="user-drawer__row">
                      <el-form-item
                        label="初始密码"
                        :error="localCredentialFieldErrors.initialPassword"
                        required
                      >
                        <el-input
                          v-model="localCredentialForm.initialPassword"
                          type="password"
                          show-password
                          maxlength="128"
                          autocomplete="new-password"
                        />
                      </el-form-item>
                      <el-form-item
                        label="确认密码"
                        :error="
                          localCredentialFieldErrors.confirmPassword ||
                          (localCredentialPasswordMismatch ? '两次输入的密码不一致。' : '')
                        "
                        required
                      >
                        <el-input
                          v-model="localCredentialForm.confirmPassword"
                          type="password"
                          show-password
                          maxlength="128"
                          autocomplete="new-password"
                        />
                      </el-form-item>
                    </div>
                    <el-checkbox :model-value="true" disabled>首次登录必须修改密码</el-checkbox>
                    <p class="user-drawer__help">确认密码只在当前页面校验，不会发送到服务器。</p>
                    <div class="user-login-methods__actions">
                      <el-button @click="localCredentialFormVisible = false">取消</el-button>
                      <el-button
                        type="primary"
                        :loading="localCredentialSubmitting"
                        :disabled="localCredentialPasswordMismatch"
                        @click="createLocalCredential"
                        >确认添加</el-button
                      >
                    </div>
                  </div>
                </template>
              </article>
            </div>
            <LoginIdentityManagementPanel
              :user-id="userId!"
              :setup-available="loginSetupOptions?.oidcSetupAvailable ?? false"
              :approved-provider="loginSetupOptions?.approvedOidcProvider ?? null"
              :globally-enabled="loginSetupOptions?.oidcGloballyEnabled ?? false"
              :user-active="userActive ?? false"
              @changed="reloadLoginMethods"
            />
          </template>
        </section>
      </el-form>

      <footer class="user-drawer__actions">
        <span>{{
          isEdit ? '用户状态与各登录方式状态相互独立。' : '新用户创建后默认为启用。'
        }}</span>
        <div>
          <el-button @click="overlayStore.requestDrawerClose">取消</el-button
          ><el-button
            type="primary"
            :loading="submitting"
            :disabled="passwordMismatch"
            @click="submit"
            >{{ isEdit ? '保存修改' : '创建用户' }}</el-button
          >
        </div>
      </footer>
    </template>
  </section>
</template>
