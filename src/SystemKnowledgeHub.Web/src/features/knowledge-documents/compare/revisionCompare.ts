import type { KnowledgeDocumentRevisionDetail } from '../api/knowledgeDocumentContracts'
import { myersLineDiff, splitBodyLines, type LineDiffSegment } from './myersLineDiff'

export const maximumCombinedContentUnits = 2_005_000
export const maximumCombinedBodyLines = 10_000

export type FieldComparisonStatus = 'unchanged' | 'added' | 'removed' | 'changed'

export interface FieldComparison {
  readonly status: FieldComparisonStatus
  readonly from: string | null
  readonly to: string | null
}

export interface RevisionComparisonLimits {
  readonly combinedContentUnits: number
  readonly combinedBodyLines: number
  readonly allowed: boolean
}

export interface ReadyRevisionComparison {
  readonly kind: 'ready'
  readonly limits: RevisionComparisonLimits
  readonly title: FieldComparison
  readonly summary: FieldComparison
  readonly body: readonly LineDiffSegment[]
  readonly identical: boolean
}

export interface OversizedRevisionComparison {
  readonly kind: 'oversized'
  readonly limits: RevisionComparisonLimits
}

export type RevisionComparison = ReadyRevisionComparison | OversizedRevisionComparison

export function compareTitle(from: string, to: string): FieldComparison {
  return { status: from === to ? 'unchanged' : 'changed', from, to }
}

export function compareSummary(from: string | null, to: string | null): FieldComparison {
  let status: FieldComparisonStatus
  if (from === to) status = 'unchanged'
  else if (from === null) status = 'added'
  else if (to === null) status = 'removed'
  else status = 'changed'
  return { status, from, to }
}

export function inspectRevisionComparisonLimits(
  from: KnowledgeDocumentRevisionDetail,
  to: KnowledgeDocumentRevisionDetail,
): RevisionComparisonLimits {
  const combinedContentUnits = contentUnits(from) + contentUnits(to)
  const combinedBodyLines = splitBodyLines(from.bodyMarkdown).length
    + splitBodyLines(to.bodyMarkdown).length
  return {
    combinedContentUnits,
    combinedBodyLines,
    allowed: combinedContentUnits <= maximumCombinedContentUnits
      && combinedBodyLines <= maximumCombinedBodyLines,
  }
}

export function compareRevisionSnapshots(
  from: KnowledgeDocumentRevisionDetail,
  to: KnowledgeDocumentRevisionDetail,
): RevisionComparison {
  const limits = inspectRevisionComparisonLimits(from, to)
  if (!limits.allowed) return { kind: 'oversized', limits }

  const title = compareTitle(from.title, to.title)
  const summary = compareSummary(from.summary, to.summary)
  const body = myersLineDiff(splitBodyLines(from.bodyMarkdown), splitBodyLines(to.bodyMarkdown))
  return {
    kind: 'ready',
    limits,
    title,
    summary,
    body,
    identical: title.status === 'unchanged'
      && summary.status === 'unchanged'
      && body.every((segment) => segment.kind === 'unchanged'),
  }
}

function contentUnits(revision: KnowledgeDocumentRevisionDetail): number {
  return revision.title.length
    + (revision.summary?.length ?? 0)
    + revision.bodyMarkdown.length
}
