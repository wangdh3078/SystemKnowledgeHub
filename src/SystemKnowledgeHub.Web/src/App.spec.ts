import { shallowMount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { describe, expect, it } from 'vitest'
import App from './App.vue'
import { useActorStore } from './app/stores/actor'

describe('App password lifecycle gate', () => {
  it('does not render the application shell while a Local password change is mandatory', async () => {
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
      accessLevel: 'Administrator',
      authenticationMethod: 'local',
      mustChangePassword: true,
    }
    actor.authStatus = 'authenticated'
    actor.initialized = true
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [{
        path: '/',
        component: { template: '<div />' },
        meta: { title: '测试', layout: 'app-shell', navigationKey: 'dashboard' },
      }],
    })
    await router.push('/')
    await router.isReady()

    const wrapper = shallowMount(App, {
      global: {
        plugins: [pinia, router],
        stubs: {
          ElConfigProvider: { template: '<div><slot /></div>' },
          ForcedPasswordChangeGate: { template: '<main class="forced-password-stub" />' },
          SecurityGate: { template: '<main class="security-stub" />' },
          AppShell: { template: '<main class="shell-stub"><slot /></main>' },
          RouterView: { template: '<div class="route-stub" />' },
        },
      },
    })

    expect(wrapper.find('.forced-password-stub').exists()).toBe(true)
    expect(wrapper.find('.shell-stub').exists()).toBe(false)
  })
})
