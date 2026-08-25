<script setup lang="ts">
import { Plus, Search, SwitchButton } from '@element-plus/icons-vue'
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessageBox } from 'element-plus'
import { useRoute } from 'vue-router'
import { logout } from '../app/security/authenticationApi'
import { useActorStore } from '../app/stores/actor'
import { useOverlayStore } from '../app/stores/overlays'
import { confirmDocumentEditDiscard, hasActiveDirtyDocumentEdit } from '../features/knowledge-documents/editor/documentEditState'

const route = useRoute()
const actorStore = useActorStore()
const overlayStore = useOverlayStore()
const profileOpen = ref(false)
const loggingOut = ref(false)
const createEnabled = computed(() =>
  actorStore.canEdit && route.name !== 'foundation' && route.name !== 'not-found',
)
const currentUserSubtitle = computed(() =>
  actorStore.currentUser?.departmentOrTeam
  ?? actorStore.currentUser?.jobTitle
  ?? actorStore.accessLevel
  ?? '当前用户',
)

function openCreate(): void {
  if (!createEnabled.value) return
  overlayStore.openDialog({ kind: 'create-knowledge-object', id: null, mode: 'create' })
}

function openSearch(): void {
  overlayStore.openDialog({ kind: 'global-search', id: null, mode: 'read' })
}

function handleGlobalSearchShortcut(event: KeyboardEvent): void {
  if (event.ctrlKey && event.key.toLowerCase() === 'k') {
    event.preventDefault()
    openSearch()
  }
  if (event.key === 'Escape') profileOpen.value = false
}

async function signOut(): Promise<void> {
  if (loggingOut.value) return
  try {
    await ElMessageBox.confirm('退出后需要重新登录才能继续访问系统。', '退出登录？', {
      confirmButtonText: '退出登录',
      cancelButtonText: '取消',
      type: 'warning',
      confirmButtonClass: 'el-button--danger',
    })
  } catch {
    return
  }
  if (hasActiveDirtyDocumentEdit.value && !(await confirmDocumentEditDiscard())) return
  loggingOut.value = true
  try {
    await logout()
    actorStore.clearCurrentUser('unauthenticated')
  } finally {
    loggingOut.value = false
  }
}

onMounted(() => window.addEventListener('keydown', handleGlobalSearchShortcut))
onBeforeUnmount(() => window.removeEventListener('keydown', handleGlobalSearchShortcut))
</script>

<template>
  <header class="app-topbar">
    <button class="app-topbar__search" type="button" title="搜索所有知识对象" @click="openSearch">
      <el-icon :size="17"><Search /></el-icon>
      <span>搜索系统、业务功能、表、字段…</span>
      <kbd>Ctrl + K</kbd>
    </button>

    <div class="app-topbar__actions">
      <el-button v-if="actorStore.canEdit" type="primary" :icon="Plus" :disabled="!createEnabled" @click="openCreate">新增</el-button>
      <span v-if="actorStore.canEdit" class="app-topbar__separator" aria-hidden="true"></span>
      <button class="app-topbar__profile" type="button" :aria-expanded="profileOpen" title="查看当前用户资料" @click="profileOpen = !profileOpen">
        <span class="app-topbar__avatar">{{ actorStore.currentUser?.displayName.slice(0, 1) ?? '?' }}</span>
        <span class="app-topbar__profile-copy"><strong>{{ actorStore.currentUser?.displayName }}</strong><small>{{ currentUserSubtitle }} · {{ actorStore.accessLevel }}</small></span>
      </button>

      <section v-if="profileOpen && actorStore.currentUser" class="app-topbar__current-user-panel" aria-label="当前用户资料">
        <div class="app-topbar__current-user-heading"><div><strong>当前用户</strong><p>身份由服务器认证并映射，不能在浏览器中切换。</p></div><el-tooltip content="关闭当前用户资料" placement="bottom"><button class="skh-icon-action" type="button" aria-label="关闭当前用户资料" @click="profileOpen = false">×</button></el-tooltip></div>
        <div class="app-topbar__current-user-summary"><span class="app-topbar__avatar">{{ actorStore.currentUser.displayName.slice(0, 1) }}</span><div><strong>{{ actorStore.currentUser.displayName }}</strong><span>{{ actorStore.accessLevel }}</span></div></div>
        <dl class="app-topbar__profile-details"><div><dt>工号</dt><dd>{{ actorStore.currentUser.employeeNo ?? '—' }}</dd></div><div><dt>邮箱</dt><dd>{{ actorStore.currentUser.email ?? '—' }}</dd></div><div><dt>部门 / 团队</dt><dd>{{ actorStore.currentUser.departmentOrTeam ?? '—' }}</dd></div><div><dt>职位</dt><dd>{{ actorStore.currentUser.jobTitle ?? '—' }}</dd></div><div><dt>知识身份</dt><dd>{{ actorStore.currentUser.knowledgeRoles.map((role) => role.name).join('、') || '未配置' }}</dd></div></dl>
        <el-button class="app-topbar__logout" :icon="SwitchButton" :loading="loggingOut" @click="signOut">退出登录</el-button>
      </section>
    </div>
  </header>
</template>
