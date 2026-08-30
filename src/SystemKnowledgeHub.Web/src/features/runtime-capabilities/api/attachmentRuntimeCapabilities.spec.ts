import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiClient } from '../../../api/client/apiClient'
import {
  clearAttachmentRuntimeCapabilitiesCache,
  decodeAttachmentRuntimeCapabilities,
  getAttachmentRuntimeCapabilities,
  type AttachmentRuntimeCapabilities,
} from './attachmentRuntimeCapabilities'

vi.mock('../../../api/client/apiClient', () => ({
  apiClient: { get: vi.fn() },
}))

const capabilities: AttachmentRuntimeCapabilities = {
  allowedImageExtensions: ['.png', '.jpg'],
  allowedFileExtensions: ['.pdf', '.txt'],
  maxImageBytes: 10 * 1024 * 1024,
  maxFileBytes: 50 * 1024 * 1024,
  maxStoredAttachmentsPerDocument: 100,
}

describe('attachment runtime capabilities', () => {
  beforeEach(() => {
    clearAttachmentRuntimeCapabilitiesCache()
    vi.mocked(apiClient.get).mockReset()
  })

  it('decodes the public attachment capability boundary strictly', () => {
    expect(decodeAttachmentRuntimeCapabilities(capabilities)).toEqual(capabilities)
    expect(() =>
      decodeAttachmentRuntimeCapabilities({
        ...capabilities,
        allowedImageExtensions: ['png'],
      }),
    ).toThrow('canonical extension')
    expect(() =>
      decodeAttachmentRuntimeCapabilities({
        ...capabilities,
        allowedFileExtensions: ['.pdf', '.pdf'],
      }),
    ).toThrow('must not contain duplicates')
    expect(() => decodeAttachmentRuntimeCapabilities({ ...capabilities, maxFileBytes: 0 })).toThrow(
      'positive safe integer',
    )
  })

  it('shares one in-flight request and clears a failed cache entry for retry', async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce(capabilities)

    const first = getAttachmentRuntimeCapabilities()
    const second = getAttachmentRuntimeCapabilities()

    await expect(first).resolves.toEqual(capabilities)
    await expect(second).resolves.toEqual(capabilities)
    expect(apiClient.get).toHaveBeenCalledTimes(1)
    expect(apiClient.get).toHaveBeenCalledWith('/runtime-capabilities/attachments', {
      decode: decodeAttachmentRuntimeCapabilities,
    })

    clearAttachmentRuntimeCapabilitiesCache()
    vi.mocked(apiClient.get)
      .mockRejectedValueOnce(new Error('offline'))
      .mockResolvedValueOnce(capabilities)

    await expect(getAttachmentRuntimeCapabilities()).rejects.toThrow('offline')
    await expect(getAttachmentRuntimeCapabilities()).resolves.toEqual(capabilities)
    expect(apiClient.get).toHaveBeenCalledTimes(3)
  })
})
