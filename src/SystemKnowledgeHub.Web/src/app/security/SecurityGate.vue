<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { Connection } from '@element-plus/icons-vue'
import { useRouter } from 'vue-router'
import { ApiError, NetworkRequestError } from '../../api/errors/ApiError'
import {
  getAuthenticationOptions,
  localLogin,
  startEnterpriseLogin,
  type AuthenticationOptions,
} from './authenticationApi'
import { useActorStore } from '../stores/actor'

const actorStore = useActorStore()
const router = useRouter()
const options = ref<AuthenticationOptions | null>(null)
const optionsLoading = ref(false)
const optionsError = ref<string | null>(null)
const antiforgeryReady = ref(false)
const username = ref('')
const password = ref('')
const submitting = ref(false)
const loginError = ref<string | null>(null)

const loginRequired = computed(
  () => actorStore.authStatus === 'unauthenticated' || actorStore.authStatus === 'session-expired',
)
const localLoginEnabled = computed(() => options.value?.localLoginEnabled === true)
const oidcLoginEnabled = computed(() => options.value?.oidcLoginEnabled === true)
const oidcButtonLabel = computed(() => options.value?.oidcDisplayName || '使用企业账号登录')
const loginFormReady = computed(() => localLoginEnabled.value && antiforgeryReady.value && !optionsLoading.value)
const loginSubtitle = computed(() => oidcLoginEnabled.value && !localLoginEnabled.value
  ? '使用企业身份访问系统知识中心'
  : '登录后访问系统知识中心')

const content = computed(() => {
  switch (actorStore.authStatus) {
    case 'loading': return { title: '正在初始化系统', message: '正在确认当前登录身份…', action: null }
    case 'identity-unmapped': return { title: '账号尚未绑定', message: '你的企业登录身份尚未关联到系统知识中心用户，请联系系统管理员完成账号绑定。', action: null }
    case 'identity-inactive': return { title: '登录身份已停用', message: '请联系系统管理员。', action: null }
    case 'account-inactive': return { title: '当前用户已停用', message: '请联系系统管理员。', action: null }
    case 'error': return { title: '无法加载当前用户', message: actorStore.message ?? '请检查网络后重试。', action: '重试' }
    default: return { title: '需要登录', message: '需要登录后才能访问系统知识中心。', action: '使用企业账号登录' }
  }
})

function enterpriseLogin(): void {
  startEnterpriseLogin()
}

async function loadAuthenticatedUserAndEnterDashboard(): Promise<boolean> {
  const loaded = await actorStore.loadCurrentUser()
  if (loaded && !actorStore.mustChangePassword) {
    await router.replace({ name: 'dashboard' })
  }
  return loaded
}

async function loadAuthenticationOptions(): Promise<void> {
  if (!loginRequired.value || optionsLoading.value) return

  optionsLoading.value = true
  optionsError.value = null
  loginError.value = null
  antiforgeryReady.value = false

  try {
    const loadedOptions = await getAuthenticationOptions()
    options.value = loadedOptions
    if (loadedOptions.localLoginEnabled) {
      antiforgeryReady.value = await actorStore.refreshAntiforgeryToken()
      if (!antiforgeryReady.value) {
        optionsError.value = '无法初始化登录安全令牌，请重试。'
      }
    }
  } catch {
    options.value = null
    optionsError.value = '无法加载登录配置，请重试或联系管理员。'
  } finally {
    optionsLoading.value = false
  }
}

async function submitLocalLogin(): Promise<void> {
  if (!loginFormReady.value || submitting.value) return

  submitting.value = true
  loginError.value = null
  try {
    await localLogin(username.value, password.value)
    password.value = ''
    username.value = ''
    const loaded = await loadAuthenticatedUserAndEnterDashboard()
    if (!loaded) loginError.value = '登录成功，但无法加载当前用户，请重试。'
  } catch (error: unknown) {
    if (error instanceof ApiError) {
      switch (error.response.code) {
        case 'invalid_credentials':
          loginError.value = '用户名或密码错误，或当前账号暂不可用。'
          break
        case 'too_many_requests':
          loginError.value = '登录尝试过于频繁，请稍后再试。'
          break
        case 'antiforgery_failed':
          await actorStore.refreshAntiforgeryToken()
          loginError.value = '登录安全令牌已失效，请重新提交。'
          break
        case 'already_authenticated': {
          const loaded = await loadAuthenticatedUserAndEnterDashboard()
          if (!loaded) loginError.value = '无法恢复当前登录状态，请重试。'
          break
        }
        default:
          loginError.value = '登录失败，请稍后重试。'
      }
    } else if (error instanceof NetworkRequestError) {
      loginError.value = '无法连接服务器，请稍后重试。'
    } else {
      loginError.value = '登录失败，请稍后重试。'
    }
  } finally {
    submitting.value = false
  }
}

function retryCurrentUser(): void {
  void actorStore.loadCurrentUser()
}

function retryOptions(): void {
  void loadAuthenticationOptions()
}

watch(loginRequired, (required) => {
  if (required) {
    void loadAuthenticationOptions()
    return
  }

  options.value = null
  optionsError.value = null
  antiforgeryReady.value = false
}, { immediate: true })

function login(): void {
  startEnterpriseLogin()
}
</script>

<template>
  <main class="security-gate" aria-live="polite">
    <div class="security-gate__layout">
      <section class="security-gate__brand" aria-label="系统知识中心">
        <div class="security-gate__brand-mark" aria-hidden="true"><el-icon :size="26"><Connection /></el-icon></div>
        <span class="security-gate__eyebrow">系统知识中心</span>
        <h1>系统知识中心</h1>
        <p>连接系统、业务、数据、规则与知识内容，将分散的系统知识沉淀为可查询、可关联、可确认的知识资产。</p>
        <div class="security-gate__capabilities" aria-label="产品能力">
          <span>知识沉淀</span><span>关系关联</span><span>可信确认</span>
        </div>
      </section>
      <section class="security-gate__card">
      <template v-if="loginRequired">
        <h1>登录系统</h1>
        <p class="security-gate__subtitle">{{ loginSubtitle }}</p>
        <p v-if="optionsLoading">正在加载登录配置…</p>
        <template v-else-if="optionsError">
          <el-alert :title="optionsError" type="error" :closable="false" show-icon />
          <el-button type="primary" @click="retryOptions">重试</el-button>
        </template>
        <template v-else-if="options">
          <template v-if="!localLoginEnabled && !oidcLoginEnabled">
            <p>当前没有可用的登录方式，请联系系统管理员。</p>
          </template>
          <template v-else>
            <form v-if="localLoginEnabled" class="security-gate__form" @submit.prevent="submitLocalLogin">
              <label for="local-login-username">账号</label>
              <el-input id="local-login-username" v-model="username" autocomplete="username" :disabled="submitting" />
              <label for="local-login-password">密码</label>
              <el-input id="local-login-password" v-model="password" type="password" autocomplete="current-password" show-password :disabled="submitting" />
              <el-alert v-if="loginError" :title="loginError" type="error" :closable="false" show-icon />
              <p v-else-if="!antiforgeryReady">正在初始化登录…</p>
              <el-button native-type="submit" type="primary" :loading="submitting" :disabled="!loginFormReady">
                {{ submitting ? '登录中…' : '登录' }}
              </el-button>
            </form>
            <div v-if="localLoginEnabled && oidcLoginEnabled" class="security-gate__divider" aria-hidden="true"><span>或</span></div>
            <el-button v-if="oidcLoginEnabled" class="security-gate__enterprise-login" type="primary" plain @click="enterpriseLogin">
              {{ oidcButtonLabel }}
            </el-button>
          </template>
        </template>
      </template>
      <template v-else>
        <h1>{{ content.title }}</h1>
        <p>{{ content.message }}</p>
        <el-button v-if="content.action === '使用企业账号登录'" type="primary" @click="login">{{ content.action }}</el-button>
        <el-button v-else-if="content.action === '重试'" type="primary" @click="retryCurrentUser">重试</el-button>
      </template>
      </section>
    </div>
  </main>
</template>
