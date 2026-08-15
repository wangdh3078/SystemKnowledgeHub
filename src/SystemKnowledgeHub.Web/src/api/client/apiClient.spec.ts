import { describe, expect, it, vi } from 'vitest'
import { createApiClient } from './apiClient'
import { NetworkRequestError } from '../errors/ApiError'

describe('apiClient', () => {
  it('uses the configured base path and typed decoder', async () => {
    const fetchImplementation = vi.fn<typeof fetch>().mockResolvedValue(
      new Response(JSON.stringify({ status: 'ok' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
    const client = createApiClient('/api', fetchImplementation)

    const payload = await client.get('/bootstrap/status', {
      decode(value: unknown) {
        if (
          typeof value !== 'object' ||
          value === null ||
          !('status' in value) ||
          value.status !== 'ok'
        ) {
          throw new Error('invalid')
        }

        return { status: value.status }
      },
    })

    expect(fetchImplementation).toHaveBeenCalledWith(
      '/api/bootstrap/status',
      expect.objectContaining({ method: 'GET' }),
    )
    expect(payload).toEqual({ status: 'ok' })
  })

  it('separates network failures from API errors', async () => {
    const fetchImplementation = vi.fn<typeof fetch>().mockRejectedValue(new TypeError('offline'))
    const client = createApiClient('/api', fetchImplementation)

    await expect(client.get('/bootstrap/status', { decode: () => ({}) })).rejects.toBeInstanceOf(
      NetworkRequestError,
    )
  })
})
