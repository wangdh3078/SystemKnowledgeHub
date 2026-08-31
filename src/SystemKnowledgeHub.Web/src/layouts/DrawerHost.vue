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
import { confirmDrawerDiscard, markDrawerDirty, resetDrawerDirty } from './drawerDirtyState'

const overlayStore = useOverlayStore()
const hasTeleportedFeature = computed(() =>
  [
    'user-management',
    'attachment-administration',
    'database-discovery-snapshot-object',
    'database-discovery-difference-entry',
  ].includes(overlayStore.currentDrawer?.kind ?? ''),
)
const largeDrawerKinds = new Set([
  'user-management',
  'add-evidence',
  'add-investigation-evidence',
  'human-confirmation',
  'add-relationship',
  'edit-business-rule',
  'edit-integration',
  'edit-database-object',
  'database-discovery-snapshot-object',
  'database-discovery-difference-entry',
])
const drawerSize = computed(() =>
  largeDrawerKinds.has(overlayStore.currentDrawer?.kind ?? '')
    ? 'var(--drawer-width-large)'
    : 'var(--drawer-width-standard)',
)
let triggerElement: HTMLElement | null = null

watch(
  () => overlayStore.currentDrawer,
  async (drawer, previousDrawer) => {
    resetDrawerDirty()
    if (drawer !== null && previousDrawer === null) {
      scrollPreserver.capture()
      triggerElement = document.activeElement instanceof HTMLElement ? document.activeElement : null
    }
    await nextTick()
    const body = document.querySelector<HTMLElement>('.el-drawer__body')
    if (body) body.scrollTop = 0
  },
  { flush: 'sync' },
)

function handleOpened(): void {
  scrollPreserver.restoreAfterFocus()
}

function restoreTriggerFocus(): void {
  const returnFocus = triggerElement
  if (!overlayStore.isDialogOpen && returnFocus?.isConnected) {
    queueMicrotask(() => returnFocus.focus({ preventScroll: true }))
  }
}

function handleClosed(): void {
  scrollPreserver.release()
  restoreTriggerFocus()
  triggerElement = null
  overlayStore.notifyDrawerClosed()
}

function handleAutoFocus(): void {
  scrollPreserver.restoreAfterFocus()
}

function handleCloseAutoFocus(): void {
  restoreTriggerFocus()
  scrollPreserver.restoreAfterFocus()
}

function handleDrawerMutation(): void {
  if (overlayStore.currentDrawer?.mode !== 'read') markDrawerDirty()
}

async function handleBeforeClose(done: () => void): Promise<void> {
  if (await confirmDrawerDiscard()) done()
}
</script>

<template>
  <el-drawer
    class="skh-drawer-host"
    :model-value="overlayStore.isDrawerOpen"
    :size="drawerSize"
    direction="rtl"
    destroy-on-close
    append-to-body
    :with-header="false"
    modal
    modal-class="skh-drawer-overlay"
    :close-on-click-modal="true"
    :close-on-press-escape="true"
    :lock-scroll="false"
    :before-close="handleBeforeClose"
    @opened="handleOpened"
    @closed="handleClosed"
    @open-auto-focus="handleAutoFocus"
    @close-auto-focus="handleCloseAutoFocus"
    @close="overlayStore.closeDrawer"
  >
    <div
      class="skh-drawer-host__content"
      @input.capture="handleDrawerMutation"
      @change.capture="handleDrawerMutation"
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
    </div>
  </el-drawer>
</template>
