import { describe, expect, it, vi } from 'vitest'
import { createApiClient } from './apiClient'
import { NetworkRequestError } from '../errors/ApiError'

describe('apiClient', () => {
  it('uses the configured base path and typed decoder', async () => {
    const fetchImplementation = vi.fn<typeof fetch>().mockImplementation(async () => new Response(JSON.stringify({ status: 'ok' }), { status: 200 }))
    const client = createApiClient('/api', fetchImplementation)
    const payload = await client.get('/bootstrap/status', { decode: (value: unknown) => value })
    expect(fetchImplementation).toHaveBeenCalledWith('/api/bootstrap/status', expect.objectContaining({ method: 'GET', credentials: 'include' }))
    expect(payload).toEqual({ status: 'ok' })
  })

  it('adds an antiforgery token only to unsafe requests', async () => {
    const fetchImplementation = vi.fn<typeof fetch>().mockImplementation(async () => new Response(JSON.stringify({ status: 'ok' }), { status: 200 }))
    const client = createApiClient('/api', fetchImplementation, () => 'request-token')
    await client.get('/current-user', { decode: () => ({}) })
    await client.post('/systems', { name: 'MES' }, { decode: () => ({}) })
    expect(fetchImplementation).toHaveBeenNthCalledWith(1, '/api/current-user', expect.objectContaining({ headers: expect.not.objectContaining({ 'X-CSRF-TOKEN': expect.anything() }) }))
    expect(fetchImplementation).toHaveBeenNthCalledWith(2, '/api/systems', expect.objectContaining({ headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'request-token' }) }))
  })

  it('separates network failures from API errors', async () => {
    const client = createApiClient('/api', vi.fn<typeof fetch>().mockRejectedValue(new TypeError('offline')))
    await expect(client.get('/bootstrap/status', { decode: () => ({}) })).rejects.toBeInstanceOf(NetworkRequestError)
  })
})
