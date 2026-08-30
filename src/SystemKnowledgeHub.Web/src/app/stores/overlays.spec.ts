import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'
import { useActorStore } from './actor'
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

  it('resolves close-then-continue work only after DrawerHost reports closed', async () => {
    const store = useOverlayStore()
    store.openDrawer({ kind: 'Detail', id: 7, mode: 'read' })

    let continued = false
    const closed = store.closeDrawerAfterClosed().then(() => {
      continued = true
    })

    expect(store.currentDrawer).toBeNull()
    expect(continued).toBe(false)

    store.notifyDrawerClosed()
    await closed

    expect(continued).toBe(true)
  })

  it('does not open Database Knowledge authoring overlays for an initialized Viewer', () => {
    const actorStore = useActorStore()
    actorStore.currentUser = {
      id: 42,
      employeeNo: null,
      displayName: '只读用户',
      email: null,
      departmentOrTeam: null,
      jobTitle: null,
      isActive: true,
      knowledgeRoles: [],
      accessLevel: 'Viewer',
      authenticationMethod: 'local',
      mustChangePassword: false,
    }
    actorStore.authStatus = 'authenticated'
    actorStore.initialized = true
    const store = useOverlayStore()

    store.openDialog({ kind: 'create-database-knowledge', id: null, mode: 'create' })
    expect(store.currentDialog).toBeNull()

    store.openDrawer({ kind: 'edit-database-object', id: 45, mode: 'edit' })
    expect(store.currentDrawer).toBeNull()

    store.openDrawer({ kind: 'database-column', id: 123, mode: 'read' })
    expect(store.currentDrawer?.kind).toBe('database-column')
  })
})
