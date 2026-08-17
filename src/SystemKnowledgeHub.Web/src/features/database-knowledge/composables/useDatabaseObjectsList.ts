import { onBeforeUnmount, ref } from 'vue'
import type { KnowledgeStatus } from '../../../api/contracts/knowledge'
import { getDatabaseObjectsList } from '../api/databaseKnowledgeApi'
import type {
  DatabaseObjectType,
  DatabaseObjectsListResponse,
  DatabaseObjectsSort,
} from '../api/databaseKnowledgeContracts'

export function useDatabaseObjectsList() {
  const systemId = ref<number | undefined>()
  const databaseSourceId = ref<number | undefined>()
  const schema = ref('')
  const objectType = ref<DatabaseObjectType | ''>('')
  const knowledgeStatus = ref<KnowledgeStatus | ''>('')
  const keyword = ref('')
  const sort = ref<DatabaseObjectsSort>('objectName:asc')
  const page = ref(1)
  const pageSize = ref(20)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const data = ref<DatabaseObjectsListResponse | null>(null)
  let requestController: AbortController | null = null

  async function load(): Promise<void> {
    requestController?.abort()
    requestController = new AbortController()
    loading.value = true
    error.value = null

    try {
      const response = await getDatabaseObjectsList(
        {
          systemId: systemId.value,
          databaseSourceId: databaseSourceId.value,
          schema: schema.value || undefined,
          objectType: objectType.value || undefined,
          knowledgeStatus: knowledgeStatus.value || undefined,
          search: keyword.value.trim() || undefined,
          sort: sort.value,
          page: page.value,
          pageSize: pageSize.value,
        },
        requestController.signal,
      )
      data.value = response
      if (
        systemId.value === undefined
        && databaseSourceId.value !== undefined
        && response.browseContext.system !== null
      ) {
        systemId.value = response.browseContext.system.id
      }
    } catch (requestError: unknown) {
      if (requestError instanceof DOMException && requestError.name === 'AbortError') return
      error.value = requestError instanceof Error ? requestError.message : '数据库对象列表加载失败。'
    } finally {
      loading.value = false
    }
  }

  function resetPageAndLoad(): void {
    page.value = 1
    void load()
  }

  function clearFilters(): void {
    systemId.value = undefined
    databaseSourceId.value = undefined
    schema.value = ''
    objectType.value = ''
    knowledgeStatus.value = ''
    keyword.value = ''
    sort.value = 'objectName:asc'
    page.value = 1
    void load()
  }

  onBeforeUnmount(() => requestController?.abort())

  return {
    systemId,
    databaseSourceId,
    schema,
    objectType,
    knowledgeStatus,
    keyword,
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
