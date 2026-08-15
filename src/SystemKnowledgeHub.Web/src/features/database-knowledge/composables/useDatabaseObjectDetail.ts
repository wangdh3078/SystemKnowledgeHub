import { onBeforeUnmount, ref } from 'vue'
import { ApiError } from '../../../api/errors/ApiError'
import { useOverlayStore } from '../../../app/stores/overlays'
import {
  getDatabaseObjectDetail,
} from '../api/databaseKnowledgeApi'
import type { DatabaseObjectDetailResponse } from '../api/databaseKnowledgeContracts'

export function useDatabaseObjectDetail() {
  const overlayStore = useOverlayStore()
  const detail = ref<DatabaseObjectDetailResponse | null>(null)
  const loading = ref(false)
  const errorMessage = ref<string | null>(null)
  const selectedColumnError = ref<string | null>(null)
  const selectedColumnId = ref<number | null>(null)
  let activeRequest: AbortController | null = null

  async function load(databaseObjectId: number, initialSelectedColumnId: number | null): Promise<void> {
    activeRequest?.abort()
    activeRequest = new AbortController()
    loading.value = true
    errorMessage.value = null
    selectedColumnError.value = null

    try {
      detail.value = await getDatabaseObjectDetail(
        databaseObjectId,
        initialSelectedColumnId ?? undefined,
        activeRequest.signal,
      )
      selectedColumnId.value = initialSelectedColumnId
      if (initialSelectedColumnId !== null) {
        overlayStore.openDrawer({ kind: 'database-column', id: initialSelectedColumnId, mode: 'read' })
      }
    } catch (error: unknown) {
      if (error instanceof DOMException && error.name === 'AbortError') {
        return
      }

      if (
        initialSelectedColumnId !== null &&
        error instanceof ApiError &&
        error.response.code === 'reference_invalid'
      ) {
        selectedColumnError.value = '链接中的字段不属于当前数据库对象，已恢复为对象详情。'
        detail.value = await getDatabaseObjectDetail(
          databaseObjectId,
          undefined,
          activeRequest.signal,
        )
        selectedColumnId.value = null
        return
      }

      errorMessage.value = error instanceof Error ? error.message : '数据库对象详情加载失败。'
    } finally {
      loading.value = false
    }
  }

  function selectColumn(columnId: number): void {
    selectedColumnId.value = columnId
    overlayStore.openDrawer({ kind: 'database-column', id: columnId, mode: 'read' })
  }

  function clearColumnSelection(): void {
    selectedColumnId.value = null
  }

  onBeforeUnmount(() => activeRequest?.abort())

  return {
    detail,
    loading,
    errorMessage,
    selectedColumnError,
    selectedColumnId,
    load,
    selectColumn,
    clearColumnSelection,
  }
}
