import { onBeforeUnmount, ref } from 'vue'
import type { ActorContext } from '../../../app/stores/actor'
import { ApiError } from '../../../api/errors/ApiError'
import {
  getBusinessFunctionDetail,
  replaceBusinessProcessSteps,
  updateBusinessFunctionOverview,
} from '../api/businessFunctionsApi'
import type {
  BusinessFunctionDetailResponse,
  BusinessProcessStepInput,
  RewriteStatus,
} from '../api/businessFunctionContracts'

export interface BusinessFunctionOverviewValues {
  readonly name: string
  readonly displayName: string | null
  readonly functionType: string
  readonly purpose: string | null
  readonly caller: string | null
  readonly input: string | null
  readonly output: string | null
  readonly rewriteStatus: RewriteStatus
}

export function useBusinessFunctionDetail(getSelectedId?: () => number | null) {
  const detail = ref<BusinessFunctionDetailResponse | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const overviewSaving = ref(false)
  const overviewSaveError = ref<string | null>(null)
  const overviewConflict = ref(false)
  const processSaving = ref(false)
  const processSaveError = ref<string | null>(null)
  const processConflict = ref(false)
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
    overviewSaving.value = false
    overviewSaveError.value = null
    overviewConflict.value = false
    processSaving.value = false
    processSaveError.value = null
    processConflict.value = false
    loading.value = true
    error.value = null
    const current = () =>
      generation === requestGeneration && isSelected(id) && !controller.signal.aborted
    try {
      const response = await getBusinessFunctionDetail(id, controller.signal)
      if (!current() || response.id !== id) return false
      detail.value = response
      overviewConflict.value = false
      processConflict.value = false
      return true
    } catch (caught: unknown) {
      if (!current() || (caught instanceof DOMException && caught.name === 'AbortError'))
        return false
      error.value = caught instanceof Error ? caught.message : '业务功能详情加载失败。'
      return false
    } finally {
      if (current()) loading.value = false
    }
  }

  async function saveOverview(
    values: BusinessFunctionOverviewValues,
    actor: ActorContext,
  ): Promise<boolean> {
    if (!detail.value || loading.value || !isSelected(detail.value.id)) return false
    const target = detail.value
    const generation = requestGeneration
    overviewSaving.value = true
    overviewSaveError.value = null
    overviewConflict.value = false
    try {
      await updateBusinessFunctionOverview(target.id, {
        ...values,
        actor,
        concurrencyToken: target.concurrencyToken,
      })
      return await (generation === requestGeneration && isSelected(target.id)
        ? load(target.id)
        : Promise.resolve(false))
    } catch (caught: unknown) {
      if (generation !== requestGeneration || !isSelected(target.id)) return false
      if (
        caught instanceof ApiError &&
        caught.status === 409 &&
        caught.response.code === 'conflict'
      ) {
        overviewConflict.value = true
        overviewSaveError.value = caught.message
      } else {
        overviewSaveError.value = caught instanceof Error ? caught.message : '概览保存失败。'
      }
      return false
    } finally {
      if (generation === requestGeneration) overviewSaving.value = false
    }
  }

  async function saveProcess(
    steps: readonly BusinessProcessStepInput[],
    actor: ActorContext,
  ): Promise<boolean> {
    if (!detail.value || loading.value || !isSelected(detail.value.id)) return false
    const target = detail.value
    const generation = requestGeneration
    processSaving.value = true
    processSaveError.value = null
    processConflict.value = false
    try {
      await replaceBusinessProcessSteps(target.id, {
        steps,
        actor,
        concurrencyToken: target.concurrencyToken,
      })
      return await (generation === requestGeneration && isSelected(target.id)
        ? load(target.id)
        : Promise.resolve(false))
    } catch (caught: unknown) {
      if (generation !== requestGeneration || !isSelected(target.id)) return false
      if (
        caught instanceof ApiError &&
        caught.status === 409 &&
        caught.response.code === 'conflict'
      ) {
        processConflict.value = true
        processSaveError.value = caught.message
      } else {
        processSaveError.value = caught instanceof Error ? caught.message : '业务流程保存失败。'
      }
      return false
    } finally {
      if (generation === requestGeneration) processSaving.value = false
    }
  }

  function clearOverviewError(): void {
    overviewSaveError.value = null
    overviewConflict.value = false
  }

  function clearProcessError(): void {
    processSaveError.value = null
    processConflict.value = false
  }

  onBeforeUnmount(() => {
    requestGeneration++
    requestController?.abort()
  })
  return {
    detail,
    loading,
    error,
    overviewSaving,
    overviewSaveError,
    overviewConflict,
    processSaving,
    processSaveError,
    processConflict,
    load,
    saveOverview,
    saveProcess,
    clearOverviewError,
    clearProcessError,
  }
}
