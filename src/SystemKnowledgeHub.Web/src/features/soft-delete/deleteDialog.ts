import type { useOverlayStore } from '../../app/stores/overlays'

export interface DeleteDialogPayload {
  readonly objectTypeLabel: string
  readonly actionLabel: string
  readonly displayName: string
  readonly concurrencyToken: string
  readonly execute: () => Promise<void>
  readonly onDeleted: () => unknown
  readonly onRefresh: () => unknown
  readonly onUnavailable: () => unknown
}

export interface DeleteDependencyBlocker {
  readonly dependencyType: string
  readonly displayName: string
  readonly count: number
}

export function openDeleteDialog(
  overlays: ReturnType<typeof useOverlayStore>,
  payload: DeleteDialogPayload,
): void {
  overlays.openDialog({ kind: 'delete-root', id: null, mode: 'edit', payload })
}

export function readDeleteDialogPayload(value: unknown): DeleteDialogPayload | null {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return null
  const item = value as Partial<DeleteDialogPayload>
  return typeof item.objectTypeLabel === 'string'
    && typeof item.actionLabel === 'string'
    && typeof item.displayName === 'string'
    && typeof item.concurrencyToken === 'string'
    && typeof item.execute === 'function'
    && typeof item.onDeleted === 'function'
    && typeof item.onRefresh === 'function'
    && typeof item.onUnavailable === 'function'
    ? item as DeleteDialogPayload
    : null
}

export function readDeleteBlockers(value: unknown): readonly DeleteDependencyBlocker[] {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return []
  const blockers = (value as Record<string, unknown>).blockers
  if (!Array.isArray(blockers)) return []
  return blockers.flatMap((value) => {
    if (typeof value !== 'object' || value === null || Array.isArray(value)) return []
    const item = value as Record<string, unknown>
    return typeof item.dependencyType === 'string'
      && typeof item.displayName === 'string'
      && typeof item.count === 'number'
      && Number.isSafeInteger(item.count)
      && item.count > 0
      ? [{ dependencyType: item.dependencyType, displayName: item.displayName, count: item.count }]
      : []
  })
}
