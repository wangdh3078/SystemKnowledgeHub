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
    const controller = new AbortController()
    request = controller
    const current = () => request === controller && !controller.signal.aborted
    loading.value = true
    view.value = null
    error.value = null
    try {
      const response = await getSystemKnowledgeView(systemId, controller.signal)
      if (current()) view.value = response
    } catch (reason: unknown) {
      if (!current()) return
      if (reason instanceof DOMException && reason.name === 'AbortError') return
      error.value = reason instanceof Error ? reason.message : '统一知识视图加载失败。'
    } finally {
      if (current()) loading.value = false
    }
  }

  onBeforeUnmount(() => request?.abort())
  return { view, loading, error, load }
}
