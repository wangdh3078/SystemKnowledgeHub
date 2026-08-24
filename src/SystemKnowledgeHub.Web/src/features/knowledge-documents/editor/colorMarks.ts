import type { MilkdownPlugin } from '@milkdown/ctx'
import type { MarkType } from '@milkdown/prose/model'
import type { Command } from '@milkdown/prose/state'
import { $command, $markSchema, $remark } from '@milkdown/utils'
import type { Nodes, Parent, PhrasingContent, Root, Text } from 'mdast'
import type { Handle, Options as ToMarkdownOptions } from 'mdast-util-to-markdown'
import type { Plugin } from 'unified'
import type {} from 'remark-stringify'
import {
  controlledColorMaximumDepth,
  isCanonicalControlledHexColor,
  normalizeControlledHexColor,
  parseControlledColorOpening,
  type ControlledColorKind,
} from '../markdown/colorSyntax'

export interface KnowledgeTextColorNode extends Parent {
  type: 'knowledgeTextColor'
  hex: string
  children: PhrasingContent[]
}

export interface KnowledgeBackgroundColorNode extends Parent {
  type: 'knowledgeBackgroundColor'
  hex: string
  children: PhrasingContent[]
}

export type KnowledgeColorNode = KnowledgeTextColorNode | KnowledgeBackgroundColorNode

declare module 'mdast' {
  interface PhrasingContentMap {
    knowledgeTextColor: KnowledgeTextColorNode
    knowledgeBackgroundColor: KnowledgeBackgroundColorNode
  }

  interface RootContentMap {
    knowledgeTextColor: KnowledgeTextColorNode
    knowledgeBackgroundColor: KnowledgeBackgroundColorNode
  }
}

type PhrasingUnit =
  | { readonly type: 'character'; readonly value: string }
  | {
      readonly type: 'node'
      readonly value: Exclude<PhrasingContent, Text>
    }

interface ParsedPhrasingSequence {
  readonly children: PhrasingContent[]
  readonly end: number
  readonly closed: boolean
}

interface InlineOpening {
  readonly kind: ControlledColorKind
  readonly hex: string
  readonly end: number
}

const phrasingParentTypes = new Set<Nodes['type']>([
  'paragraph',
  'heading',
  'emphasis',
  'strong',
  'delete',
  'link',
  'linkReference',
  'tableCell',
  'knowledgeTextColor',
  'knowledgeBackgroundColor',
])

function appendPhrasingNode(children: PhrasingContent[], node: PhrasingContent): void {
  if (node.type === 'text') {
    if (!node.value) return
    const previous = children.at(-1)
    if (previous?.type === 'text') {
      previous.value += node.value
      return
    }
  }
  children.push(node)
}

function phrasingUnits(children: readonly PhrasingContent[]): PhrasingUnit[] {
  const units: PhrasingUnit[] = []
  for (const child of children) {
    if (child.type === 'text') {
      for (const character of child.value) {
        units.push({ type: 'character', value: character })
      }
      continue
    }
    units.push({ type: 'node', value: child })
  }
  return units
}

function readInlineOpening(units: readonly PhrasingUnit[], start: number): InlineOpening | null {
  let candidate = ''
  for (let position = start; position < units.length && candidate.length < 20; position += 1) {
    const unit = units[position]!
    if (unit.type !== 'character') break
    candidate += unit.value
  }

  const opening = parseControlledColorOpening(candidate, 0)
  if (!opening) return null
  return {
    kind: opening.kind,
    hex: opening.hex,
    end: start + opening.end,
  }
}

function appendUnit(children: PhrasingContent[], unit: PhrasingUnit): void {
  if (unit.type === 'character') {
    appendPhrasingNode(children, { type: 'text', value: unit.value })
    return
  }
  appendPhrasingNode(children, unit.value)
}

function appendUnitRange(
  children: PhrasingContent[],
  units: readonly PhrasingUnit[],
  start: number,
  end: number,
): void {
  for (let position = start; position < end; position += 1) {
    appendUnit(children, units[position]!)
  }
}

function createKnowledgeColorNode(
  kind: ControlledColorKind,
  hex: string,
  children: PhrasingContent[],
): KnowledgeColorNode {
  if (kind === 'color') {
    return { type: 'knowledgeTextColor', hex, children }
  }
  return { type: 'knowledgeBackgroundColor', hex, children }
}

function parsePhrasingSequence(
  units: readonly PhrasingUnit[],
  start: number,
  stopAtClosingBrace: boolean,
  depth: number,
): ParsedPhrasingSequence {
  const children: PhrasingContent[] = []
  let position = start

  while (position < units.length) {
    const unit = units[position]!
    if (unit.type === 'character') {
      if (stopAtClosingBrace && unit.value === '}') {
        return { children, end: position + 1, closed: true }
      }
      if (unit.value === '\n' || unit.value === '\r') {
        if (stopAtClosingBrace) {
          return { children, end: position, closed: false }
        }
        appendUnit(children, unit)
        position += 1
        continue
      }
      if (unit.value === '{' && depth < controlledColorMaximumDepth) {
        const opening = readInlineOpening(units, position)
        if (opening) {
          const nested = parsePhrasingSequence(units, opening.end, true, depth + 1)
          if (nested.closed && nested.children.length > 0) {
            appendPhrasingNode(
              children,
              createKnowledgeColorNode(opening.kind, opening.hex, nested.children),
            )
            position = nested.end
            continue
          }

          appendUnitRange(children, units, position, nested.end)
          position = nested.end
          continue
        }
      }
    }

    appendUnit(children, unit)
    position += 1
  }

  return { children, end: position, closed: !stopAtClosingBrace }
}

function parsePhrasingChildren(children: readonly PhrasingContent[]): PhrasingContent[] {
  const units = phrasingUnits(children)
  return parsePhrasingSequence(units, 0, false, 0).children
}

function hasChildren(node: Nodes): node is Nodes & Parent {
  return 'children' in node && Array.isArray(node.children)
}

function isPhrasingParent(node: Nodes): node is Nodes & Parent & { children: PhrasingContent[] } {
  return hasChildren(node) && phrasingParentTypes.has(node.type)
}

function transformControlledColors(node: Nodes): void {
  if (isPhrasingParent(node)) {
    node.children = parsePhrasingChildren(node.children)
  }
  if (!hasChildren(node)) return
  node.children.forEach(transformControlledColors)
}

function isKnowledgeColorNode(value: unknown): value is KnowledgeColorNode {
  if (typeof value !== 'object' || value === null) return false
  if (!('type' in value) || !('hex' in value) || !('children' in value)) return false
  return (
    (value.type === 'knowledgeTextColor' || value.type === 'knowledgeBackgroundColor') &&
    isCanonicalControlledHexColor(value.hex) &&
    Array.isArray(value.children)
  )
}

function serializeKnowledgeColor(
  kind: ControlledColorKind,
  value: unknown,
  state: Parameters<Handle>[2],
  info: Parameters<Handle>[3],
): string {
  if (!isKnowledgeColorNode(value)) return ''
  const tracker = state.createTracker(info)
  const opening = `{${kind}:${value.hex}|`
  const before = tracker.move(opening)
  const content = tracker.move(
    state.containerPhrasing(value, {
      after: '}',
      before,
      ...tracker.current(),
    }),
  )
  return before + content + tracker.move('}')
}

const textColorHandler: Handle = (node, _parent, state, info) =>
  serializeKnowledgeColor('color', node, state, info)

const backgroundColorHandler: Handle = (node, _parent, state, info) =>
  serializeKnowledgeColor('bg', node, state, info)

const controlledColorToMarkdown: ToMarkdownOptions = {
  handlers: {
    knowledgeTextColor: textColorHandler,
    knowledgeBackgroundColor: backgroundColorHandler,
  },
}

type ControlledColorRemarkOptions = Record<string, never>

const remarkControlledColors: Plugin<[ControlledColorRemarkOptions], Root> = function () {
  const data = this.data()
  const extensions = data.toMarkdownExtensions ?? (data.toMarkdownExtensions = [])
  extensions.push(controlledColorToMarkdown)

  return (tree) => {
    transformControlledColors(tree)
  }
}

export const controlledColorRemarkPlugin = $remark(
  'knowledgeDocumentControlledColor',
  () => remarkControlledColors,
  {},
)

function validateCanonicalHex(value: unknown): void {
  if (!isCanonicalControlledHexColor(value)) {
    throw new RangeError('Controlled color marks require canonical #RRGGBB values.')
  }
}

function controlledColorDomAttrs(value: unknown, dataAttribute: string): false | { hex: string } {
  if (!(value instanceof HTMLElement)) return false
  const hex = normalizeControlledHexColor(value.getAttribute(dataAttribute))
  return hex ? { hex } : false
}

export const textColorMark = $markSchema('knowledgeTextColor', () => ({
  priority: 80,
  attrs: {
    hex: { validate: validateCanonicalHex },
  },
  parseDOM: [
    {
      tag: 'span[data-knowledge-text-color]',
      getAttrs: (node) => controlledColorDomAttrs(node, 'data-knowledge-text-color'),
    },
  ],
  toDOM: (mark) => {
    const hex = normalizeControlledHexColor(mark.attrs.hex)
    if (!hex) return ['span', 0]
    return [
      'span',
      {
        'data-knowledge-text-color': hex,
        style: `color:${hex}`,
      },
      0,
    ]
  },
  parseMarkdown: {
    match: (node) => node.type === 'knowledgeTextColor',
    runner: (state, node, markType) => {
      const hex = normalizeControlledHexColor(node.hex)
      if (!hex) {
        state.next(node.children ?? [])
        return
      }
      state.openMark(markType, { hex })
      state.next(node.children ?? [])
      state.closeMark(markType)
    },
  },
  toMarkdown: {
    match: (mark) => mark.type.name === 'knowledgeTextColor',
    runner: (state, mark) => {
      const hex = normalizeControlledHexColor(mark.attrs.hex)
      if (!hex) return
      state.withMark(mark, 'knowledgeTextColor', '', { hex })
    },
  },
}))

export const backgroundColorMark = $markSchema('knowledgeBackgroundColor', () => ({
  priority: 70,
  attrs: {
    hex: { validate: validateCanonicalHex },
  },
  parseDOM: [
    {
      tag: 'span[data-knowledge-background-color]',
      getAttrs: (node) => controlledColorDomAttrs(node, 'data-knowledge-background-color'),
    },
  ],
  toDOM: (mark) => {
    const hex = normalizeControlledHexColor(mark.attrs.hex)
    if (!hex) return ['span', 0]
    return [
      'span',
      {
        'data-knowledge-background-color': hex,
        style: `background-color:${hex}`,
      },
      0,
    ]
  },
  parseMarkdown: {
    match: (node) => node.type === 'knowledgeBackgroundColor',
    runner: (state, node, markType) => {
      const hex = normalizeControlledHexColor(node.hex)
      if (!hex) {
        state.next(node.children ?? [])
        return
      }
      state.openMark(markType, { hex })
      state.next(node.children ?? [])
      state.closeMark(markType)
    },
  },
  toMarkdown: {
    match: (mark) => mark.type.name === 'knowledgeBackgroundColor',
    runner: (state, mark) => {
      const hex = normalizeControlledHexColor(mark.attrs.hex)
      if (!hex) return
      state.withMark(mark, 'knowledgeBackgroundColor', '', { hex })
    },
  },
}))

function applyControlledColorMark(markType: MarkType, value: string): Command {
  const hex = normalizeControlledHexColor(value)
  return (state, dispatch) => {
    if (!hex) return false
    if (!dispatch) return true

    const mark = markType.create({ hex })
    const { from, to, empty } = state.selection
    const transaction = state.tr
    if (empty) {
      transaction.removeStoredMark(markType).addStoredMark(mark)
    } else {
      transaction.removeMark(from, to, markType)
      state.doc.nodesBetween(from, to, (node, position) => {
        if (!node.isText) return

        const resolved = state.doc.resolve(Math.min(position + 1, state.doc.content.size))
        const isInsideTable = Array.from(
          { length: resolved.depth + 1 },
          (_, depth) => resolved.node(depth).type.name,
        ).some((name) => name === 'table_cell' || name === 'table_header')
        if (isInsideTable) return

        const textFrom = Math.max(from, position)
        const textTo = Math.min(to, position + node.nodeSize)
        if (textFrom < textTo) transaction.addMark(textFrom, textTo, mark)
      })
    }
    dispatch(transaction.scrollIntoView())
    return true
  }
}

function clearControlledColorMark(markType: MarkType): Command {
  return (state, dispatch) => {
    if (!dispatch) return true

    const { from, to, empty } = state.selection
    const transaction = empty
      ? state.tr.removeStoredMark(markType)
      : state.tr.removeMark(from, to, markType)
    dispatch(transaction.scrollIntoView())
    return true
  }
}

export const applyTextColorCommand = $command<string, 'ApplyKnowledgeDocumentTextColor'>(
  'ApplyKnowledgeDocumentTextColor',
  (ctx) => (value) => applyControlledColorMark(textColorMark.type(ctx), value ?? ''),
)

export const clearTextColorCommand = $command<undefined, 'ClearKnowledgeDocumentTextColor'>(
  'ClearKnowledgeDocumentTextColor',
  (ctx) => () => clearControlledColorMark(textColorMark.type(ctx)),
)

export const applyBackgroundColorCommand = $command<
  string,
  'ApplyKnowledgeDocumentBackgroundColor'
>(
  'ApplyKnowledgeDocumentBackgroundColor',
  (ctx) => (value) => applyControlledColorMark(backgroundColorMark.type(ctx), value ?? ''),
)

export const clearBackgroundColorCommand = $command<
  undefined,
  'ClearKnowledgeDocumentBackgroundColor'
>(
  'ClearKnowledgeDocumentBackgroundColor',
  (ctx) => () => clearControlledColorMark(backgroundColorMark.type(ctx)),
)

export const knowledgeDocumentColorExtension: MilkdownPlugin[] = [
  controlledColorRemarkPlugin,
  backgroundColorMark,
  textColorMark,
  applyTextColorCommand,
  clearTextColorCommand,
  applyBackgroundColorCommand,
  clearBackgroundColorCommand,
].flat()
