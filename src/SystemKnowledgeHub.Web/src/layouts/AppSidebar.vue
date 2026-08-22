<script setup lang="ts">
import { Connection } from '@element-plus/icons-vue'
import { useRoute, useRouter } from 'vue-router'
import { navigationItems } from '../app/router/navigation'
import { useActorStore } from '../app/stores/actor'

const route = useRoute()
const router = useRouter()
const actorStore = useActorStore()

function navigateToDashboard(): void {
  void router.push({ name: 'dashboard' })
}

function navigate(item: (typeof navigationItems)[number]): void {
  if (!item.enabled || !item.routeName) return
  void router.push({ name: item.routeName })
}

function isVisible(item: (typeof navigationItems)[number]): boolean {
  return item.minimumAccessLevel !== 'Administrator' || actorStore.isAdministrator
}
</script>

<template>
  <aside class="app-sidebar" aria-label="主导航">
    <button
      class="app-sidebar__brand"
      type="button"
      aria-label="返回系统知识中心首页"
      @click="navigateToDashboard"
    >
      <span class="app-sidebar__brand-icon" aria-hidden="true">
        <el-icon :size="19"><Connection /></el-icon>
      </span>
      <span class="app-sidebar__brand-copy">
        <strong>系统知识中心</strong>
        <small>System Knowledge Hub</small>
      </span>
    </button>

    <nav class="app-sidebar__navigation">
      <template
        v-for="item in navigationItems.filter(isVisible)"
        :key="item.key"
      >
        <span v-if="item.groupLabel" class="app-sidebar__group-label">{{ item.groupLabel }}</span>
        <button
          class="app-sidebar__item"
          :class="{
            'app-sidebar__item--active': route.meta.navigationKey === item.key,
          }"
          type="button"
          :disabled="!item.enabled"
          :aria-current="route.meta.navigationKey === item.key ? 'page' : undefined"
          :title="item.enabled ? item.label : `${item.label}将在业务切片中实现`"
          @click="navigate(item)"
        >
          <el-icon :size="17"><component :is="item.icon" /></el-icon>
          <span>{{ item.label }}</span>
        </button>
      </template>
    </nav>

    <div class="app-sidebar__footer">
      <span class="app-sidebar__status-dot" aria-hidden="true"></span>
      <span>MVP · 渐进知识整理</span>
    </div>
  </aside>
</template>
