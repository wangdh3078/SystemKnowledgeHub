<script setup lang="ts">
import { computed, ref } from 'vue'
import { ApiError, NetworkRequestError } from '../../api/errors/ApiError'
import { changeMyLocalPassword } from '../../features/users/api/usersApi'
import { useActorStore } from '../stores/actor'

const emit = defineEmits<{ changed: [] }>()
const actorStore = useActorStore()
const currentPassword = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const submitting = ref(false)
const requestError = ref<string | null>(null)
const serverFieldErrors = ref<Readonly<Record<string, readonly string[]>>>({})
const showConfirmRequired = ref(false)

const currentPasswordError = computed(() => serverFieldErrors.value.currentPassword?.[0] ?? null)
const newPasswordError = computed(() => serverFieldErrors.value.newPassword?.[0] ?? null)
const confirmPasswordError = computed(() => {
  if (confirmPassword.value.length === 0) return showConfirmRequired.value ? '请再次输入新密码。' : null
  return confirmPassword.value === newPassword.value ? null : '两次输入的新密码不一致。'
})

async function submit(): Promise<void> {
  requestError.value = null
  serverFieldErrors.value = {}
  showConfirmRequired.value = false
  if (newPassword.value.length < 8 || newPassword.value.length > 128) {
    serverFieldErrors.value = { newPassword: ['新密码长度必须为 8 到 128 个字符。'] }
    return
  }
  if (confirmPassword.value.length === 0) {
    showConfirmRequired.value = true
    return
  }
  if (newPassword.value !== confirmPassword.value) return
  if (submitting.value) return

  submitting.value = true
  try {
    await changeMyLocalPassword(currentPassword.value, newPassword.value)
    currentPassword.value = ''
    newPassword.value = ''
    confirmPassword.value = ''
    showConfirmRequired.value = false
    emit('changed')
  } catch (error: unknown) {
    if (error instanceof ApiError) {
      serverFieldErrors.value = error.response.fieldErrors ?? {}
      if (error.response.code === 'antiforgery_failed') {
        await actorStore.refreshAntiforgeryToken()
        requestError.value = '请求验证令牌已失效，请重新提交。'
      } else if (error.response.code === 'password_change_not_available') {
        requestError.value = '当前认证方式不支持修改本地密码。'
      } else if (error.response.code === 'session_expired') {
        requestError.value = '登录会话已失效，请重新登录。'
      } else if (Object.keys(serverFieldErrors.value).length === 0) {
        requestError.value = error.message
      }
    } else if (error instanceof NetworkRequestError) {
      requestError.value = '无法连接服务器，请稍后重试。'
    } else {
      requestError.value = '密码修改失败，请稍后重试。'
    }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <form class="local-password-form" @submit.prevent="submit">
    <p class="local-password-form__hint">密码长度为 8–128 个字符；系统不会自动修剪或转换输入。</p>
    <label for="current-local-password">当前密码</label>
    <el-input id="current-local-password" v-model="currentPassword" type="password" autocomplete="current-password" show-password :disabled="submitting" />
    <span v-if="currentPasswordError" class="local-password-form__field-error">{{ currentPasswordError }}</span>

    <label for="new-local-password">新密码</label>
    <el-input id="new-local-password" v-model="newPassword" type="password" autocomplete="new-password" show-password :disabled="submitting" />
    <span v-if="newPasswordError" class="local-password-form__field-error">{{ newPasswordError }}</span>

    <label for="confirm-local-password">确认新密码</label>
    <el-input id="confirm-local-password" v-model="confirmPassword" type="password" autocomplete="new-password" show-password :disabled="submitting" />
    <span v-if="confirmPasswordError" class="local-password-form__field-error">{{ confirmPasswordError }}</span>

    <el-alert v-if="requestError" :title="requestError" type="error" :closable="false" show-icon />
    <el-button native-type="submit" type="primary" :loading="submitting" :disabled="Boolean(confirmPasswordError)">
      {{ submitting ? '正在修改…' : '修改密码' }}
    </el-button>
  </form>
</template>
