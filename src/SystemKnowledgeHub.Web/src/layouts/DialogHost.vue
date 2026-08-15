<script setup lang="ts">
import { computed } from 'vue'
import { DocumentAdd } from '@element-plus/icons-vue'
import { useOverlayStore } from '../app/stores/overlays'

const overlayStore = useOverlayStore()
const hasFeatureDialog = computed(() =>
  overlayStore.currentDialog?.kind === 'create-knowledge-object'
  || overlayStore.currentDialog?.kind === 'create-system'
  || overlayStore.currentDialog?.kind === 'create-business-function'
  || overlayStore.currentDialog?.kind === 'create-business-rule'
  || overlayStore.currentDialog?.kind === 'change-knowledge-status'
  || overlayStore.currentDialog?.kind === 'create-unknown-item',
)
const dialogWidth = computed(() =>
  overlayStore.currentDialog?.kind === 'change-knowledge-status'
    ? '620px'
    : hasFeatureDialog.value ? '780px' : '460px',
)
</script>

<template>
  <el-dialog
    :model-value="overlayStore.isDialogOpen"
    :width="dialogWidth"
    append-to-body
    destroy-on-close
    :show-close="false"
    class="authoring-dialog"
    @close="overlayStore.closeDialog"
  >
    <div id="dialog-feature-content"></div>
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
