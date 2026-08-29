import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiClient } from '../../../api/client/apiClient'
import {
  checkAdministratorAttachmentIntegrity,
  deleteAdministratorAttachment,
  getAdministratorAttachment,
  getAdministratorAttachments,
} from './administratorAttachmentsApi'

vi.mock('../../../api/client/apiClient', () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    deleteWithBody: vi.fn(),
  },
}))

describe('administrator attachment API', () => {
  beforeEach(() => {
    vi.mocked(apiClient.get).mockReset()
    vi.mocked(apiClient.post).mockReset()
    vi.mocked(apiClient.deleteWithBody).mockReset()
  })

  it('encodes list filters through the typed admin boundary', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ items: [] } as never)

    await getAdministratorAttachments({
      query: 'MES 图纸',
      kind: 'Image',
      extension: '.png',
      referenceStatus: 'HistoricalOnly',
      storageState: 'DeletePending',
      page: 2,
      pageSize: 20,
    })

    expect(apiClient.get).toHaveBeenCalledWith(
      '/admin/attachments?page=2&pageSize=20&query=MES+%E5%9B%BE%E7%BA%B8&kind=Image&extension=.png&referenceStatus=HistoricalOnly&storageState=DeletePending',
      expect.objectContaining({ decode: expect.any(Function) }),
    )
  })

  it('uses explicit single-item detail, integrity and versioned delete endpoints', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({} as never)
    vi.mocked(apiClient.post).mockResolvedValue({} as never)
    vi.mocked(apiClient.deleteWithBody).mockResolvedValue(undefined as never)

    await getAdministratorAttachment(17)
    await checkAdministratorAttachmentIntegrity(17)
    await deleteAdministratorAttachment(17, 'version-5')

    expect(apiClient.get).toHaveBeenCalledWith(
      '/admin/attachments/17',
      expect.objectContaining({ decode: expect.any(Function) }),
    )
    expect(apiClient.post).toHaveBeenCalledWith(
      '/admin/attachments/17/integrity-check',
      {},
      expect.objectContaining({ decode: expect.any(Function) }),
    )
    expect(apiClient.deleteWithBody).toHaveBeenCalledWith(
      '/admin/attachments/17',
      { concurrencyToken: 'version-5' },
      expect.objectContaining({ decode: expect.any(Function) }),
    )
  })

  it('rejects unsafe IDs before issuing a request', async () => {
    await expect(getAdministratorAttachment(0)).rejects.toThrow('附件 ID 无效')
    await expect(
      checkAdministratorAttachmentIntegrity(Number.MAX_SAFE_INTEGER + 1),
    ).rejects.toThrow('附件 ID 无效')
    await expect(deleteAdministratorAttachment(-1, 'version')).rejects.toThrow('附件 ID 无效')
    expect(apiClient.get).not.toHaveBeenCalled()
    expect(apiClient.post).not.toHaveBeenCalled()
    expect(apiClient.deleteWithBody).not.toHaveBeenCalled()
  })
})
