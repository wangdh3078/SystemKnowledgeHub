import MarkdownIt, { type Token } from 'markdown-it'

const legacyBreakValues = new Set(['<br>', '<br/>', '<br >', '<br />'])
const compatibilityParser = new MarkdownIt({ html: false })

function sourceLines(markdown: string): string[] {
  return markdown.split(/\r\n|\n|\r/)
}

export function isLegacyBreakParagraph(
  tokens: readonly Token[],
  index: number,
  lines: readonly string[],
): boolean {
  const open = tokens[index]
  const inline = tokens[index + 1]
  const close = tokens[index + 2]
  const line = inline?.map?.[0]

  return open?.type === 'paragraph_open'
    && open.level === 0
    && !open.hidden
    && inline?.type === 'inline'
    && inline.level === 1
    && typeof line === 'number'
    && inline.map?.[1] === line + 1
    && lines[line] === inline.content
    && legacyBreakValues.has(inline.content)
    && inline.children?.length === 1
    && inline.children[0]?.type === 'text'
    && inline.children[0].content === inline.content
    && close?.type === 'paragraph_close'
    && close.level === 0
}

export function canonicalizeLegacyBreakParagraphs(markdown: string): string {
  const lines = sourceLines(markdown)
  const tokens = compatibilityParser.parse(markdown, {})
  const lineIndexes: number[] = []
  for (let index = 0; index < tokens.length; index += 1) {
    if (!isLegacyBreakParagraph(tokens, index, lines)) continue
    const line = tokens[index + 1]?.map?.[0]
    if (typeof line === 'number') lineIndexes.push(line)
  }

  let changed = false
  for (const lineIndex of lineIndexes.reverse()) {
    const hasFollowingContent = lines.slice(lineIndex + 1).some((line) => line.length > 0)
    if (!hasFollowingContent) continue

    lines[lineIndex] = '\\'
    if (lines[lineIndex + 1] === '') lines.splice(lineIndex + 1, 1)
    changed = true
  }

  return changed ? lines.join('\n') : markdown
}
