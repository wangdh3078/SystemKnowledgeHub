import { ref } from 'vue'
type ApiId = number
import type { PersonSnapshotInput, UnknownItemDetailResponse } from '../api/unknownItemContracts'
import { unknownItemsApi } from '../api/unknownItemsApi'

export function useUnknownItemDetail() {
  const detail = ref<UnknownItemDetailResponse | null>(null)
  const loading = ref(false)
  const saving = ref(false)
  const error = ref<string | null>(null)
  async function load(id: ApiId): Promise<void> {
    loading.value = true; error.value = null
    try { detail.value = await unknownItemsApi.detail(id) }
    catch (cause: unknown) { error.value = cause instanceof Error ? cause.message : '待确认事项详情加载失败。' }
    finally { loading.value = false }
  }
  async function run(action: () => Promise<unknown>): Promise<boolean> {
    if (!detail.value) return false
    saving.value = true; error.value = null
    try { await action(); await load(detail.value.id); return true }
    catch (cause: unknown) { error.value = cause instanceof Error ? cause.message : '操作失败。'; return false }
    finally { saving.value = false }
  }
  const person = (name: string, role: string): PersonSnapshotInput => ({
    displayName: name, roleOrIdentity: role, occurredAt: new Date().toISOString(), team: null,
    externalUserKey: null, source: 'Manual', note: null,
  })
  return { detail, loading, saving, error, load, run, person }
}
