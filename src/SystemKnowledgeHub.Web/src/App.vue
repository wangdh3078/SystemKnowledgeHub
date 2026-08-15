<script setup lang="ts">
import { RouterView, useRoute } from 'vue-router'
import { applicationLocale } from './app/config/locale'
import AppShell from './layouts/AppShell.vue'

const route = useRoute()
</script>

<template>
  <el-config-provider :locale="applicationLocale">
    <AppShell v-if="route.meta.layout === 'app-shell'">
      <RouterView />
    </AppShell>
    <RouterView v-else />
  </el-config-provider>
</template>
