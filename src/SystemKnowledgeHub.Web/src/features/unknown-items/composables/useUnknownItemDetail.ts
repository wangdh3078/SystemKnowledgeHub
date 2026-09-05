import { onBeforeUnmount, ref } from 'vue'
import type { PersonSnapshotInput, UnknownItemDetailResponse } from '../api/unknownItemContracts'
import { unknownItemsApi } from '../api/unknownItemsApi'

export function useUnknownItemDetail(getSelectedId?: () => number | null) {
  const detail = ref<UnknownItemDetailResponse | null>(null)
  const loading = ref(false)
  const saving = ref(false)
  const error = ref<string | null>(null)
  let controller: AbortController | null = null
  let generation = 0
  let selectedId: number | null = null
  const isSelected = (id: number) => selectedId === id && (!getSelectedId || getSelectedId() === id)
  async function load(id: number): Promise<boolean> {
    controller?.abort()
    const request = new AbortController()
    controller = request
    const requestGeneration = ++generation
    selectedId = id
    detail.value = null
    loading.value = true
    saving.value = false
    error.value = null
    const current = () => requestGeneration === generation && isSelected(id) && !request.signal.aborted
    try {
      const response = await unknownItemsApi.detail(id, request.signal)
      if (!current() || response.id !== id) return false
      detail.value = response
      return true
    } catch (cause: unknown) {
      if (!current() || (cause instanceof DOMException && cause.name === 'AbortError')) return false
      error.value = cause instanceof Error ? cause.message : '待确认事项详情加载失败。'
      return false
    } finally {
      if (current()) loading.value = false
    }
  }
  async function run(action: () => Promise<unknown>): Promise<boolean> {
    if (!detail.value || loading.value || !isSelected(detail.value.id)) return false
    const target = detail.value
    const requestGeneration = generation
    saving.value = true
    error.value = null
    try {
      await action()
      if (requestGeneration !== generation || !isSelected(target.id)) return false
      return await load(target.id)
    } catch (cause: unknown) {
      if (requestGeneration !== generation || !isSelected(target.id)) return false
      error.value = cause instanceof Error ? cause.message : '操作失败。'
      return false
    } finally {
      if (requestGeneration === generation) saving.value = false
    }
  }
  const person = (name: string, role: string): PersonSnapshotInput => ({
    displayName: name, roleOrIdentity: role, occurredAt: new Date().toISOString(), team: null,
    externalUserKey: null, source: 'Manual', note: null,
  })
  onBeforeUnmount(() => { generation++; controller?.abort() })
  return { detail, loading, saving, error, load, run, person }
}
