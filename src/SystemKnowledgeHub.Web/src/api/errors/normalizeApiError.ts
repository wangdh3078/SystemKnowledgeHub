import { isApiErrorCode, type ApiErrorResponse, type FieldErrors } from '../contracts/errors'
import { ApiError, UnexpectedResponseError } from './ApiError'

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function readFieldErrors(value: unknown): FieldErrors | null | undefined {
  if (value === null) {
    return null
  }

  if (!isRecord(value)) {
    return undefined
  }

  const parsed: Record<string, readonly string[]> = {}

  for (const [field, messages] of Object.entries(value)) {
    if (!Array.isArray(messages) || !messages.every((message) => typeof message === 'string')) {
      return undefined
    }

    parsed[field] = messages
  }

  return parsed
}

export function parseApiErrorResponse(value: unknown): ApiErrorResponse | null {
  if (!isRecord(value)) {
    return null
  }

  const fieldErrors = readFieldErrors(value.fieldErrors)
  const details =
    value.details === null ? null : isRecord(value.details) ? value.details : undefined

  if (
    !isApiErrorCode(value.code) ||
    typeof value.message !== 'string' ||
    fieldErrors === undefined ||
    details === undefined
  ) {
    return null
  }

  return {
    code: value.code,
    message: value.message,
    fieldErrors,
    details,
  }
}

export async function normalizeApiError(response: Response): Promise<ApiError> {
  let payload: unknown

  try {
    payload = await response.json()
  } catch {
    throw new UnexpectedResponseError(`请求失败（HTTP ${response.status}），且响应不是有效 JSON。`)
  }

  const errorResponse = parseApiErrorResponse(payload)

  if (errorResponse === null) {
    throw new UnexpectedResponseError(
      `请求失败（HTTP ${response.status}），且错误结构不符合 API 契约。`,
    )
  }

  return new ApiError(response.status, errorResponse)
}
