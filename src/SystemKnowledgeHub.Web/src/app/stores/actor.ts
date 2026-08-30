import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { ApiError } from '../../api/errors/ApiError'
import { getCurrentUser } from '../../features/users/api/usersApi'
import type { AccessLevel, CurrentUserProfile } from '../../features/users/api/userContracts'
import { getAntiforgeryToken } from '../security/securityApi'

export interface ActorContext {
  readonly displayName: string
  readonly role: string | null
}

export type AuthStatus =
  | 'loading'
  | 'authenticated'
  | 'unauthenticated'
  | 'session-expired'
  | 'identity-unmapped'
  | 'identity-inactive'
  | 'account-inactive'
  | 'error'

type AuthFailureStatus = Exclude<AuthStatus, 'loading' | 'authenticated'>

const accessRanks: Readonly<Record<AccessLevel, number>> = {
  Viewer: 0,
  Editor: 1,
  Administrator: 2,
}

function resolveAuthStatus(error: unknown): AuthFailureStatus {
  if (!(error instanceof ApiError)) return 'error'

  switch (error.response.code) {
    case 'session_expired': return 'session-expired'
    case 'identity_unmapped': return 'identity-unmapped'
    case 'identity_inactive': return 'identity-inactive'
    case 'account_inactive': return 'account-inactive'
    case 'unauthenticated': return 'unauthenticated'
    default: return error.status === 401 ? 'unauthenticated' : 'error'
  }
}

export const useActorStore = defineStore('actor', () => {
  const currentUser = ref<CurrentUserProfile | null>(null)
  const authStatus = ref<AuthStatus>('loading')
  const initialized = ref(false)
  const antiforgeryToken = ref<string | null>(null)
  const message = ref<string | null>(null)
  let initialization: Promise<boolean> | null = null

  const loading = computed(() => authStatus.value === 'loading')
  const isAuthenticated = computed(() => authStatus.value === 'authenticated' && currentUser.value !== null)
  const accessLevel = computed<AccessLevel | null>(() => currentUser.value?.accessLevel ?? null)
  const canEdit = computed(() => accessLevel.value !== null && accessRanks[accessLevel.value] >= accessRanks.Editor)
  const isAdministrator = computed(() => accessLevel.value === 'Administrator')
  const mustChangePassword = computed(() => currentUser.value?.mustChangePassword === true)
  const authenticationMethod = computed(() => currentUser.value?.authenticationMethod ?? null)
  const actor = computed<ActorContext>(() => {
    const user = currentUser.value
    const activeRole = user?.knowledgeRoles.find((role) => role.isActive)
    return {
      displayName: user?.displayName ?? '未认证用户',
      role: activeRole?.name ?? null,
    }
  })
  const displayName = computed(() => actor.value.displayName)
  const role = computed(() => actor.value.role)

  function hasMinimumAccessLevel(minimum: AccessLevel): boolean {
    return accessLevel.value !== null && accessRanks[accessLevel.value] >= accessRanks[minimum]
  }

  function clearCurrentUser(
    status: AuthFailureStatus = 'unauthenticated',
    errorMessage: string | null = null,
  ): void {
    currentUser.value = null
    antiforgeryToken.value = null
    authStatus.value = status
    message.value = errorMessage
  }

  async function refreshAntiforgeryToken(): Promise<boolean> {
    try {
      antiforgeryToken.value = await getAntiforgeryToken()
      return true
    } catch (error: unknown) {
      if (isAuthenticated.value) handleSecurityError(error)
      message.value = error instanceof Error ? error.message : '无法建立请求验证，请重试。'
      return false
    }
  }

  async function loadCurrentUser(): Promise<boolean> {
    authStatus.value = 'loading'
    message.value = null
    try {
      currentUser.value = await getCurrentUser()
      authStatus.value = 'authenticated'
      initialized.value = true
      await refreshAntiforgeryToken()
      return isAuthenticated.value
    } catch (error: unknown) {
      initialized.value = true
      clearCurrentUser(resolveAuthStatus(error), error instanceof Error ? error.message : '无法加载当前用户。')
      return false
    }
  }

  async function initialize(): Promise<boolean> {
    if (initialized.value) return isAuthenticated.value
    if (initialization !== null) return initialization
    initialization = loadCurrentUser().finally(() => { initialization = null })
    return initialization
  }

  async function refreshCurrentUser(): Promise<boolean> {
    return loadCurrentUser()
  }

  function handleSecurityError(error: unknown): void {
    if (error instanceof ApiError && error.response.code === 'must_change_password' && isAuthenticated.value) {
      void loadCurrentUser()
      return
    }
    const status = resolveAuthStatus(error)
    if (status !== 'error') {
      clearCurrentUser(status, error instanceof Error ? error.message : null)
      return
    }

    if (error instanceof ApiError && error.response.code === 'forbidden' && isAuthenticated.value) {
      void loadCurrentUser()
    }
  }

  return {
    currentUser,
    authStatus,
    initialized,
    antiforgeryToken,
    message,
    loading,
    isAuthenticated,
    accessLevel,
    canEdit,
    isAdministrator,
    mustChangePassword,
    authenticationMethod,
    actor,
    displayName,
    role,
    hasMinimumAccessLevel,
    initialize,
    loadCurrentUser,
    refreshCurrentUser,
    refreshAntiforgeryToken,
    clearCurrentUser,
    handleSecurityError,
  }
})
