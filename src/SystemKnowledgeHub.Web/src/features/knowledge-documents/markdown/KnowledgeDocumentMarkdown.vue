<script setup lang="ts">
/* eslint-disable vue/no-v-html -- renderMarkdown keeps author-provided raw HTML disabled. */
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { hydrateMermaidBlocks } from './mermaidHydrator'
import { renderMarkdown } from './renderMarkdown'

const props = defineProps<{
  markdown: string
}>()

const rootElement = ref<HTMLElement | null>(null)
const renderedHtml = computed(() => renderMarkdown(props.markdown))
let hydrationVersion = 0

function hydrateRenderedMarkdown(): void {
  hydrationVersion += 1
  const expectedVersion = hydrationVersion

  void nextTick(async () => {
    const root = rootElement.value
    if (!root || expectedVersion !== hydrationVersion) return
    await hydrateMermaidBlocks(root)
  })
}

onMounted(hydrateRenderedMarkdown)
onBeforeUnmount(() => {
  hydrationVersion += 1
})
watch(renderedHtml, hydrateRenderedMarkdown, { flush: 'post' })
</script>

<template>
  <div ref="rootElement" class="knowledge-document-markdown" v-html="renderedHtml"></div>
</template>

<style scoped>
.knowledge-document-markdown :deep(.knowledge-document-mermaid) {
  margin: 16px 0;
  overflow-x: auto;
}

.knowledge-document-markdown :deep(.knowledge-document-mermaid__caption) {
  margin-bottom: 6px;
  color: #64748b;
  font-size: 12px;
}

.knowledge-document-markdown :deep(.knowledge-document-mermaid__output) {
  min-width: 0;
  text-align: center;
}

.knowledge-document-markdown :deep(.knowledge-document-mermaid__output svg) {
  max-width: 100%;
  height: auto;
}

.knowledge-document-markdown :deep(.knowledge-document-mermaid__error) {
  margin: 8px 0 0;
  color: #b42318;
  font-size: 13px;
}

.knowledge-document-markdown :deep(.knowledge-document-task-list-item) {
  list-style: none;
}

.knowledge-document-markdown :deep(.knowledge-document-task-checkbox) {
  margin: 0 6px 0 0;
  accent-color: #2563eb;
  vertical-align: -1px;
}
</style>
