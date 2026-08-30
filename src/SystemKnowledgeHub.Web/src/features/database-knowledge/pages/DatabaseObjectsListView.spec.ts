import { createPinia, setActivePinia } from 'pinia'
import { ref } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useActorStore } from '../../../app/stores/actor'
import type { AccessLevel, CurrentUserProfile } from '../../users/api/userContracts'
import { getSystemsList } from '../../systems/api/systemsApi'
import type { DatabaseObjectsListResponse } from '../api/databaseKnowledgeContracts'
import { useDatabaseObjectsList } from '../composables/useDatabaseObjectsList'
import DatabaseObjectsListView from './DatabaseObjectsListView.vue'

const overlayState = vi.hoisted(() => ({ openDialog: vi.fn() }))
const routerState = vi.hoisted(() => ({ replace: vi.fn(), push: vi.fn() }))

vi.mock('../../../app/stores/overlays', () => ({
  useOverlayStore: () => overlayState,
}))
vi.mock('vue-router', () => ({
  useRoute: () => ({ query: {} }),
  useRouter: () => routerState,
}))
vi.mock('../../systems/api/systemsApi', () => ({ getSystemsList: vi.fn() }))
vi.mock('../composables/useDatabaseObjectsList', () => ({ useDatabaseObjectsList: vi.fn() }))

const currentUser: CurrentUserProfile = {
  id: 42,
  employeeNo: null,
  displayName: '权限回归用户',
  email: null,
  departmentOrTeam: null,
  jobTitle: null,
  isActive: true,
  knowledgeRoles: [],
  accessLevel: 'Viewer',
  authenticationMethod: 'local',
  mustChangePassword: false,
}

const data: DatabaseObjectsListResponse = {
  browseContext: {
    system: { id: 12, name: 'MES' },
    databaseSources: [{
      id: 9,
      name: 'MES Oracle',
      engine: 'Oracle',
      concurrencyToken: 'source-token',
      canDelete: true,
    }],
    schemas: ['MES'],
  },
  items: [],
  page: 1,
  pageSize: 20,
  total: 0,
}

const stubs = {
  ElButton: { template: '<button type="button"><slot /></button>' },
  ElIcon: { template: '<span><slot /></span>' },
  ElInput: { template: '<input />' },
  ElOption: { template: '<option />' },
  ElPagination: { template: '<div />' },
  ElSelect: { template: '<select><slot /></select>' },
  ElTable: { template: '<div><slot /></div>' },
  ElTableColumn: { template: '<div />' },
  ElTooltip: { template: '<span><slot /></span>' },
  EmptyState: { template: '<div />' },
  ErrorState: { template: '<div />' },
  KnowledgeStatusBadge: { template: '<span />' },
  LoadingState: { template: '<div />' },
}

function mockList(): void {
  vi.mocked(useDatabaseObjectsList).mockReturnValue({
    systemId: ref<number | undefined>(),
    databaseSourceId: ref<number | undefined>(),
    schema: ref(''),
    objectType: ref(''),
    knowledgeStatus: ref(''),
    keyword: ref(''),
    sort: ref('objectName:asc'),
    page: ref(1),
    pageSize: ref(20),
    loading: ref(false),
    error: ref<string | null>(null),
    data: ref(data),
    load: vi.fn().mockResolvedValue(undefined),
    resetPageAndLoad: vi.fn(),
    clearFilters: vi.fn(),
  })
}

async function mountFor(accessLevel: AccessLevel) {
  const pinia = createPinia()
  setActivePinia(pinia)
  const actorStore = useActorStore()
  actorStore.currentUser = { ...currentUser, accessLevel }
  actorStore.authStatus = 'authenticated'
  actorStore.initialized = true

  const wrapper = mount(DatabaseObjectsListView, {
    global: { plugins: [pinia], stubs },
  })
  await flushPromises()
  return wrapper
}

describe('DatabaseObjectsListView write-action visibility', () => {
  beforeEach(() => {
    mockList()
    vi.mocked(getSystemsList).mockResolvedValue({ items: [], page: 1, pageSize: 100, total: 0 })
    overlayState.openDialog.mockReset()
    routerState.replace.mockReset()
    routerState.push.mockReset()
  })

  it('keeps Viewer read-only even when a write capability is present in list data', async () => {
    const wrapper = await mountFor('Viewer')

    expect(wrapper.text()).not.toContain('新增数据库对象')
    expect(wrapper.find('button[aria-label="删除数据库源"]').exists()).toBe(false)
  })

  it.each<AccessLevel>(['Editor', 'Administrator'])('shows the create entry to %s', async (accessLevel) => {
    const wrapper = await mountFor(accessLevel)

    expect(wrapper.text()).toContain('新增数据库对象')
  })
})
