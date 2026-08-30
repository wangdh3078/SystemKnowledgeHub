import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiClient } from '../../../api/client/apiClient'
import type { LocalLoginMethod } from './userContracts'
import { resetUserLocalPassword } from './usersApi'

vi.mock('../../../api/client/apiClient', () => ({
  apiClient: { post: vi.fn() },
}))

describe('usersApi Local password reset', () => {
  beforeEach(() => vi.mocked(apiClient.post).mockReset())

  it('sends one temporary password and the credential token only', async () => {
    const local: LocalLoginMethod = {
      exists: true,
      username: 'managed-local',
      isActive: false,
      mustChangePassword: false,
      lastPasswordChangedAt: '2026-08-30T01:00:00Z',
      lockedUntil: null,
      globallyEnabled: true,
      concurrencyToken: 'credential-token',
    }
    vi.mocked(apiClient.post).mockResolvedValue({
      ...local,
      mustChangePassword: true,
      concurrencyToken: 'next-token',
    })

    await resetUserLocalPassword(42, local, 'AUTH-B04 temporary password')

    expect(apiClient.post).toHaveBeenCalledWith(
      '/users/42/local-credential/reset-password',
      {
        newPassword: 'AUTH-B04 temporary password',
        credentialConcurrencyToken: 'credential-token',
      },
      expect.objectContaining({ decode: expect.any(Function) }),
    )
    expect(JSON.stringify(vi.mocked(apiClient.post).mock.calls[0]?.[1])).not.toContain('confirm')
  })
})
