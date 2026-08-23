import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ElMessageBox } from 'element-plus'
import { logout } from '../app/security/authenticationApi'
import { useActorStore } from '../app/stores/actor'
import { setActiveDocumentEditDirty } from '../features/knowledge-documents/editor/documentEditState'
import AppTopBar from './AppTopBar.vue'

vi.mock('../app/security/authenticationApi', () => ({ logout: vi.fn() }))
vi.mock('element-plus', () => ({ ElMessageBox: { confirm: vi.fn() } }))

const currentUser = {
  id: 1,
  employeeNo: null,
  displayName: '管理员',
  email: null,
  departmentOrTeam: null,
  jobTitle: null,
  isActive: true,
  knowledgeRoles: [],
  accessLevel: 'Administrator' as const,
}

async function mountTopBar(accessLevel: 'Administrator' | 'Editor' | 'Viewer' = 'Administrator') {
  const pinia = createPinia()
  setActivePinia(pinia)
  const actorStore = useActorStore()
  actorStore.currentUser = { ...currentUser, accessLevel }
  actorStore.authStatus = 'authenticated'
  actorStore.initialized = true
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/', component: { template: '<div />' }, name: 'dashboard' }],
  })
  await router.push('/')
  await router.isReady()
  return mount(AppTopBar, {
    global: {
      plugins: [pinia, router],
      components: {
        ElButton: { emits: ['click'], template: '<button @click.stop="$emit(\'click\')"><slot /></button>' },
        ElIcon: { template: '<span><slot /></span>' },
      },
    },
  })
}

describe('AppTopBar logout confirmation', () => {
  beforeEach(() => {
    vi.mocked(logout).mockReset()
    vi.mocked(ElMessageBox.confirm).mockReset()
    setActiveDocumentEditDirty(false)
  })

  it('keeps the session when logout confirmation is cancelled', async () => {
    vi.mocked(ElMessageBox.confirm).mockRejectedValue('cancel')
    const wrapper = await mountTopBar()
    await wrapper.get('.app-topbar__profile').trigger('click')
    await wrapper.get('.app-topbar__logout').trigger('click')
    await flushPromises()

    expect(logout).not.toHaveBeenCalled()
    expect(useActorStore().isAuthenticated).toBe(true)
  })

  it('does not expose the global create entry to a Viewer', async () => {
    const wrapper = await mountTopBar('Viewer')

    expect(wrapper.findAll('button').some((button) => button.text() === '新增')).toBe(false)
  })

  it('requires the existing dirty-document confirmation after logout confirmation', async () => {
    setActiveDocumentEditDirty(true)
    vi.mocked(ElMessageBox.confirm)
      .mockResolvedValueOnce({} as never)
      .mockRejectedValueOnce('cancel')
    const wrapper = await mountTopBar()
    await wrapper.get('.app-topbar__profile').trigger('click')
    await wrapper.get('.app-topbar__logout').trigger('click')
    await flushPromises()

    expect(ElMessageBox.confirm).toHaveBeenCalledTimes(2)
    expect(logout).not.toHaveBeenCalled()
  })

  it('calls the real logout endpoint only after both confirmations', async () => {
    setActiveDocumentEditDirty(true)
    vi.mocked(ElMessageBox.confirm).mockResolvedValue({} as never)
    vi.mocked(logout).mockResolvedValue()
    const wrapper = await mountTopBar()
    await wrapper.get('.app-topbar__profile').trigger('click')
    await wrapper.get('.app-topbar__logout').trigger('click')
    await flushPromises()

    expect(logout).toHaveBeenCalledOnce()
    expect(useActorStore().authStatus).toBe('unauthenticated')
  })
})
