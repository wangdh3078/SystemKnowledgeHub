import { describe, expect, it } from 'vitest'
import { decodeUserDetail, decodeUsersList } from './userContracts'

const summary = {
  id: 42,
  employeeNo: 'EMP-042',
  displayName: '权限投影用户',
  email: null,
  departmentOrTeam: null,
  jobTitle: null,
  accessLevel: 'Editor',
  isActive: true,
  knowledgeRoles: [],
  updatedAt: '2026-08-30T00:00:00Z',
}

describe('User AccessLevel projections', () => {
  it('decodes a controlled AccessLevel in list and detail projections', () => {
    expect(decodeUsersList({ items: [summary], page: 1, pageSize: 20, total: 1 }).items[0]?.accessLevel).toBe('Editor')
    expect(decodeUserDetail({ ...summary, createdAt: '2026-08-29T00:00:00Z', concurrencyToken: 'token' }).accessLevel).toBe('Editor')
  })

  it('rejects unsupported AccessLevel wire values', () => {
    expect(() => decodeUsersList({ items: [{ ...summary, accessLevel: 'Owner' }], page: 1, pageSize: 20, total: 1 }))
      .toThrow('usersList.items[0].accessLevel must be a supported access level')
  })
})
