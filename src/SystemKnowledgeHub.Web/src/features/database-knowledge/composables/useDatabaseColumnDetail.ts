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
    activeRequest = new AbortController()
    loading.value = true
    errorMessage.value = null
    detail.value = null

    try {
      detail.value = await getDatabaseColumnDetail(id, activeRequest.signal)
    } catch (error: unknown) {
      if (error instanceof DOMException && error.name === 'AbortError') {
        return
      }
      errorMessage.value = error instanceof Error ? error.message : '字段详情加载失败。'
    } finally {
      loading.value = false
    }
  }

  watch(
    columnId,
    (id) => {
      if (id === null) {
        activeRequest?.abort()
        detail.value = null
        errorMessage.value = null
        return
      }
      void load(id)
    },
    { immediate: true },
  )

  onBeforeUnmount(() => activeRequest?.abort())

  async function reload(): Promise<void> {
    if (columnId.value !== null) {
      await load(columnId.value)
    }
  }

  return { detail, loading, errorMessage, reload }
}
