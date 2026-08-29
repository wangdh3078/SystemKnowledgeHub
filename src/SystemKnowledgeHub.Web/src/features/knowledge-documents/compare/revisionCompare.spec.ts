import { describe, expect, it } from 'vitest'
import type { KnowledgeDocumentRevisionDetail } from '../api/knowledgeDocumentContracts'
import {
  compareRevisionSnapshots,
  compareSummary,
  compareTitle,
  inspectRevisionComparisonLimits,
  maximumCombinedBodyLines,
  maximumCombinedContentUnits,
} from './revisionCompare'

function revision(
  overrides: Partial<KnowledgeDocumentRevisionDetail> = {},
): KnowledgeDocumentRevisionDetail {
  return {
    id: 1,
    knowledgeDocumentId: 7,
    revisionNumber: 1,
    revisionOrigin: 'ContentSave',
    lifecycleContext: 'Draft',
    authorUserId: 9,
    authorDisplayName: '作者快照',
    createdAt: '2026-08-23T01:00:00Z',
    changeSummary: null,
    restoreReason: null,
    restoredFromRevisionNumber: null,
    isCurrent: false,
    isLatestPublished: false,
    title: '',
    summary: null,
    bodyMarkdown: '',
    attachmentReferences: [],
    ...overrides,
  }
}

describe('revision field comparison', () => {
  it('compares Title as unchanged or changed', () => {
    expect(compareTitle('A', 'A').status).toBe('unchanged')
    expect(compareTitle('A', 'B')).toEqual({ status: 'changed', from: 'A', to: 'B' })
  })

  it('covers every nullable Summary transition', () => {
    expect(compareSummary(null, null).status).toBe('unchanged')
    expect(compareSummary(null, 'new').status).toBe('added')
    expect(compareSummary('old', null).status).toBe('removed')
    expect(compareSummary('old', 'new').status).toBe('changed')
    expect(compareSummary('same', 'same').status).toBe('unchanged')
  })
})

describe('revision comparison limits', () => {
  it('allows the exact string-unit limit and blocks one unit over without a body result', () => {
    const exactFrom = revision({ title: 'a'.repeat(1_002_500) })
    const exactTo = revision({ title: 'b'.repeat(1_002_500), revisionNumber: 2 })
    expect(inspectRevisionComparisonLimits(exactFrom, exactTo)).toEqual({
      combinedContentUnits: maximumCombinedContentUnits,
      combinedBodyLines: 0,
      allowed: true,
    })

    const over = compareRevisionSnapshots(
      exactFrom,
      revision({ title: `${exactTo.title}x`, revisionNumber: 2 }),
    )
    expect(over).toEqual({
      kind: 'oversized',
      limits: {
        combinedContentUnits: maximumCombinedContentUnits + 1,
        combinedBodyLines: 0,
        allowed: false,
      },
    })
    expect('body' in over).toBe(false)
  })

  it('allows exactly 10,000 body lines and blocks 10,001', () => {
    const fiveThousandLines = Array.from({ length: 5_000 }, () => 'a').join('\n')
    const exactFrom = revision({ bodyMarkdown: fiveThousandLines })
    const exactTo = revision({ bodyMarkdown: fiveThousandLines, revisionNumber: 2 })
    expect(inspectRevisionComparisonLimits(exactFrom, exactTo).combinedBodyLines).toBe(
      maximumCombinedBodyLines,
    )
    expect(compareRevisionSnapshots(exactFrom, exactTo).kind).toBe('ready')

    const over = revision({
      bodyMarkdown: `${fiveThousandLines}\nextra`,
      revisionNumber: 2,
    })
    const result = compareRevisionSnapshots(exactFrom, over)
    expect(result.kind).toBe('oversized')
    expect(result.limits.combinedBodyLines).toBe(maximumCombinedBodyLines + 1)
  })
})

describe('revision raw Markdown comparison', () => {
  it('compares a legacy BR snapshot as immutable raw source', () => {
    const legacyBody = 'A\n\n<br />\n\nB'
    const from = revision({ bodyMarkdown: legacyBody })
    const to = revision({ bodyMarkdown: 'A\n\n\\\nB', revisionNumber: 2 })
    const result = compareRevisionSnapshots(from, to)

    expect(result.kind).toBe('ready')
    if (result.kind !== 'ready') return
    expect(
      result.body.some((segment) => segment.kind === 'removed' && segment.lines.includes('<br />')),
    ).toBe(true)
    expect(from.bodyMarkdown).toBe(legacyBody)
  })
})
