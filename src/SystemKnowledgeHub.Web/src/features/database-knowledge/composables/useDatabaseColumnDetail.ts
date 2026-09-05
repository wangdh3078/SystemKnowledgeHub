import { onBeforeUnmount, ref, watch, type Ref } from 'vue'
import {
  getDatabaseColumnDetail,
} from '../api/databaseKnowledgeApi'
import type { DatabaseColumnDetailResponse } from '../api/databaseKnowledgeContracts'

export function useDatabaseColumnDetail(columnId: Ref<number | null>) {
  const detail = ref<DatabaseColumnDetailResponse | null>(null)
  const loading = ref(false)
  const errorMessage = ref<string | null>(null)
  let activeRequest: AbortController | null = null

  async function load(id: number): Promise<void> {
    activeRequest?.abort()
    const controller = new AbortController()
    activeRequest = controller
    const current = () => activeRequest === controller && !controller.signal.aborted && columnId.value === id
    loading.value = true
    detail.value = null
    errorMessage.value = null

    try {
      const response = await getDatabaseColumnDetail(id, controller.signal)
      if (current() && response.id === id) detail.value = response
    } catch (error: unknown) {
      if (!current()) return
      if (error instanceof DOMException && error.name === 'AbortError') {
        return
      }
      errorMessage.value = error instanceof Error ? error.message : '字段详情加载失败。'
    } finally {
      if (current()) loading.value = false
    }
  }

  watch(
    columnId,
    (id) => {
      if (id === null) {
        activeRequest?.abort()
        loading.value = false
        detail.value = null
        errorMessage.value = null
        return
      }
      void load(id)
    },
    { immediate: true, flush: 'sync' },
  )

  onBeforeUnmount(() => activeRequest?.abort())

  async function reload(): Promise<void> {
    if (columnId.value !== null) {
      await load(columnId.value)
    }
  }

  return { detail, loading, errorMessage, reload }
}
