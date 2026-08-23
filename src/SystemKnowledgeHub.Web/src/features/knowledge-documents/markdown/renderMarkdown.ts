import MarkdownIt from 'markdown-it'

const renderer = new MarkdownIt({ html: false, linkify: true })
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
