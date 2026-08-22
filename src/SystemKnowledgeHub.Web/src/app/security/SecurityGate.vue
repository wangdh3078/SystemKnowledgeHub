<script setup lang="ts">
import { computed } from 'vue'
import { useActorStore } from '../stores/actor'

const actorStore = useActorStore()

const content = computed(() => {
  switch (actorStore.authStatus) {
    case 'loading': return { title: '正在初始化系统', message: '正在确认当前登录身份…', action: null }
    case 'session-expired': return { title: '登录状态已过期', message: '请重新登录后继续访问系统知识中心。', action: '重新登录' }
    case 'identity-unmapped': return { title: '账号尚未绑定', message: '你的企业登录身份尚未关联到系统知识中心用户，请联系系统管理员完成账号绑定。', action: null }
    case 'identity-inactive': return { title: '登录身份已停用', message: '请联系系统管理员。', action: null }
    case 'account-inactive': return { title: '当前用户已停用', message: '请联系系统管理员。', action: null }
    case 'error': return { title: '无法加载当前用户', message: actorStore.message ?? '请检查网络后重试。', action: '重试' }
    default: return { title: '需要登录', message: '需要登录后才能访问 System Knowledge Hub。', action: '使用企业账号登录' }
  }
})

function login(): void {
  const returnUrl = `${window.location.pathname}${window.location.search}${window.location.hash}`
  window.location.assign(`/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`)
}

function retry(): void {
  void actorStore.loadCurrentUser()
}
</script>

<template>
  <main class="security-gate" aria-live="polite">
    <section class="security-gate__card">
      <span>System Knowledge Hub</span>
      <h1>{{ content.title }}</h1>
      <p>{{ content.message }}</p>
      <el-button v-if="content.action === '使用企业账号登录' || content.action === '重新登录'" type="primary" @click="login">
        {{ content.action }}
      </el-button>
      <el-button v-else-if="content.action === '重试'" type="primary" @click="retry">重试</el-button>
    </section>
  </main>
</template>
