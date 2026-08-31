export const apiErrorCodes = [
  'validation_error',
  'not_found',
  'conflict',
  'invalid_state',
  'reference_invalid',
  'business_rule_violation',
  'unauthenticated',
  'session_expired',
  'forbidden',
  'identity_unmapped',
  'identity_inactive',
  'account_inactive',
  'antiforgery_failed',
  'invalid_credentials',
  'too_many_requests',
  'already_authenticated',
  'must_change_password',
  'password_change_not_available',
  'concurrency_conflict',
  'payload_too_large',
  'unsupported_media_type',
  'attachment_storage_unavailable',
  'attachment_unavailable',
  'preview_not_supported',
  'preview_limit_exceeded',
  'secret_already_set',
  'SecretMissing',
  'SecretUnavailable',
  'Disabled',
  'ProviderUnavailable',
  'ConnectionFailed',
  'AuthenticationFailed',
  'InsufficientPrivilege',
  'UnsupportedDatabaseVersion',
  'Timeout',
  'Cancelled',
  'ConcurrencyConflict',
  'DiscoveryAlreadyRunning',
  'DiscoveryTargetImmutable',
] as const

export type ApiErrorCode = (typeof apiErrorCodes)[number]

export type FieldErrors = Readonly<Record<string, readonly string[]>>

export interface ApiErrorResponse {
  readonly code: ApiErrorCode
  readonly message: string
  readonly fieldErrors: FieldErrors | null
  readonly details: Readonly<Record<string, unknown>> | null
}

export function isApiErrorCode(value: unknown): value is ApiErrorCode {
  return typeof value === 'string' && apiErrorCodes.some((candidate) => candidate === value)
}
