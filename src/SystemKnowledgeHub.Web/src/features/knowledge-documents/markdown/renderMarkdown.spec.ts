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
})
