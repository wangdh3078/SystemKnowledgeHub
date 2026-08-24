import MarkdownIt from 'markdown-it'
import { describe, expect, it } from 'vitest'
import {
  controlledColorMarkdownItPlugin,
  isCanonicalControlledHexColor,
  normalizeControlledHexColor,
  parseControlledColorText,
} from './colorSyntax'

function render(markdown: string): string {
  return new MarkdownIt({ html: false }).use(controlledColorMarkdownItPlugin).render(markdown)
}

describe('controlled color syntax', () => {
  it('accepts only six-digit hex values and canonicalizes them to uppercase', () => {
    expect(normalizeControlledHexColor('#e53935')).toBe('#E53935')
    expect(isCanonicalControlledHexColor('#E53935')).toBe(true)

    expect(normalizeControlledHexColor('#E53')).toBeNull()
    expect(normalizeControlledHexColor('red')).toBeNull()
    expect(normalizeControlledHexColor('url(javascript:alert(1))')).toBeNull()
    expect(normalizeControlledHexColor('expression(alert(1))')).toBeNull()
    expect(normalizeControlledHexColor(0xe53935)).toBeNull()
    expect(isCanonicalControlledHexColor('#e53935')).toBe(false)
  })

  it('parses nested text and background spans into one canonical tree', () => {
    expect(parseControlledColorText('前{bg:#fff59d|警告：{color:#e53935|立即处理}}后')).toEqual([
      { type: 'text', value: '前' },
      {
        type: 'span',
        kind: 'bg',
        hex: '#FFF59D',
        contentStart: 13,
        contentEnd: 36,
        end: 37,
        children: [
          { type: 'text', value: '警告：' },
          {
            type: 'span',
            kind: 'color',
            hex: '#E53935',
            contentStart: 31,
            contentEnd: 35,
            end: 36,
            children: [{ type: 'text', value: '立即处理' }],
          },
        ],
      },
      { type: 'text', value: '后' },
    ])
  })

  it.each([
    '{color:#E53|短色值}',
    '{color:red|命名色}',
    '{color:url(javascript:alert(1))|危险}',
    '{color:#E53935|}',
    '{color:#E53935|未闭合',
    '{color:#E53935|第一行\n第二行}',
  ])('leaves invalid or non-inline syntax inert: %s', (source) => {
    expect(parseControlledColorText(source)).toEqual([{ type: 'text', value: source }])
  })

  it('renders fixed span attributes while retaining nested Markdown', () => {
    const rendered = render('{bg:#fff59d|警告：{color:#e53935|**立即处理**}}')

    expect(rendered).toContain(
      '<span class="knowledge-document-background-color" style="background-color:#FFF59D">',
    )
    expect(rendered).toContain(
      '<span class="knowledge-document-text-color" style="color:#E53935"><strong>立即处理</strong></span>',
    )
  })

  it('renders a controlled color span inside a Markdown link label', () => {
    const rendered = render('[{color:#E53935|系统知识中心链接}](/knowledge-documents)')

    expect(rendered).toContain(
      '<a href="/knowledge-documents"><span class="knowledge-document-text-color" style="color:#E53935">系统知识中心链接</span></a>',
    )
  })

  it('keeps raw HTML escaped and invalid style payloads literal', () => {
    const rendered = render(
      [
        '{color:#E53935|<img src=x onerror=alert(1)>}',
        '',
        '{color:url(javascript:alert(1))|危险}',
        '',
        '<script>alert(1)</script>',
      ].join('\n'),
    )

    expect(rendered).toContain(
      '<span class="knowledge-document-text-color" style="color:#E53935">&lt;img src=x onerror=alert(1)&gt;</span>',
    )
    expect(rendered).toContain('{color:url(javascript:alert(1))|危险}')
    expect(rendered).toContain('&lt;script&gt;alert(1)&lt;/script&gt;')
    expect(rendered).not.toContain('<img ')
    expect(rendered).not.toContain('<script>')
    expect(rendered).not.toContain('style="color:url')
  })
})
