<script setup lang="ts">
import { Plus, Search } from '@element-plus/icons-vue'
import { computed, onBeforeUnmount, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useActorStore } from '../app/stores/actor'
import { useOverlayStore } from '../app/stores/overlays'

const route = useRoute()
const actorStore = useActorStore()
const overlayStore = useOverlayStore()
const createEnabled = computed(() =>
  route.name !== 'foundation' && route.name !== 'not-found',
)

function openCreate(): void {
  if (!createEnabled.value) return
  overlayStore.openDialog({ kind: 'create-knowledge-object', id: null, mode: 'create' })
}

function openSearch(): void {
  overlayStore.openDialog({ kind: 'global-search', id: null, mode: 'read' })
}

function handleGlobalSearchShortcut(event: KeyboardEvent): void {
  if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
    event.preventDefault()
    openSearch()
  }
}

onMounted(() => window.addEventListener('keydown', handleGlobalSearchShortcut))
onBeforeUnmount(() => window.removeEventListener('keydown', handleGlobalSearchShortcut))
</script>

<template>
  <header class="app-topbar">
    <button
      class="app-topbar__search"
      type="button"
      title="搜索所有知识对象"
      @click="openSearch"
    >
      <el-icon :size="17"><Search /></el-icon>
      <span>搜索系统、业务功能、表、字段…</span>
      <kbd>⌘ K</kbd>
    </button>

    <div class="app-topbar__actions">
      <el-button
        type="primary"
        :icon="Plus"
        :disabled="!createEnabled"
        :title="createEnabled ? '新增知识对象' : '当前页面暂未开放新增'"
        @click="openCreate"
      >新增</el-button>
      <span class="app-topbar__separator" aria-hidden="true"></span>
      <button class="app-topbar__profile" type="button" title="本地开发用户">
        <span class="app-topbar__avatar">{{ actorStore.displayName.slice(0, 1) }}</span>
        <span class="app-topbar__profile-copy">
          <strong>{{ actorStore.displayName }}</strong>
          <small>{{ actorStore.role ?? '知识整理人员' }}</small>
        </span>
      </button>
    </div>
  </header>
</template>
