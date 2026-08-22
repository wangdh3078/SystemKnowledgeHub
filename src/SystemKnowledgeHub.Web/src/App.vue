<script setup lang="ts">
import { RouterView, useRoute } from 'vue-router'
import { applicationLocale } from './app/config/locale'
import AppShell from './layouts/AppShell.vue'
import SecurityGate from './app/security/SecurityGate.vue'
import { useActorStore } from './app/stores/actor'

const route = useRoute()
const actorStore = useActorStore()
</script>

<template>
  <el-config-provider :locale="applicationLocale">
    <SecurityGate v-if="!actorStore.isAuthenticated" />
    <AppShell v-else-if="route.meta.layout === 'app-shell'">
      <RouterView />
    </AppShell>
    <RouterView v-else />
  </el-config-provider>
</template>
