import { afterEach, describe, expect, it } from 'vitest'
import { Editor, defaultValueCtx, rootCtx } from '@milkdown/core'
import { commonmark } from '@milkdown/preset-commonmark'
import { gfm } from '@milkdown/preset-gfm'
import { getMarkdown } from '@milkdown/utils'

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
    .use(commonmark)
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
})
