/* eslint-disable vue/one-component-per-file, vue/require-default-prop */
import { computed, defineComponent, h, inject, provide, type ComputedRef, type InjectionKey } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { UserDetail } from '../api/userContracts'
import {
  createUser,
  createUserLocalCredential,
  getKnowledgeRoles,
  getUser,
  getUserLoginMethods,
  getUserLoginSetupOptions,
  setLocalCredentialActiveState,
} from '../api/usersApi'
import UserManagementDrawer from './UserManagementDrawer.vue'

const actorState = vi.hoisted(() => ({ actor: { displayName: '系统管理员', role: 'Administrator' } }))
const overlayState = vi.hoisted(() => ({ closeDrawer: vi.fn(), requestDrawerClose: vi.fn() }))

vi.mock('../../../app/stores/actor', () => ({ useActorStore: () => actorState }))
vi.mock('../../../app/stores/overlays', () => ({ useOverlayStore: () => overlayState }))
vi.mock('element-plus', () => ({ ElMessage: { success: vi.fn() } }))
vi.mock('../api/usersApi', () => ({
  createUser: vi.fn(),
  createUserLocalCredential: vi.fn(),
  getKnowledgeRoles: vi.fn(),
  getUser: vi.fn(),
  getUserLoginMethods: vi.fn(),
  getUserLoginSetupOptions: vi.fn(),
  setLocalCredentialActiveState: vi.fn(),
  updateUser: vi.fn(),
}))

interface RadioContext {
  readonly value: ComputedRef<unknown>
  readonly select: (value: unknown) => void
}
const radioKey: InjectionKey<RadioContext> = Symbol('radio-group')

const components = {
  ElForm: defineComponent({
    setup(_props, { slots, expose }) {
      expose({ validate: () => Promise.resolve(true) })
      return () => h('form', slots.default?.())
    },
  }),
  ElFormItem: defineComponent({
    props: { label: String, error: String },
    setup(props, { slots }) {
      return () => h('label', [props.label ? h('span', props.label) : null, slots.default?.(), props.error ? h('em', props.error) : null])
    },
  }),
  ElInput: defineComponent({
    inheritAttrs: false,
    props: { modelValue: { type: String, default: '' }, type: String, disabled: Boolean, readonly: Boolean },
    emits: ['update:modelValue'],
    setup(props, { attrs, emit }) {
      return () => h('input', {
        ...attrs,
        value: props.modelValue,
        type: props.type ?? 'text',
        disabled: props.disabled,
        readonly: props.readonly,
        onInput: (event: Event) => emit('update:modelValue', (event.target as HTMLInputElement).value),
      })
    },
  }),
  ElRadioGroup: defineComponent({
    props: { modelValue: String },
    emits: ['update:modelValue', 'change'],
    setup(props, { slots, emit }) {
      provide(radioKey, {
        value: computed(() => props.modelValue),
        select: (value) => {
          emit('update:modelValue', value)
          emit('change', value)
        },
      })
      return () => h('div', slots.default?.())
    },
  }),
  ElRadio: defineComponent({
    props: { value: String, disabled: Boolean },
    setup(props, { slots }) {
      const group = inject(radioKey)!
      return () => h('label', { class: 'el-radio' }, [
        h('input', {
          type: 'radio',
          value: props.value,
          disabled: props.disabled,
          checked: group.value.value === props.value,
          onChange: () => group.select(props.value),
        }),
        slots.default?.(),
      ])
    },
  }),
  ElButton: defineComponent({
    props: { disabled: Boolean, loading: Boolean },
    emits: ['click'],
    setup(props, { slots, emit }) {
      return () => h('button', { type: 'button', disabled: props.disabled, onClick: () => emit('click') }, slots.default?.())
    },
  }),
  ElAlert: defineComponent({
    props: { title: String, description: String },
    setup(props, { slots }) {
      return () => h('div', [props.title, props.description, slots.default?.()])
    },
  }),
  ElCheckbox: defineComponent({
    setup(_props, { slots }) {
      return () => h('label', [h('input', { type: 'checkbox', checked: true, disabled: true }), slots.default?.()])
    },
  }),
  ElSelect: { template: '<div><slot /></div>' },
  ElOption: { template: '<span />' },
  ElTag: { template: '<span><slot /></span>' },
  ElTooltip: { template: '<span><slot /></span>' },
}

const user: UserDetail = {
  id: 42,
  employeeNo: 'EMP-042',
  displayName: '无登录用户',
  email: null,
  departmentOrTeam: null,
  jobTitle: null,
  isActive: true,
  knowledgeRoles: [],
  createdAt: '2026-08-30T00:00:00Z',
  updatedAt: '2026-08-30T00:00:00Z',
  concurrencyToken: 'token',
}

function mountDrawer(userId: number | null = null) {
  return mount(UserManagementDrawer, {
    props: { userId },
    global: {
      components,
      stubs: { LoginIdentityManagementPanel: { template: '<div>企业统一登录映射维护</div>' } },
    },
  })
}

async function selectMode(wrapper: ReturnType<typeof mountDrawer>, mode: 'local' | 'oidc' | 'none') {
  await wrapper.find(`input[type="radio"][value="${mode}"]`).trigger('change')
  await flushPromises()
}

describe('UserManagementDrawer login setup', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getKnowledgeRoles).mockResolvedValue([])
    vi.mocked(getUserLoginSetupOptions).mockResolvedValue({
      localGloballyEnabled: true,
      oidcGloballyEnabled: false,
      oidcSetupAvailable: true,
      approvedOidcProvider: 'CorporateOidc',
    })
    vi.mocked(getUser).mockResolvedValue(user)
    vi.mocked(getUserLoginMethods).mockResolvedValue({
      userId: 42,
      local: {
        exists: false,
        username: null,
        isActive: null,
        mustChangePassword: null,
        lastPasswordChangedAt: null,
        lockedUntil: null,
        globallyEnabled: true,
        concurrencyToken: null,
      },
      oidc: [],
    })
    vi.mocked(createUser).mockResolvedValue(user)
    vi.mocked(createUserLocalCredential).mockResolvedValue({
      exists: true,
      username: 'existing-local',
      isActive: true,
      mustChangePassword: true,
      lastPasswordChangedAt: '2026-08-30T01:00:00Z',
      lockedUntil: null,
      globallyEnabled: true,
      concurrencyToken: 'credential-token',
    })
  })

  it('switches among three explicit modes and keeps Local and OIDC fields mutually exclusive', async () => {
    const wrapper = mountDrawer()
    await flushPromises()
    expect(wrapper.text()).toContain('01基础资料')
    expect(wrapper.text()).toContain('02知识身份')
    expect(wrapper.text()).toContain('03登录方式')
    expect(wrapper.findAll('.user-login-setup__choices .el-radio')).toHaveLength(3)
    expect(wrapper.find('.user-login-setup__choices').text()).toContain('企业统一登录（OIDC / SSO）')

    await selectMode(wrapper, 'local')
    expect(wrapper.text()).toContain('登录用户名')
    expect(wrapper.text()).toContain('初始密码')
    expect(wrapper.text()).toContain('确认密码')
    expect(wrapper.text()).toContain('首次登录必须修改密码')
    expect(wrapper.text()).not.toContain('Subject / sub')

    await selectMode(wrapper, 'oidc')
    expect(wrapper.text()).toContain('身份提供方')
    expect(wrapper.text()).toContain('Subject / sub')
    expect(wrapper.text()).toContain('当前部署未启用企业统一登录')
    expect(wrapper.text()).not.toContain('初始密码')

    await selectMode(wrapper, 'none')
    expect(wrapper.text()).toContain('该用户当前无法登录系统。')
    expect(wrapper.text()).not.toContain('Subject / sub')
    expect(wrapper.text()).not.toContain('初始密码')
  })

  it('shows password mismatch and disables Create User without sending the confirmation value', async () => {
    const wrapper = mountDrawer()
    await flushPromises()
    await selectMode(wrapper, 'local')
    await wrapper.find('input[autocomplete="username"]').setValue('local-user')
    const passwordFields = wrapper.findAll('input[autocomplete="new-password"]')
    await passwordFields[0].setValue('exact password  一')
    await passwordFields[1].setValue('exact password  二')
    expect(wrapper.text()).toContain('两次输入的密码不一致。')
    expect(wrapper.findAll('button').find((button) => button.text() === '创建用户')?.attributes('disabled')).toBeDefined()
    expect(createUser).not.toHaveBeenCalled()

    await passwordFields[1].setValue('exact password  一')
    await wrapper.findAll('button').find((button) => button.text() === '创建用户')?.trigger('click')
    await flushPromises()
    expect(createUser).toHaveBeenCalledWith(expect.objectContaining({
      loginSetup: {
        type: 'local',
        username: 'local-user',
        initialPassword: 'exact password  一',
      },
    }))
    expect(JSON.stringify(vi.mocked(createUser).mock.calls[0]?.[0])).not.toContain('confirmPassword')
  })

  it('shows the no-login state when an existing user has no credential or identity', async () => {
    const wrapper = mountDrawer(42)
    await flushPromises()
    expect(wrapper.text()).toContain('该用户当前无法登录系统。')
    expect(wrapper.text()).toContain('添加本地账号')
    expect(getUserLoginMethods).toHaveBeenCalledWith(42)
  })

  it('adds Local to an existing user and never sends confirmation password', async () => {
    const wrapper = mountDrawer(42)
    await flushPromises()
    await wrapper.findAll('button').find((button) => button.text() === '添加本地账号')?.trigger('click')
    const username = wrapper.find('input[autocomplete="username"]')
    const passwords = wrapper.findAll('input[autocomplete="new-password"]')
    await username.setValue('existing-local')
    await passwords[0].setValue('exact existing password 空格')
    await passwords[1].setValue('exact existing password 空格')
    await wrapper.findAll('button').find((button) => button.text() === '确认添加')?.trigger('click')
    await flushPromises()

    expect(createUserLocalCredential).toHaveBeenCalledWith(42, 'existing-local', 'exact existing password 空格')
    expect(JSON.stringify(vi.mocked(createUserLocalCredential).mock.calls[0])).not.toContain('confirmPassword')
  })

  it('uses the credential projection and its own token for Local active state', async () => {
    const local = {
      exists: true,
      username: 'managed-local',
      isActive: true,
      mustChangePassword: false,
      lastPasswordChangedAt: '2026-08-30T01:00:00Z',
      lockedUntil: null,
      globallyEnabled: true,
      concurrencyToken: 'credential-token',
    } as const
    vi.mocked(getUserLoginMethods).mockResolvedValue({ userId: 42, local, oidc: [] })
    vi.mocked(setLocalCredentialActiveState).mockResolvedValue({ ...local, isActive: false, concurrencyToken: 'next-token' })
    const wrapper = mountDrawer(42)
    await flushPromises()
    expect(wrapper.text()).toContain('managed-local')
    expect(wrapper.text()).toContain('最近密码变更时间')
    await wrapper.findAll('button').find((button) => button.text() === '停用')?.trigger('click')
    await flushPromises()
    expect(setLocalCredentialActiveState).toHaveBeenCalledWith(42, local, false)
  })
})
