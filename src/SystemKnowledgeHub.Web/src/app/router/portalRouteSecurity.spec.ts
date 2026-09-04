import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory } from 'vue-router'
import { describe, expect, it, vi } from 'vitest'
import { useActorStore } from '../stores/actor'
import { createApplicationRouter } from './index'

describe('Portal route security bootstrap', () => {
  it('bypasses current-user initialization for Portal but keeps it for Admin routes', async () => {
    setActivePinia(createPinia())
    const actor = useActorStore()
    const initialize = vi.spyOn(actor, 'initialize').mockResolvedValue(false)
    const router = createApplicationRouter(createMemoryHistory())

    await router.push('/portal')
    await router.isReady()
    expect(initialize).not.toHaveBeenCalled()

    await router.push('/dashboard')
    expect(initialize).toHaveBeenCalledTimes(1)
  })

  it('keeps Administrator route authorization after authenticated bootstrap', async () => {
    setActivePinia(createPinia())
    const actor = useActorStore()
    actor.currentUser = {
      id: 1,
      employeeNo: null,
      displayName: 'Reader',
      email: null,
      departmentOrTeam: null,
      jobTitle: null,
      isActive: true,
      knowledgeRoles: [],
      accessLevel: 'Viewer',
      authenticationMethod: 'local',
      mustChangePassword: false,
    }
    actor.authStatus = 'authenticated'
    actor.initialized = true
    const router = createApplicationRouter(createMemoryHistory())

    await router.push('/portal-management')
    expect(router.currentRoute.value.name).toBe('access-forbidden')
  })
})
