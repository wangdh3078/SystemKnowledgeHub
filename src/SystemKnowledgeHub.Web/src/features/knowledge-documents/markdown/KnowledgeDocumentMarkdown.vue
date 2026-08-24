<script setup lang="ts">
/* eslint-disable vue/no-v-html -- renderMarkdown keeps author-provided raw HTML disabled. */
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { hydrateMermaidBlocks } from './mermaidHydrator'
import { codeCardIcons, renderMarkdown } from './renderMarkdown'

const props = defineProps<{
  markdown: string
}>()

const rootElement = ref<HTMLElement | null>(null)
const renderedHtml = computed(() => renderMarkdown(props.markdown))
let hydrationVersion = 0
const copyResetTimers = new Set<ReturnType<typeof setTimeout>>()

function getCodeCard(target: EventTarget | null): HTMLElement | null {
  return target instanceof Element
    ? target.closest<HTMLElement>('[data-knowledge-document-code-card]')
    : null
}

async function copyRawCode(source: string): Promise<boolean> {
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(source)
      return true
    }
  } catch {
    // Fall through to the legacy browser copy command when clipboard permission is unavailable.
  }

  const textarea = document.createElement('textarea')
  textarea.value = source
  textarea.setAttribute('readonly', '')
  textarea.style.position = 'fixed'
  textarea.style.opacity = '0'
  document.body.append(textarea)
  textarea.select()
  const copied = document.execCommand('copy')
  textarea.remove()
  return copied
}

function resetCopyLabel(button: HTMLButtonElement): void {
  button.innerHTML = codeCardIcons.copy
  button.setAttribute('aria-label', '复制代码')
  button.setAttribute('title', '复制代码')
}

async function handleCodeCardClick(event: MouseEvent): Promise<void> {
  const target = event.target
  if (!(target instanceof Element)) return

  const copyButton = target.closest<HTMLButtonElement>('[data-knowledge-document-code-copy]')
  if (copyButton) {
    const card = getCodeCard(copyButton)
    const source = card?.querySelector('code')?.textContent ?? ''
    if (await copyRawCode(source)) {
      copyButton.innerHTML = codeCardIcons.copy
      copyButton.setAttribute('aria-label', '代码已复制')
      copyButton.setAttribute('title', '已复制')
      const timer = setTimeout(() => {
        copyResetTimers.delete(timer)
        resetCopyLabel(copyButton)
      }, 1600)
      copyResetTimers.add(timer)
    }
    return
  }

  const collapseButton = target.closest<HTMLButtonElement>('[data-knowledge-document-code-collapse]')
  if (!collapseButton) return
  const card = getCodeCard(collapseButton)
  if (!card) return
  const collapsed = card.classList.toggle('is-collapsed')
  collapseButton.setAttribute('aria-expanded', String(!collapsed))
  collapseButton.setAttribute('aria-label', collapsed ? '展开代码' : '收起代码')
  collapseButton.setAttribute('title', collapsed ? '展开代码' : '收起代码')
  collapseButton.innerHTML = collapsed ? codeCardIcons.expand : codeCardIcons.collapse
}

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
  copyResetTimers.forEach((timer) => clearTimeout(timer))
  copyResetTimers.clear()
})
watch(renderedHtml, hydrateRenderedMarkdown, { flush: 'post' })
</script>

<template>
  <div
    ref="rootElement"
    class="knowledge-document-markdown knowledge-markdown-content"
    @click="handleCodeCardClick"
    v-html="renderedHtml"
  ></div>
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
