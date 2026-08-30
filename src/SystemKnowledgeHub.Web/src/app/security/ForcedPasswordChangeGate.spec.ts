import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { logout } from './authenticationApi'
import ForcedPasswordChangeGate from './ForcedPasswordChangeGate.vue'
import { useActorStore } from '../stores/actor'

vi.mock('./authenticationApi', () => ({ logout: vi.fn() }))
vi.mock('element-plus', () => ({ ElMessage: { success: vi.fn() } }))

async function mountGate() {
  const pinia = createPinia()
  setActivePinia(pinia)
  const actor = useActorStore()
  actor.currentUser = {
    id: 1,
    employeeNo: null,
    displayName: '临时密码用户',
    email: null,
    departmentOrTeam: null,
    jobTitle: null,
    isActive: true,
    knowledgeRoles: [],
    accessLevel: 'Viewer',
    authenticationMethod: 'local',
    mustChangePassword: true,
  }
  actor.authStatus = 'authenticated'
  actor.initialized = true
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/dashboard', name: 'dashboard', component: { template: '<div />' } },
      { path: '/admin/users', name: 'users-management', component: { template: '<div />' } },
    ],
  })
  await router.push('/admin/users')
  await router.isReady()
  const wrapper = mount(ForcedPasswordChangeGate, {
    global: {
      plugins: [pinia, router],
      stubs: {
        LocalPasswordChangeForm: {
          emits: ['changed'],
          template: '<button class="complete-password-change" @click="$emit(\'changed\')">完成改密</button>',
        },
        ElButton: {
          emits: ['click'],
          template: '<button class="forced-password-gate__logout" @click="$emit(\'click\')"><slot /></button>',
        },
        ElIcon: { template: '<span><slot /></span>' },
      },
    },
  })
  return { wrapper, router, actor }
}

describe('ForcedPasswordChangeGate account-route clearing', () => {
  beforeEach(() => vi.mocked(logout).mockReset())

  it('clears the old protected route after password change invalidates the session', async () => {
    const { wrapper, router, actor } = await mountGate()

    await wrapper.get('.complete-password-change').trigger('click')
    await flushPromises()

    expect(actor.authStatus).toBe('unauthenticated')
    expect(router.currentRoute.value.name).toBe('dashboard')
  })

  it('clears the old protected route when the temporary-password user logs out', async () => {
    vi.mocked(logout).mockResolvedValue()
    const { wrapper, router, actor } = await mountGate()

    await wrapper.get('.forced-password-gate__logout').trigger('click')
    await flushPromises()

    expect(logout).toHaveBeenCalledOnce()
    expect(actor.authStatus).toBe('unauthenticated')
    expect(router.currentRoute.value.name).toBe('dashboard')
  })
})
