import { describe, expect, it } from 'vitest'
import {
  applyHeading,
  insertCodeBlock,
  insertHorizontalRule,
  insertLink,
  insertMermaid,
  insertMermaidDiagram,
  insertTable,
  toggleBulletList,
  toggleInlineWrap,
  toggleOrderedList,
  toggleQuote,
  toggleTaskList,
} from './sourceMarkdownTransforms'

const selection = (anchor: number, head = anchor) => ({ anchor, head })

describe('source Markdown toolbar transforms', () => {
  it.each([
    ['h1', '# 正文'],
    ['h2', '## 正文'],
    ['h3', '### 正文'],
    ['h4', '#### 正文'],
    ['h5', '##### 正文'],
    ['h6', '###### 正文'],
  ] as const)('turns the current source line into %s', (level, expected) => {
    expect(applyHeading('正文', selection(0), level).source).toBe(expected)
  })

  it('returns a heading source line to a paragraph', () => {
    expect(applyHeading('## 正文', selection(0), 'paragraph').source).toBe('正文')
  })

  it.each([
    ['bold', '**'],
    ['italic', '*'],
    ['inline code', '`'],
  ])('wraps and toggles %s without a rich-text state', (_name, delimiter) => {
    const applied = toggleInlineWrap('ORA-12541', selection(0, 9), delimiter)
    expect(applied.source).toBe(`${delimiter}ORA-12541${delimiter}`)
    expect(
      toggleInlineWrap(applied.source, selection(0, applied.source.length), delimiter).source,
    ).toBe('ORA-12541')
  })

  it('transforms selected source lines into quote, bullet, ordered, and task Markdown', () => {
    expect(toggleQuote('注意事项', selection(0)).source).toBe('> 注意事项')
    expect(toggleBulletList('a\nb', selection(0, 3)).source).toBe('- a\n- b')
    expect(toggleOrderedList('a\nb', selection(0, 3)).source).toBe('1. a\n2. b')
    expect(toggleTaskList('检查 Listener\n检查服务', selection(0, 16)).source).toBe(
      '- [ ] 检查 Listener\n- [ ] 检查服务',
    )
  })

  it('inserts code, link, table, Mermaid, and horizontal-rule source', () => {
    expect(insertCodeBlock('SELECT 1;', selection(0, 9), 'sql').source).toBe(
      '```sql\nSELECT 1;\n```',
    )
    expect(insertLink('OpenAI', selection(0, 6), 'OpenAI', 'https://openai.com').source).toBe(
      '[OpenAI](https://openai.com)',
    )
    expect(insertTable('', selection(0), 3, 2).source).toBe(
      '| 列1 | 列2 |\n| --- | --- |\n| 内容 | 内容 |\n| 内容 | 内容 |',
    )
    expect(insertMermaid('', selection(0)).source).toContain('```mermaid\nflowchart TD')
    expect(insertHorizontalRule('正文', selection(0)).source).toBe('正文\n\n---\n')
  })

  it.each([
    ['flowchart', 'flowchart TD\n  A[开始] --> B[处理]'],
    ['sequence', 'sequenceDiagram'],
    ['gantt', 'gantt'],
    ['class', 'classDiagram'],
    ['state', 'stateDiagram-v2'],
    ['pie', 'pie showData'],
    ['er', 'erDiagram'],
    ['journey', 'journey'],
  ] as const)('inserts the %s Mermaid template directly into raw source', (diagramType, marker) => {
    const inserted = insertMermaidDiagram('前缀后缀', selection(2), diagramType)

    expect(inserted.source).toContain(`\`\`\`mermaid\n${marker}`)
    expect(inserted.source.startsWith('前缀```mermaid\n')).toBe(true)
    expect(inserted.source.endsWith('```后缀')).toBe(true)
    expect(inserted.selection).toEqual({ anchor: 2 + '```mermaid\n'.length, head: 2 + '```mermaid\n'.length })
  })
})
