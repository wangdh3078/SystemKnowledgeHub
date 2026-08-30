import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../api/errors/ApiError'
import type { CurrentUserProfile } from '../../features/users/api/userContracts'
import { getCurrentUser } from '../../features/users/api/usersApi'
import { getAuthenticationOptions, localLogin, startEnterpriseLogin } from './authenticationApi'
import { getAntiforgeryToken } from './securityApi'
import SecurityGate from './SecurityGate.vue'
import { useActorStore } from '../stores/actor'

const routerState = vi.hoisted(() => ({ replace: vi.fn() }))

vi.mock('../../features/users/api/usersApi', () => ({ getCurrentUser: vi.fn() }))
vi.mock('./authenticationApi', () => ({
  getAuthenticationOptions: vi.fn(),
  localLogin: vi.fn(),
  startEnterpriseLogin: vi.fn(),
}))
vi.mock('./securityApi', () => ({ getAntiforgeryToken: vi.fn() }))
vi.mock('vue-router', () => ({ useRouter: () => routerState }))

const currentUser: CurrentUserProfile = {
  id: 7,
  employeeNo: 'EMP-007',
  displayName: '本地管理员',
  email: null,
  departmentOrTeam: null,
  jobTitle: null,
  isActive: true,
  knowledgeRoles: [],
  accessLevel: 'Administrator',
  authenticationMethod: 'local',
  mustChangePassword: false,
}

let pinia: Pinia

const testComponents = {
  ElButton: {
    props: { disabled: Boolean, loading: Boolean },
    template: '<button :disabled="disabled"><slot /></button>',
  },
  ElAlert: {
    props: { title: { type: String, required: true } },
    template: '<div role="alert">{{ title }}</div>',
  },
  ElInput: {
    props: { modelValue: { type: String, required: true }, disabled: Boolean },
    emits: ['update:modelValue'],
    methods: {
      update(this: { $emit: (event: 'update:modelValue', value: string) => void }, event: Event): void {
        this.$emit('update:modelValue', (event.target as HTMLInputElement).value)
      },
    },
    template: '<input :value="modelValue" :disabled="disabled" @input="update" />',
  },
}

function mountGate() {
  return mount(SecurityGate, {
    global: {
      plugins: [pinia],
      components: testComponents,
    },
  })
}

describe('SecurityGate', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
    vi.mocked(getAuthenticationOptions).mockReset()
    vi.mocked(localLogin).mockReset()
    vi.mocked(getCurrentUser).mockReset()
    vi.mocked(getAntiforgeryToken).mockReset()
    vi.mocked(startEnterpriseLogin).mockReset()
    routerState.replace.mockReset()
    vi.mocked(getAntiforgeryToken).mockResolvedValue('anonymous-token')
  })

  it('renders only the Local Login form in Local-only mode', async () => {
    useActorStore().authStatus = 'unauthenticated'
    vi.mocked(getAuthenticationOptions).mockResolvedValue({
      localLoginEnabled: true,
      oidcLoginEnabled: false,
      oidcDisplayName: null,
    })

    const wrapper = mountGate()
    await flushPromises()

    expect(wrapper.text()).toContain('账号')
    expect(wrapper.text()).toContain('密码')
    expect(wrapper.text()).toContain('登录')
    expect(wrapper.find('.security-gate__enterprise-login').exists()).toBe(false)
    expect(wrapper.find('.security-gate__divider').exists()).toBe(false)
  })

  it('renders only the friendly enterprise button in OIDC-only mode', async () => {
    useActorStore().authStatus = 'unauthenticated'
    vi.mocked(getAuthenticationOptions).mockResolvedValue({
      localLoginEnabled: false,
      oidcLoginEnabled: true,
      oidcDisplayName: 'Microsoft Entra ID 登录',
    })

    const wrapper = mountGate()
    await flushPromises()

    expect(wrapper.find('form').exists()).toBe(false)
    expect(wrapper.text()).toContain('Microsoft Entra ID 登录')
    await wrapper.get('.security-gate__enterprise-login').trigger('click')
    expect(startEnterpriseLogin).toHaveBeenCalledWith()
  })

  it('renders Local Login and enterprise login together only in Both mode', async () => {
    useActorStore().authStatus = 'unauthenticated'
    vi.mocked(getAuthenticationOptions).mockResolvedValue({
      localLoginEnabled: true,
      oidcLoginEnabled: true,
      oidcDisplayName: '企业测试登录',
    })

    const wrapper = mountGate()
    await flushPromises()

    expect(wrapper.find('form').exists()).toBe(true)
    expect(wrapper.find('.security-gate__divider').text()).toContain('或')
    expect(wrapper.find('.security-gate__enterprise-login').text()).toContain('企业测试登录')
  })

  it.each([
    ['invalid_credentials', 401, '用户名或密码错误，或当前账号暂不可用。'],
    ['too_many_requests', 429, '登录尝试过于频繁，请稍后再试。'],
  ] as const)('renders the safe Local Login message for %s', async (code, status, message) => {
    useActorStore().authStatus = 'unauthenticated'
    vi.mocked(getAuthenticationOptions).mockResolvedValue({
      localLoginEnabled: true,
      oidcLoginEnabled: false,
      oidcDisplayName: null,
    })
    vi.mocked(localLogin).mockRejectedValue(new ApiError(status, {
      code,
      message: 'server detail is not displayed',
      fieldErrors: null,
      details: null,
    }))

    const wrapper = mountGate()
    await flushPromises()
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[role="alert"]').text()).toBe(message)
  })

  it('refreshes the antiforgery token without resubmitting a failed password request', async () => {
    useActorStore().authStatus = 'unauthenticated'
    vi.mocked(getAuthenticationOptions).mockResolvedValue({
      localLoginEnabled: true,
      oidcLoginEnabled: false,
      oidcDisplayName: null,
    })
    vi.mocked(localLogin).mockRejectedValue(new ApiError(403, {
      code: 'antiforgery_failed',
      message: 'ignored',
      fieldErrors: null,
      details: null,
    }))

    const wrapper = mountGate()
    await flushPromises()
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(getAntiforgeryToken).toHaveBeenCalledTimes(2)
    expect(localLogin).toHaveBeenCalledOnce()
    expect(wrapper.get('[role="alert"]').text()).toBe('登录安全令牌已失效，请重新提交。')
  })

  it('loads Current User after a successful Local Login instead of constructing a profile from username', async () => {
    useActorStore().authStatus = 'unauthenticated'
    vi.mocked(getAuthenticationOptions).mockResolvedValue({
      localLoginEnabled: true,
      oidcLoginEnabled: false,
      oidcDisplayName: null,
    })
    vi.mocked(localLogin).mockResolvedValue()
    vi.mocked(getCurrentUser).mockResolvedValue(currentUser)

    const wrapper = mountGate()
    await flushPromises()
    const inputs = wrapper.findAll('input')
    await inputs[0].setValue('local-admin')
    await inputs[1].setValue('correct password')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(localLogin).toHaveBeenCalledWith('local-admin', 'correct password')
    expect(getCurrentUser).toHaveBeenCalledOnce()
    expect(useActorStore().currentUser).toEqual(currentUser)
    expect(routerState.replace).toHaveBeenCalledWith({ name: 'dashboard' })
  })

  it('keeps the authoritative forced-password gate instead of navigating after temporary-password login', async () => {
    useActorStore().authStatus = 'unauthenticated'
    vi.mocked(getAuthenticationOptions).mockResolvedValue({
      localLoginEnabled: true,
      oidcLoginEnabled: false,
      oidcDisplayName: null,
    })
    vi.mocked(localLogin).mockResolvedValue()
    vi.mocked(getCurrentUser).mockResolvedValue({ ...currentUser, mustChangePassword: true })

    const wrapper = mountGate()
    await flushPromises()
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(useActorStore().mustChangePassword).toBe(true)
    expect(routerState.replace).not.toHaveBeenCalled()
  })
})
