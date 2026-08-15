export interface EnvironmentConfig {
  readonly apiBaseUrl: string
}

export type EnvironmentSource = Readonly<Record<string, string | boolean | undefined>>

export function readEnvironment(source: EnvironmentSource): EnvironmentConfig {
  const configuredApiBaseUrl = source.VITE_API_BASE_URL

  if (configuredApiBaseUrl !== undefined && typeof configuredApiBaseUrl !== 'string') {
    throw new Error('VITE_API_BASE_URL 必须是字符串。')
  }

  const apiBaseUrl = (configuredApiBaseUrl ?? '/api').trim()

  if (apiBaseUrl.length === 0) {
    throw new Error('VITE_API_BASE_URL 不能为空。')
  }

  return {
    apiBaseUrl: apiBaseUrl.length > 1 ? apiBaseUrl.replace(/\/+$/, '') : apiBaseUrl,
  }
}

export const environment = readEnvironment(import.meta.env)
