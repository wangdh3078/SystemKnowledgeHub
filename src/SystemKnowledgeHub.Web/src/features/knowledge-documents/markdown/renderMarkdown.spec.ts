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
    const rendered = renderMarkdown([
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
    ].join('\n'))

    expect(rendered).toContain('A&lt;br /&gt;B')
    expect(rendered).toContain('&lt;BR /&gt;')
    expect(rendered).toContain('&lt;br class=&quot;unsafe&quot;&gt;')
    expect(rendered).not.toContain('<br class=')
  })

  it('does not let legacy compatibility bypass the raw HTML security boundary', () => {
    const rendered = renderMarkdown([
      '<script>alert(1)</script>',
      '<img src=x onerror=alert(1)>',
      '<a href="javascript:alert(1)">x</a>',
    ].join('\n\n'))

    expect(rendered).toContain('&lt;script&gt;alert(1)&lt;/script&gt;')
    expect(rendered).toContain('&lt;img src=x onerror=alert(1)&gt;')
    expect(rendered).toContain('&lt;a href=&quot;javascript:alert(1)&quot;&gt;x&lt;/a&gt;')
    expect(rendered).not.toContain('<script>')
    expect(rendered).not.toContain('<img ')
    expect(rendered).not.toContain('<a href="javascript:')
  })
})
