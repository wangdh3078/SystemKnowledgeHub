<script setup lang="ts">
/* eslint-disable vue/no-v-html -- renderMarkdown keeps author-provided raw HTML disabled. */
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { hydrateMermaidBlocks } from './mermaidHydrator'
import {
  codeCardIcons,
  renderMarkdown,
  type MarkdownAttachmentImageContext,
} from './renderMarkdown'

const props = defineProps<{
  markdown: string
  attachmentImageContext?: MarkdownAttachmentImageContext
}>()

const rootElement = ref<HTMLElement | null>(null)
const renderedHtml = computed(() => renderMarkdown(props.markdown, props.attachmentImageContext))
let hydrationVersion = 0
const copyResetTimers = new Map<HTMLButtonElement, ReturnType<typeof setTimeout>>()

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
    return false
  }
  return false
}

function clearCopyResetTimer(button: HTMLButtonElement): void {
  const timer = copyResetTimers.get(button)
  if (timer !== undefined) clearTimeout(timer)
  copyResetTimers.delete(button)
}

function copyFeedbackElement(button: HTMLButtonElement): HTMLElement | null {
  return (
    getCodeCard(button)?.querySelector<HTMLElement>(
      '[data-knowledge-document-code-copy-feedback]',
    ) ?? null
  )
}

function resetCopyState(button: HTMLButtonElement): void {
  button.innerHTML = codeCardIcons.copy
  button.setAttribute('aria-label', '复制代码')
  button.setAttribute('title', '复制代码')
  button.removeAttribute('data-copy-state')
  const feedback = copyFeedbackElement(button)
  if (feedback) feedback.textContent = ''
}

function scheduleCopyReset(button: HTMLButtonElement): void {
  clearCopyResetTimer(button)
  const timer = setTimeout(() => {
    copyResetTimers.delete(button)
    resetCopyState(button)
  }, 2500)
  copyResetTimers.set(button, timer)
}

function setCopySucceeded(button: HTMLButtonElement): void {
  button.innerHTML = codeCardIcons.copied
  button.setAttribute('aria-label', '已复制')
  button.setAttribute('title', '已复制')
  button.setAttribute('data-copy-state', 'success')
  const feedback = copyFeedbackElement(button)
  if (feedback) feedback.textContent = ''
  scheduleCopyReset(button)
}

function setCopyFailed(button: HTMLButtonElement): void {
  clearCopyResetTimer(button)
  resetCopyState(button)
  button.setAttribute('data-copy-state', 'failure')
  const feedback = copyFeedbackElement(button)
  if (feedback) feedback.textContent = '复制失败'
  scheduleCopyReset(button)
}

async function handleCodeCardClick(event: MouseEvent): Promise<void> {
  const target = event.target
  if (!(target instanceof Element)) return

  const copyButton = target.closest<HTMLButtonElement>('[data-knowledge-document-code-copy]')
  if (copyButton) {
    const card = getCodeCard(copyButton)
    const source = card?.querySelector('code')?.textContent ?? ''
    if (await copyRawCode(source)) {
      setCopySucceeded(copyButton)
    } else setCopyFailed(copyButton)
    return
  }

  const collapseButton = target.closest<HTMLButtonElement>(
    '[data-knowledge-document-code-collapse]',
  )
  if (!collapseButton) return
  const card = getCodeCard(collapseButton)
  if (!card) return
  const collapsed = card.classList.toggle('is-collapsed')
  collapseButton.setAttribute('aria-expanded', String(!collapsed))
  collapseButton.setAttribute('aria-label', collapsed ? '展开代码' : '收起代码')
  collapseButton.setAttribute('title', collapsed ? '展开代码' : '收起代码')
  collapseButton.innerHTML = collapsed ? codeCardIcons.expand : codeCardIcons.collapse
}

function handleAttachmentImageError(event: Event): void {
  const image = event.target
  if (
    !(image instanceof HTMLImageElement) ||
    !image.hasAttribute('data-knowledge-document-attachment-image')
  ) {
    return
  }
  const container = image.closest<HTMLElement>(
    '[data-knowledge-document-attachment-image-container]',
  )
  const fallback = container?.querySelector<HTMLElement>(
    '.knowledge-document-attachment-image-unavailable',
  )
  image.hidden = true
  container?.classList.add('is-unavailable')
  if (fallback) fallback.hidden = false
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
    @error.capture="handleAttachmentImageError"
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
