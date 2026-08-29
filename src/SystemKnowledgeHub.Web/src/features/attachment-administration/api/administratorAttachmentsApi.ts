import { apiClient } from '../../../api/client/apiClient'
import { isSafeApiId } from '../../../api/contracts/id'
import {
  decodeAdministratorAttachmentDetail,
  decodeAdministratorAttachmentIntegrity,
  decodeAdministratorAttachmentList,
  decodeAdministratorAttachmentStatistics,
  type AdministratorAttachmentDetail,
  type AdministratorAttachmentIntegrity,
  type AdministratorAttachmentListResponse,
  type AdministratorAttachmentReferenceFilter,
  type AdministratorAttachmentStatistics,
} from './administratorAttachmentContracts'
import type { AttachmentKind } from '../../knowledge-documents/api/attachmentContracts'

export interface AdministratorAttachmentListParameters {
  readonly query?: string
  readonly kind?: AttachmentKind
  readonly extension?: string
  readonly referenceStatus?: Exclude<AdministratorAttachmentReferenceFilter, ''>
  readonly storageState?: 'Ready' | 'DeletePending'
  readonly page: number
  readonly pageSize: number
}

export function getAdministratorAttachments(
  parameters: AdministratorAttachmentListParameters,
  signal?: AbortSignal,
): Promise<AdministratorAttachmentListResponse> {
  const query = new URLSearchParams({
    page: String(parameters.page),
    pageSize: String(parameters.pageSize),
  })
  if (parameters.query) query.set('query', parameters.query)
  if (parameters.kind) query.set('kind', parameters.kind)
  if (parameters.extension) query.set('extension', parameters.extension)
  if (parameters.referenceStatus) query.set('referenceStatus', parameters.referenceStatus)
  if (parameters.storageState) query.set('storageState', parameters.storageState)
  return apiClient.get(`/admin/attachments?${query.toString()}`, {
    signal,
    decode: decodeAdministratorAttachmentList,
  })
}

export function getAdministratorAttachmentStatistics(
  signal?: AbortSignal,
): Promise<AdministratorAttachmentStatistics> {
  return apiClient.get('/admin/attachments/statistics', {
    signal,
    decode: decodeAdministratorAttachmentStatistics,
  })
}

export function getAdministratorAttachment(
  attachmentId: number,
  signal?: AbortSignal,
): Promise<AdministratorAttachmentDetail> {
  if (!isSafeApiId(attachmentId)) return Promise.reject(new RangeError('附件 ID 无效。'))
  return apiClient.get(`/admin/attachments/${encodeURIComponent(String(attachmentId))}`, {
    signal,
    decode: decodeAdministratorAttachmentDetail,
  })
}

export function checkAdministratorAttachmentIntegrity(
  attachmentId: number,
): Promise<AdministratorAttachmentIntegrity> {
  if (!isSafeApiId(attachmentId)) return Promise.reject(new RangeError('附件 ID 无效。'))
  return apiClient.post(
    `/admin/attachments/${encodeURIComponent(String(attachmentId))}/integrity-check`,
    {},
    { decode: decodeAdministratorAttachmentIntegrity },
  )
}

export function deleteAdministratorAttachment(
  attachmentId: number,
  concurrencyToken: string,
): Promise<void> {
  if (!isSafeApiId(attachmentId)) return Promise.reject(new RangeError('附件 ID 无效。'))
  return apiClient.deleteWithBody(
    `/admin/attachments/${encodeURIComponent(String(attachmentId))}`,
    { concurrencyToken },
    { decode: () => undefined },
  )
}
