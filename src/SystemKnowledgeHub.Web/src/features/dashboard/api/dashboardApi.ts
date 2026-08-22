import { apiClient } from '../../../api/client/apiClient'
import { decodeDashboard, type DashboardResponse } from './dashboardContracts'

export function getDashboard(systemId?: number, signal?: AbortSignal): Promise<DashboardResponse> {
  const query = systemId === undefined ? '' : `?systemId=${encodeURIComponent(String(systemId))}`
  return apiClient.get(`/dashboard${query}`, { signal, decode: decodeDashboard })
}
