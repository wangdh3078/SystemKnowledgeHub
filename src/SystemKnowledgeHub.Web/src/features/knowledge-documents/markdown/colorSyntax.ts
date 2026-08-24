import type { MarkdownIt, StateInline, Token } from 'markdown-it'

export const controlledColorMaximumDepth = 8

export type ControlledColorKind = 'color' | 'bg'

export interface ControlledColorText {
  readonly type: 'text'
  readonly value: string
}

export interface ControlledColorOpening {
  readonly kind: ControlledColorKind
  readonly hex: string
  readonly end: number
}

export interface ControlledColorSpan {
  readonly type: 'span'
  readonly kind: ControlledColorKind
  readonly hex: string
  readonly children: readonly ControlledColorNode[]
  readonly contentStart: number
  readonly contentEnd: number
  readonly end: number
}

export type ControlledColorNode = ControlledColorText | ControlledColorSpan

const openingPattern = /^\{(color|bg):(#[0-9A-Fa-f]{6})\|/
const canonicalHexPattern = /^#[0-9A-F]{6}$/
const textColorOpenToken = 'knowledge_text_color_open'
const textColorCloseToken = 'knowledge_text_color_close'
const backgroundColorOpenToken = 'knowledge_background_color_open'
const backgroundColorCloseToken = 'knowledge_background_color_close'
const markdownItDepthKey = 'knowledgeDocumentControlledColorDepth'

interface ParsedSequence {
  readonly children: readonly ControlledColorNode[]
  readonly end: number
  readonly closed: boolean
}

function appendText(nodes: ControlledColorNode[], value: string): void {
  if (!value) return
  const previous = nodes.at(-1)
  if (previous?.type === 'text') {
    nodes[nodes.length - 1] = { type: 'text', value: previous.value + value }
    return
  }
  nodes.push({ type: 'text', value })
}

export function parseControlledColorOpening(
  source: string,
  start: number,
): ControlledColorOpening | null {
  if (!Number.isSafeInteger(start) || start < 0 || start >= source.length) return null
  const opening = openingPattern.exec(source.slice(start))
  if (!opening) return null

  return {
    kind: opening[1] as ControlledColorKind,
    hex: opening[2]!.toUpperCase(),
    end: start + opening[0].length,
  }
}

function parseSequence(
  source: string,
  start: number,
  stopAtClosingBrace: boolean,
  depth: number,
): ParsedSequence {
  const children: ControlledColorNode[] = []
  let text = ''
  let position = start

  const flushText = () => {
    appendText(children, text)
    text = ''
  }

  while (position < source.length) {
    const character = source[position]!
    if (stopAtClosingBrace && character === '}') {
      flushText()
      return { children, end: position + 1, closed: true }
    }

    if (character === '\n' || character === '\r') {
      if (stopAtClosingBrace) {
        flushText()
        return { children, end: position, closed: false }
      }
      text += character
      position += 1
      continue
    }

    if (character === '{' && depth < controlledColorMaximumDepth) {
      const opening = parseControlledColorOpening(source, position)
      if (opening) {
        const contentStart = opening.end
        const nested = parseSequence(source, contentStart, true, depth + 1)
        if (nested.closed && nested.children.length > 0) {
          flushText()
          children.push({
            type: 'span',
            kind: opening.kind,
            hex: opening.hex,
            children: nested.children,
            contentStart,
            contentEnd: nested.end - 1,
            end: nested.end,
          })
          position = nested.end
          continue
        }

        // A syntactically valid opener that is empty or unclosed is one inert
        // literal. Consuming it as a unit prevents a nested suffix from being
        // interpreted independently inside malformed outer syntax.
        text += source.slice(position, nested.end)
        position = nested.end
        continue
      }
    }

    text += character
    position += 1
  }

  flushText()
  return { children, end: position, closed: !stopAtClosingBrace }
}

export function normalizeControlledHexColor(value: unknown): string | null {
  if (typeof value !== 'string' || !/^#[0-9A-Fa-f]{6}$/.test(value)) return null
  return value.toUpperCase()
}

export function isCanonicalControlledHexColor(value: unknown): value is string {
  return typeof value === 'string' && canonicalHexPattern.test(value)
}

export function parseControlledColorText(source: string): readonly ControlledColorNode[] {
  return parseSequence(source, 0, false, 0).children
}

export function parseControlledColorSpan(
  source: string,
  start: number,
  depth = 0,
): ControlledColorSpan | null {
  if (!Number.isSafeInteger(start) || start < 0 || start >= source.length) return null
  if (!Number.isSafeInteger(depth) || depth < 0 || depth >= controlledColorMaximumDepth) return null
  const opening = parseControlledColorOpening(source, start)
  if (!opening) return null

  const contentStart = opening.end
  const parsed = parseSequence(source, contentStart, true, depth + 1)
  if (!parsed.closed || parsed.children.length === 0) return null
  return {
    type: 'span',
    kind: opening.kind,
    hex: opening.hex,
    children: parsed.children,
    contentStart,
    contentEnd: parsed.end - 1,
    end: parsed.end,
  }
}

function currentMarkdownItDepth(state: StateInline): number {
  const value = state.env[markdownItDepthKey]
  return typeof value === 'number' && Number.isSafeInteger(value) && value >= 0 ? value : 0
}

function controlledColorInlineRule(state: StateInline, silent: boolean): boolean {
  const depth = currentMarkdownItDepth(state)
  const span = parseControlledColorSpan(state.src, state.pos, depth)
  if (!span) return false
  if (silent) {
    state.pos = span.end
    return true
  }

  const isTextColor = span.kind === 'color'
  const open = state.push(isTextColor ? textColorOpenToken : backgroundColorOpenToken, 'span', 1)
  open.meta = { hex: span.hex }

  const previousDepth = state.env[markdownItDepthKey]
  state.env[markdownItDepthKey] = depth + 1
  const innerTokens: Token[] = []
  state.md.inline.parse(
    state.src.slice(span.contentStart, span.contentEnd),
    state.md,
    state.env,
    innerTokens,
  )
  state.env[markdownItDepthKey] = previousDepth

  const childLevelOffset = state.level
  for (const token of innerTokens) {
    token.level += childLevelOffset
    state.tokens.push(token)
  }

  state.push(isTextColor ? textColorCloseToken : backgroundColorCloseToken, 'span', -1)
  state.pos = span.end
  return true
}

function tokenHex(token: Token): string | null {
  return normalizeControlledHexColor(token.meta?.hex)
}

export function controlledColorMarkdownItPlugin(markdown: MarkdownIt): void {
  markdown.inline.ruler.before(
    'emphasis',
    'knowledge-document-controlled-color',
    controlledColorInlineRule,
  )
  markdown.renderer.rules[textColorOpenToken] = (tokens, index) => {
    const hex = tokenHex(tokens[index]!)
    return hex ? `<span class="knowledge-document-text-color" style="color:${hex}">` : ''
  }
  markdown.renderer.rules[textColorCloseToken] = () => '</span>'
  markdown.renderer.rules[backgroundColorOpenToken] = (tokens, index) => {
    const hex = tokenHex(tokens[index]!)
    return hex
      ? `<span class="knowledge-document-background-color" style="background-color:${hex}">`
      : ''
  }
  markdown.renderer.rules[backgroundColorCloseToken] = () => '</span>'
}
