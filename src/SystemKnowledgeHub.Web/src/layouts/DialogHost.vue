<script setup lang="ts">
import { computed, watch } from 'vue'
import { DocumentAdd } from '@element-plus/icons-vue'
import { useOverlayStore } from '../app/stores/overlays'
import KnowledgeStatusDialogContent from '../features/knowledge-status/components/KnowledgeStatusDialogContent.vue'
import KnowledgeDocumentRestoreDialogContent from '../features/knowledge-documents/components/KnowledgeDocumentRestoreDialogContent.vue'
import DeleteConfirmationDialogContent from '../features/soft-delete/components/DeleteConfirmationDialogContent.vue'
import AttachmentPreviewHost from '../features/knowledge-documents/components/AttachmentPreviewHost.vue'
import { overlayScrollPreserver as scrollPreserver } from './overlayScrollPreservation'

const overlayStore = useOverlayStore()
const hasFeatureDialog = computed(
  () =>
    overlayStore.currentDialog?.kind === 'create-knowledge-object' ||
    overlayStore.currentDialog?.kind === 'database-discovery-connection-profile' ||
    overlayStore.currentDialog?.kind === 'database-discovery-connection-secret' ||
    overlayStore.currentDialog?.kind === 'create-system' ||
    overlayStore.currentDialog?.kind === 'create-business-function' ||
    overlayStore.currentDialog?.kind === 'create-business-rule' ||
    overlayStore.currentDialog?.kind === 'create-integration' ||
    overlayStore.currentDialog?.kind === 'create-database-knowledge' ||
    overlayStore.currentDialog?.kind === 'create-database-source' ||
    overlayStore.currentDialog?.kind === 'register-database-object' ||
    overlayStore.currentDialog?.kind === 'register-database-column' ||
    overlayStore.currentDialog?.kind === 'change-knowledge-status' ||
    overlayStore.currentDialog?.kind === 'global-search' ||
    overlayStore.currentDialog?.kind === 'create-unknown-item' ||
    overlayStore.currentDialog?.kind === 'knowledge-role-management' ||
    overlayStore.currentDialog?.kind === 'restore-knowledge-document-revision' ||
    overlayStore.currentDialog?.kind === 'delete-root' ||
    overlayStore.currentDialog?.kind === 'attachment-preview',
)
const dialogWidth = computed(() =>
  overlayStore.currentDialog?.kind === 'global-search'
    ? '980px'
    : overlayStore.currentDialog?.kind === 'attachment-preview'
      ? '92vw'
      : overlayStore.currentDialog?.kind === 'change-knowledge-status'
        ? '620px'
        : overlayStore.currentDialog?.kind === 'database-discovery-connection-secret'
          ? '460px'
          : overlayStore.currentDialog?.kind === 'restore-knowledge-document-revision'
            ? '680px'
            : overlayStore.currentDialog?.kind === 'delete-root'
              ? '520px'
              : hasFeatureDialog.value
                ? '780px'
                : '460px',
)
const dialogTitle = computed(() =>
  overlayStore.currentDialog?.kind === 'attachment-preview' ? '附件只读预览' : undefined,
)

watch(
  () => overlayStore.isDialogOpen,
  (open) => {
    if (open) scrollPreserver.capture()
  },
  { flush: 'sync' },
)

function handleOpened(): void {
  scrollPreserver.restoreAfterFocus()
  overlayStore.notifyDialogOpened()
}

function handleClosed(): void {
  scrollPreserver.release()
}

function handleAutoFocus(): void {
  scrollPreserver.restoreAfterFocus()
}
</script>

<template>
  <el-dialog
    :model-value="overlayStore.isDialogOpen"
    :width="dialogWidth"
    :title="dialogTitle"
    append-to-body
    destroy-on-close
    :show-close="false"
    :class="[
      'authoring-dialog',
      {
        'global-search-dialog': overlayStore.currentDialog?.kind === 'global-search',
        'knowledge-document-restore-host':
          overlayStore.currentDialog?.kind === 'restore-knowledge-document-revision',
        'attachment-preview-host': overlayStore.currentDialog?.kind === 'attachment-preview',
      },
    ]"
    @opened="handleOpened"
    @closed="handleClosed"
    @open-auto-focus="handleAutoFocus"
    @close-auto-focus="handleAutoFocus"
    @close="overlayStore.closeDialog"
  >
    <div id="dialog-feature-content"></div>
    <KnowledgeStatusDialogContent />
    <KnowledgeDocumentRestoreDialogContent />
    <DeleteConfirmationDialogContent />
    <AttachmentPreviewHost />
    <div v-if="overlayStore.isDialogOpen && !hasFeatureDialog" class="dialog-host__foundation">
      <el-icon :size="24"><DocumentAdd /></el-icon>
      <div>
        <strong>对话框宿主已就绪</strong>
        <p>正式创建或确认内容由后续 Feature 提供。</p>
      </div>
    </div>
    <template v-if="!hasFeatureDialog" #footer>
      <el-button @click="overlayStore.closeDialog">关闭</el-button>
    </template>
  </el-dialog>
</template>
