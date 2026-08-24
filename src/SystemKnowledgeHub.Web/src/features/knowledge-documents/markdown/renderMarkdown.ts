import MarkdownIt from 'markdown-it'
import { controlledColorMarkdownItPlugin } from './colorSyntax'
import { isLegacyBreakParagraph } from './legacyMarkdownBreaks'

const renderer = new MarkdownIt({ html: false, linkify: true })
renderer.use(controlledColorMarkdownItPlugin)

renderer.core.ruler.after('inline', 'task-list-items', (state) => {
  const listContainers: number[] = []
  const listItems: number[] = []
  const visitedListItems = new Set<number>()

  for (let index = 0; index < state.tokens.length; index += 1) {
    const token = state.tokens[index]!

    if (token.type === 'bullet_list_open' || token.type === 'ordered_list_open') {
      listContainers.push(index)
      continue
    }
    if (token.type === 'bullet_list_close' || token.type === 'ordered_list_close') {
      listContainers.pop()
      continue
    }
    if (token.type === 'list_item_open') {
      listItems.push(index)
      continue
    }
    if (token.type === 'list_item_close') {
      listItems.pop()
      continue
    }
    if (token.type !== 'inline' || listContainers.length === 0 || listItems.length === 0) {
      continue
    }

    const currentListContainer = listContainers.at(-1)!
    const currentListItem = listItems.at(-1)!
    if (visitedListItems.has(currentListItem)) continue
    visitedListItems.add(currentListItem)

    const marker = /^\[([ xX])\]\s+/u.exec(token.content)
    const firstChild = token.children?.[0]
    if (!marker || firstChild?.type !== 'text' || !/^\[[ xX]\]\s+/u.test(firstChild.content)) {
      continue
    }

    const checked = marker[1]?.toLowerCase() === 'x'
    const checkbox = new state.Token('task_checkbox', 'input', 0)
    checkbox.attrSet('data-checked', checked ? 'true' : 'false')
    firstChild.content = firstChild.content.replace(/^\[[ xX]\]\s+/u, '')
    token.children!.unshift(checkbox)

    state.tokens[currentListContainer]!.attrJoin('class', 'knowledge-document-task-list')
    state.tokens[currentListItem]!.attrJoin('class', 'knowledge-document-task-list-item')
  }
})
renderer.core.ruler.after('inline', 'legacy-break-paragraph', (state) => {
  const lines = state.src.split(/\r\n|\n|\r/)
  for (let index = 0; index < state.tokens.length; index += 1) {
    if (!isLegacyBreakParagraph(state.tokens, index, lines)) continue
    const hardBreak = new state.Token('hardbreak', 'br', 0)
    state.tokens[index + 1]!.children = [hardBreak]
  }
})

renderer.renderer.rules.task_checkbox = (tokens, index) => {
  const checked = tokens[index]!.attrGet('data-checked') === 'true'
  const checkedAttribute = checked ? ' checked' : ''
  const label = checked ? '任务项：已完成' : '任务项：未完成'
  return `<input class="knowledge-document-task-checkbox" type="checkbox" disabled${checkedAttribute} aria-label="${label}">`
}

renderer.renderer.rules.fence = (tokens, index) => {
  const token = tokens[index]!
  const declaredLanguage = token.info.trim().split(/\s+/u)[0]?.toLowerCase() ?? ''
  const language = /^[a-z0-9_-]+$/u.test(declaredLanguage) ? declaredLanguage : ''
  if (language !== 'mermaid') {
    const languageLabel = language || 'plain'
    const source = renderer.utils.escapeHtml(token.content)
    const languageClass = language ? ` language-${language}` : ''
    return [
      '<section class="knowledge-document-code-card" data-knowledge-document-code-card>',
      '<header class="knowledge-document-code-card__header">',
      `<span class="knowledge-document-code-card__language">${renderer.utils.escapeHtml(languageLabel)}</span>`,
      '<span class="knowledge-document-code-card__actions">',
      '<button type="button" class="knowledge-document-code-card__copy" data-knowledge-document-code-copy aria-label="复制代码">复制代码</button>',
      '<button type="button" class="knowledge-document-code-card__collapse" data-knowledge-document-code-collapse aria-label="收起代码" aria-expanded="true">⌃</button>',
      '</span></header>',
      '<pre class="knowledge-document-code-card__body"><code class="',
      languageClass.trim(),
      '">',
      source,
      '</code></pre></section>\n',
    ].join('')
  }

  const source = renderer.utils.escapeHtml(token.content)
  return [
    '<figure class="knowledge-document-mermaid" data-knowledge-document-mermaid>',
    '<figcaption class="knowledge-document-mermaid__caption">Mermaid 图表源码</figcaption>',
    '<pre class="knowledge-document-mermaid__source"><code>',
    source,
    '</code></pre>',
    '</figure>\n',
  ].join('')
}

const originalTableOpen = renderer.renderer.rules.table_open
renderer.renderer.rules.table_open = (tokens, index, options, environment, self) =>
  `<div class="knowledge-markdown-table-wrap">${
    originalTableOpen
      ? originalTableOpen(tokens, index, options, environment, self)
      : self.renderToken(tokens, index, options)
  }`

const originalTableClose = renderer.renderer.rules.table_close
renderer.renderer.rules.table_close = (tokens, index, options, environment, self) =>
  `${
    originalTableClose
      ? originalTableClose(tokens, index, options, environment, self)
      : self.renderToken(tokens, index, options)
  }</div>`

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
