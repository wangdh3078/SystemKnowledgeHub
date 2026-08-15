import { describe, expect, it } from 'vitest'
import { isSafeApiId, parseSafeApiId } from './databaseKnowledgeApi'

describe('database knowledge safe API IDs', () => {
  it('accepts only positive Number.MAX_SAFE_INTEGER values', () => {
    expect(isSafeApiId(1)).toBe(true)
    expect(isSafeApiId(Number.MAX_SAFE_INTEGER)).toBe(true)
    expect(isSafeApiId(0)).toBe(false)
    expect(isSafeApiId(Number.MAX_SAFE_INTEGER + 1)).toBe(false)
  })

  it('parses canonical route values without rounding unsafe IDs', () => {
    expect(parseSafeApiId('123')).toBe(123)
    expect(parseSafeApiId('01')).toBeNull()
    expect(parseSafeApiId('-1')).toBeNull()
    expect(parseSafeApiId('9007199254740992')).toBeNull()
    expect(parseSafeApiId(['123'])).toBeNull()
  })
})
