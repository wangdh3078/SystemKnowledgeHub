import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'
import { useOverlayStore } from './overlays'

describe('overlayStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('keeps only one drawer descriptor and replaces it in place', () => {
    const store = useOverlayStore()

    store.openDrawer({ kind: 'First', id: 1, mode: 'read' })
    store.openDrawer({ kind: 'Second', id: 2, mode: 'edit' })

    expect(store.currentDrawer).toEqual({
      surface: 'drawer',
      kind: 'Second',
      id: 2,
      mode: 'edit',
    })
    expect(store.isDrawerOpen).toBe(true)

    store.closeDrawer()
    expect(store.currentDrawer).toBeNull()
  })

  it('replaces a drawer with a dialog instead of nesting overlays', () => {
    const store = useOverlayStore()

    store.openDrawer({ kind: 'Detail', id: 7, mode: 'read' })
    store.openDialog({ kind: 'Create', id: null, mode: 'create' })

    expect(store.isDrawerOpen).toBe(false)
    expect(store.currentDialog).toEqual({
      surface: 'dialog',
      kind: 'Create',
      id: null,
      mode: 'create',
    })
  })
})
