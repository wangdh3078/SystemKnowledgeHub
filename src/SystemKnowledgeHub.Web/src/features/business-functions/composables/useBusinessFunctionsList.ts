import { onBeforeUnmount, ref } from 'vue'
import type { KnowledgeStatus } from '../../../api/contracts/knowledge'
import { getBusinessFunctionsList } from '../api/businessFunctionsApi'
import { getSystemsList } from '../../systems/api/systemsApi'
import type { SystemSummary } from '../../systems/api/systemsContracts'
import type {
  BusinessFunctionsListResponse,
  BusinessFunctionsSort,
  RewriteStatus,
} from '../api/businessFunctionContracts'

export function useBusinessFunctionsList(initialSystemId?: number) {
  const keyword = ref('')
  const systemId = ref<number | undefined>(initialSystemId)
  const functionType = ref<string | undefined>()
  const rewriteStatus = ref<RewriteStatus | undefined>()
  const knowledgeStatus = ref<KnowledgeStatus | undefined>()
  const hasUnknownItems = ref<boolean | undefined>()
  const sort = ref<BusinessFunctionsSort>('updatedAt:desc')
  const page = ref(1)
  const pageSize = ref(20)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const data = ref<BusinessFunctionsListResponse | null>(null)
  const systemOptions = ref<readonly SystemSummary[]>([])
  let requestController: AbortController | null = null

  async function load(): Promise<void> {
    requestController?.abort()
    requestController = new AbortController()
    loading.value = data.value === null
    error.value = null

    try {
      data.value = await getBusinessFunctionsList({
        systemId: systemId.value,
        search: keyword.value.trim() || undefined,
        functionType: functionType.value,
        rewriteStatus: rewriteStatus.value,
        knowledgeStatus: knowledgeStatus.value,
        hasUnknownItems: hasUnknownItems.value,
        sort: sort.value,
        page: page.value,
        pageSize: pageSize.value,
      }, requestController.signal)
    } catch (caught: unknown) {
      if (caught instanceof DOMException && caught.name === 'AbortError') return
      error.value = caught instanceof Error ? caught.message : '业务功能列表加载失败。'
    } finally {
      loading.value = false
    }
  }

  async function loadSystemOptions(): Promise<void> {
    try {
      const response = await getSystemsList({ sort: 'name:asc', page: 1, pageSize: 100 })
      systemOptions.value = response.items
    } catch {
      systemOptions.value = []
    }
  }

  function resetPageAndLoad(): void {
    page.value = 1
    void load()
  }

  function clearFilters(): void {
    keyword.value = ''
    systemId.value = undefined
    functionType.value = undefined
    rewriteStatus.value = undefined
    knowledgeStatus.value = undefined
    hasUnknownItems.value = undefined
    resetPageAndLoad()
  }

  onBeforeUnmount(() => requestController?.abort())

  return {
    keyword,
    systemId,
    functionType,
    rewriteStatus,
    knowledgeStatus,
    hasUnknownItems,
    sort,
    page,
    pageSize,
    loading,
    error,
    data,
    systemOptions,
    load,
    loadSystemOptions,
    resetPageAndLoad,
    clearFilters,
  }
}
