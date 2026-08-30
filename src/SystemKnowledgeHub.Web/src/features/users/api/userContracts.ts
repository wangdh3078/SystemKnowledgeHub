import { isSafeApiId } from '../../../api/contracts/id'
import type { ActorContext } from '../../../app/stores/actor'

export type UsersSort = 'displayName:asc' | 'displayName:desc' | 'updatedAt:asc' | 'updatedAt:desc'

export interface KnowledgeRoleSummary {
  readonly id: number
  readonly name: string
  readonly description: string | null
  readonly isActive: boolean
}

export interface UserSummary {
  readonly id: number
  readonly employeeNo: string | null
  readonly displayName: string
  readonly email: string | null
  readonly departmentOrTeam: string | null
  readonly jobTitle: string | null
  readonly isActive: boolean
  readonly knowledgeRoles: readonly KnowledgeRoleSummary[]
  readonly updatedAt: string
}

export interface UsersListResponse {
  readonly items: readonly UserSummary[]
  readonly page: number
  readonly pageSize: number
  readonly total: number
}

export interface UserDetail extends UserSummary {
  readonly createdAt: string
  readonly concurrencyToken: string
}

export interface CurrentUserProfile {
  readonly id: number
  readonly employeeNo: string | null
  readonly displayName: string
  readonly email: string | null
  readonly departmentOrTeam: string | null
  readonly jobTitle: string | null
  readonly isActive: boolean
  readonly knowledgeRoles: readonly KnowledgeRoleSummary[]
  readonly accessLevel: AccessLevel
  readonly authenticationMethod: AuthenticationMethod
  readonly mustChangePassword: boolean
}

export type AccessLevel = 'Viewer' | 'Editor' | 'Administrator'
export type AuthenticationMethod = 'local' | 'oidc'

export interface KnowledgeRole extends KnowledgeRoleSummary {
  readonly updatedAt: string
  readonly concurrencyToken: string
}

export interface LoginIdentity {
  readonly id: number
  readonly userId: number
  readonly provider: string
  readonly subject: string
  readonly isActive: boolean
  readonly createdAt: string
  readonly updatedAt: string
  readonly concurrencyToken: string
}

export interface UserWriteRequest {
  readonly employeeNo: string | null
  readonly displayName: string
  readonly email: string | null
  readonly departmentOrTeam: string | null
  readonly jobTitle: string | null
  readonly knowledgeRoleIds: readonly number[]
  readonly actor: ActorContext
}

export interface UpdateUserRequest extends UserWriteRequest {
  readonly concurrencyToken: string
}

export interface SetActiveStateRequest {
  readonly isActive: boolean
  readonly actor: ActorContext
  readonly concurrencyToken: string
}

export interface UserAccessLevelResponse {
  readonly userId: number
  readonly accessLevel: AccessLevel
  readonly concurrencyToken: string
}

export interface KnowledgeRoleWriteRequest {
  readonly name: string
  readonly description: string | null
  readonly actor: ActorContext
}

export interface UpdateKnowledgeRoleRequest extends KnowledgeRoleWriteRequest {
  readonly concurrencyToken: string
}

function readObject(value: unknown, field: string): Record<string, unknown> {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    throw new Error(`${field} must be an object`)
  }
  return value as Record<string, unknown>
}

function readString(value: unknown, field: string): string {
  if (typeof value !== 'string') throw new Error(`${field} must be a string`)
  return value
}

function readNullableString(value: unknown, field: string): string | null {
  if (value === null) return null
  return readString(value, field)
}

function readBoolean(value: unknown, field: string): boolean {
  if (typeof value !== 'boolean') throw new Error(`${field} must be a boolean`)
  return value
}

function readAccessLevel(value: unknown, field: string): AccessLevel {
  if (value === 'Viewer' || value === 'Editor' || value === 'Administrator') return value
  throw new Error(`${field} must be a supported access level`)
}

function readAuthenticationMethod(value: unknown, field: string): AuthenticationMethod {
  if (value === 'local' || value === 'oidc') return value
  throw new Error(`${field} must be a supported authentication method`)
}

function readInteger(value: unknown, field: string, minimum = 0): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < minimum) {
    throw new Error(`${field} must be a safe integer`)
  }
  return value
}

function readId(value: unknown, field: string): number {
  const id = readInteger(value, field, 1)
  if (!isSafeApiId(id)) throw new Error(`${field} must be a safe API id`)
  return id
}

function decodeRoleSummary(value: unknown, field: string): KnowledgeRoleSummary {
  const root = readObject(value, field)
  return {
    id: readId(root.id, `${field}.id`),
    name: readString(root.name, `${field}.name`),
    description: readNullableString(root.description, `${field}.description`),
    isActive: readBoolean(root.isActive, `${field}.isActive`),
  }
}

function decodeUserSummary(value: unknown, field: string): UserSummary {
  const root = readObject(value, field)
  if (!Array.isArray(root.knowledgeRoles)) throw new Error(`${field}.knowledgeRoles must be an array`)
  return {
    id: readId(root.id, `${field}.id`),
    employeeNo: readNullableString(root.employeeNo, `${field}.employeeNo`),
    displayName: readString(root.displayName, `${field}.displayName`),
    email: readNullableString(root.email, `${field}.email`),
    departmentOrTeam: readNullableString(root.departmentOrTeam, `${field}.departmentOrTeam`),
    jobTitle: readNullableString(root.jobTitle, `${field}.jobTitle`),
    isActive: readBoolean(root.isActive, `${field}.isActive`),
    knowledgeRoles: root.knowledgeRoles.map((role, index) => decodeRoleSummary(role, `${field}.knowledgeRoles[${index}]`)),
    updatedAt: readString(root.updatedAt, `${field}.updatedAt`),
  }
}

export function decodeUsersList(value: unknown): UsersListResponse {
  const root = readObject(value, 'usersList')
  if (!Array.isArray(root.items)) throw new Error('usersList.items must be an array')
  return {
    items: root.items.map((item, index) => decodeUserSummary(item, `usersList.items[${index}]`)),
    page: readInteger(root.page, 'usersList.page', 1),
    pageSize: readInteger(root.pageSize, 'usersList.pageSize', 1),
    total: readInteger(root.total, 'usersList.total'),
  }
}

export function decodeUserDetail(value: unknown): UserDetail {
  const summary = decodeUserSummary(value, 'userDetail')
  const root = readObject(value, 'userDetail')
  return {
    ...summary,
    createdAt: readString(root.createdAt, 'userDetail.createdAt'),
    concurrencyToken: readString(root.concurrencyToken, 'userDetail.concurrencyToken'),
  }
}

export function decodeCurrentUser(value: unknown): CurrentUserProfile {
  const root = readObject(value, 'currentUser')
  if (!Array.isArray(root.knowledgeRoles)) {
    throw new Error('currentUser.knowledgeRoles must be an array')
  }
  return {
    id: readId(root.id, 'currentUser.id'),
    employeeNo: readNullableString(root.employeeNo, 'currentUser.employeeNo'),
    displayName: readString(root.displayName, 'currentUser.displayName'),
    email: readNullableString(root.email, 'currentUser.email'),
    departmentOrTeam: readNullableString(root.departmentOrTeam, 'currentUser.departmentOrTeam'),
    jobTitle: readNullableString(root.jobTitle, 'currentUser.jobTitle'),
    isActive: readBoolean(root.isActive, 'currentUser.isActive'),
    knowledgeRoles: root.knowledgeRoles.map((role, index) =>
      decodeRoleSummary(role, `currentUser.knowledgeRoles[${index}]`),
    ),
    accessLevel: readAccessLevel(root.accessLevel, 'currentUser.accessLevel'),
    authenticationMethod: readAuthenticationMethod(root.authenticationMethod, 'currentUser.authenticationMethod'),
    mustChangePassword: readBoolean(root.mustChangePassword, 'currentUser.mustChangePassword'),
  }
}

export function decodeKnowledgeRoles(value: unknown): readonly KnowledgeRole[] {
  if (!Array.isArray(value)) throw new Error('knowledgeRoles must be an array')
  return value.map((item, index) => {
    const field = `knowledgeRoles[${index}]`
    const summary = decodeRoleSummary(item, field)
    const root = readObject(item, field)
    return {
      ...summary,
      updatedAt: readString(root.updatedAt, `${field}.updatedAt`),
      concurrencyToken: readString(root.concurrencyToken, `${field}.concurrencyToken`),
    }
  })
}

export function decodeKnowledgeRole(value: unknown): KnowledgeRole {
  const root = readObject(value, 'knowledgeRole')
  return {
    ...decodeRoleSummary(value, 'knowledgeRole'),
    updatedAt: readString(root.updatedAt, 'knowledgeRole.updatedAt'),
    concurrencyToken: readString(root.concurrencyToken, 'knowledgeRole.concurrencyToken'),
  }
}

export function decodeLoginIdentities(value: unknown): readonly LoginIdentity[] {
  if (!Array.isArray(value)) throw new Error('loginIdentities must be an array')
  return value.map((item, index) => {
    const field = `loginIdentities[${index}]`
    const root = readObject(item, field)
    return {
      id: readId(root.id, `${field}.id`),
      userId: readId(root.userId, `${field}.userId`),
      provider: readString(root.provider, `${field}.provider`),
      subject: readString(root.subject, `${field}.subject`),
      isActive: readBoolean(root.isActive, `${field}.isActive`),
      createdAt: readString(root.createdAt, `${field}.createdAt`),
      updatedAt: readString(root.updatedAt, `${field}.updatedAt`),
      concurrencyToken: readString(root.concurrencyToken, `${field}.concurrencyToken`),
    }
  })
}

export function decodeUserAccessLevel(value: unknown): UserAccessLevelResponse {
  const root = readObject(value, 'userAccessLevel')
  return {
    userId: readId(root.userId, 'userAccessLevel.userId'),
    accessLevel: readAccessLevel(root.accessLevel, 'userAccessLevel.accessLevel'),
    concurrencyToken: readString(root.concurrencyToken, 'userAccessLevel.concurrencyToken'),
  }
}
