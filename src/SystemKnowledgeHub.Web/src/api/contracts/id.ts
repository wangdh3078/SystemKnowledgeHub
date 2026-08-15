export function isSafeApiId(value: number): boolean {
  return Number.isSafeInteger(value) && value >= 1
}

export function parseSafeApiId(value: unknown): number | null {
  if (typeof value === 'number') {
    return isSafeApiId(value) ? value : null
  }
  if (typeof value !== 'string' || !/^[1-9]\d*$/.test(value)) {
    return null
  }
  const parsed = Number(value)
  return isSafeApiId(parsed) ? parsed : null
}
