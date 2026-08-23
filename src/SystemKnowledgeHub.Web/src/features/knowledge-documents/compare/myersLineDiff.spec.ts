import { describe, expect, it } from 'vitest'
import { myersLineDiff, splitBodyLines } from './myersLineDiff'

describe('myersLineDiff', () => {
  it('handles empty, add, remove, unchanged and replacement inputs', () => {
    expect(myersLineDiff([], [])).toEqual([])
    expect(myersLineDiff([], ['a'])).toEqual([{ kind: 'added', lines: ['a'] }])
    expect(myersLineDiff(['a'], [])).toEqual([{ kind: 'removed', lines: ['a'] }])
    expect(myersLineDiff(['a'], ['a'])).toEqual([{ kind: 'unchanged', lines: ['a'] }])
    expect(myersLineDiff(['a'], ['b'])).toEqual([
      { kind: 'removed', lines: ['a'] },
      { kind: 'added', lines: ['b'] },
    ])
  })

  it('finds middle insertion and deletion', () => {
    expect(myersLineDiff(['a', 'c'], ['a', 'b', 'c'])).toEqual([
      { kind: 'unchanged', lines: ['a'] },
      { kind: 'added', lines: ['b'] },
      { kind: 'unchanged', lines: ['c'] },
    ])
    expect(myersLineDiff(['a', 'b', 'c'], ['a', 'c'])).toEqual([
      { kind: 'unchanged', lines: ['a'] },
      { kind: 'removed', lines: ['b'] },
      { kind: 'unchanged', lines: ['c'] },
    ])
  })

  it('is deterministic for duplicate, repeated Markdown, code and Chinese lines', () => {
    const from = ['# 标题', '重复', '重复', '```sql', 'SELECT 1;', '```', '结论']
    const to = ['# 标题', '重复', '新增', '重复', '```sql', 'SELECT 2;', '```', '结论']
    const expected = myersLineDiff(from, to)
    expect(expected).toEqual([
      { kind: 'unchanged', lines: ['# 标题', '重复'] },
      { kind: 'added', lines: ['新增'] },
      { kind: 'unchanged', lines: ['重复', '```sql'] },
      { kind: 'removed', lines: ['SELECT 1;'] },
      { kind: 'added', lines: ['SELECT 2;'] },
      { kind: 'unchanged', lines: ['```', '结论'] },
    ])
    for (let run = 0; run < 5; run += 1) expect(myersLineDiff(from, to)).toEqual(expected)
  })

  it('preserves blank lines and treats a final LF as a final empty line token', () => {
    expect(splitBodyLines('')).toEqual([])
    expect(splitBodyLines('a\n')).toEqual(['a', ''])
    expect(myersLineDiff(splitBodyLines('a\n\nb'), splitBodyLines('a\nb'))).toEqual([
      { kind: 'unchanged', lines: ['a'] },
      { kind: 'removed', lines: [''] },
      { kind: 'unchanged', lines: ['b'] },
    ])
    expect(myersLineDiff(splitBodyLines('a'), splitBodyLines('a\n'))).toEqual([
      { kind: 'unchanged', lines: ['a'] },
      { kind: 'added', lines: [''] },
    ])
  })

  it('produces a shortest valid edit script for exhaustive small duplicate-line inputs', () => {
    const inputs: string[][] = [[]]
    for (let length = 1; length <= 4; length += 1) {
      for (let mask = 0; mask < 2 ** length; mask += 1) {
        inputs.push(Array.from({ length }, (_, index) => (mask & (1 << index)) === 0 ? 'a' : 'b'))
      }
    }

    for (const from of inputs) {
      for (const to of inputs) {
        const diff = myersLineDiff(from, to)
        const rebuilt = diff.flatMap((segment) =>
          segment.kind === 'removed' ? [] : segment.lines)
        const editCount = diff
          .filter((segment) => segment.kind !== 'unchanged')
          .reduce((total, segment) => total + segment.lines.length, 0)
        expect(rebuilt).toEqual(to)
        expect(editCount).toBe(from.length + to.length - 2 * longestCommonSubsequence(from, to))
      }
    }
  })
})

function longestCommonSubsequence(from: readonly string[], to: readonly string[]): number {
  const row = new Array<number>(to.length + 1).fill(0)
  for (const fromLine of from) {
    let diagonal = 0
    for (let index = 1; index <= to.length; index += 1) {
      const previous = row[index]
      row[index] = fromLine === to[index - 1]
        ? diagonal + 1
        : Math.max(row[index], row[index - 1])
      diagonal = previous
    }
  }
  return row[to.length]
}
