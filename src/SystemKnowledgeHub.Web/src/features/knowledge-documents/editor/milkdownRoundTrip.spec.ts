import { afterEach, describe, expect, it } from 'vitest'
import { Editor, defaultValueCtx, editorViewCtx, rootCtx } from '@milkdown/core'
import { gfm } from '@milkdown/preset-gfm'
import { getMarkdown } from '@milkdown/utils'
import { canonicalizeLegacyBreakParagraphs } from '../markdown/legacyMarkdownBreaks'
import { knowledgeDocumentCommonmark } from './milkdownConfig'

const corpus = `# 标题

普通中文段落。

## 列表

1. 第一步
2. 第二步

- 项目 A
- 项目 B

> 注意事项

\`inline code\`

\`\`\`bash
dotnet run
\`\`\`

| 名称 | 值 |
| --- | --- |
| A | B |

[链接](https://example.com)
`

const roots: HTMLElement[] = []

afterEach(() => {
  roots.splice(0).forEach((root) => root.remove())
})

async function roundTrip(markdown: string): Promise<string> {
  const root = document.createElement('div')
  document.body.append(root)
  roots.push(root)
  const editor = await Editor.make()
    .config((ctx) => {
      ctx.set(rootCtx, root)
      ctx.set(defaultValueCtx, markdown)
    })
    .use(knowledgeDocumentCommonmark)
    .use(gfm)
    .create()
  const result = editor.action(getMarkdown())
  await editor.destroy()
  return result
}

describe('Milkdown Markdown round trip', () => {
  it('preserves the supported document structures and stabilizes after normalization', async () => {
    const first = await roundTrip(corpus)
    const second = await roundTrip(first)

    expect(first).toContain('# 标题')
    expect(first).toContain('1. 第一步')
    expect(first).toContain('* 项目 A')
    expect(first).toContain('> 注意事项')
    expect(first).toContain('`inline code`')
    expect(first).toContain('```bash\ndotnet run\n```')
    expect(first).toContain('| 名称 | 值 |')
    expect(first).toContain('[链接](https://example.com)')
    expect(first).toContain('普通中文段落。')
    expect(second).toBe(first)
  })

  it('serializes native hard breaks without generated HTML and remains stable for Chinese text', async () => {
    const markdown = ['第一行\\', '第二行\\', '第三行'].join('\n')
    const first = await roundTrip(markdown)
    const second = await roundTrip(first)

    expect(first).toBe(`${markdown}\n`)
    expect(first).not.toMatch(/<br\s*\/?>/)
    expect(second).toBe(first)
  })

  it('keeps consecutive Markdown-native hard breaks stable', async () => {
    const markdown = ['第一行\\', '\\', '第三行'].join('\n')
    const first = await roundTrip(markdown)

    expect(first).toBe(`${markdown}\n`)
    expect(first).not.toContain('<br')
    expect(await roundTrip(first)).toBe(first)
  })

  it('does not serialize an intermediate empty paragraph as an HTML BR token', async () => {
    const root = document.createElement('div')
    document.body.append(root)
    roots.push(root)
    const editor = await Editor.make()
      .config((ctx) => {
        ctx.set(rootCtx, root)
        ctx.set(defaultValueCtx, 'A\n\nB')
      })
      .use(knowledgeDocumentCommonmark)
      .use(gfm)
      .create()

    editor.action((ctx) => {
      const view = ctx.get(editorViewCtx)
      const { schema } = view.state
      const paragraph = schema.nodes.paragraph!
      const doc = schema.nodes.doc!.create(null, [
        paragraph.create(null, schema.text('A')),
        paragraph.create(),
        paragraph.create(null, schema.text('B')),
      ])
      view.dispatch(view.state.tr.replaceWith(0, view.state.doc.content.size, doc.content))
    })

    const markdown = editor.action(getMarkdown())
    await editor.destroy()

    expect(markdown).not.toMatch(/<br\s*\/?>/)
    expect(markdown).toContain('A')
    expect(markdown).toContain('B')
  })

  it('round-trips the canonical form of a proven legacy empty-paragraph fixture', async () => {
    const canonical = canonicalizeLegacyBreakParagraphs('A\n\n<br />\n\nB')

    expect(canonical).toBe('A\n\n\\\nB')
    const first = await roundTrip(canonical)
    expect(first).toBe(`${canonical}\n`)
    expect(await roundTrip(first)).toBe(first)
  })
})
