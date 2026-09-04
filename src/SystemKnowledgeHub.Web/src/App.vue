<script setup lang="ts">
import { RouterView, useRoute } from 'vue-router'
import { applicationLocale } from './app/config/locale'
import AppShell from './layouts/AppShell.vue'
import PortalLayout from './layouts/PortalLayout.vue'
import SecurityGate from './app/security/SecurityGate.vue'
import ForcedPasswordChangeGate from './app/security/ForcedPasswordChangeGate.vue'
import { useActorStore } from './app/stores/actor'

const route = useRoute()
const actorStore = useActorStore()
</script>

<template>
  <el-config-provider :locale="applicationLocale">
    <PortalLayout v-if="route.meta.layout === 'portal'">
      <RouterView />
    </PortalLayout>
    <SecurityGate v-else-if="!actorStore.isAuthenticated" />
    <ForcedPasswordChangeGate v-else-if="actorStore.mustChangePassword" />
    <AppShell v-else-if="route.meta.layout === 'app-shell'">
      <RouterView />
    </AppShell>
    <RouterView v-else />
  </el-config-provider>
</template>
