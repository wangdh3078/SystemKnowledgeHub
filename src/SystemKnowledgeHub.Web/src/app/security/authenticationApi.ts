import { apiClient } from '../../api/client/apiClient'

export function logout(): Promise<void> {
  return apiClient.postRoot('/auth/logout')
}
