import { reactive, ref } from 'vue'
import type { UnknownItemStatus, UnknownItemPriority, UnknownItemsListResponse } from '../api/unknownItemContracts'
import { unknownItemsApi } from '../api/unknownItemsApi'

export function useUnknownItemsList() {
  const data = ref<UnknownItemsListResponse | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const filters = reactive({ keyword: '', priority: '' as UnknownItemPriority | '', status: '' as UnknownItemStatus | '', page: 1, pageSize: 20 })
  async function load(): Promise<void> {
    loading.value = true; error.value = null
    try {
      data.value = await unknownItemsApi.list({
        keyword: filters.keyword || undefined,
        priority: filters.priority || undefined,
        status: filters.status || undefined,
        page: filters.page,
        pageSize: filters.pageSize,
        sort: 'updatedAt:desc',
      })
    } catch (cause: unknown) { error.value = cause instanceof Error ? cause.message : '待确认事项加载失败。' }
    finally { loading.value = false }
  }
  return { data, loading, error, filters, load }
}
