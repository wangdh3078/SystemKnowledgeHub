import { describe, expect, it } from 'vitest'
import { renderMarkdown } from './renderMarkdown'

describe('renderMarkdown', () => {
  it('renders Markdown while leaving raw HTML inert and rejecting javascript links', () => {
    const rendered = renderMarkdown(
      '# 标题\n\n<script>alert(1)</script>\n\n[危险链接](javascript:alert(1))',
    )

    expect(rendered).toContain('<h1>标题</h1>')
    expect(rendered).toContain('&lt;script&gt;alert(1)&lt;/script&gt;')
    expect(rendered).not.toContain('<script>')
    expect(rendered).not.toContain('href="javascript:')
  })

  it('marks external links as a safe new browsing context', () => {
    expect(renderMarkdown('[官网](https://example.test)')).toContain('rel="noopener noreferrer"')
  })

  it('renders fenced code as safe technical cards with a language fallback', () => {
    const rendered = renderMarkdown('```sql\nSELECT 1;\n```\n\n```\nplain text\n```')

    expect(rendered).toContain('knowledge-document-code-card')
    expect(rendered).toContain('knowledge-document-code-card__language">sql</span>')
    expect(rendered).toContain('knowledge-document-code-card__language">plain</span>')
    expect(rendered).toContain('data-knowledge-document-code-copy')
    expect(rendered).toContain('data-knowledge-document-code-collapse')
    expect(rendered).toContain('data-icon="copy"')
    expect(rendered).toContain('data-icon="chevron-up"')
    expect(rendered).toContain('<span class="hljs-keyword">SELECT</span>')
    expect(rendered).toContain('<span class="hljs-number">1</span>;')
  })

  it.each([
    ['plaintext', 'plain text', false],
    ['csharp', 'public class Sample {}', true],
    ['javascript', 'const total = 1;', true],
    ['typescript', 'interface Item { id: number }', true],
    ['json', '{"name":"value","enabled":true}', true],
    ['sql', 'SELECT id FROM users;', true],
    ['bash', 'echo "$HOME"', true],
    ['powershell', 'Write-Host "ok"', true],
    ['python', 'def hello():\n  return True', true],
    ['java', 'public class Sample {}', true],
    ['cpp', '#include <vector>', true],
    ['c', '#include <stdio.h>', true],
    ['go', 'package main', true],
    ['rust', 'fn main() {}', true],
    ['html', '<section>safe</section>', true],
    ['xml', '<root><node /></root>', true],
    ['css', '.card { color: red; }', true],
    ['yaml', 'name: value', true],
    ['markdown', '# Heading', true],
    ['dockerfile', 'FROM alpine', true],
  ] as const)('uses the selected %s highlighter without executing source', (language, source, highlighted) => {
    const rendered = renderMarkdown(`\`\`\`${language}\n${source}\n\`\`\``)
    const container = document.createElement('div')
    container.innerHTML = rendered
    const code = container.querySelector('code')

    expect(code?.classList.contains(`language-${language}`)).toBe(true)
    expect(code?.classList.contains('hljs')).toBe(true)
    expect(code?.textContent).toBe(`${source}\n`)
    expect(code?.querySelectorAll('span[class^="hljs-"]').length)
      .toBeGreaterThanOrEqual(highlighted ? 1 : 0)
    expect(container.querySelector('script')).toBeNull()
  })

  it('wraps GFM tables for responsive internal scrolling while preserving table semantics', () => {
    const rendered = renderMarkdown('| 字段 | 类型 |\n| --- | --- |\n| Id | bigint |')

    expect(rendered).toContain('<div class="knowledge-markdown-table-wrap"><table>')
    expect(rendered).toContain('</table>\n</div>')
  })

  it('renders canonical text/background colors with nested Markdown content', () => {
    const rendered = renderMarkdown(
      [
        '{color:#e53935|严重告警}',
        '',
        '{bg:#fff3b0|请人工确认}',
        '',
        '{bg:#fff3b0|{color:#e53935|**重点**与[说明](https://example.test/docs)}}',
      ].join('\n'),
    )

    expect(rendered).toContain(
      '<span class="knowledge-document-text-color" style="color:#E53935">严重告警</span>',
    )
    expect(rendered).toContain(
      '<span class="knowledge-document-background-color" style="background-color:#FFF3B0">请人工确认</span>',
    )
    expect(rendered).toContain(
      '<span class="knowledge-document-background-color" style="background-color:#FFF3B0"><span class="knowledge-document-text-color" style="color:#E53935"><strong>重点</strong>与<a href="https://example.test/docs" target="_blank" rel="noopener noreferrer">说明</a></span></span>',
    )
  })

  it('keeps invalid color payloads literal and preserves the raw HTML security boundary', () => {
    const rendered = renderMarkdown(
      [
        '{color:#E53|短色值}',
        '{color:red|命名色}',
        '{color:url(javascript:alert(1))|危险样式}',
        '{bg:expression(alert(1))|危险背景}',
        '',
        '{color:#E53935|<img src=x onerror=alert(1)>}',
        '',
        '{bg:#FFF3B0|[危险链接](javascript:alert(1))}',
        '',
        '<script>alert(1)</script>',
      ].join('\n'),
    )

    expect(rendered).toContain('{color:#E53|短色值}')
    expect(rendered).toContain('{color:red|命名色}')
    expect(rendered).toContain('{color:url(javascript:alert(1))|危险样式}')
    expect(rendered).toContain('{bg:expression(alert(1))|危险背景}')
    expect(rendered).not.toContain('style="color:url')
    expect(rendered).not.toContain('style="background-color:expression')
    expect(rendered).toContain(
      '<span class="knowledge-document-text-color" style="color:#E53935">&lt;img src=x onerror=alert(1)&gt;</span>',
    )
    expect(rendered).toContain('&lt;script&gt;alert(1)&lt;/script&gt;')
    expect(rendered).not.toContain('<img ')
    expect(rendered).not.toContain('<script>')
    expect(rendered).not.toContain('href="javascript:')
  })

  it('renders Markdown-native hard breaks', () => {
    const rendered = renderMarkdown(['第一行\\', '第二行'].join('\n'))

    expect(rendered).toContain('第一行<br>')
    expect(rendered).toContain('第二行')
  })

  it.each(['<br>', '<br/>', '<br >', '<br />'])(
    'renders the proven standalone legacy token %s as a safe line break',
    (legacyBreak) => {
      const rendered = renderMarkdown(`A\n\n${legacyBreak}\n\nB`)

      expect(rendered).toContain('<p><br>')
      expect(rendered).not.toContain('&lt;br')
      expect(rendered).toContain('<p>A</p>')
      expect(rendered).toContain('<p>B</p>')
    },
  )

  it('keeps inline, nested, code, attributed, and differently-cased BR text inert', () => {
    const rendered = renderMarkdown(
      [
        'A<br />B',
        '',
        '> <br />',
        '',
        '    <br />',
        '',
        '```html',
        '<br />',
        '```',
        '',
        '<BR />',
        '',
        '<br class="unsafe">',
      ].join('\n'),
    )

    expect(rendered).toContain('A&lt;br /&gt;B')
    expect(rendered).toContain('&lt;BR /&gt;')
    expect(rendered).toContain('&lt;br class=&quot;unsafe&quot;&gt;')
    expect(rendered).not.toContain('<br class=')
  })

  it('does not let legacy compatibility bypass the raw HTML security boundary', () => {
    const rendered = renderMarkdown(
      [
        '<script>alert(1)</script>',
        '<img src=x onerror=alert(1)>',
        '<a href="javascript:alert(1)">x</a>',
      ].join('\n\n'),
    )

    expect(rendered).toContain('&lt;script&gt;alert(1)&lt;/script&gt;')
    expect(rendered).toContain('&lt;img src=x onerror=alert(1)&gt;')
    expect(rendered).toContain('&lt;a href=&quot;javascript:alert(1)&quot;&gt;x&lt;/a&gt;')
    expect(rendered).not.toContain('<script>')
    expect(rendered).not.toContain('<img ')
    expect(rendered).not.toContain('<a href="javascript:')
  })
})
