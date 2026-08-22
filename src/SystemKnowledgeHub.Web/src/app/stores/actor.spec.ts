import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { ApiError } from '../../api/errors/ApiError'
import { getCurrentUser } from '../../features/users/api/usersApi'
import { getAntiforgeryToken } from '../security/securityApi'
import type { CurrentUserProfile } from '../../features/users/api/userContracts'
import { useActorStore } from './actor'

vi.mock('../../features/users/api/usersApi', () => ({ getCurrentUser: vi.fn() }))
vi.mock('../security/securityApi', () => ({ getAntiforgeryToken: vi.fn() }))

const currentUser: CurrentUserProfile = {
  id: 42, employeeNo: 'EMP-042', displayName: '王敏', email: 'wang.min@example.com',
  departmentOrTeam: '制造系统组', jobTitle: '知识工程师', isActive: true,
  knowledgeRoles: [{ id: 7, name: 'MES 业务专家', description: null, isActive: true }], accessLevel: 'Editor',
}

describe('actor Current User store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(getCurrentUser).mockReset()
    vi.mocked(getAntiforgeryToken).mockReset()
    vi.mocked(getAntiforgeryToken).mockResolvedValue('request-token')
  })

  it('loads the authenticated canonical User without localStorage identity selection', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(currentUser)
    const store = useActorStore()
    await store.initialize()
    expect(store.currentUser?.id).toBe(42)
    expect(store.authStatus).toBe('authenticated')
    expect(store.canEdit).toBe(true)
    expect(store.isAdministrator).toBe(false)
    expect(store.antiforgeryToken).toBe('request-token')
    expect(store.actor).toEqual({ displayName: '王敏', role: 'MES 业务专家' })
  })

  it('classifies an unauthenticated current-user response without a fallback profile', async () => {
    vi.mocked(getCurrentUser).mockRejectedValue(new ApiError(401, { code: 'unauthenticated', message: '尚未登录。', fieldErrors: null, details: { authStatus: 'missing' } }))
    const store = useActorStore()
    await store.initialize()
    expect(store.currentUser).toBeNull()
    expect(store.authStatus).toBe('unauthenticated')
    expect(store.canEdit).toBe(false)
  })
})
