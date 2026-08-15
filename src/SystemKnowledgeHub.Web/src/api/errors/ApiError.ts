import type { ApiErrorResponse } from '../contracts/errors'

export class ApiError extends Error {
  public readonly category = 'api'
  public readonly status: number
  public readonly response: ApiErrorResponse

  public constructor(status: number, response: ApiErrorResponse) {
    super(response.message)
    this.name = 'ApiError'
    this.status = status
    this.response = response
  }
}

export class NetworkRequestError extends Error {
  public readonly category = 'network'

  public constructor(message = '无法连接服务器，请检查网络后重试。') {
    super(message)
    this.name = 'NetworkRequestError'
  }
}

export class UnexpectedResponseError extends Error {
  public readonly category = 'unexpected'

  public constructor(message = '服务器返回了无法识别的内容。') {
    super(message)
    this.name = 'UnexpectedResponseError'
  }
}

export type RequestError = ApiError | NetworkRequestError | UnexpectedResponseError
