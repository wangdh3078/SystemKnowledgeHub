import { createPinia, setActivePinia } from 'pinia'
import { mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { describe, expect, it } from 'vitest'
import { useActorStore } from '../app/stores/actor'
import AppSidebar from './AppSidebar.vue'

const currentUser = {
  id: 1,
  employeeNo: null,
  displayName: '当前用户',
  email: null,
  departmentOrTeam: null,
  jobTitle: null,
  isActive: true,
  knowledgeRoles: [],
  accessLevel: 'Administrator' as const,
}

async function mountSidebar(accessLevel: 'Administrator' | 'Editor' | 'Viewer') {
  const pinia = createPinia()
  setActivePinia(pinia)
  const actor = useActorStore()
  actor.currentUser = { ...currentUser, accessLevel }
  actor.authStatus = 'authenticated'
  actor.initialized = true
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'dashboard', component: { template: '<div />' } },
      {
        path: '/admin/attachments',
        name: 'attachment-administration',
        component: { template: '<div />' },
        meta: {
          title: '附件管理',
          layout: 'app-shell',
          navigationKey: 'attachments',
        },
      },
    ],
  })
  await router.push('/')
  await router.isReady()
  return mount(AppSidebar, {
    global: {
      plugins: [pinia, router],
      components: { ElIcon: { template: '<span><slot /></span>' } },
      stubs: { Connection: true },
    },
  })
}

describe('AppSidebar attachment administration visibility', () => {
  it('shows the attachment governance entry only to administrators', async () => {
    expect((await mountSidebar('Administrator')).text()).toContain('附件管理')
    expect((await mountSidebar('Editor')).text()).not.toContain('附件管理')
    expect((await mountSidebar('Viewer')).text()).not.toContain('附件管理')
  })
})
