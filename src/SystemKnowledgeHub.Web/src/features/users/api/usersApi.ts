import { apiClient } from '../../../api/client/apiClient'
import type { ActorContext } from '../../../app/stores/actor'
import {
  decodeCurrentUser,
  decodeLoginIdentities,
  decodeKnowledgeRole,
  decodeKnowledgeRoles,
  decodeUserDetail,
  decodeUserLoginMethods,
  decodeUserLoginSetupOptions,
  decodeUserAccessLevel,
  decodeUsersList,
  type CurrentUserProfile,
  type CreateUserRequest,
  type KnowledgeRole,
  type LoginIdentity,
  type LocalLoginMethod,
  type KnowledgeRoleWriteRequest,
  type SetActiveStateRequest,
  type UpdateKnowledgeRoleRequest,
  type UpdateUserRequest,
  type UserDetail,
  type UserLoginMethods,
  type UserLoginSetupOptions,
  type UserAccessLevelResponse,
  type UsersListResponse,
  type UsersSort,
} from './userContracts'

export function getCurrentUser(signal?: AbortSignal): Promise<CurrentUserProfile> {
  return apiClient.get('/current-user', { signal, decode: decodeCurrentUser })
}

export function changeMyLocalPassword(currentPassword: string, newPassword: string): Promise<void> {
  return apiClient.put('/current-user/password', { currentPassword, newPassword }, {
    decode: () => undefined,
  })
}

export interface UsersListParameters {
  readonly keyword?: string
  readonly isActive?: boolean
  readonly sort: UsersSort
  readonly page: number
  readonly pageSize: number
}

export function getUsersList(parameters: UsersListParameters, signal?: AbortSignal): Promise<UsersListResponse> {
  const query = new URLSearchParams({
    sort: parameters.sort,
    page: String(parameters.page),
    pageSize: String(parameters.pageSize),
  })
  if (parameters.keyword) query.set('keyword', parameters.keyword)
  if (parameters.isActive !== undefined) query.set('isActive', String(parameters.isActive))
  return apiClient.get(`/users?${query.toString()}`, { signal, decode: decodeUsersList })
}

export function getUser(userId: number, signal?: AbortSignal): Promise<UserDetail> {
  return apiClient.get(`/users/${userId}`, { signal, decode: decodeUserDetail })
}

export function createUser(request: CreateUserRequest): Promise<UserDetail> {
  return apiClient.post('/users', request, { decode: decodeUserDetail })
}

export function getUserLoginSetupOptions(signal?: AbortSignal): Promise<UserLoginSetupOptions> {
  return apiClient.get('/users/login-setup-options', { signal, decode: decodeUserLoginSetupOptions })
}

export function getUserLoginMethods(userId: number, signal?: AbortSignal): Promise<UserLoginMethods> {
  return apiClient.get(`/users/${userId}/login-methods`, { signal, decode: decodeUserLoginMethods })
}

export function createUserLocalCredential(
  userId: number,
  username: string,
  initialPassword: string,
): Promise<LocalLoginMethod> {
  return apiClient.post(`/users/${userId}/local-credential`, { username, initialPassword }, {
    decode: (value) => decodeUserLoginMethods({
      userId,
      local: value,
      oidc: [],
    }).local,
  })
}

export function setLocalCredentialActiveState(
  userId: number,
  local: LocalLoginMethod,
  isActive: boolean,
): Promise<LocalLoginMethod> {
  if (!local.concurrencyToken) throw new Error('本地账号并发标记缺失，请重新加载。')
  return apiClient.put(`/users/${userId}/local-credential/active-state`, {
    isActive,
    concurrencyToken: local.concurrencyToken,
  }, {
    decode: (value) => decodeUserLoginMethods({
      userId,
      local: value,
      oidc: [],
    }).local,
  })
}

export function resetUserLocalPassword(
  userId: number,
  local: LocalLoginMethod,
  newPassword: string,
): Promise<LocalLoginMethod> {
  if (!local.concurrencyToken) throw new Error('本地账号并发标记缺失，请重新加载。')
  return apiClient.post(`/users/${userId}/local-credential/reset-password`, {
    newPassword,
    credentialConcurrencyToken: local.concurrencyToken,
  }, {
    decode: (value) => decodeUserLoginMethods({
      userId,
      local: value,
      oidc: [],
    }).local,
  })
}

export function updateUser(userId: number, request: UpdateUserRequest): Promise<UserDetail> {
  return apiClient.put(`/users/${userId}`, request, { decode: decodeUserDetail })
}

export function setUserActiveState(
  userId: number,
  isActive: boolean,
  concurrencyToken: string,
  actor: ActorContext,
): Promise<UserDetail> {
  const request: SetActiveStateRequest = { isActive, concurrencyToken, actor }
  return apiClient.put(`/users/${userId}/active-state`, request, { decode: decodeUserDetail })
}

export function getLoginIdentities(userId: number, signal?: AbortSignal): Promise<readonly LoginIdentity[]> {
  return apiClient.get(`/users/${userId}/login-identities`, { signal, decode: decodeLoginIdentities })
}

export function createLoginIdentity(
  userId: number,
  request: { readonly provider: string; readonly subject: string },
): Promise<LoginIdentity> {
  return apiClient.post(`/users/${userId}/login-identities`, request, {
    decode: (value) => decodeLoginIdentities([value])[0]!,
  })
}

export function setLoginIdentityActiveState(
  userId: number,
  loginIdentity: LoginIdentity,
  isActive: boolean,
): Promise<LoginIdentity> {
  return apiClient.put(`/users/${userId}/login-identities/${loginIdentity.id}/active-state`, {
    isActive,
    concurrencyToken: loginIdentity.concurrencyToken,
  }, { decode: (value) => decodeLoginIdentities([value])[0]! })
}

export function setUserAccessLevel(
  userId: number,
  accessLevel: import('./userContracts').AccessLevel,
  concurrencyToken: string,
): Promise<UserAccessLevelResponse> {
  return apiClient.put(`/users/${userId}/access-level`, { accessLevel, concurrencyToken }, {
    decode: decodeUserAccessLevel,
  })
}

export function getKnowledgeRoles(isActive?: boolean, signal?: AbortSignal): Promise<readonly KnowledgeRole[]> {
  const query = isActive === undefined ? '' : `?isActive=${String(isActive)}`
  return apiClient.get(`/knowledge-roles${query}`, { signal, decode: decodeKnowledgeRoles })
}

export function createKnowledgeRole(request: KnowledgeRoleWriteRequest): Promise<KnowledgeRole> {
  return apiClient.post('/knowledge-roles', request, { decode: decodeKnowledgeRole })
}

export function updateKnowledgeRole(
  knowledgeRoleId: number,
  request: UpdateKnowledgeRoleRequest,
): Promise<KnowledgeRole> {
  return apiClient.put(`/knowledge-roles/${knowledgeRoleId}`, request, { decode: decodeKnowledgeRole })
}

export function setKnowledgeRoleActiveState(
  knowledgeRoleId: number,
  isActive: boolean,
  concurrencyToken: string,
  actor: ActorContext,
): Promise<KnowledgeRole> {
  const request: SetActiveStateRequest = { isActive, concurrencyToken, actor }
  return apiClient.put(`/knowledge-roles/${knowledgeRoleId}/active-state`, request, { decode: decodeKnowledgeRole })
}
