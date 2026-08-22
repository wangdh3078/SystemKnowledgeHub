import { apiClient } from '../../api/client/apiClient'

function readRequestToken(value: unknown): string {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    throw new Error('Antiforgery 响应不符合预期。')
  }

  const token = (value as Record<string, unknown>).requestToken
  if (typeof token !== 'string' || token.length === 0) {
    throw new Error('Antiforgery 响应不包含 request token。')
  }

  return token
}

export function getAntiforgeryToken(): Promise<string> {
  return apiClient.get('/antiforgery/token', { decode: readRequestToken })
}
