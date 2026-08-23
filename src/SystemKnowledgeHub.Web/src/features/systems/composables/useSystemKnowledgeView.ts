import { onBeforeUnmount, ref } from 'vue'
import { getSystemKnowledgeView } from '../api/systemsApi'
import type { SystemKnowledgeView } from '../api/systemKnowledgeViewContracts'

export function useSystemKnowledgeView() {
  const view = ref<SystemKnowledgeView | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  let request: AbortController | null = null

  async function load(systemId: number): Promise<void> {
    request?.abort()
    request = new AbortController()
    loading.value = true
    error.value = null
    try {
      view.value = await getSystemKnowledgeView(systemId, request.signal)
    } catch (reason: unknown) {
      if (reason instanceof DOMException && reason.name === 'AbortError') return
      error.value = reason instanceof Error ? reason.message : '统一知识视图加载失败。'
    } finally {
      loading.value = false
    }
  }

  onBeforeUnmount(() => request?.abort())
  return { view, loading, error, load }
}
