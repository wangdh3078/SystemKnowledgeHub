export interface MarkdownSourceSelection {
  readonly anchor: number
  readonly head: number
}

export interface MarkdownSourceTransformResult {
  readonly source: string
  readonly selection: MarkdownSourceSelection
}

export type MarkdownHeadingLevel = 'paragraph' | 'h1' | 'h2' | 'h3' | 'h4' | 'h5' | 'h6'

function clamp(value: number, maximum: number): number {
  return Math.min(Math.max(0, value), maximum)
}

function normalizedSelection(
  source: string,
  selection: MarkdownSourceSelection,
): {
  readonly from: number
  readonly to: number
} {
  const anchor = clamp(selection.anchor, source.length)
  const head = clamp(selection.head, source.length)
  return anchor <= head ? { from: anchor, to: head } : { from: head, to: anchor }
}

function result(source: string, anchor: number, head = anchor): MarkdownSourceTransformResult {
  return {
    source,
    selection: {
      anchor: clamp(anchor, source.length),
      head: clamp(head, source.length),
    },
  }
}

function lineRange(
  source: string,
  selection: MarkdownSourceSelection,
): {
  readonly start: number
  readonly end: number
} {
  const { from, to } = normalizedSelection(source, selection)
  const selectedEnd = to > from && source[to - 1] === '\n' ? to - 1 : to
  const start = source.lastIndexOf('\n', from - 1) + 1
  const nextBreak = source.indexOf('\n', selectedEnd)
  return { start, end: nextBreak === -1 ? source.length : nextBreak }
}

function transformLines(
  source: string,
  selection: MarkdownSourceSelection,
  transform: (line: string, index: number) => string,
): MarkdownSourceTransformResult {
  const { start, end } = lineRange(source, selection)
  const original = source.slice(start, end)
  const next = original.split('\n').map(transform).join('\n')
  const updated = `${source.slice(0, start)}${next}${source.slice(end)}`
  return result(updated, start, start + next.length)
}

function selectedOrCurrentLine(
  source: string,
  selection: MarkdownSourceSelection,
): { readonly from: number; readonly to: number; readonly text: string } {
  const selected = normalizedSelection(source, selection)
  if (selected.from !== selected.to) {
    return { ...selected, text: source.slice(selected.from, selected.to) }
  }
  const line = lineRange(source, selection)
  return { ...line, from: line.start, to: line.end, text: source.slice(line.start, line.end) }
}

function toggleLinePrefix(
  source: string,
  selection: MarkdownSourceSelection,
  prefix: RegExp,
  addPrefix: (line: string, index: number) => string,
): MarkdownSourceTransformResult {
  const { start, end } = lineRange(source, selection)
  const lines = source.slice(start, end).split('\n')
  const meaningful = lines.filter((line) => line.trim().length > 0)
  const remove = meaningful.length > 0 && meaningful.every((line) => prefix.test(line))
  return transformLines(source, selection, (line, index) => {
    if (!line.trim()) return line
    return remove ? line.replace(prefix, '$1') : addPrefix(line, index)
  })
}

export function applyHeading(
  source: string,
  selection: MarkdownSourceSelection,
  level: MarkdownHeadingLevel,
): MarkdownSourceTransformResult {
  const marker = level === 'paragraph' ? '' : `${'#'.repeat(Number(level.slice(1)))} `
  return transformLines(source, selection, (line) => {
    if (!line.trim()) return line
    const withoutHeading = line.replace(/^(\s{0,3})#{1,6}\s+/, '$1')
    return marker ? withoutHeading.replace(/^(\s{0,3})/, `$1${marker}`) : withoutHeading
  })
}

export function toggleInlineWrap(
  source: string,
  selection: MarkdownSourceSelection,
  delimiter: string,
): MarkdownSourceTransformResult {
  const { from, to } = normalizedSelection(source, selection)
  const selected = source.slice(from, to)
  if (!selected) {
    const updated = `${source.slice(0, from)}${delimiter}${delimiter}${source.slice(to)}`
    return result(updated, from + delimiter.length)
  }
  if (selected.startsWith(delimiter) && selected.endsWith(delimiter)) {
    const unwrapped = selected.slice(delimiter.length, -delimiter.length)
    const updated = `${source.slice(0, from)}${unwrapped}${source.slice(to)}`
    return result(updated, from, from + unwrapped.length)
  }
  const wrapped = `${delimiter}${selected}${delimiter}`
  const updated = `${source.slice(0, from)}${wrapped}${source.slice(to)}`
  return result(updated, from + delimiter.length, from + delimiter.length + selected.length)
}

export function toggleQuote(
  source: string,
  selection: MarkdownSourceSelection,
): MarkdownSourceTransformResult {
  return toggleLinePrefix(source, selection, /^(\s*)>\s?/, (line) => line.replace(/^(\s*)/, '$1> '))
}

export function toggleBulletList(
  source: string,
  selection: MarkdownSourceSelection,
): MarkdownSourceTransformResult {
  return toggleLinePrefix(source, selection, /^(\s*)[-*+]\s+/, (line) =>
    line.replace(/^(\s*)/, '$1- '),
  )
}

export function toggleOrderedList(
  source: string,
  selection: MarkdownSourceSelection,
): MarkdownSourceTransformResult {
  return toggleLinePrefix(source, selection, /^(\s*)\d+\.\s+/, (line, index) =>
    line.replace(/^(\s*)/, `$1${index + 1}. `),
  )
}

export function toggleTaskList(
  source: string,
  selection: MarkdownSourceSelection,
): MarkdownSourceTransformResult {
  return toggleLinePrefix(source, selection, /^(\s*)-\s+\[[ xX]\]\s+/, (line) => {
    const content = line.replace(/^(\s*)(?:[-*+]\s+)?(?:\[[ xX]\]\s*)?/, '$1')
    return content.replace(/^(\s*)/, '$1- [ ] ')
  })
}

export function insertCodeBlock(
  source: string,
  selection: MarkdownSourceSelection,
  language: string,
): MarkdownSourceTransformResult {
  const target = selectedOrCurrentLine(source, selection)
  const normalizedLanguage = language.trim().toLowerCase()
  const fence = `\`\`\`${normalizedLanguage}\n${target.text}\n\`\`\``
  const updated = `${source.slice(0, target.from)}${fence}${source.slice(target.to)}`
  const contentStart = target.from + normalizedLanguage.length + 4
  return result(updated, contentStart, contentStart + target.text.length)
}

export function insertHorizontalRule(
  source: string,
  selection: MarkdownSourceSelection,
): MarkdownSourceTransformResult {
  const { end } = lineRange(source, selection)
  const prefix = source.slice(0, end)
  const suffix = source.slice(end)
  const separator = prefix.length === 0 ? '' : '\n\n'
  const insertion = `${separator}---\n`
  const updated = `${prefix}${insertion}${suffix}`
  return result(updated, end + insertion.length)
}

export function insertMarkdown(
  source: string,
  selection: MarkdownSourceSelection,
  markdown: string,
): MarkdownSourceTransformResult {
  const { from, to } = normalizedSelection(source, selection)
  const updated = `${source.slice(0, from)}${markdown}${source.slice(to)}`
  return result(updated, from + markdown.length)
}

export function insertLink(
  source: string,
  selection: MarkdownSourceSelection,
  displayText: string,
  url: string,
): MarkdownSourceTransformResult {
  return insertMarkdown(source, selection, `[${displayText}](${url})`)
}

export function insertTable(
  source: string,
  selection: MarkdownSourceSelection,
  rows: number,
  columns: number,
): MarkdownSourceTransformResult {
  const boundedRows = Math.min(10, Math.max(2, Math.trunc(rows)))
  const boundedColumns = Math.min(10, Math.max(2, Math.trunc(columns)))
  const header = Array.from({ length: boundedColumns }, (_, index) => `列${index + 1}`)
  const divider = Array.from({ length: boundedColumns }, () => '---')
  const body = Array.from({ length: boundedRows - 1 }, () =>
    Array.from({ length: boundedColumns }, () => '内容'),
  )
  const row = (cells: readonly string[]) => `| ${cells.join(' | ')} |`
  return insertMarkdown(source, selection, [row(header), row(divider), ...body.map(row)].join('\n'))
}

export const DEFAULT_MERMAID_MARKDOWN = `\`\`\`mermaid
flowchart LR
  A[开始] --> B[结束]
\`\`\``

export function insertMermaid(
  source: string,
  selection: MarkdownSourceSelection,
): MarkdownSourceTransformResult {
  return insertMarkdown(source, selection, DEFAULT_MERMAID_MARKDOWN)
}
