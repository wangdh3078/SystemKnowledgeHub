import { onBeforeUnmount, ref } from 'vue'
import { ApiError } from '../../../api/errors/ApiError'
import { businessRulesApi } from '../api/businessRulesApi'
import type {
  BusinessRuleDetailResponse,
  UpdateBusinessRuleInput,
} from '../api/businessRuleContracts'

export function useBusinessRuleDetail(getSelectedId?: () => number | null) {
  const detail = ref<BusinessRuleDetailResponse | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const saving = ref(false)
  const conflict = ref(false)
  let requestController: AbortController | null = null
  let requestGeneration = 0
  let selectedId: number | null = null
  const isSelected = (id: number) => selectedId === id && (!getSelectedId || getSelectedId() === id)
  async function load(id: number): Promise<boolean> {
    requestController?.abort()
    const controller = new AbortController()
    requestController = controller
    const generation = ++requestGeneration
    selectedId = id
    detail.value = null
    saving.value = false
    conflict.value = false
    loading.value = true
    error.value = null
    const current = () =>
      generation === requestGeneration && isSelected(id) && !controller.signal.aborted
    try {
      const response = await businessRulesApi.detail(id, controller.signal)
      if (!current() || response.id !== id) return false
      detail.value = response
      conflict.value = false
      return true
    } catch (caught: unknown) {
      if (!current() || (caught instanceof DOMException && caught.name === 'AbortError'))
        return false
      error.value = caught instanceof Error ? caught.message : '业务规则详情加载失败。'
      return false
    } finally {
      if (current()) loading.value = false
    }
  }
  async function save(values: Omit<UpdateBusinessRuleInput, 'concurrencyToken'>): Promise<boolean> {
    if (!detail.value || loading.value || !isSelected(detail.value.id)) return false
    const target = detail.value
    const generation = requestGeneration
    saving.value = true
    error.value = null
    conflict.value = false
    try {
      await businessRulesApi.update(target.id, {
        ...values,
        concurrencyToken: target.concurrencyToken,
      })
      return await (generation === requestGeneration && isSelected(target.id)
        ? load(target.id)
        : Promise.resolve(false))
    } catch (caught: unknown) {
      conflict.value = caught instanceof ApiError && caught.status === 409
      error.value = caught instanceof Error ? caught.message : '业务规则保存失败。'
      return false
    } finally {
      if (generation === requestGeneration) saving.value = false
    }
  }
  onBeforeUnmount(() => {
    requestGeneration++
    requestController?.abort()
  })
  return { detail, loading, error, saving, conflict, load, save }
}
