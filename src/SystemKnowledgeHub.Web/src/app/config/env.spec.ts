import { describe, expect, it } from 'vitest'
import { readEnvironment } from './env'

describe('readEnvironment', () => {
  it('uses the relative API base path by default', () => {
    expect(readEnvironment({}).apiBaseUrl).toBe('/api')
  })

  it('normalizes a trailing slash', () => {
    expect(readEnvironment({ VITE_API_BASE_URL: '/internal-api/' }).apiBaseUrl).toBe(
      '/internal-api',
    )
  })

  it('rejects an empty API base path', () => {
    expect(() => readEnvironment({ VITE_API_BASE_URL: '   ' })).toThrow(
      'VITE_API_BASE_URL 不能为空。',
    )
  })
})
