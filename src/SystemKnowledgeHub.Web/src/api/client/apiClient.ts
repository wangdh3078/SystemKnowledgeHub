import { environment } from '../../app/config/env'
import { ApiError, NetworkRequestError, UnexpectedResponseError } from '../errors/ApiError'
import { normalizeApiError } from '../errors/normalizeApiError'

export type ResponseDecoder<TResponse> = (payload: unknown) => TResponse

interface RequestOptions<TResponse> {
  readonly signal?: AbortSignal
  readonly decode: ResponseDecoder<TResponse>
}

interface RequestWithBodyOptions<TResponse> extends RequestOptions<TResponse> {
  readonly headers?: Readonly<Record<string, string>>
}

function joinUrl(baseUrl: string, path: string): string {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`
  return `${baseUrl}${normalizedPath}`
}

function createRequestInit(
  method: string,
  signal: AbortSignal | undefined,
  body?: unknown,
  headers?: Readonly<Record<string, string>>,
): RequestInit {
  return {
    method,
    signal,
    headers: {
      Accept: 'application/json',
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
      ...headers,
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  }
}

export function createApiClient(baseUrl: string, fetchImplementation: typeof fetch = fetch) {
  async function request<TResponse>(
    path: string,
    method: string,
    options: RequestOptions<TResponse>,
    body?: unknown,
    headers?: Readonly<Record<string, string>>,
  ): Promise<TResponse> {
    let response: Response

    try {
      response = await fetchImplementation(
        joinUrl(baseUrl, path),
        createRequestInit(method, options.signal, body, headers),
      )
    } catch (error: unknown) {
      if (error instanceof DOMException && error.name === 'AbortError') {
        throw error
      }

      throw new NetworkRequestError()
    }

    if (!response.ok) {
      throw await normalizeApiError(response)
    }

    let payload: unknown

    try {
      payload = await response.json()
    } catch {
      throw new UnexpectedResponseError()
    }

    try {
      return options.decode(payload)
    } catch (error: unknown) {
      if (
        error instanceof ApiError ||
        error instanceof NetworkRequestError ||
        error instanceof UnexpectedResponseError
      ) {
        throw error
      }

      throw new UnexpectedResponseError()
    }
  }

  return {
    get<TResponse>(path: string, options: RequestOptions<TResponse>): Promise<TResponse> {
      return request(path, 'GET', options)
    },

    post<TRequest, TResponse>(
      path: string,
      body: TRequest,
      options: RequestWithBodyOptions<TResponse>,
    ): Promise<TResponse> {
      return request(path, 'POST', options, body, options.headers)
    },

    put<TRequest, TResponse>(
      path: string,
      body: TRequest,
      options: RequestWithBodyOptions<TResponse>,
    ): Promise<TResponse> {
      return request(path, 'PUT', options, body, options.headers)
    },

    delete<TResponse>(path: string, options: RequestOptions<TResponse>): Promise<TResponse> {
      return request(path, 'DELETE', options)
    },
  }
}

export const apiClient = createApiClient(environment.apiBaseUrl)
