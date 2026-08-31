<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useActorStore } from '../../../app/stores/actor'

const actorStore = useActorStore()
const route = useRoute()
const activeSection = computed(() => {
  const path = route.path
  if (path.startsWith('/database-discovery/snapshots')) return 'snapshots'
  if (path.startsWith('/database-discovery/differences')) return 'differences'
  if (path.startsWith('/database-discovery/runs')) return 'runs'
  if (
    path === '/database-discovery' ||
    path.startsWith('/database-discovery/connections') ||
    path.startsWith('/admin/database-discovery/connections')
  )
    return 'connections'
  return null
})
</script>
<template>
  <nav class="discovery-tabs" aria-label="数据库发现">
    <RouterLink
      v-if="actorStore.isAdministrator"
      :to="{ name: 'database-discovery-connections' }"
      :class="{ 'is-active': activeSection === 'connections' }"
      :aria-current="activeSection === 'connections' ? 'page' : undefined"
      >连接配置</RouterLink
    >
    <RouterLink
      :to="{ name: 'database-discovery-runs' }"
      :class="{ 'is-active': activeSection === 'runs' }"
      :aria-current="activeSection === 'runs' ? 'page' : undefined"
      >发现运行</RouterLink
    >
    <RouterLink
      :to="{ name: 'database-discovery-snapshots' }"
      :class="{ 'is-active': activeSection === 'snapshots' }"
      :aria-current="activeSection === 'snapshots' ? 'page' : undefined"
      >发现快照</RouterLink
    >
    <RouterLink
      :to="{ name: 'database-discovery-differences' }"
      :class="{ 'is-active': activeSection === 'differences' }"
      :aria-current="activeSection === 'differences' ? 'page' : undefined"
      >差异审查</RouterLink
    >
  </nav>
</template>
