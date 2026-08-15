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

export function useBusinessFunctionDetail() {
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

  async function load(id: number): Promise<boolean> {
    requestController?.abort()
    requestController = new AbortController()
    loading.value = detail.value === null
    error.value = null
    try {
      detail.value = await getBusinessFunctionDetail(id, requestController.signal)
      overviewConflict.value = false
      processConflict.value = false
      return true
    } catch (caught: unknown) {
      if (caught instanceof DOMException && caught.name === 'AbortError') return false
      error.value = caught instanceof Error ? caught.message : '业务功能详情加载失败。'
      return false
    } finally {
      loading.value = false
    }
  }

  async function saveOverview(
    values: BusinessFunctionOverviewValues,
    actor: ActorContext,
  ): Promise<boolean> {
    if (!detail.value) return false
    overviewSaving.value = true
    overviewSaveError.value = null
    overviewConflict.value = false
    try {
      await updateBusinessFunctionOverview(detail.value.id, {
        ...values,
        actor,
        concurrencyToken: detail.value.concurrencyToken,
      })
      return await load(detail.value.id)
    } catch (caught: unknown) {
      if (caught instanceof ApiError && caught.status === 409 && caught.response.code === 'conflict') {
        overviewConflict.value = true
        overviewSaveError.value = caught.message
      } else {
        overviewSaveError.value = caught instanceof Error ? caught.message : '概览保存失败。'
      }
      return false
    } finally {
      overviewSaving.value = false
    }
  }

  async function saveProcess(
    steps: readonly BusinessProcessStepInput[],
    actor: ActorContext,
  ): Promise<boolean> {
    if (!detail.value) return false
    processSaving.value = true
    processSaveError.value = null
    processConflict.value = false
    try {
      await replaceBusinessProcessSteps(detail.value.id, {
        steps,
        actor,
        concurrencyToken: detail.value.concurrencyToken,
      })
      return await load(detail.value.id)
    } catch (caught: unknown) {
      if (caught instanceof ApiError && caught.status === 409 && caught.response.code === 'conflict') {
        processConflict.value = true
        processSaveError.value = caught.message
      } else {
        processSaveError.value = caught instanceof Error ? caught.message : '业务流程保存失败。'
      }
      return false
    } finally {
      processSaving.value = false
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

  onBeforeUnmount(() => requestController?.abort())
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
