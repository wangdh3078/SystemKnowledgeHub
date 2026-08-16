<script setup lang="ts">
import { computed } from 'vue'
import { useOverlayStore } from '../app/stores/overlays'
import AppContentArea from './AppContentArea.vue'
import AppSidebar from './AppSidebar.vue'
import AppTopBar from './AppTopBar.vue'
import ContextRailHost from './ContextRailHost.vue'
import DialogHost from './DialogHost.vue'
import DrawerHost from './DrawerHost.vue'
import CreateUnknownItemFlow from '../features/unknown-items/components/CreateUnknownItemFlow.vue'
import CreateBusinessRuleFlow from '../features/business-rules/components/CreateBusinessRuleFlow.vue'
import CreateIntegrationFlow from '../features/integrations/components/CreateIntegrationFlow.vue'

const overlayStore = useOverlayStore()
const shellClass = computed(() => ({
  'app-shell--drawer-open': overlayStore.isDrawerOpen,
}))
</script>

<template>
  <div class="app-shell" :class="shellClass">
    <AppSidebar />
    <div class="app-shell__workspace">
      <AppTopBar />
      <AppContentArea>
        <slot />

        <template #context-rail>
          <ContextRailHost />
        </template>
      </AppContentArea>
    </div>
    <DrawerHost />
    <DialogHost />
    <CreateUnknownItemFlow />
    <CreateBusinessRuleFlow />
    <CreateIntegrationFlow />
  </div>
</template>
