<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'
import { parseSafeApiId } from '../api/contracts/id'
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
import CreateDatabaseKnowledgeFlow from '../features/database-knowledge/components/CreateDatabaseKnowledgeFlow.vue'
import CreateSystemFlow from '../features/systems/components/CreateSystemFlow.vue'
import CreateKnowledgeObjectChooser from '../features/systems/components/CreateKnowledgeObjectChooser.vue'
import CreateBusinessFunctionFlow from '../features/business-functions/components/CreateBusinessFunctionFlow.vue'
import CreateKnowledgeDocumentDialog from '../features/knowledge-documents/components/CreateKnowledgeDocumentDialog.vue'
import GlobalSearchOverlay from '../features/search/components/GlobalSearchOverlay.vue'

const overlayStore = useOverlayStore()
const route = useRoute()
const globalDocumentCreateOpen = ref(false)
const createSystemContextId = computed(() =>
  route.name === 'system-detail' ? parseSafeApiId(route.params.id) ?? undefined : undefined,
)

function openGlobalDocumentCreate(): void {
  globalDocumentCreateOpen.value = true
}
</script>

<template>
  <div class="app-shell">
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
    <Teleport v-if="overlayStore.currentDialog?.kind === 'create-knowledge-object'" defer to="#dialog-feature-content">
      <CreateKnowledgeObjectChooser
        :enabled-kinds="['system', 'business-function', 'database-knowledge', 'business-rule', 'integration', 'knowledge-document']"
        :system-context="createSystemContextId ? '当前系统' : '在下一步选择'"
        @choose-knowledge-document="openGlobalDocumentCreate"
      />
    </Teleport>
    <CreateKnowledgeDocumentDialog
      :open="globalDocumentCreateOpen"
      @close="globalDocumentCreateOpen = false"
    />
    <GlobalSearchOverlay />
    <CreateUnknownItemFlow />
    <CreateBusinessRuleFlow />
    <CreateIntegrationFlow />
    <CreateDatabaseKnowledgeFlow />
    <CreateSystemFlow v-if="route.name !== 'systems-list'" />
    <CreateBusinessFunctionFlow
      v-if="route.name !== 'business-functions-list'"
      :initial-system-id="createSystemContextId"
    />
  </div>
</template>
