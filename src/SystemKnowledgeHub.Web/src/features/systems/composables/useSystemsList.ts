import { onBeforeUnmount, ref } from 'vue'
import type { KnowledgeStatus } from '../../../api/contracts/knowledge'
import { getSystemsList } from '../api/systemsApi'
import type {
  SystemLifecycle,
  SystemsListResponse,
  SystemsSort,
} from '../api/systemsContracts'

export function useSystemsList() {
  const keyword = ref('')
  const lifecycle = ref<SystemLifecycle | ''>('')
  const technology = ref('')
  const knowledgeStatus = ref<KnowledgeStatus | ''>('')
  const sort = ref<SystemsSort>('updatedAt:desc')
  const page = ref(1)
  const pageSize = ref(20)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const data = ref<SystemsListResponse | null>(null)
  let requestController: AbortController | null = null

  async function load(): Promise<void> {
    requestController?.abort()
    requestController = new AbortController()
    loading.value = true
    error.value = null

    try {
      data.value = await getSystemsList(
        {
          search: keyword.value.trim() || undefined,
          lifecycle: lifecycle.value || undefined,
          technology: technology.value.trim() || undefined,
          knowledgeStatus: knowledgeStatus.value || undefined,
          sort: sort.value,
          page: page.value,
          pageSize: pageSize.value,
        },
        requestController.signal,
      )
    } catch (requestError: unknown) {
      if (requestError instanceof DOMException && requestError.name === 'AbortError') return
      error.value = requestError instanceof Error ? requestError.message : '系统列表加载失败。'
    } finally {
      loading.value = false
    }
  }

  function resetPageAndLoad(): void {
    page.value = 1
    void load()
  }

  function clearFilters(): void {
    keyword.value = ''
    lifecycle.value = ''
    technology.value = ''
    knowledgeStatus.value = ''
    sort.value = 'updatedAt:desc'
    page.value = 1
    void load()
  }

  onBeforeUnmount(() => requestController?.abort())

  return {
    keyword,
    lifecycle,
    technology,
    knowledgeStatus,
    sort,
    page,
    pageSize,
    loading,
    error,
    data,
    load,
    resetPageAndLoad,
    clearFilters,
  }
}
