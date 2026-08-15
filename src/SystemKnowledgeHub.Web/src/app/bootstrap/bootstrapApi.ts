import { apiClient } from '../../api/client/apiClient'

export interface BootstrapStatus {
  readonly status: 'ok'
  readonly databaseProvider: 'SQLite'
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

export function decodeBootstrapStatus(value: unknown): BootstrapStatus {
  if (!isRecord(value) || value.status !== 'ok' || value.databaseProvider !== 'SQLite') {
    throw new Error('Bootstrap 状态响应不符合预期。')
  }

  return {
    status: value.status,
    databaseProvider: value.databaseProvider,
  }
}

export function getBootstrapStatus(signal?: AbortSignal): Promise<BootstrapStatus> {
  return apiClient.get('/bootstrap/status', {
    signal,
    decode: decodeBootstrapStatus,
  })
}
