import { onBeforeUnmount, ref } from 'vue'
import { getDashboard } from '../api/dashboardApi'
import type { DashboardResponse } from '../api/dashboardContracts'

export function useDashboard() {
  const data = ref<DashboardResponse | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  let activeRequest: AbortController | null = null

  async function load(systemId?: number): Promise<void> {
    activeRequest?.abort()
    activeRequest = new AbortController()
    loading.value = true
    error.value = null

    try {
      data.value = await getDashboard(systemId, activeRequest.signal)
    } catch (requestError: unknown) {
      if (requestError instanceof DOMException && requestError.name === 'AbortError') return
      error.value = requestError instanceof Error ? requestError.message : '总览加载失败。'
    } finally {
      loading.value = false
    }
  }

  onBeforeUnmount(() => activeRequest?.abort())

  return { data, loading, error, load }
}
