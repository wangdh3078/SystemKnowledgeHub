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

interface BlobRequestOptions {
  readonly signal?: AbortSignal
  readonly headers?: Readonly<Record<string, string>>
}

export type AntiforgeryTokenProvider = () => string | null
export type SecurityErrorHandler = (error: ApiError, path: string) => void

let antiforgeryTokenProvider: AntiforgeryTokenProvider = () => null
let securityErrorHandler: SecurityErrorHandler | null = null

export function setApiAntiforgeryTokenProvider(provider: AntiforgeryTokenProvider): void {
  antiforgeryTokenProvider = provider
}

export function setApiSecurityErrorHandler(handler: SecurityErrorHandler | null): void {
  securityErrorHandler = handler
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
  antiforgeryToken?: string | null,
  bodyKind: 'json' | 'form' = 'json',
): RequestInit {
  const isUnsafe = ['POST', 'PUT', 'PATCH', 'DELETE'].includes(method)
  return {
    method,
    signal,
    headers: {
      Accept: 'application/json',
      ...(body === undefined || bodyKind === 'form' ? {} : { 'Content-Type': 'application/json' }),
      ...headers,
      ...(!isUnsafe || !antiforgeryToken ? {} : { 'X-CSRF-TOKEN': antiforgeryToken }),
    },
    credentials: 'include',
    ...(body === undefined
      ? {}
      : { body: bodyKind === 'form' ? (body as FormData) : JSON.stringify(body) }),
  }
}

export function createApiClient(
  baseUrl: string,
  fetchImplementation: typeof fetch = fetch,
  getAntiforgeryToken: AntiforgeryTokenProvider = () => null,
) {
  async function request<TResponse>(
    path: string,
    method: string,
    options: RequestOptions<TResponse>,
    body?: unknown,
    headers?: Readonly<Record<string, string>>,
    bodyKind: 'json' | 'form' = 'json',
  ): Promise<TResponse> {
    let response: Response

    try {
      response = await fetchImplementation(
        joinUrl(baseUrl, path),
        createRequestInit(method, options.signal, body, headers, getAntiforgeryToken(), bodyKind),
      )
    } catch (error: unknown) {
      if (error instanceof DOMException && error.name === 'AbortError') {
        throw error
      }

      throw new NetworkRequestError()
    }

    if (!response.ok) {
      const error = await normalizeApiError(response)
      securityErrorHandler?.(error, path)
      throw error
    }

    if (response.status === 204) {
      return options.decode(undefined)
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

  async function requestBlob(path: string, options: BlobRequestOptions): Promise<Blob> {
    let response: Response

    try {
      response = await fetchImplementation(
        joinUrl(baseUrl, path),
        createRequestInit('GET', options.signal, undefined, options.headers),
      )
    } catch (error: unknown) {
      if (error instanceof DOMException && error.name === 'AbortError') throw error
      throw new NetworkRequestError()
    }

    if (!response.ok) {
      const error = await normalizeApiError(response)
      securityErrorHandler?.(error, path)
      throw error
    }

    try {
      return await response.blob()
    } catch {
      throw new UnexpectedResponseError()
    }
  }

  return {
    get<TResponse>(path: string, options: RequestOptions<TResponse>): Promise<TResponse> {
      return request(path, 'GET', options)
    },

    getBlob(path: string, options: BlobRequestOptions = {}): Promise<Blob> {
      return requestBlob(path, options)
    },

    post<TRequest, TResponse>(
      path: string,
      body: TRequest,
      options: RequestWithBodyOptions<TResponse>,
    ): Promise<TResponse> {
      return request(path, 'POST', options, body, options.headers)
    },

    postForm<TResponse>(
      path: string,
      body: FormData,
      options: RequestWithBodyOptions<TResponse>,
    ): Promise<TResponse> {
      return request(path, 'POST', options, body, options.headers, 'form')
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

    deleteWithBody<TRequest, TResponse>(
      path: string,
      body: TRequest,
      options: RequestWithBodyOptions<TResponse>,
    ): Promise<TResponse> {
      return request(path, 'DELETE', options, body, options.headers)
    },

    async postRoot<TRequest>(path: string, body?: TRequest): Promise<void> {
      let response: Response
      try {
        response = await fetchImplementation(
          path,
          createRequestInit('POST', undefined, body, undefined, getAntiforgeryToken()),
        )
      } catch {
        throw new NetworkRequestError()
      }
      if (!response.ok) {
        const error = await normalizeApiError(response)
        securityErrorHandler?.(error, path)
        throw error
      }
    },
  }
}

export const apiClient = createApiClient(environment.apiBaseUrl, fetch, () =>
  antiforgeryTokenProvider(),
)
