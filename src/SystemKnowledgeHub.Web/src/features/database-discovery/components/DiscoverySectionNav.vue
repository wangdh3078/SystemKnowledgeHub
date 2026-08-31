<script setup lang="ts">
import { useActorStore } from '../../../app/stores/actor'
const actorStore = useActorStore()
</script>
<template>
  <nav class="discovery-tabs" aria-label="数据库发现">
    <RouterLink v-if="actorStore.isAdministrator" :to="{ name: 'database-discovery-connections' }"
      >连接配置</RouterLink
    >
    <RouterLink :to="{ name: 'database-discovery-runs' }">发现运行</RouterLink>
    <span>发现快照</span><span>差异审查</span>
  </nav>
</template>
