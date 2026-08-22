import { onBeforeUnmount, ref } from 'vue'
import { getUsersList } from '../api/usersApi'
import type { UsersListResponse, UsersSort } from '../api/userContracts'

export function useUsersManagement() {
  const keyword = ref('')
  const isActive = ref<boolean | ''>('')
  const sort = ref<UsersSort>('displayName:asc')
  const page = ref(1)
  const pageSize = ref(20)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const data = ref<UsersListResponse | null>(null)
  let requestController: AbortController | null = null

  async function load(): Promise<void> {
    requestController?.abort()
    requestController = new AbortController()
    loading.value = true
    error.value = null
    try {
      data.value = await getUsersList({
        keyword: keyword.value.trim() || undefined,
        isActive: isActive.value === '' ? undefined : isActive.value,
        sort: sort.value,
        page: page.value,
        pageSize: pageSize.value,
      }, requestController.signal)
    } catch (requestError: unknown) {
      if (requestError instanceof DOMException && requestError.name === 'AbortError') return
      error.value = requestError instanceof Error ? requestError.message : '用户列表加载失败。'
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
    isActive.value = ''
    sort.value = 'displayName:asc'
    page.value = 1
    void load()
  }

  onBeforeUnmount(() => requestController?.abort())

  return {
    keyword,
    isActive,
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
