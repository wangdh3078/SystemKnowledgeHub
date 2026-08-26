import { describe, expect, it } from 'vitest'
import { formatLocalDateTimeToMinute } from './dateTime'

describe('formatLocalDateTimeToMinute', () => {
  it('keeps the year and fixed minute precision across different years', () => {
    expect(formatLocalDateTimeToMinute('2025-01-02T03:04:00')).toBe('2025-01-02 03:04')
    expect(formatLocalDateTimeToMinute('2026-11-12T13:14:59')).toBe('2026-11-12 13:14')
  })

  it('preserves an invalid wire value instead of inventing a date', () => {
    expect(formatLocalDateTimeToMinute('not-a-date')).toBe('not-a-date')
  })
})
