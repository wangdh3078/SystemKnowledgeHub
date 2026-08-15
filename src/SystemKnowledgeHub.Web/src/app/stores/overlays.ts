import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

export type OverlayMode = 'read' | 'create' | 'edit'

export type OverlaySurface = 'drawer' | 'dialog'

export interface OverlayDescriptor {
  readonly surface: OverlaySurface
  readonly kind: string
  readonly id: number | null
  readonly mode: OverlayMode
  readonly payload?: unknown
}

export const useOverlayStore = defineStore('overlays', () => {
  const currentOverlay = ref<OverlayDescriptor | null>(null)
  const currentDrawer = computed(() =>
    currentOverlay.value?.surface === 'drawer' ? currentOverlay.value : null,
  )
  const currentDialog = computed(() =>
    currentOverlay.value?.surface === 'dialog' ? currentOverlay.value : null,
  )
  const isDrawerOpen = computed(() => currentDrawer.value !== null)
  const isDialogOpen = computed(() => currentDialog.value !== null)

  function openDrawer(descriptor: Omit<OverlayDescriptor, 'surface'>): void {
    currentOverlay.value = { surface: 'drawer', ...descriptor }
  }

  function closeDrawer(): void {
    if (currentOverlay.value?.surface === 'drawer') {
      currentOverlay.value = null
    }
  }

  function openDialog(descriptor: Omit<OverlayDescriptor, 'surface'>): void {
    currentOverlay.value = { surface: 'dialog', ...descriptor }
  }

  function closeDialog(): void {
    if (currentOverlay.value?.surface === 'dialog') {
      currentOverlay.value = null
    }
  }

  return {
    currentOverlay,
    currentDrawer,
    currentDialog,
    isDrawerOpen,
    isDialogOpen,
    openDrawer,
    openDialog,
    closeDrawer,
    closeDialog,
  }
})
