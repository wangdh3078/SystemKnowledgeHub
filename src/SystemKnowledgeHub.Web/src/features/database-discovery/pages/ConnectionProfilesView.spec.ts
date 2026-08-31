import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../api/errors/ApiError'
import { useActorStore } from '../../../app/stores/actor'
import type { AccessLevel, CurrentUserProfile } from '../../users/api/userContracts'
import type {
  ConnectionProfile,
  DiscoveryRun,
  SourceOption,
} from '../api/databaseDiscoveryContracts'
import { discoveryPageStubs } from '../test/discoveryPageTestSupport'
import ConnectionProfilesView from './ConnectionProfilesView.vue'

const router = vi.hoisted(() => ({ push: vi.fn() }))
const messages = vi.hoisted(() => ({
  confirm: vi.fn(),
  error: vi.fn(),
  success: vi.fn(),
  warning: vi.fn(),
}))
const api = vi.hoisted(() => ({
  clearSecret: vi.fn(),
  createProfile: vi.fn(),
  listProfiles: vi.fn(),
  listSourceOptions: vi.fn(),
  replaceSecret: vi.fn(),
  setProfileEnabled: vi.fn(),
  setSecret: vi.fn(),
  testConnection: vi.fn(),
  triggerDiscovery: vi.fn(),
  updateProfile: vi.fn(),
}))

vi.mock('vue-router', () => ({ useRouter: () => router }))
vi.mock('element-plus', () => ({
  ElMessage: {
    error: messages.error,
    success: messages.success,
    warning: messages.warning,
  },
  ElMessageBox: { confirm: messages.confirm },
}))
vi.mock('../api/databaseDiscoveryApi', () => api)

const user: CurrentUserProfile = {
  id: 1,
  employeeNo: null,
  displayName: '数据库发现管理员',
  email: null,
  departmentOrTeam: null,
  jobTitle: null,
  isActive: true,
  knowledgeRoles: [],
  accessLevel: 'Administrator',
  authenticationMethod: 'local',
  mustChangePassword: false,
}
const oracleProfile: ConnectionProfile = {
  id: 1,
  databaseSourceId: 10,
  databaseSourceName: '核心 Oracle',
  name: 'Oracle 只读',
  providerType: 'Oracle',
  host: 'oracle.internal',
  port: 1521,
  databaseName: null,
  serviceName: 'ORCL',
  username: 'discovery',
  includedSchemas: ['APP'],
  isEnabled: true,
  connectionStatus: 'Succeeded',
  hasSecret: true,
  lastConnectionTestAt: '2026-08-30T00:00:00Z',
  lastConnectionTestErrorCode: null,
  lastConnectionTestSummary: '连接成功',
  lastDiscoveryAt: null,
  lastSuccessfulDiscoveryAt: null,
  concurrencyToken: 'oracle-token',
}
const postgresProfile: ConnectionProfile = {
  ...oracleProfile,
  id: 2,
  databaseSourceId: 20,
  databaseSourceName: '分析 PostgreSQL',
  name: 'PostgreSQL 只读',
  providerType: 'PostgreSql',
  host: 'postgres.internal',
  port: 5432,
  databaseName: 'analytics',
  serviceName: null,
  includedSchemas: ['public'],
  connectionStatus: 'Unknown',
  hasSecret: false,
  concurrencyToken: 'postgres-token',
}
const createdPostgresProfile: ConnectionProfile = {
  ...postgresProfile,
  id: 3,
  databaseSourceId: 30,
  databaseSourceName: '待配置 PG',
  name: 'CRM 只读',
  databaseName: 'crm',
  host: 'crm-db.internal',
  includedSchemas: ['SalesOps', 'Audit'],
  hasSecret: false,
  concurrencyToken: 'created-token',
}
const sources: readonly SourceOption[] = [
  { id: 10, name: '核心库', engine: 'Oracle', systemName: 'ERP', hasConnectionProfile: true },
  { id: 20, name: '分析库', engine: 'PostgreSQL', systemName: 'BI', hasConnectionProfile: true },
  {
    id: 30,
    name: '待配置 PG',
    engine: 'PostgreSQL',
    systemName: 'CRM',
    hasConnectionProfile: false,
  },
]
const queuedRun: DiscoveryRun = {
  id: 88,
  profileId: oracleProfile.id,
  databaseSourceId: oracleProfile.databaseSourceId,
  databaseSourceName: oracleProfile.databaseSourceName,
  profileName: oracleProfile.name,
  providerType: oracleProfile.providerType,
  status: 'Queued',
  baseSnapshotId: null,
  snapshotId: null,
  differenceId: null,
  scopeGenerationId: 1,
  queuedAt: '2026-08-30T00:00:00Z',
  startedAt: null,
  completedAt: null,
  cancellationRequestedAt: null,
  providerVersion: null,
  objectCounts: null,
  errorCode: null,
  errorSummary: null,
  concurrencyToken: 'run-token',
}

let wrapper: VueWrapper | undefined

function mountFor(accessLevel: AccessLevel): VueWrapper {
  const pinia = createPinia()
  setActivePinia(pinia)
  const actor = useActorStore()
  actor.currentUser = { ...user, accessLevel }
  actor.authStatus = 'authenticated'
  actor.initialized = true
  wrapper = mount(ConnectionProfilesView, {
    global: {
      plugins: [pinia],
      stubs: {
        ...discoveryPageStubs,
        Teleport: true,
        EmptyState: { props: ['title'], template: '<p>{{ title }}</p>' },
        ErrorState: { props: ['title', 'message'], template: '<p>{{ title }} {{ message }}</p>' },
        LoadingState: { props: ['message'], template: '<p>{{ message }}</p>' },
      },
    },
  })
  return wrapper
}

function button(view: VueWrapper, label: string, index = 0) {
  const matches = view.findAll('button').filter((item) => item.text() === label)
  expect(matches.length).toBeGreaterThan(index)
  return matches[index]!
}

describe('ConnectionProfilesView', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    api.listProfiles.mockResolvedValue([oracleProfile, postgresProfile])
    api.listSourceOptions.mockResolvedValue(sources)
    api.createProfile.mockResolvedValue(createdPostgresProfile)
    api.setSecret.mockResolvedValue({ ...postgresProfile, hasSecret: true })
    api.replaceSecret.mockResolvedValue(oracleProfile)
    api.clearSecret.mockResolvedValue({ ...oracleProfile, hasSecret: false })
    api.setProfileEnabled.mockResolvedValue({ ...oracleProfile, isEnabled: false })
    api.triggerDiscovery.mockResolvedValue(queuedRun)
    messages.confirm.mockResolvedValue(undefined)
  })

  afterEach(() => {
    wrapper?.unmount()
    wrapper = undefined
  })

  it('renders the profile list and switches the create form locator between Oracle and PostgreSQL', async () => {
    const view = mountFor('Administrator')
    await flushPromises()

    expect(view.text()).toContain('Oracle 只读')
    expect(view.text()).toContain('PostgreSQL 只读')
    expect(view.text()).toContain('oracle.internal:1521 / ORCL')
    expect(view.text()).toContain('postgres.internal:5432 / analytics')

    await button(view, '新增数据库连接').trigger('click')
    await flushPromises()
    expect(view.find('[data-label="服务名"]').exists()).toBe(true)
    expect(view.find('[data-label="数据库名"]').exists()).toBe(false)

    await view.find('[data-radio-value="PostgreSql"]').trigger('click')
    await flushPromises()
    expect(view.find('[data-label="服务名"]').exists()).toBe(false)
    expect(view.find('[data-label="数据库名"]').exists()).toBe(true)

    await view.find('[data-label="数据库来源"] [data-option-value="30"]').trigger('click')
    await view.find('[data-label="名称"] input').setValue(' CRM 只读 ')
    await view.find('[data-label="主机（Host）"] input').setValue(' crm-db.internal ')
    await view.find('[data-label="数据库名"] input').setValue(' crm ')
    await view.find('[data-label="用户名"] input').setValue(' discovery ')
    await view
      .find('[data-label="包含的架构（Schema，保留大小写，每行一个）"] textarea')
      .setValue('SalesOps\nAudit')
    await view
      .find('[data-label="连接密码（通过独立密钥接口保存）"] input')
      .setValue('create-secret')
    await button(view, '保存').trigger('click')
    await flushPromises()

    expect(api.createProfile).toHaveBeenCalledWith({
      databaseSourceId: 30,
      name: 'CRM 只读',
      providerType: 'PostgreSql',
      host: 'crm-db.internal',
      port: 5432,
      databaseName: 'crm',
      serviceName: null,
      authenticationMode: 'UsernamePassword',
      username: 'discovery',
      providerSpecificOptions: { version: 1 },
      includedSchemas: ['SalesOps', 'Audit'],
      isEnabled: true,
    })
    expect(api.setSecret).toHaveBeenCalledWith(createdPostgresProfile, 'create-secret')
  })

  it('sets, replaces, and clears secrets only through the dedicated actions', async () => {
    const view = mountFor('Administrator')
    await flushPromises()

    await button(view, '设置密码').trigger('click')
    await flushPromises()
    await view.find('input[aria-label="连接密码"]').setValue('new-postgres-secret')
    await button(view, '保存密码').trigger('click')
    await flushPromises()
    expect(api.setSecret).toHaveBeenCalledWith(postgresProfile, 'new-postgres-secret')

    await button(view, '替换密码').trigger('click')
    await flushPromises()
    await view.find('input[aria-label="连接密码"]').setValue('replacement-secret')
    await button(view, '保存密码').trigger('click')
    await flushPromises()
    expect(api.replaceSecret).toHaveBeenCalledWith(oracleProfile, 'replacement-secret')

    await button(view, '清除密码').trigger('click')
    await flushPromises()
    expect(messages.confirm).toHaveBeenCalledOnce()
    expect(api.clearSecret).toHaveBeenCalledWith(oracleProfile)
    expect(messages.success).toHaveBeenCalledWith('连接密码已清除。')
  })

  it('surfaces connection-test success and failure and routes a queued discovery run', async () => {
    api.testConnection
      .mockResolvedValueOnce({
        profileId: oracleProfile.id,
        succeeded: true,
        summary: '连接成功',
        providerVersion: '19c',
        databaseName: null,
        serviceName: 'ORCL',
        containerName: 'PDB1',
        concurrencyToken: 'tested-token',
      })
      .mockRejectedValueOnce(
        new ApiError(400, {
          code: 'InsufficientPrivilege',
          message: '账号权限不足',
          fieldErrors: null,
          details: null,
        }),
      )

    const view = mountFor('Administrator')
    await flushPromises()

    await button(view, '测试连接').trigger('click')
    await flushPromises()
    expect(view.text()).toContain('连接成功 · 19c · ORCL · PDB1')
    expect(messages.success).toHaveBeenCalledWith('连接测试成功。')

    await button(view, '测试连接').trigger('click')
    await flushPromises()
    expect(view.text()).toContain('InsufficientPrivilege · 账号权限不足')
    expect(messages.error).toHaveBeenCalledWith('InsufficientPrivilege · 账号权限不足')

    await button(view, '开始发现').trigger('click')
    await flushPromises()
    expect(api.triggerDiscovery).toHaveBeenCalledWith(oracleProfile)
    expect(router.push).toHaveBeenCalledWith({
      name: 'database-discovery-runs',
      query: { runId: '88' },
    })
  })

  it.each<AccessLevel>(['Viewer', 'Editor'])(
    'keeps profile data readable but hides write entry points from %s',
    async (accessLevel) => {
      const view = mountFor(accessLevel)
      await flushPromises()

      const writeLabels = [
        '新增数据库连接',
        '编辑',
        '停用',
        '启用',
        '替换密码',
        '设置密码',
        '清除密码',
        '测试连接',
        '开始发现',
      ]
      expect(
        view.findAll('button').filter((item) => writeLabels.includes(item.text())),
      ).toHaveLength(0)
    },
  )
})
