import { afterEach, describe, expect, it } from 'vitest'
import { Editor, defaultValueCtx, editorViewCtx, rootCtx } from '@milkdown/core'
import { gfm } from '@milkdown/preset-gfm'
import { getMarkdown } from '@milkdown/utils'
import { canonicalizeLegacyBreakParagraphs } from '../markdown/legacyMarkdownBreaks'
import { knowledgeDocumentColorExtension } from './colorMarks'
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
    .use(knowledgeDocumentColorExtension)
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

  it('canonicalizes color marks and preserves the saved source after reload', async () => {
    const source = [
      '{color:#e53935|严重告警}',
      '',
      '{bg:#fff3b0|请人工确认}',
      '',
      '{bg:#fff3b0|{color:#e53935|重点内容}}',
    ].join('\n')
    const canonical = [
      '{color:#E53935|严重告警}',
      '',
      '{bg:#FFF3B0|请人工确认}',
      '',
      '{bg:#FFF3B0|{color:#E53935|重点内容}}',
      '',
    ].join('\n')

    const saved = await roundTrip(source)
    const reloaded = await roundTrip(saved)

    expect(saved).toBe(canonical)
    expect(reloaded).toBe(saved)
  })

  it('round-trips H1 through H6 and every standard toolbar Markdown form', async () => {
    const source = [
      '# H1',
      '## H2',
      '### H3',
      '#### H4',
      '##### H5',
      '###### H6',
      '',
      '**加粗** *斜体* `inline`',
      '',
      '- 无序',
      '',
      '1. 有序',
      '',
      '> 引用',
      '',
      '```',
      'plain',
      '```',
      '',
      '```sql',
      'SELECT 1;',
      '```',
      '',
      '```json',
      '{"ok":true}',
      '```',
      '',
      '[文档](https://example.com/docs)',
      '',
      '---',
    ].join('\n')
    const saved = await roundTrip(source)

    for (let level = 1; level <= 6; level += 1) {
      expect(saved).toContain(`${'#'.repeat(level)} H${level}`)
    }
    expect(saved).toContain('**加粗**')
    expect(saved).toContain('*斜体*')
    expect(saved).toContain('`inline`')
    expect(saved).toContain('```\nplain\n```')
    expect(saved).toContain('```sql\nSELECT 1;\n```')
    expect(saved).toContain('```json\n{"ok":true}\n```')
    expect(saved).toContain('[文档](https://example.com/docs)')
    expect(await roundTrip(saved)).toBe(saved)
  })

  it('preserves Task List, Table, Mermaid and controlled colors in one saved source', async () => {
    const source = [
      '- [ ] 未完成',
      '- [x] 已完成',
      '  - [ ] 嵌套待办',
      '',
      '| 字段 | 说明 |',
      '| --- | --- |',
      '| ID | 主键 |',
      '',
      '```mermaid',
      'flowchart LR',
      '  A[开始] --> B[结束]',
      '```',
      '',
      '{bg:#FFF3B0|{color:#E53935|重点内容}}',
      '',
      '[{color:#E53935|彩色链接}](/knowledge-documents)',
    ].join('\n')
    const saved = await roundTrip(source)

    expect(saved).toContain('* [ ] 未完成')
    expect(saved).toContain('* [x] 已完成')
    expect(saved).toContain('  * [ ] 嵌套待办')
    expect(saved).toContain('| 字段 | 说明 |')
    expect(saved).toContain('```mermaid\nflowchart LR')
    expect(saved).toContain('{bg:#FFF3B0|{color:#E53935|重点内容}}')
    expect(saved).toContain('[{color:#E53935|彩色链接}](/knowledge-documents)')
    expect(await roundTrip(saved)).toBe(saved)
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
