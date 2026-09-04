import { environment } from '../../../app/config/env'
import { NetworkRequestError, UnexpectedResponseError } from '../../../api/errors/ApiError'
import { normalizeApiError } from '../../../api/errors/normalizeApiError'
import {
  decodePortalHome,
  decodePortalPage,
  decodePortalTree,
  type PortalHomeResponse,
  type PortalPageResponse,
  type PortalTreeResponse,
} from './portalReadContracts'

type Decoder<T> = (value: unknown) => T

export function createPortalReadClient(baseUrl: string, fetchImplementation: typeof fetch = fetch) {
  async function get<T>(path: string, decode: Decoder<T>, signal?: AbortSignal): Promise<T> {
    let response: Response
    try {
      response = await fetchImplementation(`${baseUrl}${path}`, {
        method: 'GET',
        credentials: 'omit',
        headers: { Accept: 'application/json' },
        signal,
      })
    } catch (error: unknown) {
      if (error instanceof DOMException && error.name === 'AbortError') throw error
      throw new NetworkRequestError()
    }
    if (!response.ok) throw await normalizeApiError(response)
    let payload: unknown
    try {
      payload = await response.json()
    } catch {
      throw new UnexpectedResponseError()
    }
    try {
      return decode(payload)
    } catch {
      throw new UnexpectedResponseError()
    }
  }

  return {
    getHome(signal?: AbortSignal): Promise<PortalHomeResponse> {
      return get('/portal/home', decodePortalHome, signal)
    },
    getTree(signal?: AbortSignal): Promise<PortalTreeResponse> {
      return get('/portal/tree', decodePortalTree, signal)
    },
    getPage(id: number, signal?: AbortSignal): Promise<PortalPageResponse> {
      return get(`/portal/pages/${id}`, decodePortalPage, signal)
    },
  }
}

export const portalReadApi = createPortalReadClient(environment.apiBaseUrl)
