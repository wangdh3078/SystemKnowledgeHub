<script setup lang="ts">
import { computed, nextTick, watch } from 'vue'
import { DocumentChecked } from '@element-plus/icons-vue'
import { useOverlayStore } from '../app/stores/overlays'
import EvidenceDrawerContent from '../features/evidence/components/EvidenceDrawerContent.vue'
import RelationshipDrawerContent from '../features/relationships/components/RelationshipDrawerContent.vue'
import BusinessRuleDrawerContent from '../features/business-rules/components/BusinessRuleDrawerContent.vue'
import ColumnDetailDrawer from '../features/database-knowledge/components/ColumnDetailDrawer.vue'
import DatabaseObjectKnowledgeDrawer from '../features/database-knowledge/components/DatabaseObjectKnowledgeDrawer.vue'
import IntegrationDrawerContent from '../features/integrations/components/IntegrationDrawerContent.vue'
import { overlayScrollPreserver as scrollPreserver } from './overlayScrollPreservation'

const overlayStore = useOverlayStore()
const hasTeleportedFeature = computed(() =>
  ['user-management', 'attachment-administration'].includes(overlayStore.currentDrawer?.kind ?? ''),
)

watch(
  () => overlayStore.currentDrawer,
  async (drawer, previousDrawer) => {
    if (drawer !== null && previousDrawer === null) scrollPreserver.capture()
    await nextTick()
    const body = document.querySelector<HTMLElement>('.el-drawer__body')
    if (body) body.scrollTop = 0
  },
  { flush: 'sync' },
)

function handleOpened(): void {
  scrollPreserver.restoreAfterFocus()
}

function handleClosed(): void {
  scrollPreserver.release()
  overlayStore.notifyDrawerClosed()
}

function handleAutoFocus(): void {
  scrollPreserver.restoreAfterFocus()
}
</script>

<template>
  <el-drawer
    :model-value="overlayStore.isDrawerOpen"
    size="var(--drawer-width)"
    direction="rtl"
    destroy-on-close
    append-to-body
    :with-header="false"
    :modal="false"
    :lock-scroll="false"
    @opened="handleOpened"
    @closed="handleClosed"
    @open-auto-focus="handleAutoFocus"
    @close-auto-focus="handleAutoFocus"
    @close="overlayStore.closeDrawer"
  >
    <div id="drawer-feature-content"></div>
    <EvidenceDrawerContent
      v-if="
        overlayStore.currentDrawer &&
        ['add-evidence', 'add-investigation-evidence', 'evidence', 'human-confirmation'].includes(
          overlayStore.currentDrawer.kind,
        )
      "
      :drawer="overlayStore.currentDrawer"
    />
    <RelationshipDrawerContent
      v-else-if="
        overlayStore.currentDrawer &&
        ['add-relationship', 'relationship'].includes(overlayStore.currentDrawer.kind)
      "
      :drawer="overlayStore.currentDrawer"
    />
    <BusinessRuleDrawerContent
      v-else-if="
        overlayStore.currentDrawer &&
        ['business-rule', 'edit-business-rule'].includes(overlayStore.currentDrawer.kind)
      "
      :drawer="overlayStore.currentDrawer"
    />
    <IntegrationDrawerContent
      v-else-if="
        overlayStore.currentDrawer &&
        ['integration', 'edit-integration'].includes(overlayStore.currentDrawer.kind)
      "
      :drawer="overlayStore.currentDrawer"
    />
    <ColumnDetailDrawer
      v-else-if="overlayStore.currentDrawer?.kind === 'database-column'"
      :column-id="overlayStore.currentDrawer.id"
    />
    <DatabaseObjectKnowledgeDrawer
      v-else-if="overlayStore.currentDrawer?.kind === 'edit-database-object'"
      :database-object-id="overlayStore.currentDrawer.id"
    />
    <div v-else-if="!hasTeleportedFeature" class="drawer-host__foundation">
      <el-icon :size="24"><DocumentChecked /></el-icon>
      <strong>详情抽屉宿主已就绪</strong>
      <p>当前仅验证单实例 Drawer 的打开、替换与关闭。正式对象内容由后续 Feature 提供。</p>
      <dl v-if="overlayStore.currentDrawer">
        <div>
          <dt>类型</dt>
          <dd>{{ overlayStore.currentDrawer.kind }}</dd>
        </div>
        <div>
          <dt>模式</dt>
          <dd>{{ overlayStore.currentDrawer.mode }}</dd>
        </div>
      </dl>
    </div>
  </el-drawer>
</template>
