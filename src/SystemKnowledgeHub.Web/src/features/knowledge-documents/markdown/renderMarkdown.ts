import MarkdownIt from 'markdown-it'
import { isLegacyBreakParagraph } from './legacyMarkdownBreaks'

const renderer = new MarkdownIt({ html: false, linkify: true })
renderer.core.ruler.after('inline', 'legacy-break-paragraph', (state) => {
  const lines = state.src.split(/\r\n|\n|\r/)
  for (let index = 0; index < state.tokens.length; index += 1) {
    if (!isLegacyBreakParagraph(state.tokens, index, lines)) continue
    const hardBreak = new state.Token('hardbreak', 'br', 0)
    state.tokens[index + 1]!.children = [hardBreak]
  }
})
const originalLinkOpen = renderer.renderer.rules.link_open
renderer.renderer.rules.link_open = (tokens, index, options, environment, self) => {
  const token = tokens[index]
  const href = token.attrGet('href')
  if (typeof href === 'string' && (href.startsWith('http://') || href.startsWith('https://'))) {
    token.attrSet('target', '_blank')
    token.attrSet('rel', 'noopener noreferrer')
  }
  return originalLinkOpen
    ? originalLinkOpen(tokens, index, options, environment, self)
    : self.renderToken(tokens, index, options)
}

export function renderMarkdown(markdown: string): string {
  return renderer.render(markdown)
}
