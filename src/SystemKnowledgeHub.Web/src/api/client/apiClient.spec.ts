import { describe, expect, it, vi } from 'vitest'
import { createApiClient } from './apiClient'
import { NetworkRequestError } from '../errors/ApiError'

describe('apiClient', () => {
  it('uses the configured base path and typed decoder', async () => {
    const fetchImplementation = vi
      .fn<typeof fetch>()
      .mockImplementation(
        async () => new Response(JSON.stringify({ status: 'ok' }), { status: 200 }),
      )
    const client = createApiClient('/api', fetchImplementation)
    const payload = await client.get('/bootstrap/status', { decode: (value: unknown) => value })
    expect(fetchImplementation).toHaveBeenCalledWith(
      '/api/bootstrap/status',
      expect.objectContaining({ method: 'GET', credentials: 'include' }),
    )
    expect(payload).toEqual({ status: 'ok' })
  })

  it('adds an antiforgery token only to unsafe requests', async () => {
    const fetchImplementation = vi
      .fn<typeof fetch>()
      .mockImplementation(
        async () => new Response(JSON.stringify({ status: 'ok' }), { status: 200 }),
      )
    const client = createApiClient('/api', fetchImplementation, () => 'request-token')
    await client.get('/current-user', { decode: () => ({}) })
    await client.post('/systems', { name: 'MES' }, { decode: () => ({}) })
    expect(fetchImplementation).toHaveBeenNthCalledWith(
      1,
      '/api/current-user',
      expect.objectContaining({
        headers: expect.not.objectContaining({ 'X-CSRF-TOKEN': expect.anything() }),
      }),
    )
    expect(fetchImplementation).toHaveBeenNthCalledWith(
      2,
      '/api/systems',
      expect.objectContaining({
        headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'request-token' }),
      }),
    )
  })

  it('posts multipart form data with antiforgery without overriding the browser boundary header', async () => {
    const fetchImplementation = vi
      .fn<typeof fetch>()
      .mockImplementation(
        async () => new Response(JSON.stringify({ attachmentId: 9 }), { status: 201 }),
      )
    const client = createApiClient('/api', fetchImplementation, () => 'request-token')
    const form = new FormData()
    form.append('file', new File(['png'], 'diagram.png', { type: 'image/png' }))

    await client.postForm('/knowledge-documents/7/attachments', form, {
      decode: (value: unknown) => value,
    })

    const request = fetchImplementation.mock.calls[0]?.[1]
    expect(request?.body).toBe(form)
    expect(request?.headers).toEqual(
      expect.objectContaining({
        Accept: 'application/json',
        'X-CSRF-TOKEN': 'request-token',
      }),
    )
    expect(request?.headers).not.toEqual(
      expect.objectContaining({ 'Content-Type': expect.anything() }),
    )
  })

  it('separates network failures from API errors', async () => {
    const client = createApiClient(
      '/api',
      vi.fn<typeof fetch>().mockRejectedValue(new TypeError('offline')),
    )
    await expect(client.get('/bootstrap/status', { decode: () => ({}) })).rejects.toBeInstanceOf(
      NetworkRequestError,
    )
  })

  it('sends body and antiforgery token for root POST endpoints that return no content', async () => {
    const fetchImplementation = vi
      .fn<typeof fetch>()
      .mockResolvedValue(new Response(null, { status: 204 }))
    const client = createApiClient('/api', fetchImplementation, () => 'request-token')

    await client.postRoot('/auth/local/login', { username: 'local-admin', password: 'secret' })

    expect(fetchImplementation).toHaveBeenCalledWith(
      '/auth/local/login',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ username: 'local-admin', password: 'secret' }),
        headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'request-token' }),
        credentials: 'include',
      }),
    )
  })

  it('sends a DELETE body with antiforgery and accepts a 204 response without parsing JSON', async () => {
    const fetchImplementation = vi
      .fn<typeof fetch>()
      .mockResolvedValue(new Response(null, { status: 204 }))
    const decode = vi.fn(() => undefined)
    const client = createApiClient('/api', fetchImplementation, () => 'request-token')

    await client.deleteWithBody('/systems/7', { concurrencyToken: 'opaque-token' }, { decode })

    expect(fetchImplementation).toHaveBeenCalledWith(
      '/api/systems/7',
      expect.objectContaining({
        method: 'DELETE',
        body: JSON.stringify({ concurrencyToken: 'opaque-token' }),
        headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'request-token' }),
        credentials: 'include',
      }),
    )
    expect(decode).toHaveBeenCalledWith(undefined)
  })
})
