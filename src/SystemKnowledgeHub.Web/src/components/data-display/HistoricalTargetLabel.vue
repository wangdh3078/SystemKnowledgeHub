<script setup lang="ts">
import { computed } from 'vue'
import type { RouteLocationRaw } from 'vue-router'

interface HistoricalIdentity {
  readonly id: number
  readonly targetType: string
  readonly displayName: string
  readonly isDeleted: boolean
  readonly isNavigable: boolean
}

const props = defineProps<{
  identity: HistoricalIdentity
  to?: RouteLocationRaw | null
}>()

const canNavigate = computed(() => !props.identity.isDeleted && props.identity.isNavigable && props.to)
</script>

<template>
  <span class="historical-target-label" :class="{ 'is-deleted': identity.isDeleted }">
    <router-link v-if="canNavigate" :to="to!" class="historical-target-label__link">{{ identity.displayName }}</router-link>
    <span v-else class="historical-target-label__name">{{ identity.displayName }}</span>
    <el-tag v-if="identity.isDeleted" type="info" effect="plain" size="small">已删除</el-tag>
  </span>
</template>

<style scoped>
.historical-target-label { display: inline-flex; align-items: center; gap: var(--space-2); min-width: 0; }
.historical-target-label__link { color: var(--el-color-primary); text-decoration: none; }
.historical-target-label__link:hover { text-decoration: underline; }
.historical-target-label.is-deleted .historical-target-label__name { color: var(--color-text-muted); text-decoration: line-through; }
.historical-target-label.is-deleted .historical-target-label__name:hover { cursor: default; }
</style>
