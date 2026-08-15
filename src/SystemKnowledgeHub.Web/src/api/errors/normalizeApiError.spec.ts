import { describe, expect, it } from 'vitest'
import { ApiError, UnexpectedResponseError } from './ApiError'
import { normalizeApiError } from './normalizeApiError'

describe('normalizeApiError', () => {
  it('preserves the frozen API business error contract', async () => {
    const response = new Response(
      JSON.stringify({
        code: 'conflict',
        message: '内容已被其他操作修改，请刷新后重试。',
        fieldErrors: null,
        details: { resourceType: 'DatabaseColumn', resourceId: 123 },
      }),
      { status: 409, headers: { 'Content-Type': 'application/json' } },
    )

    const error = await normalizeApiError(response)

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(409)
    expect(error.response.code).toBe('conflict')
    expect(error.response.details).toEqual({
      resourceType: 'DatabaseColumn',
      resourceId: 123,
    })
  })

  it('rejects an unrecognized error payload', async () => {
    const response = new Response(JSON.stringify({ message: 'broken' }), {
      status: 500,
      headers: { 'Content-Type': 'application/json' },
    })

    await expect(normalizeApiError(response)).rejects.toBeInstanceOf(UnexpectedResponseError)
  })
})
