<script setup lang="ts">
import { Connection, SwitchButton } from '@element-plus/icons-vue'
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import { useRouter } from 'vue-router'
import { logout } from './authenticationApi'
import LocalPasswordChangeForm from './LocalPasswordChangeForm.vue'
import { useActorStore } from '../stores/actor'

const actorStore = useActorStore()
const router = useRouter()
const loggingOut = ref(false)

function passwordChanged(): void {
  actorStore.clearCurrentUser('unauthenticated')
  void router.replace({ name: 'dashboard' })
  ElMessage.success('密码已修改，请使用新密码重新登录。')
}

async function signOut(): Promise<void> {
  if (loggingOut.value) return
  loggingOut.value = true
  try {
    await logout()
  } finally {
    actorStore.clearCurrentUser('unauthenticated')
    await router.replace({ name: 'dashboard' })
    loggingOut.value = false
  }
}
</script>

<template>
  <main class="security-gate forced-password-gate" aria-live="polite">
    <div class="security-gate__layout">
      <section class="security-gate__brand" aria-label="系统知识中心">
        <div class="security-gate__brand-mark" aria-hidden="true"><el-icon :size="26"><Connection /></el-icon></div>
        <span class="security-gate__eyebrow">账号安全</span>
        <h1>首次进入前，请设置自己的密码</h1>
        <p>当前账号使用临时密码。完成修改后，所有旧的本地登录会话都会失效，你需要使用新密码重新登录。</p>
      </section>
      <section class="security-gate__card forced-password-gate__card">
        <h1>必须修改密码</h1>
        <p class="security-gate__subtitle">在完成此步骤前，业务和管理功能不会开放。</p>
        <LocalPasswordChangeForm @changed="passwordChanged" />
        <el-button class="forced-password-gate__logout" :icon="SwitchButton" :loading="loggingOut" @click="signOut">退出登录</el-button>
      </section>
    </div>
  </main>
</template>
