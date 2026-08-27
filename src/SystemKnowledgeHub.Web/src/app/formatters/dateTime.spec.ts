import { describe, expect, it } from 'vitest'
import { formatDateTime } from './dateTime'

describe('formatDateTime', () => {
  it('renders every local date-time part with seconds and leading zeroes', () => {
    expect(formatDateTime('2026-01-02T03:04:05')).toBe('2026-01-02 03:04:05')
  })

  it('keeps the full year across the year boundary', () => {
    expect(formatDateTime('2025-12-31T23:59:59')).toBe('2025-12-31 23:59:59')
    expect(formatDateTime('2026-01-01T00:00:00')).toBe('2026-01-01 00:00:00')
  })

  it.each([null, undefined, ''])('renders a missing value as the canonical empty marker', (value) => {
    expect(formatDateTime(value)).toBe('—')
  })

  it('fails safely for an invalid wire value', () => {
    expect(formatDateTime('not-a-date')).toBe('—')
  })
})
