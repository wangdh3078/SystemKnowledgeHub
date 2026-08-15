<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const hasContextRail = computed(() => route.meta.hasContextRail === true)
</script>

<template>
  <div class="app-content-area" :class="{ 'app-content-area--without-rail': !hasContextRail }">
    <main class="app-content-area__main">
      <slot />
    </main>
    <aside v-if="hasContextRail" class="app-content-area__rail" aria-label="关系与缺口">
      <slot name="context-rail" />
    </aside>
  </div>
</template>
