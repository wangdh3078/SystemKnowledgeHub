<script setup lang="ts">
import PortalSectionRenderer from '../../portal-reading/components/PortalSectionRenderer.vue'
import type { PortalPreview } from '../api/portalManagementContracts'

defineProps<{ modelValue: boolean; preview: PortalPreview | null; loading: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    width="min(1160px, 90vw)"
    class="portal-preview-dialog"
    append-to-body
    destroy-on-close
    @close="emit('update:modelValue', false)"
  >
    <template #header>
      <div class="portal-preview-dialog__header">
        <span class="portal-preview-dialog__marker">预览</span
        ><strong>{{ preview?.page?.title ?? '页面预览' }}</strong>
      </div>
    </template>
    <div v-loading="loading" class="portal-preview-dialog__body">
      <section
        v-if="preview && !preview.readiness.canPublish"
        class="portal-preview-blockers"
        aria-label="发布阻塞原因"
      >
        <h3>当前无法完整预览</h3>
        <p v-for="item in preview.readiness.blockers" :key="item.code">{{ item.message }}</p>
      </section>
      <article v-if="preview?.page" class="portal-preview-article">
        <header>
          <p>{{ preview.page.primaryTarget.type }} · {{ preview.page.primaryTarget.title }}</p>
          <h1>{{ preview.page.title }}</h1>
        </header>
        <PortalSectionRenderer
          v-for="section in preview.page.sections"
          :key="section.id"
          :section="section"
          :page-id="preview.page.id"
          preview-mode
        />
      </article>
    </div>
  </el-dialog>
</template>
