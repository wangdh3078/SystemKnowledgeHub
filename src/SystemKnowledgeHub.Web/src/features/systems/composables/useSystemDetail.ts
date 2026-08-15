import { onBeforeUnmount, ref } from 'vue'
import type { ActorContext } from '../../../app/stores/actor'
import { ApiError } from '../../../api/errors/ApiError'
import { getSystemDetail, updateSystemOverview } from '../api/systemsApi'
import type {
  SystemDeployment,
  SystemDetailResponse,
  SystemRepository,
} from '../api/systemsContracts'

export interface SystemOverviewValues {
  readonly displayName: string
  readonly systemType: string
  readonly purpose: string | null
  readonly mainUsers: readonly string[]
  readonly repository: SystemRepository
  readonly deployment: readonly SystemDeployment[]
  readonly mainProjects: readonly string[]
  readonly mainEntryPoints: readonly string[]
  readonly notes: string | null
}

export function useSystemDetail() {
  const detail = ref<SystemDetailResponse | null>(null)
  const loading = ref(false)
  const pageError = ref<string | null>(null)
  const saving = ref(false)
  const saveError = ref<string | null>(null)
  const concurrencyConflict = ref(false)
  let requestController: AbortController | null = null

  async function load(systemId: number): Promise<boolean> {
    requestController?.abort()
    requestController = new AbortController()
    loading.value = detail.value === null
    pageError.value = null

    try {
      detail.value = await getSystemDetail(systemId, requestController.signal)
      concurrencyConflict.value = false
      return true
    } catch (error: unknown) {
      if (error instanceof DOMException && error.name === 'AbortError') return false
      pageError.value = error instanceof Error ? error.message : '系统详情加载失败。'
      return false
    } finally {
      loading.value = false
    }
  }

  async function saveOverview(
    values: SystemOverviewValues,
    actor: ActorContext,
  ): Promise<boolean> {
    if (!detail.value) return false
    saving.value = true
    saveError.value = null
    concurrencyConflict.value = false

    try {
      await updateSystemOverview(detail.value.id, {
        ...values,
        actor,
        concurrencyToken: detail.value.concurrencyToken,
      })
      return await load(detail.value.id)
    } catch (error: unknown) {
      if (error instanceof ApiError && error.status === 409 && error.response.code === 'conflict') {
        concurrencyConflict.value = true
        saveError.value = error.message
      } else {
        saveError.value = error instanceof Error ? error.message : '概览保存失败。'
      }
      return false
    } finally {
      saving.value = false
    }
  }

  function clearSaveError(): void {
    saveError.value = null
    concurrencyConflict.value = false
  }

  onBeforeUnmount(() => requestController?.abort())

  return {
    detail,
    loading,
    pageError,
    saving,
    saveError,
    concurrencyConflict,
    load,
    saveOverview,
    clearSaveError,
  }
}
