import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiClient } from '../../api/client/apiClient'
import { getAuthenticationOptions, localLogin } from './authenticationApi'

vi.mock('../../api/client/apiClient', () => ({
  apiClient: {
    get: vi.fn(),
    postRoot: vi.fn(),
  },
}))

describe('authenticationApi', () => {
  beforeEach(() => {
    vi.mocked(apiClient.get).mockReset()
    vi.mocked(apiClient.postRoot).mockReset()
  })

  it('decodes only the public authentication options contract', async () => {
    vi.mocked(apiClient.get).mockImplementation(async (_path, options) => options.decode({
      localLoginEnabled: true,
      oidcLoginEnabled: false,
      oidcDisplayName: null,
    }))

    await expect(getAuthenticationOptions()).resolves.toEqual({
      localLoginEnabled: true,
      oidcLoginEnabled: false,
      oidcDisplayName: null,
    })
    expect(apiClient.get).toHaveBeenCalledWith('/auth/options', expect.objectContaining({ decode: expect.any(Function) }))
  })

  it('posts only Local Login credentials through the shared client', async () => {
    vi.mocked(apiClient.postRoot).mockResolvedValue()

    await localLogin('local-admin', 'correct password')

    expect(apiClient.postRoot).toHaveBeenCalledWith('/auth/local/login', {
      username: 'local-admin',
      password: 'correct password',
    })
  })
})
