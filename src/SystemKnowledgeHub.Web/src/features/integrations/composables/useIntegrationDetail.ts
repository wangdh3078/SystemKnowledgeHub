import { onBeforeUnmount, ref } from 'vue'
import { ApiError } from '../../../api/errors/ApiError'
import { integrationsApi } from '../api/integrationsApi'
import type {
  IntegrationContractField,
  IntegrationDetailResponse,
  IntegrationWriteInput,
} from '../api/integrationContracts'
import type { ActorContext } from '../../../app/stores/actor'
export function useIntegrationDetail(getSelectedId?: () => number | null) {
  const detail = ref<IntegrationDetailResponse | null>(null)
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
      const response = await integrationsApi.detail(id, controller.signal)
      if (!current() || response.id !== id) return false
      detail.value = response
      conflict.value = false
      return true
    } catch (caught: unknown) {
      if (!current() || (caught instanceof DOMException && caught.name === 'AbortError'))
        return false
      error.value = caught instanceof Error ? caught.message : '集成关系加载失败。'
      return false
    } finally {
      if (current()) loading.value = false
    }
  }
  async function saveOverview(input: IntegrationWriteInput): Promise<boolean> {
    if (!detail.value || loading.value || !isSelected(detail.value.id)) return false
    const target = detail.value
    const generation = requestGeneration
    saving.value = true
    try {
      await integrationsApi.updateOverview(target.id, {
        ...input,
        concurrencyToken: target.concurrencyToken,
      })
      return await (generation === requestGeneration && isSelected(target.id)
        ? load(target.id)
        : Promise.resolve(false))
    } catch (caught: unknown) {
      conflict.value = caught instanceof ApiError && caught.status === 409
      error.value = caught instanceof Error ? caught.message : '集成关系保存失败。'
      return false
    } finally {
      if (generation === requestGeneration) saving.value = false
    }
  }
  async function saveFields(
    fields: readonly IntegrationContractField[],
    actor: ActorContext,
  ): Promise<boolean> {
    if (!detail.value || loading.value || !isSelected(detail.value.id)) return false
    const target = detail.value
    const generation = requestGeneration
    saving.value = true
    try {
      await integrationsApi.replaceContractFields(target.id, {
        fields,
        actor,
        concurrencyToken: target.concurrencyToken,
      })
      return await (generation === requestGeneration && isSelected(target.id)
        ? load(target.id)
        : Promise.resolve(false))
    } catch (caught: unknown) {
      error.value = caught instanceof Error ? caught.message : '契约字段保存失败。'
      return false
    } finally {
      if (generation === requestGeneration) saving.value = false
    }
  }
  onBeforeUnmount(() => {
    requestGeneration++
    requestController?.abort()
  })
  return { detail, loading, error, saving, conflict, load, saveOverview, saveFields }
}
