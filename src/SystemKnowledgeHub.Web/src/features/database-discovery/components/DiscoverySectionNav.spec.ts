import { createPinia, setActivePinia } from 'pinia'
import { mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it } from 'vitest'
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
function mountFor(accessLevel: AccessLevel) {
  const pinia = createPinia()
  setActivePinia(pinia)
  const actor = useActorStore()
  actor.currentUser = { ...user, accessLevel }
  actor.authStatus = 'authenticated'
  actor.initialized = true
  return mount(DiscoverySectionNav, {
    global: {
      plugins: [pinia],
      stubs: { RouterLink: { props: ['to'], template: '<a><slot /></a>' } },
    },
  })
}
describe('Database Discovery navigation authorization', () => {
  beforeEach(() => setActivePinia(createPinia()))
  it.each<AccessLevel>(['Viewer', 'Editor'])(
    'keeps connection management hidden from %s',
    (level) => {
      expect(mountFor(level).text()).not.toContain('连接配置')
    },
  )
  it('shows connection management to Administrator', () => {
    expect(mountFor('Administrator').text()).toContain('连接配置')
  })
  it('keeps read surfaces visible to Viewer', () => {
    const text = mountFor('Viewer').text()
    expect(text).toContain('发现运行')
    expect(text).toContain('发现快照')
    expect(text).toContain('差异审查')
  })
})
