import { onBeforeUnmount, ref } from 'vue'
import type { AttachmentKind } from '../../knowledge-documents/api/attachmentContracts'
import {
  getAdministratorAttachments,
  getAdministratorAttachmentStatistics,
} from '../api/administratorAttachmentsApi'
import type {
  AdministratorAttachmentListResponse,
  AdministratorAttachmentReferenceFilter,
  AdministratorAttachmentStatistics,
} from '../api/administratorAttachmentContracts'

export function useAdministratorAttachments() {
  const query = ref('')
  const kind = ref<AttachmentKind | ''>('')
  const extension = ref('')
  const referenceStatus = ref<AdministratorAttachmentReferenceFilter>('')
  const storageState = ref<'Ready' | 'DeletePending' | ''>('')
  const page = ref(1)
  const pageSize = ref(20)
  const data = ref<AdministratorAttachmentListResponse | null>(null)
  const statistics = ref<AdministratorAttachmentStatistics | null>(null)
  const loading = ref(false)
  const statisticsLoading = ref(false)
  const error = ref<string | null>(null)
  const statisticsError = ref<string | null>(null)
  let listController: AbortController | null = null
  let statisticsController: AbortController | null = null

  async function loadList(): Promise<void> {
    listController?.abort()
    listController = new AbortController()
    loading.value = true
    error.value = null
    try {
      data.value = await getAdministratorAttachments(
        {
          query: query.value.trim() || undefined,
          kind: kind.value || undefined,
          extension: extension.value.trim().toLowerCase() || undefined,
          referenceStatus: referenceStatus.value || undefined,
          storageState: storageState.value || undefined,
          page: page.value,
          pageSize: pageSize.value,
        },
        listController.signal,
      )
    } catch (reason: unknown) {
      if (reason instanceof DOMException && reason.name === 'AbortError') return
      error.value = reason instanceof Error ? reason.message : '附件列表加载失败。'
    } finally {
      loading.value = false
    }
  }

  async function loadStatistics(): Promise<void> {
    statisticsController?.abort()
    statisticsController = new AbortController()
    statisticsLoading.value = true
    statisticsError.value = null
    try {
      statistics.value = await getAdministratorAttachmentStatistics(statisticsController.signal)
    } catch (reason: unknown) {
      if (reason instanceof DOMException && reason.name === 'AbortError') return
      statisticsError.value = reason instanceof Error ? reason.message : '附件存储统计加载失败。'
    } finally {
      statisticsLoading.value = false
    }
  }

  function resetPageAndLoad(): void {
    page.value = 1
    void loadList()
  }

  function clearFilters(): void {
    query.value = ''
    kind.value = ''
    extension.value = ''
    referenceStatus.value = ''
    storageState.value = ''
    page.value = 1
    void loadList()
  }

  async function refresh(): Promise<void> {
    await Promise.all([loadList(), loadStatistics()])
  }

  onBeforeUnmount(() => {
    listController?.abort()
    statisticsController?.abort()
  })

  return {
    query,
    kind,
    extension,
    referenceStatus,
    storageState,
    page,
    pageSize,
    data,
    statistics,
    loading,
    statisticsLoading,
    error,
    statisticsError,
    loadList,
    loadStatistics,
    resetPageAndLoad,
    clearFilters,
    refresh,
  }
}
