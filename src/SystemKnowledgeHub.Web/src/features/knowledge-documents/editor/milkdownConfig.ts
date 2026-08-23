import {
  commonmark,
  remarkPreserveEmptyLinePlugin,
} from '@milkdown/preset-commonmark'

const preserveEmptyLinePlugins = new Set(remarkPreserveEmptyLinePlugin)

// Empty Markdown paragraphs are whitespace, not HTML. Milkdown's optional
// preservation plugin serializes an intermediate empty paragraph as <br />.
export const knowledgeDocumentCommonmark = commonmark.filter(
  (plugin) => !preserveEmptyLinePlugins.has(plugin),
)
