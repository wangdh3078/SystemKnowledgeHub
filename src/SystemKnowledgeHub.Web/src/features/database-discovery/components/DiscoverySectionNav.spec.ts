import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { defineComponent } from 'vue'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'
import { afterEach, describe, expect, it } from 'vitest'
import { useActorStore } from '../../../app/stores/actor'
import type { AccessLevel, CurrentUserProfile } from '../../users/api/userContracts'
import DiscoverySectionNav from './DiscoverySectionNav.vue'

const user: CurrentUserProfile = {
  id: 1,
  employeeNo: null,
  displayName: '发现审查人',
  email: null,
  departmentOrTeam: null,
  jobTitle: null,
  isActive: true,
  knowledgeRoles: [],
  accessLevel: 'Viewer',
  authenticationMethod: 'local',
  mustChangePassword: false,
}
const Page = defineComponent({ template: '<div />' })
let wrapper: VueWrapper | undefined

function createTestRouter(): Router {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/database-discovery/connections',
        alias: '/database-discovery',
        name: 'database-discovery-connections',
        component: Page,
      },
      { path: '/database-discovery/runs', name: 'database-discovery-runs', component: Page },
      {
        path: '/database-discovery/snapshots',
        name: 'database-discovery-snapshots',
        component: Page,
      },
      {
        path: '/database-discovery/snapshots/:id',
        name: 'database-discovery-snapshot',
        component: Page,
      },
      {
        path: '/database-discovery/differences',
        name: 'database-discovery-differences',
        component: Page,
      },
      {
        path: '/database-discovery/differences/:id',
        name: 'database-discovery-difference',
        component: Page,
      },
    ],
  })
}

async function mountAt(accessLevel: AccessLevel, path: string) {
  const pinia = createPinia()
  setActivePinia(pinia)
  const actor = useActorStore()
  actor.currentUser = { ...user, accessLevel }
  actor.authStatus = 'authenticated'
  actor.initialized = true
  const router = createTestRouter()
  await router.push(path)
  await router.isReady()
  wrapper = mount(DiscoverySectionNav, { global: { plugins: [pinia, router] } })
  return { view: wrapper, router }
}

function activeLabel(view: VueWrapper): string {
  return view.find('[aria-current="page"]').text()
}

afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
})

describe('Database Discovery route navigation', () => {
  it.each<AccessLevel>(['Viewer', 'Editor'])(
    'keeps connection management hidden from %s while all read routes stay clickable',
    async (level) => {
      const { view } = await mountAt(level, '/database-discovery/runs')
      expect(view.text()).not.toContain('连接配置')
      expect(view.findAll('a').map((item) => item.text())).toEqual([
        '发现运行',
        '发现快照',
        '差异审查',
      ])
    },
  )

  it('provides four real routes to Administrator', async () => {
    const { view } = await mountAt('Administrator', '/database-discovery')
    expect(view.findAll('a').map((item) => item.text())).toEqual([
      '连接配置',
      '发现运行',
      '发现快照',
      '差异审查',
    ])
    expect(view.findAll('a').map((item) => item.attributes('href'))).toEqual([
      '/database-discovery/connections',
      '/database-discovery/runs',
      '/database-discovery/snapshots',
      '/database-discovery/differences',
    ])
    expect(activeLabel(view)).toBe('连接配置')
  })

  it.each([
    ['/database-discovery/connections', '连接配置'],
    ['/database-discovery/runs', '发现运行'],
    ['/database-discovery/snapshots', '发现快照'],
    ['/database-discovery/snapshots/41', '发现快照'],
    ['/database-discovery/differences', '差异审查'],
    ['/database-discovery/differences/51', '差异审查'],
  ])('derives the active tab from direct route %s', async (path, label) => {
    const { view } = await mountAt('Administrator', path)
    expect(activeLabel(view)).toBe(label)
  })

  it('tracks browser back and forward from route state', async () => {
    const { view, router } = await mountAt('Administrator', '/database-discovery/runs')
    await router.push('/database-discovery/snapshots/41')
    await router.push('/database-discovery/differences/51')
    expect(activeLabel(view)).toBe('差异审查')

    router.back()
    await flushPromises()
    expect(activeLabel(view)).toBe('发现快照')

    router.forward()
    await flushPromises()
    expect(activeLabel(view)).toBe('差异审查')
  })
})
