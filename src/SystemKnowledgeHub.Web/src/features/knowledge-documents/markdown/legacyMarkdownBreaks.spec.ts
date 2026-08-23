import { describe, expect, it } from 'vitest'
import { canonicalizeLegacyBreakParagraphs } from './legacyMarkdownBreaks'

describe('canonicalizeLegacyBreakParagraphs', () => {
  it.each(['<br>', '<br/>', '<br >', '<br />'])(
    'canonicalizes the proven standalone legacy token %s',
    (legacyBreak) => {
      expect(canonicalizeLegacyBreakParagraphs(`A\n\n${legacyBreak}\n\nB`))
        .toBe('A\n\n\\\nB')
    },
  )

  it('canonicalizes consecutive legacy empty paragraphs without HTML', () => {
    expect(canonicalizeLegacyBreakParagraphs('A\n\n<br />\n\n<br />\n\nB'))
      .toBe('A\n\n\\\n\\\nB')
  })

  it.each([
    'A<br />B',
    '> <br />',
    '    <br />',
    '```html\n<br />\n```',
    '<BR />',
    '<br class="unsafe">',
    '\\<br />',
  ])('does not broaden compatibility to %s', (markdown) => {
    expect(canonicalizeLegacyBreakParagraphs(markdown)).toBe(markdown)
  })
})
