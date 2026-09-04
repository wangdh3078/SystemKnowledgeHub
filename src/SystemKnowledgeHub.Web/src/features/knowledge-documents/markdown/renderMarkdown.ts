import MarkdownIt from 'markdown-it'
import hljs from 'highlight.js/lib/core'
import bash from 'highlight.js/lib/languages/bash'
import dos from 'highlight.js/lib/languages/dos'
import c from 'highlight.js/lib/languages/c'
import cpp from 'highlight.js/lib/languages/cpp'
import csharp from 'highlight.js/lib/languages/csharp'
import css from 'highlight.js/lib/languages/css'
import dockerfile from 'highlight.js/lib/languages/dockerfile'
import go from 'highlight.js/lib/languages/go'
import ini from 'highlight.js/lib/languages/ini'
import java from 'highlight.js/lib/languages/java'
import javascript from 'highlight.js/lib/languages/javascript'
import json from 'highlight.js/lib/languages/json'
import kotlin from 'highlight.js/lib/languages/kotlin'
import less from 'highlight.js/lib/languages/less'
import markdown from 'highlight.js/lib/languages/markdown'
import nginx from 'highlight.js/lib/languages/nginx'
import php from 'highlight.js/lib/languages/php'
import powershell from 'highlight.js/lib/languages/powershell'
import python from 'highlight.js/lib/languages/python'
import ruby from 'highlight.js/lib/languages/ruby'
import rust from 'highlight.js/lib/languages/rust'
import scss from 'highlight.js/lib/languages/scss'
import sql from 'highlight.js/lib/languages/sql'
import typescript from 'highlight.js/lib/languages/typescript'
import xml from 'highlight.js/lib/languages/xml'
import yaml from 'highlight.js/lib/languages/yaml'
import { icon } from '@fortawesome/fontawesome-svg-core'
import { faCheck, faChevronDown, faChevronUp, faCopy } from '@fortawesome/free-solid-svg-icons'
import { controlledColorMarkdownItPlugin } from './colorSyntax'
import { isLegacyBreakParagraph } from './legacyMarkdownBreaks'
import { knowledgeDocumentImageContentUrl } from '../api/knowledgeDocumentAttachmentsApi'

export interface MarkdownAttachmentImageContext {
  readonly documentId: number
  readonly revisionNumber?: number
  readonly imageAttachmentIds: readonly number[]
  readonly transientImageUrls?: ReadonlyMap<number, string>
  readonly resolveImageUrl?: (attachmentId: number) => string
}

interface MarkdownRenderEnvironment {
  readonly attachmentImageContext?: MarkdownAttachmentImageContext
}

const renderer = new MarkdownIt({ html: false, linkify: true })
renderer.use(controlledColorMarkdownItPlugin)

const codeHighlightLanguages = {
  bash,
  dos,
  c,
  cpp,
  csharp,
  css,
  dockerfile,
  go,
  ini,
  java,
  javascript,
  json,
  kotlin,
  less,
  markdown,
  nginx,
  php,
  powershell,
  python,
  ruby,
  rust,
  scss,
  sql,
  typescript,
  xml,
  yaml,
} as const

Object.entries(codeHighlightLanguages).forEach(([name, definition]) => {
  hljs.registerLanguage(name, definition)
})

hljs.registerLanguage('toml', (definition) => ({
  name: 'TOML',
  contains: [
    { className: 'section', begin: /^\s*\[[^\]]+\]/mu },
    { className: 'attr', begin: /[A-Za-z0-9_.-]+(?=\s*=)/u },
    definition.QUOTE_STRING_MODE,
    definition.C_NUMBER_MODE,
    definition.HASH_COMMENT_MODE,
  ],
}))

const highlighterLanguageNames: Readonly<Record<string, string>> = {
  bash: 'bash',
  batch: 'dos',
  c: 'c',
  cpp: 'cpp',
  csharp: 'csharp',
  css: 'css',
  dockerfile: 'dockerfile',
  go: 'go',
  html: 'xml',
  ini: 'ini',
  java: 'java',
  javascript: 'javascript',
  json: 'json',
  jsonc: 'json',
  kotlin: 'kotlin',
  less: 'less',
  markdown: 'markdown',
  nginx: 'nginx',
  php: 'php',
  plsql: 'sql',
  powershell: 'powershell',
  python: 'python',
  ruby: 'ruby',
  rust: 'rust',
  scss: 'scss',
  shell: 'bash',
  sql: 'sql',
  typescript: 'typescript',
  xml: 'xml',
  yaml: 'yaml',
  toml: 'toml',
}

function renderJsxLikeCode(source: string, language: 'javascript' | 'typescript'): string {
  return source
    .split(/(<\/?[A-Za-z][^>]*>)/gu)
    .map((part) =>
      /^<\/?[A-Za-z][^>]*>$/u.test(part)
        ? `<span class="hljs-tag">${renderer.utils.escapeHtml(part)}</span>`
        : hljs.highlight(part, { language, ignoreIllegals: true }).value,
    )
    .join('')
}

function renderVueSfc(source: string): string {
  const blockPattern = /(<(template|script|style)\b[^>]*>)([\s\S]*?)(<\/\2>)/giu
  let offset = 0
  let rendered = ''
  for (const match of source.matchAll(blockPattern)) {
    const index = match.index ?? 0
    rendered += hljs.highlight(source.slice(offset, index), {
      language: 'xml',
      ignoreIllegals: true,
    }).value
    const opening = match[1]!
    const blockType = match[2]!.toLowerCase()
    const content = match[3]!
    const closing = match[4]!
    const language =
      blockType === 'script'
        ? /\blang\s*=\s*["'](?:ts|tsx)["']/iu.test(opening)
          ? 'typescript'
          : 'javascript'
        : blockType === 'style'
          ? /\blang\s*=\s*["']scss["']/iu.test(opening)
            ? 'scss'
            : /\blang\s*=\s*["']less["']/iu.test(opening)
              ? 'less'
              : 'css'
          : 'xml'
    rendered += hljs.highlight(opening, { language: 'xml', ignoreIllegals: true }).value
    rendered += hljs.highlight(content, { language, ignoreIllegals: true }).value
    rendered += hljs.highlight(closing, { language: 'xml', ignoreIllegals: true }).value
    offset = index + match[0].length
  }
  return (
    rendered + hljs.highlight(source.slice(offset), { language: 'xml', ignoreIllegals: true }).value
  )
}

function renderHighlightedCode(source: string, language: string): string {
  if (language === 'vue') return renderVueSfc(source)
  if (language === 'jsx') return renderJsxLikeCode(source, 'javascript')
  if (language === 'tsx') return renderJsxLikeCode(source, 'typescript')
  const highlighterLanguage = highlighterLanguageNames[language]
  if (!highlighterLanguage) return renderer.utils.escapeHtml(source)
  return hljs.highlight(source, { language: highlighterLanguage, ignoreIllegals: true }).value
}
const codeCopyIcon = icon(faCopy, {
  classes: ['knowledge-document-code-card__control-icon'],
}).html.join('')
const codeCopiedIcon = icon(faCheck, {
  classes: ['knowledge-document-code-card__control-icon'],
}).html.join('')
const codeCollapseIcon = icon(faChevronUp, {
  classes: ['knowledge-document-code-card__control-icon'],
}).html.join('')
const codeExpandIcon = icon(faChevronDown, {
  classes: ['knowledge-document-code-card__control-icon'],
}).html.join('')

export const codeCardIcons = {
  copy: codeCopyIcon,
  copied: codeCopiedIcon,
  collapse: codeCollapseIcon,
  expand: codeExpandIcon,
} as const

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
  const declaredLanguage = token.info.trim().split(/\s+/u)[0] ?? ''
  const normalizedDeclaredLanguage = declaredLanguage.toLowerCase()
  const language = /^[a-z0-9_-]+$/u.test(normalizedDeclaredLanguage)
    ? normalizedDeclaredLanguage
    : ''
  if (language !== 'mermaid') {
    const languageLabel = declaredLanguage || 'plain'
    const source = renderHighlightedCode(token.content, language)
    const languageClass = language ? `language-${language}` : ''
    const codeClass = ['hljs', languageClass].filter(Boolean).join(' ')
    return [
      '<section class="knowledge-document-code-card" data-knowledge-document-code-card>',
      '<header class="knowledge-document-code-card__header">',
      `<span class="knowledge-document-code-card__language">${renderer.utils.escapeHtml(languageLabel)}</span>`,
      '<span class="knowledge-document-code-card__actions">',
      `<button type="button" class="knowledge-document-code-card__copy" data-knowledge-document-code-copy aria-label="复制代码" title="复制代码">${codeCardIcons.copy}</button>`,
      '<span class="knowledge-document-code-card__copy-feedback" data-knowledge-document-code-copy-feedback aria-live="polite"></span>',
      `<button type="button" class="knowledge-document-code-card__collapse" data-knowledge-document-code-collapse aria-label="收起代码" title="收起代码" aria-expanded="true">${codeCardIcons.collapse}</button>`,
      '</span></header>',
      `<pre class="knowledge-document-code-card__body"><code class="${codeClass}">`,
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

function unavailableAttachmentImage(alt: string, attachmentId?: number): string {
  const label = alt.trim() ? `图片不可用：${alt}` : '图片不可用'
  const id = attachmentId === undefined ? '' : ` data-attachment-id="${attachmentId}"`
  return `<span class="knowledge-document-attachment-image-unavailable" role="img" aria-label="${renderer.utils.escapeHtml(label)}"${id}>图片暂不可用</span>`
}

const originalImage = renderer.renderer.rules.image
renderer.renderer.rules.image = (tokens, index, options, environment, self) => {
  const token = tokens[index]!
  const sourceValue = token.attrGet('src')
  const source = typeof sourceValue === 'string' ? sourceValue : String(sourceValue ?? '')
  if (!source.startsWith('attachment:')) {
    return originalImage
      ? originalImage(tokens, index, options, environment, self)
      : self.renderToken(tokens, index, options)
  }

  const alt = token.content
  const match = /^attachment:([1-9]\d*)$/u.exec(source)
  if (!match) return unavailableAttachmentImage(alt)
  const attachmentId = Number(match[1])
  if (!Number.isSafeInteger(attachmentId)) return unavailableAttachmentImage(alt)

  const imageContext = (environment as MarkdownRenderEnvironment | undefined)
    ?.attachmentImageContext
  if (!imageContext?.imageAttachmentIds.includes(attachmentId)) {
    return unavailableAttachmentImage(alt, attachmentId)
  }

  const transientUrl = imageContext.transientImageUrls?.get(attachmentId)
  let resolvedSource: string
  try {
    resolvedSource =
      transientUrl ??
      imageContext.resolveImageUrl?.(attachmentId) ??
      knowledgeDocumentImageContentUrl(
        imageContext.documentId,
        attachmentId,
        imageContext.revisionNumber,
      )
  } catch {
    return unavailableAttachmentImage(alt, attachmentId)
  }

  const escapedAlt = renderer.utils.escapeHtml(alt)
  const escapedSource = renderer.utils.escapeHtml(resolvedSource)
  return [
    `<span class="knowledge-document-attachment-image" data-knowledge-document-attachment-image-container data-attachment-id="${attachmentId}">`,
    `<img src="${escapedSource}" alt="${escapedAlt}" loading="lazy" decoding="async" data-knowledge-document-attachment-image>`,
    `<span class="knowledge-document-attachment-image-unavailable" role="img" aria-label="${renderer.utils.escapeHtml(alt.trim() ? `图片不可用：${alt}` : '图片不可用')}" hidden>图片暂不可用</span>`,
    '</span>',
  ].join('')
}

export function renderMarkdown(
  markdown: string,
  attachmentImageContext?: MarkdownAttachmentImageContext,
): string {
  return renderer.render(markdown, { attachmentImageContext })
}
