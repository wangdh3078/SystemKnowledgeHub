import { onBeforeUnmount, ref } from 'vue'
import type { ActorContext } from '../../../app/stores/actor'
import { ApiError } from '../../../api/errors/ApiError'
import {
  getSystemDetail,
  updateSystemLifecycle,
  updateSystemOverview,
  updateSystemTechnology,
} from '../api/systemsApi'
import type {
  SystemDeployment,
  SystemDetailResponse,
  SystemLifecycle,
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

export function useSystemDetail(getSelectedId?: () => number | null) {
  const detail = ref<SystemDetailResponse | null>(null)
  const loading = ref(false)
  const pageError = ref<string | null>(null)
  const saving = ref(false)
  const saveError = ref<string | null>(null)
  const concurrencyConflict = ref(false)
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
    saveError.value = null
    concurrencyConflict.value = false
    loading.value = true
    pageError.value = null
    const current = () =>
      generation === requestGeneration && isSelected(id) && !controller.signal.aborted
    try {
      const response = await getSystemDetail(id, controller.signal)
      if (!current() || response.id !== id) return false
      detail.value = response
      concurrencyConflict.value = false
      return true
    } catch (caught: unknown) {
      if (!current() || (caught instanceof DOMException && caught.name === 'AbortError'))
        return false
      pageError.value = caught instanceof Error ? caught.message : '系统详情加载失败。'
      return false
    } finally {
      if (current()) loading.value = false
    }
  }

  async function saveOverview(values: SystemOverviewValues, actor: ActorContext): Promise<boolean> {
    if (!detail.value || loading.value || !isSelected(detail.value.id)) return false
    const target = detail.value
    const generation = requestGeneration
    saving.value = true
    saveError.value = null
    concurrencyConflict.value = false

    try {
      await updateSystemOverview(target.id, {
        ...values,
        actor,
        concurrencyToken: target.concurrencyToken,
      })
      return await (generation === requestGeneration && isSelected(target.id)
        ? load(target.id)
        : Promise.resolve(false))
    } catch (error: unknown) {
      if (generation !== requestGeneration || !isSelected(target.id)) return false
      if (error instanceof ApiError && error.status === 409 && error.response.code === 'conflict') {
        concurrencyConflict.value = true
        saveError.value = error.message
      } else {
        saveError.value = error instanceof Error ? error.message : '概览保存失败。'
      }
      return false
    } finally {
      if (generation === requestGeneration) saving.value = false
    }
  }

  async function saveTechnology(
    technologies: readonly string[],
    actor: ActorContext,
  ): Promise<boolean> {
    if (!detail.value || loading.value || !isSelected(detail.value.id)) return false
    const target = detail.value
    const generation = requestGeneration
    saving.value = true
    saveError.value = null
    concurrencyConflict.value = false

    try {
      await updateSystemTechnology(target.id, {
        technologies,
        actor,
        concurrencyToken: target.concurrencyToken,
      })
      return await (generation === requestGeneration && isSelected(target.id)
        ? load(target.id)
        : Promise.resolve(false))
    } catch (error: unknown) {
      if (generation !== requestGeneration || !isSelected(target.id)) return false
      setSaveError(error, '技术信息保存失败。')
      return false
    } finally {
      if (generation === requestGeneration) saving.value = false
    }
  }

  async function saveLifecycle(
    targetLifecycle: SystemLifecycle,
    actor: ActorContext,
  ): Promise<boolean> {
    if (!detail.value || loading.value || !isSelected(detail.value.id)) return false
    const target = detail.value
    const generation = requestGeneration
    saving.value = true
    saveError.value = null
    concurrencyConflict.value = false

    try {
      await updateSystemLifecycle(target.id, {
        targetLifecycle,
        actor,
        concurrencyToken: target.concurrencyToken,
      })
      return await (generation === requestGeneration && isSelected(target.id)
        ? load(target.id)
        : Promise.resolve(false))
    } catch (error: unknown) {
      if (generation !== requestGeneration || !isSelected(target.id)) return false
      setSaveError(error, '生命周期保存失败。')
      return false
    } finally {
      if (generation === requestGeneration) saving.value = false
    }
  }

  function setSaveError(error: unknown, fallbackMessage: string): void {
    if (error instanceof ApiError && error.status === 409 && error.response.code === 'conflict') {
      concurrencyConflict.value = true
      saveError.value = error.message
      return
    }

    saveError.value = error instanceof Error ? error.message : fallbackMessage
  }

  function clearSaveError(): void {
    saveError.value = null
    concurrencyConflict.value = false
  }

  onBeforeUnmount(() => {
    requestGeneration++
    requestController?.abort()
  })

  return {
    detail,
    loading,
    pageError,
    saving,
    saveError,
    concurrencyConflict,
    load,
    saveOverview,
    saveTechnology,
    saveLifecycle,
    clearSaveError,
  }
}
