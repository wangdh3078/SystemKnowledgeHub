import { apiClient } from '../../api/client/apiClient'

export interface AuthenticationOptions {
  readonly localLoginEnabled: boolean
  readonly oidcLoginEnabled: boolean
  readonly oidcDisplayName: string | null
}

interface LocalLoginRequest {
  readonly username: string
  readonly password: string
}

function readAuthenticationOptions(value: unknown): AuthenticationOptions {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    throw new Error('登录配置响应不符合预期。')
  }

  const options = value as Record<string, unknown>
  if (
    typeof options.localLoginEnabled !== 'boolean'
    || typeof options.oidcLoginEnabled !== 'boolean'
    || (options.oidcDisplayName !== null && typeof options.oidcDisplayName !== 'string')
  ) {
    throw new Error('登录配置响应不符合预期。')
  }

  return {
    localLoginEnabled: options.localLoginEnabled,
    oidcLoginEnabled: options.oidcLoginEnabled,
    oidcDisplayName: options.oidcDisplayName,
  }
}

export function getAuthenticationOptions(): Promise<AuthenticationOptions> {
  return apiClient.get('/auth/options', { decode: readAuthenticationOptions })
}

export function localLogin(username: string, password: string): Promise<void> {
  const request: LocalLoginRequest = { username, password }
  return apiClient.postRoot('/auth/local/login', request)
}

export function startEnterpriseLogin(): void {
  window.location.assign('/auth/login')
}

export function logout(): Promise<void> {
  return apiClient.postRoot('/auth/logout')
}
